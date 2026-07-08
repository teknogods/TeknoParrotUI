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

        /// <summary>Start (or ref-count) the backend poll thread.</summary>
        public static void Acquire()
        {
            lock (Sync)
            {
                _refCount++;
                if (_running)
                    return;

                try
                {
                    // Allow gamepad input while the game window (not ours) has focus.
                    SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

                    if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_GAMECONTROLLER) != 0)
                    {
                        Debug.WriteLine($"SDL2GamepadBackend: SDL_InitSubSystem failed: {SDL.SDL_GetError()}");
                        return;
                    }
                }
                catch (DllNotFoundException)
                {
                    // Native SDL2 not available on this platform/package (e.g. Android
                    // head without SDL natives) — gamepad input disabled, no crash.
                    Debug.WriteLine("SDL2GamepadBackend: native SDL2 library not found, gamepad input disabled");
                    return;
                }

                _running = true;
                _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "SDL2GamepadBackend" };
                _pollThread.Start();
            }
        }

        /// <summary>Release the backend; shuts down when the last consumer releases.</summary>
        public static void Release()
        {
            Thread pollThread;
            lock (Sync)
            {
                if (_refCount > 0)
                    _refCount--;
                if (_refCount > 0 || !_running)
                    return;
                _running = false;
                pollThread = _pollThread;
                _pollThread = null;
            }
            pollThread?.Join(2000);
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
                                var state = States[slot];
                                state.PacketNumber++;
                                state.Gamepad = gamepad;
                                States[slot] = state;
                            }
                            Connected[slot] = true;
                        }
                    }

                    Thread.Sleep(5);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SDL2GamepadBackend poll loop died: {ex}");
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
                        continue;

                    Controllers[freeSlot] = handle;
                    InstanceIds[freeSlot] = instanceId;
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
