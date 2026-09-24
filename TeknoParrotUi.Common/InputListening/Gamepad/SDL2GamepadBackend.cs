using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using SDL2;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Cross-platform gamepad backend. SDL GameController devices retain their
    /// XInput-shaped state; every SDL joystick also exposes its physical
    /// buttons, axes and hats so unmapped arcade controllers are usable.
    /// Android supplies the same snapshots through its native input events.
    ///
    /// SDL calls are confined to a single poll thread; consumers read cached
    /// state via <see cref="GetState"/> / <see cref="IsConnected"/>.
    /// </summary>
    public static class SDL2GamepadBackend
    {
        public const int MaxSlots = 8;

        private static readonly object Sync = new object();
        private static readonly IntPtr[] Controllers = new IntPtr[MaxSlots];
        private static readonly IntPtr[] Joysticks = new IntPtr[MaxSlots];
        private static readonly bool[] OwnsJoystick = new bool[MaxSlots];
        private static readonly bool[] PlatformSlots = new bool[MaxSlots];
        private static readonly string[] Names = new string[MaxSlots];
        private static readonly int[] InstanceIds = new int[MaxSlots];
        private static readonly State[] States = new State[MaxSlots];
        private static readonly RawJoystickState[] RawStates = new RawJoystickState[MaxSlots];
        private static readonly bool[] Connected = new bool[MaxSlots];

        private static Thread _pollThread;
        private static volatile bool _running;
        private static int _refCount;
        private static bool _linuxResolverConfigured;
        private static string _nativeSource = "bundled SDL2";
        private static string _backendStatus = "SDL2 has not started";

        public static string BackendStatus => _backendStatus;

        private static void ConfigureLinuxNativeLibrary()
        {
            if (!OperatingSystem.IsLinux() || _linuxResolverConfigured)
                return;

            // SDL2-CS ships SDL 2.0.14 for Linux, older than the Steam Deck.
            // Prefer the distribution's current SDL2 before the first P/Invoke;
            // retain the bundled library for systems without a system SDL2.
            NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, (name, _, _) =>
            {
                if (name != "SDL2")
                    return IntPtr.Zero;
                if (NativeLibrary.TryLoad("libSDL2-2.0.so.0", out var library))
                {
                    _nativeSource = "system SDL2";
                    return library;
                }
                _nativeSource = "bundled SDL2 (system SDL2 unavailable)";
                return IntPtr.Zero;
            });
            _linuxResolverConfigured = true;
        }

        // Android has no SDL2 native library in the APK. Its Activity supplies
        // physical gamepad events through these same slot snapshots.
        public static Action PlatformDeviceRefresh { get; set; }

        public readonly record struct PlatformGamepadDevice(int Id, string Name, short[] AxisRest);

        public static void UpdatePlatformDevices(IReadOnlyList<PlatformGamepadDevice> devices)
        {
            lock (Sync)
            {
                for (int slot = 0; slot < MaxSlots; slot++)
                {
                    if (!PlatformSlots[slot]) continue;
                    var found = false;
                    foreach (var device in devices)
                        if (device.Id == InstanceIds[slot]) { found = true; break; }
                    if (!found) ClearPlatformSlot(slot);
                }
                foreach (var device in devices)
                {
                    if (FindPlatformSlot(device.Id) >= 0) continue;
                    var slot = Array.FindIndex(Connected, connected => !connected);
                    if (slot < 0) break;
                    PlatformSlots[slot] = true;
                    InstanceIds[slot] = device.Id;
                    Names[slot] = device.Name;
                    RawStates[slot] = new RawJoystickState(new bool[256],
                        device.AxisRest is { Length: 64 } ? (short[])device.AxisRest.Clone() : new short[64],
                        Array.Empty<byte>());
                    Connected[slot] = true;
                }
            }
        }

        public static void UpdatePlatformButton(int deviceId, int code, bool pressed)
        {
            lock (Sync)
            {
                var slot = FindPlatformSlot(deviceId);
                if (slot < 0 || code < 0 || code >= 256) return;
                var old = RawStates[slot];
                if (old.Button(code) == pressed) return;
                var buttons = (bool[])old.Buttons.Clone();
                buttons[code] = pressed;
                RawStates[slot] = new RawJoystickState(buttons, old.Axes, old.Hats);
                var state = States[slot];
                state.PacketNumber++;
                States[slot] = state;
            }
        }

        public static void UpdatePlatformAxis(int deviceId, int code, short value)
        {
            lock (Sync)
            {
                var slot = FindPlatformSlot(deviceId);
                if (slot < 0 || code < 0 || code >= 64) return;
                var old = RawStates[slot];
                if (old.Axis(code) == value) return;
                var axes = (short[])old.Axes.Clone();
                axes[code] = value;
                RawStates[slot] = new RawJoystickState(old.Buttons, axes, old.Hats);
                var state = States[slot];
                state.PacketNumber++;
                States[slot] = state;
            }
        }

        private static int FindPlatformSlot(int deviceId)
        {
            for (int slot = 0; slot < MaxSlots; slot++)
                if (PlatformSlots[slot] && InstanceIds[slot] == deviceId) return slot;
            return -1;
        }

        public static bool HasPlatformDevice(int deviceId)
        {
            lock (Sync)
                return FindPlatformSlot(deviceId) >= 0;
        }

        private static void ClearPlatformSlot(int slot)
        {
            PlatformSlots[slot] = false;
            Connected[slot] = false;
            Names[slot] = null;
            InstanceIds[slot] = 0;
            States[slot] = default;
            RawStates[slot] = RawJoystickState.Empty;
        }

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
                if (OperatingSystem.IsAndroid())
                {
                    PlatformDeviceRefresh?.Invoke();
                    return;
                }
                if (_running)
                    return;

                try
                {
                    ConfigureLinuxNativeLibrary();
                    // Allow gamepad input while the game window (not ours) has focus.
                    SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

                    if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_GAMECONTROLLER) != 0)
                    {
                        _backendStatus = $"SDL2 initialization failed: {SDL.SDL_GetError()}";
                        Trace(_backendStatus);
                        return;
                    }
                    SDL.SDL_GetVersion(out var version);
                    _backendStatus = $"{_nativeSource}: {version.major}.{version.minor}.{version.patch}";
                    Trace($"SDL_InitSubSystem ok ({_backendStatus})");
                }
                catch (DllNotFoundException error)
                {
                    // Native SDL2 not available on this platform/package (e.g. Android
                    // head without SDL natives) — gamepad input disabled, no crash.
                    _backendStatus = "SDL2 unavailable; install libSDL2-2.0.so.0";
                    Trace(_backendStatus + ": " + error.Message);
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

        public static string GetDeviceName(int slot)
        {
            lock (Sync)
                return slot >= 0 && slot < MaxSlots ? Names[slot] : null;
        }

        public static RawJoystickState GetRawState(int slot)
        {
            lock (Sync)
                return slot >= 0 && slot < MaxSlots ? RawStates[slot] ?? RawJoystickState.Empty : RawJoystickState.Empty;
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
                    SDL.SDL_JoystickUpdate();
                    RefreshDeviceSlots();

                    lock (Sync)
                    {
                        for (int slot = 0; slot < MaxSlots; slot++)
                        {
                            if (Joysticks[slot] == IntPtr.Zero)
                            {
                                Connected[slot] = false;
                                continue;
                            }

                            var gamepad = Controllers[slot] != IntPtr.Zero
                                ? ReadGamepad(Controllers[slot]) : default;
                            var raw = ReadRawJoystick(Joysticks[slot]);
                            if (!gamepad.Equals(States[slot].Gamepad) ||
                                !raw.SameControls(RawStates[slot] ?? RawJoystickState.Empty))
                            {
                                if (gamepad.Buttons != States[slot].Gamepad.Buttons)
                                    Trace($"slot {slot} buttons {States[slot].Gamepad.Buttons} -> {gamepad.Buttons} (pkt {States[slot].PacketNumber + 1})");
                                var state = States[slot];
                                state.PacketNumber++;
                                state.Gamepad = gamepad;
                                States[slot] = state;
                                RawStates[slot] = raw;
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
                _backendStatus = "SDL2 polling stopped: " + ex.Message;
                Trace($"PollLoop DIED: {ex}");
            }
            finally
            {
                lock (Sync)
                {
                    for (int slot = 0; slot < MaxSlots; slot++)
                    {
                        CloseSlot(slot);
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
                    if (Joysticks[slot] != IntPtr.Zero &&
                        SDL.SDL_JoystickGetAttached(Joysticks[slot]) == SDL.SDL_bool.SDL_FALSE)
                    {
                        CloseSlot(slot);
                        Trace($"slot {slot} detached");
                    }
                }

                // Attach new devices to free slots.
                int numJoysticks = SDL.SDL_NumJoysticks();
                for (int deviceIndex = 0; deviceIndex < numJoysticks; deviceIndex++)
                {
                    int instanceId = SDL.SDL_JoystickGetDeviceInstanceID(deviceIndex);
                    if (IsInstanceAssigned(instanceId))
                        continue;

                    int freeSlot = Array.FindIndex(Connected, connected => !connected);
                    if (freeSlot < 0)
                        break;

                    if (SDL.SDL_IsGameController(deviceIndex) == SDL.SDL_bool.SDL_TRUE)
                    {
                        var controller = SDL.SDL_GameControllerOpen(deviceIndex);
                        if (controller != IntPtr.Zero)
                        {
                            Controllers[freeSlot] = controller;
                            Joysticks[freeSlot] = SDL.SDL_GameControllerGetJoystick(controller);
                        }
                    }
                    if (Joysticks[freeSlot] == IntPtr.Zero)
                    {
                        Joysticks[freeSlot] = SDL.SDL_JoystickOpen(deviceIndex);
                        OwnsJoystick[freeSlot] = Joysticks[freeSlot] != IntPtr.Zero;
                    }
                    if (Joysticks[freeSlot] == IntPtr.Zero)
                    {
                        Trace($"SDL_JoystickOpen({deviceIndex}) FAILED: {SDL.SDL_GetError()}");
                        continue;
                    }
                    InstanceIds[freeSlot] = instanceId;
                    Names[freeSlot] = SDL.SDL_JoystickNameForIndex(deviceIndex);
                    RawStates[freeSlot] = ReadRawJoystick(Joysticks[freeSlot]);
                    Connected[freeSlot] = true;
                    Trace($"attached '{Names[freeSlot]}' (instance {instanceId}) to slot {freeSlot}, mapped={Controllers[freeSlot] != IntPtr.Zero}");
                }
            }
        }

        private static bool IsInstanceAssigned(int instanceId)
        {
            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (Joysticks[slot] != IntPtr.Zero && InstanceIds[slot] == instanceId)
                    return true;
            }
            return false;
        }

        private static void CloseSlot(int slot)
        {
            if (OwnsJoystick[slot] && Joysticks[slot] != IntPtr.Zero)
                SDL.SDL_JoystickClose(Joysticks[slot]);
            if (Controllers[slot] != IntPtr.Zero)
                SDL.SDL_GameControllerClose(Controllers[slot]);
            Controllers[slot] = IntPtr.Zero;
            Joysticks[slot] = IntPtr.Zero;
            OwnsJoystick[slot] = false;
            Names[slot] = null;
            InstanceIds[slot] = 0;
            Connected[slot] = false;
            States[slot] = default;
            RawStates[slot] = RawJoystickState.Empty;
        }

        private static RawJoystickState ReadRawJoystick(IntPtr joystick)
        {
            var buttons = new bool[Math.Max(0, SDL.SDL_JoystickNumButtons(joystick))];
            var axes = new short[Math.Max(0, SDL.SDL_JoystickNumAxes(joystick))];
            var hats = new byte[Math.Max(0, SDL.SDL_JoystickNumHats(joystick))];
            for (int i = 0; i < buttons.Length; i++) buttons[i] = SDL.SDL_JoystickGetButton(joystick, i) != 0;
            for (int i = 0; i < axes.Length; i++) axes[i] = SDL.SDL_JoystickGetAxis(joystick, i);
            for (int i = 0; i < hats.Length; i++) hats[i] = SDL.SDL_JoystickGetHat(joystick, i);
            return new RawJoystickState(buttons, axes, hats);
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
