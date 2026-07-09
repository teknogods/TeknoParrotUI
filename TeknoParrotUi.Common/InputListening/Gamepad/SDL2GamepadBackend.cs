using System;
using System.Diagnostics;
using System.Threading;
using SDL2;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Cross-platform gamepad backend built on SDL2's GameController API, which
    /// deliberately mirrors XInput semantics (same button set, same 16-bit stick
    /// range, independent analog triggers). Maintains XInput-shaped
    /// <see cref="State"/> snapshots for up to 4 player slots so the existing
    /// game-mapping logic in <see cref="InputListenerXInput"/> runs unchanged.
    ///
    /// SDL calls are confined to a single poll thread; consumers read cached
    /// state via <see cref="GetState"/> / <see cref="IsConnected"/>.
    /// </summary>
    public static class SDL2GamepadBackend
    {
        public const int MaxSlots = 4;

        private static readonly object Sync = new object();
        private static readonly IntPtr[] Controllers = new IntPtr[MaxSlots];
        private static readonly int[] InstanceIds = new int[MaxSlots];
        private static readonly State[] States = new State[MaxSlots];
        private static readonly bool[] Connected = new bool[MaxSlots];

        private static Thread _pollThread;
        private static volatile bool _running;
        private static int _refCount;

        // Temporary lifecycle tracing: set TP_SDL2_TRACE=1 to log to %TEMP%\tp-sdl2-trace.log
        private static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("TP_SDL2_TRACE") == "1";
        /// <summary>Temporary debug tracing hook, also usable by consumers.</summary>
        public static void Trace(string msg)
        {
            Debug.WriteLine("SDL2GamepadBackend: " + msg);
            if (!TraceEnabled) return;
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tp-sdl2-trace.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>
        /// Start (or ref-count) the backend. SDL is initialised ONCE per process
        /// and never quit: re-initialising SDL_INIT_GAMECONTROLLER in the same
        /// process breaks state delivery for RawInput-driver pads — the device
        /// re-attaches but buttons/axes never update again (verified with an
        /// Xbox One Elite 2: first init traces every press, after quit+init the
        /// pad attaches and stays silent). The poll thread simply throttles
        /// down while nobody holds a reference.
        /// </summary>
        public static void Acquire()
        {
            lock (Sync)
            {
                _refCount++;
                Trace($"Acquire -> refCount={_refCount} running={_running}");
                if (_running)
                    return;

                try
                {
                    // Allow gamepad input while the game window (not ours) has focus.
                    SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

                    if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_GAMECONTROLLER) != 0)
                    {
                        Trace($"SDL_InitSubSystem FAILED: {SDL.SDL_GetError()}");
                        return;
                    }
                    Trace("SDL_InitSubSystem ok");
                }
                catch (DllNotFoundException)
                {
                    // Native SDL2 not available on this platform/package (e.g. Android
                    // head without SDL natives) — gamepad input disabled, no crash.
                    Trace("native SDL2 library not found, gamepad input disabled");
                    return;
                }

                _running = true;
                _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "SDL2GamepadBackend" };
                _pollThread.Start();
            }
        }

        /// <summary>
        /// Release a reference. Intentionally NO teardown — see <see cref="Acquire"/>;
        /// the poll thread throttles down when the last consumer releases.
        /// </summary>
        public static void Release()
        {
            lock (Sync)
            {
                if (_refCount > 0)
                    _refCount--;
                Trace($"Release -> refCount={_refCount} (subsystem stays alive)");
            }
        }

        public static bool IsConnected(int slot)
        {
            return slot >= 0 && slot < MaxSlots && Connected[slot];
        }

        public static State GetState(int slot)
        {
            lock (Sync)
            {
                return slot >= 0 && slot < MaxSlots ? States[slot] : default;
            }
        }

        private static void PollLoop()
        {
            Trace("PollLoop started");
            try
            {
                while (_running)
                {
                    SDL.SDL_GameControllerUpdate();
                    RefreshDeviceSlots();

                    lock (Sync)
                    {
                        for (int slot = 0; slot < MaxSlots; slot++)
                        {
                            if (Controllers[slot] == IntPtr.Zero)
                            {
                                Connected[slot] = false;
                                continue;
                            }

                            var gamepad = ReadGamepad(Controllers[slot]);
                            if (!gamepad.Equals(States[slot].Gamepad))
                            {
                                if (gamepad.Buttons != States[slot].Gamepad.Buttons)
                                    Trace($"slot {slot} buttons {States[slot].Gamepad.Buttons} -> {gamepad.Buttons} (pkt {States[slot].PacketNumber + 1})");
                                var state = States[slot];
                                state.PacketNumber++;
                                state.Gamepad = gamepad;
                                States[slot] = state;
                            }
                            Connected[slot] = true;
                        }
                    }

                    // Throttle down when nobody is consuming (subsystem must stay
                    // alive — see Acquire — but no need to poll at full rate)
                    Thread.Sleep(_refCount > 0 ? 5 : 250);
                }
            }
            catch (Exception ex)
            {
                Trace($"PollLoop DIED: {ex}");
            }
            finally
            {
                lock (Sync)
                {
                    for (int slot = 0; slot < MaxSlots; slot++)
                    {
                        if (Controllers[slot] != IntPtr.Zero)
                        {
                            SDL.SDL_GameControllerClose(Controllers[slot]);
                            Controllers[slot] = IntPtr.Zero;
                        }
                        Connected[slot] = false;
                    }
                }
                SDL.SDL_QuitSubSystem(SDL.SDL_INIT_GAMECONTROLLER);
                Trace("PollLoop exited, SDL_QuitSubSystem done");
            }
        }

        /// <summary>Handle hot-plug: assign newly attached controllers to free slots, drop detached ones.</summary>
        private static void RefreshDeviceSlots()
        {
            lock (Sync)
            {
                // Drop controllers that went away.
                for (int slot = 0; slot < MaxSlots; slot++)
                {
                    if (Controllers[slot] != IntPtr.Zero &&
                        SDL.SDL_GameControllerGetAttached(Controllers[slot]) == SDL.SDL_bool.SDL_FALSE)
                    {
                        SDL.SDL_GameControllerClose(Controllers[slot]);
                        Controllers[slot] = IntPtr.Zero;
                        Connected[slot] = false;
                        States[slot] = default;
                        Trace($"slot {slot} detached");
                    }
                }

                // Attach new devices to free slots.
                int numJoysticks = SDL.SDL_NumJoysticks();
                for (int deviceIndex = 0; deviceIndex < numJoysticks; deviceIndex++)
                {
                    if (SDL.SDL_IsGameController(deviceIndex) == SDL.SDL_bool.SDL_FALSE)
                        continue;

                    int instanceId = SDL.SDL_JoystickGetDeviceInstanceID(deviceIndex);
                    if (IsInstanceAssigned(instanceId))
                        continue;

                    int freeSlot = Array.IndexOf(Controllers, IntPtr.Zero);
                    if (freeSlot < 0)
                        break;

                    var handle = SDL.SDL_GameControllerOpen(deviceIndex);
                    if (handle == IntPtr.Zero)
                    {
                        Trace($"SDL_GameControllerOpen({deviceIndex}) FAILED: {SDL.SDL_GetError()}");
                        continue;
                    }

                    Controllers[freeSlot] = handle;
                    InstanceIds[freeSlot] = instanceId;
                    Trace($"attached '{SDL.SDL_GameControllerName(handle)}' (instance {instanceId}) to slot {freeSlot}");
                }
            }
        }

        private static bool IsInstanceAssigned(int instanceId)
        {
            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (Controllers[slot] != IntPtr.Zero && InstanceIds[slot] == instanceId)
                    return true;
            }
            return false;
        }

        private static XiGamepad ReadGamepad(IntPtr controller)
        {
            var gamepad = new XiGamepad();

            GamepadButtonFlags buttons = 0;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A)) buttons |= GamepadButtonFlags.A;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B)) buttons |= GamepadButtonFlags.B;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X)) buttons |= GamepadButtonFlags.X;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y)) buttons |= GamepadButtonFlags.Y;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK)) buttons |= GamepadButtonFlags.Back;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START)) buttons |= GamepadButtonFlags.Start;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK)) buttons |= GamepadButtonFlags.LeftThumb;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK)) buttons |= GamepadButtonFlags.RightThumb;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER)) buttons |= GamepadButtonFlags.LeftShoulder;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER)) buttons |= GamepadButtonFlags.RightShoulder;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP)) buttons |= GamepadButtonFlags.DPadUp;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN)) buttons |= GamepadButtonFlags.DPadDown;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT)) buttons |= GamepadButtonFlags.DPadLeft;
            if (Pressed(controller, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT)) buttons |= GamepadButtonFlags.DPadRight;
            gamepad.Buttons = buttons;

            gamepad.LeftThumbX = SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
            // SDL Y axes are positive-down; XInput is positive-up. Bitwise NOT == -v-1, overflow-safe.
            gamepad.LeftThumbY = (short)~SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
            gamepad.RightThumbX = SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
            gamepad.RightThumbY = (short)~SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
            // SDL triggers: 0..32767 -> XInput bytes 0..255.
            gamepad.LeftTrigger = (byte)(SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT) >> 7);
            gamepad.RightTrigger = (byte)(SDL.SDL_GameControllerGetAxis(controller, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT) >> 7);

            return gamepad;
        }

        private static bool Pressed(IntPtr controller, SDL.SDL_GameControllerButton button)
        {
            return SDL.SDL_GameControllerGetButton(controller, button) == 1;
        }
    }
}
