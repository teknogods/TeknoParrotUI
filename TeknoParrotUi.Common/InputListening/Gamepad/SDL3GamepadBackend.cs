using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Cross-platform gamepad backend. SDL3 gamepads retain their
    /// XInput-shaped state; every SDL joystick also exposes its physical
    /// buttons, axes and hats so unmapped arcade controllers are usable.
    /// Android supplies the same snapshots through its native input events.
    ///
    /// SDL is initialized on the UI thread, then polled on one worker thread;
    /// consumers read cached state via <see cref="GetState"/> /
    /// <see cref="IsConnected"/>.
    /// </summary>
    public static class SDL3GamepadBackend
    {
        // Covers multi-board arcade cabinets and several player controllers.
        // Binding slot indexes remain the same for existing profiles.
        public const int MaxSlots = 16;

        private static readonly object Sync = new object();
        private static readonly IntPtr[] Gamepads = new IntPtr[MaxSlots];
        private static readonly IntPtr[] Joysticks = new IntPtr[MaxSlots];
        private static readonly bool[] OwnsJoystick = new bool[MaxSlots];
        private static readonly bool[] PlatformSlots = new bool[MaxSlots];
        private static readonly string[] Names = new string[MaxSlots];
        private static readonly int[] InstanceIds = new int[MaxSlots];
        private static readonly uint[] SdlInstanceIds = new uint[MaxSlots];
        private static readonly State[] States = new State[MaxSlots];
        private static readonly RawJoystickState[] RawStates = new RawJoystickState[MaxSlots];
        private static readonly bool[] Connected = new bool[MaxSlots];

        private static Thread _pollThread;
        private static volatile bool _running;
        private static int _refCount;
        private static bool _initialized;
        private static bool _nativeResolverConfigured;
        private static string _nativeSource = "SDL3 runtime";
        private static string _backendStatus = "SDL3 has not started";

        public static string BackendStatus => _backendStatus;

        private static void ConfigureNativeLibrary()
        {
            if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS()) ||
                _nativeResolverConfigured)
                return;

            NativeLibrary.SetDllImportResolver(typeof(SDL3Native).Assembly, (name, _, _) =>
            {
                if (name != "SDL3")
                    return IntPtr.Zero;
                if (OperatingSystem.IsLinux())
                {
                    var architecture = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => "linux-x64",
                        Architecture.Arm64 => "linux-arm64",
                        _ => null
                    };
                    if (architecture != null)
                    {
                        foreach (var file in new[] { "libSDL3.so.0", "libSDL3.so" })
                        {
                            var vendoredPath = System.IO.Path.Combine(AppContext.BaseDirectory,
                                "Native", "SDL3", architecture, file);
                            if (NativeLibrary.TryLoad(vendoredPath, out var vendored))
                            {
                                _nativeSource = "bundled SDL3";
                                return vendored;
                            }
                        }
                        foreach (var file in new[] { "libSDL3.so.0", "libSDL3.so" })
                        {
                            var path = System.IO.Path.Combine(AppContext.BaseDirectory,
                                "runtimes", architecture, "native", file);
                            if (NativeLibrary.TryLoad(path, out var library))
                            {
                                _nativeSource = "bundled SDL3";
                                return library;
                            }
                        }
                    }
                    foreach (var file in new[] { "libSDL3.so.0", "libSDL3.so" })
                    {
                        foreach (var folder in new[] { AppContext.BaseDirectory,
                                     System.IO.Path.Combine(AppContext.BaseDirectory, "libs") })
                        {
                            var bundledPath = System.IO.Path.Combine(folder, file);
                            if (NativeLibrary.TryLoad(bundledPath, out var bundled))
                            {
                                _nativeSource = "bundled SDL3";
                                return bundled;
                            }
                        }
                        if (NativeLibrary.TryLoad(file, out var resolved))
                        {
                            // A single-file .NET host can resolve its extracted
                            // bundled native asset through this bare name too.
                            _nativeSource = "runtime-resolved SDL3";
                            return resolved;
                        }
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    var architecture = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => "win-x64",
                        Architecture.X86 => "win-x86",
                        Architecture.Arm64 => "win-arm64",
                        _ => null
                    };
                    if (architecture != null)
                    {
                        var path = System.IO.Path.Combine(AppContext.BaseDirectory,
                            "Native", "SDL3", architecture, "SDL3.dll");
                        if (NativeLibrary.TryLoad(path, out var library))
                        {
                            _nativeSource = "bundled SDL3";
                            return library;
                        }
                    }
                    var legacyPublishPath = System.IO.Path.Combine(AppContext.BaseDirectory, "libs", "SDL3.dll");
                    if (NativeLibrary.TryLoad(legacyPublishPath, out var published))
                    {
                        _nativeSource = "bundled SDL3";
                        return published;
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    var architecture = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => "osx-x64",
                        Architecture.Arm64 => "osx-arm64",
                        _ => null
                    };
                    if (architecture != null)
                    {
                        var vendoredPath = System.IO.Path.Combine(AppContext.BaseDirectory,
                            "Native", "SDL3", architecture, "libSDL3.dylib");
                        if (NativeLibrary.TryLoad(vendoredPath, out var vendored))
                        {
                            _nativeSource = "bundled SDL3";
                            return vendored;
                        }
                        var path = System.IO.Path.Combine(AppContext.BaseDirectory,
                            "runtimes", architecture, "native", "libSDL3.dylib");
                        if (NativeLibrary.TryLoad(path, out var library))
                        {
                            _nativeSource = "bundled SDL3";
                            return library;
                        }
                    }
                    foreach (var folder in new[] { AppContext.BaseDirectory,
                                 System.IO.Path.Combine(AppContext.BaseDirectory, "libs") })
                    {
                        var path = System.IO.Path.Combine(folder, "libSDL3.dylib");
                        if (NativeLibrary.TryLoad(path, out var library))
                        {
                            _nativeSource = "bundled SDL3";
                            return library;
                        }
                    }
                    if (NativeLibrary.TryLoad("libSDL3.dylib", out var resolved))
                    {
                        _nativeSource = "runtime-resolved SDL3";
                        return resolved;
                    }
                }
                return IntPtr.Zero;
            });
            _nativeResolverConfigured = true;
        }

        // Android uses its OS controller API. Its Activity supplies
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
                if (OperatingSystem.IsAndroid())
                    _backendStatus = $"Android controller API: {devices.Count} detected, {Array.FindAll(PlatformSlots, connected => connected).Length} available";
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

        public static void UpdatePlatformGamepad(int deviceId, XiGamepad gamepad)
        {
            lock (Sync)
            {
                var slot = FindPlatformSlot(deviceId);
                if (slot < 0 || States[slot].Gamepad.Equals(gamepad)) return;
                var state = States[slot];
                state.Gamepad = gamepad;
                state.PacketNumber++;
                States[slot] = state;
            }
        }

        public static XiGamepad GetPlatformGamepad(int deviceId)
        {
            lock (Sync)
            {
                var slot = FindPlatformSlot(deviceId);
                return slot >= 0 ? States[slot].Gamepad : default;
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

        // Temporary lifecycle tracing: set TP_SDL3_TRACE=1 to log to %TEMP%\tp-sdl3-trace.log
        private static readonly bool TraceEnabled = Environment.GetEnvironmentVariable("TP_SDL3_TRACE") == "1";
        /// <summary>Temporary debug tracing hook, also usable by consumers.</summary>
        public static void Trace(string msg)
        {
            Debug.WriteLine("SDL3GamepadBackend: " + msg);
            if (!TraceEnabled) return;
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tp-sdl3-trace.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>
        /// Initialize SDL3 before starting a background listener. SDL3 documents
        /// subsystem initialization as a main-thread operation, so desktop hosts
        /// call this during UI startup. Android uses native Android input instead.
        /// </summary>
        public static void InitializeOnMainThread()
        {
            if (OperatingSystem.IsAndroid()) return;
            lock (Sync)
                InitializeNative();
        }

        private static bool InitializeNative()
        {
            if (_initialized) return true;
            try
            {
                ConfigureNativeLibrary();
                var version = SDL3Native.SDL_GetVersion();
                if (version < 3002000)
                {
                    _backendStatus = "SDL3 3.2.0 or newer is required";
                    Trace(_backendStatus);
                    return false;
                }
                // Continue receiving controls when a launched game has focus.
                SDL3Native.SDL_SetHint(SDL3Native.JoystickAllowBackgroundEvents, "1");
                if (SDL3Native.SDL_InitSubSystem(SDL3Native.InitGamepad) == 0)
                {
                    _backendStatus = $"SDL3 initialization failed: {SDL3Native.GetError()}";
                    Trace(_backendStatus);
                    return false;
                }

                _backendStatus = $"{_nativeSource}: {version / 1000000}.{version / 1000 % 1000}.{version % 1000}";
                _initialized = true;
                Trace($"SDL_InitSubSystem ok ({_backendStatus})");
                return true;
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                _backendStatus = OperatingSystem.IsLinux()
                    ? "SDL3 unavailable; install libSDL3.so.0 or use the bundled Linux runtime"
                    : "SDL3 unavailable; the matching SDL3 runtime is missing";
                Trace(_backendStatus + ": " + error.Message);
                return false;
            }
        }

        /// <summary>
        /// Start (or ref-count) the backend. SDL stays initialized for the
        /// process lifetime; the poll thread throttles down while nobody holds
        /// a reference. This also avoids moving subsystem initialization to a
        /// background thread during later game sessions.
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

                // Desktop UI startup calls InitializeOnMainThread first. This
                // fallback keeps command-line and test hosts functional.
                if (!InitializeNative())
                    return;

                _running = true;
                _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "SDL3GamepadBackend" };
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
            lock (Sync)
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
                    SDL3Native.SDL_UpdateGamepads();
                    SDL3Native.SDL_UpdateJoysticks();
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

                            var gamepad = Gamepads[slot] != IntPtr.Zero
                                ? ReadGamepad(Gamepads[slot]) : default;
                            var previousRaw = RawStates[slot] ?? RawJoystickState.Empty;
                            var raw = ReadRawJoystick(Joysticks[slot], previousRaw);
                            if (!gamepad.Equals(States[slot].Gamepad) ||
                                !ReferenceEquals(raw, previousRaw))
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
                _backendStatus = "SDL3 polling stopped: " + ex.Message;
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
                    _running = false;
                }
                // Keep SDL initialized for the lifetime of the process. A later
                // Acquire can restart polling without initializing it off-main.
                Trace("PollLoop exited");
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
                        SDL3Native.SDL_JoystickConnected(Joysticks[slot]) == 0)
                    {
                        CloseSlot(slot);
                        Trace($"slot {slot} detached");
                    }
                }

                // SDL3 returns an allocated list of persistent instance IDs.
                var available = SDL3Native.SDL_GetJoysticks(out var numJoysticks);
                if (available == IntPtr.Zero)
                    return;
                try
                {
                    for (int deviceIndex = 0; deviceIndex < numJoysticks; deviceIndex++)
                    {
                        var instanceId = unchecked((uint)Marshal.ReadInt32(available, deviceIndex * sizeof(uint)));
                        if (instanceId == 0 || IsInstanceAssigned(instanceId))
                            continue;

                        int freeSlot = Array.FindIndex(Connected, connected => !connected);
                        if (freeSlot < 0)
                            break;

                        if (SDL3Native.SDL_IsGamepad(instanceId) != 0)
                        {
                            var gamepad = SDL3Native.SDL_OpenGamepad(instanceId);
                            if (gamepad != IntPtr.Zero)
                            {
                                var joystick = SDL3Native.SDL_GetGamepadJoystick(gamepad);
                                if (joystick != IntPtr.Zero)
                                {
                                    Gamepads[freeSlot] = gamepad;
                                    Joysticks[freeSlot] = joystick;
                                }
                                else
                                    SDL3Native.SDL_CloseGamepad(gamepad);
                            }
                        }
                        if (Joysticks[freeSlot] == IntPtr.Zero)
                        {
                            Joysticks[freeSlot] = SDL3Native.SDL_OpenJoystick(instanceId);
                            OwnsJoystick[freeSlot] = Joysticks[freeSlot] != IntPtr.Zero;
                        }
                        if (Joysticks[freeSlot] == IntPtr.Zero)
                        {
                            Trace($"SDL_OpenJoystick({instanceId}) FAILED: {SDL3Native.GetError()}");
                            continue;
                        }
                        SdlInstanceIds[freeSlot] = instanceId;
                        Names[freeSlot] = SDL3Native.GetJoystickNameForID(instanceId);
                        RawStates[freeSlot] = ReadRawJoystick(Joysticks[freeSlot], RawJoystickState.Empty);
                        Connected[freeSlot] = true;
                        Trace($"attached '{Names[freeSlot]}' (instance {instanceId}) to slot {freeSlot}, mapped={Gamepads[freeSlot] != IntPtr.Zero}");
                    }
                }
                finally
                {
                    SDL3Native.SDL_free(available);
                }
            }
        }

        private static bool IsInstanceAssigned(uint instanceId)
        {
            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (Joysticks[slot] != IntPtr.Zero && SdlInstanceIds[slot] == instanceId)
                    return true;
            }
            return false;
        }

        private static void CloseSlot(int slot)
        {
            if (OwnsJoystick[slot] && Joysticks[slot] != IntPtr.Zero)
                SDL3Native.SDL_CloseJoystick(Joysticks[slot]);
            if (Gamepads[slot] != IntPtr.Zero)
                SDL3Native.SDL_CloseGamepad(Gamepads[slot]);
            Gamepads[slot] = IntPtr.Zero;
            Joysticks[slot] = IntPtr.Zero;
            OwnsJoystick[slot] = false;
            Names[slot] = null;
            InstanceIds[slot] = 0;
            SdlInstanceIds[slot] = 0;
            Connected[slot] = false;
            States[slot] = default;
            RawStates[slot] = RawJoystickState.Empty;
        }

        private static RawJoystickState ReadRawJoystick(IntPtr joystick, RawJoystickState previous)
        {
            var buttonCount = Math.Max(0, SDL3Native.SDL_GetNumJoystickButtons(joystick));
            var axisCount = Math.Max(0, SDL3Native.SDL_GetNumJoystickAxes(joystick));
            var hatCount = Math.Max(0, SDL3Native.SDL_GetNumJoystickHats(joystick));
            if (previous.Buttons.Length != buttonCount || previous.Axes.Length != axisCount ||
                previous.Hats.Length != hatCount)
            {
                var allButtons = new bool[buttonCount];
                var allAxes = new short[axisCount];
                var allHats = new byte[hatCount];
                for (int i = 0; i < buttonCount; i++) allButtons[i] = SDL3Native.SDL_GetJoystickButton(joystick, i) != 0;
                for (int i = 0; i < axisCount; i++) allAxes[i] = SDL3Native.SDL_GetJoystickAxis(joystick, i);
                for (int i = 0; i < hatCount; i++) allHats[i] = SDL3Native.SDL_GetJoystickHat(joystick, i);
                return new RawJoystickState(allButtons, allAxes, allHats);
            }

            bool[] buttons = null;
            short[] axes = null;
            byte[] hats = null;
            for (int i = 0; i < buttonCount; i++)
            {
                var value = SDL3Native.SDL_GetJoystickButton(joystick, i) != 0;
                if (value == previous.Buttons[i]) continue;
                buttons ??= (bool[])previous.Buttons.Clone();
                buttons[i] = value;
            }
            for (int i = 0; i < axisCount; i++)
            {
                var value = SDL3Native.SDL_GetJoystickAxis(joystick, i);
                if (value == previous.Axes[i]) continue;
                axes ??= (short[])previous.Axes.Clone();
                axes[i] = value;
            }
            for (int i = 0; i < hatCount; i++)
            {
                var value = SDL3Native.SDL_GetJoystickHat(joystick, i);
                if (value == previous.Hats[i]) continue;
                hats ??= (byte[])previous.Hats.Clone();
                hats[i] = value;
            }
            return buttons == null && axes == null && hats == null
                ? previous
                : new RawJoystickState(buttons ?? previous.Buttons, axes ?? previous.Axes, hats ?? previous.Hats);
        }

        private static XiGamepad ReadGamepad(IntPtr controller)
        {
            var gamepad = new XiGamepad();

            GamepadButtonFlags buttons = 0;
            if (Pressed(controller, SDL3Native.GamepadButton.South)) buttons |= GamepadButtonFlags.A;
            if (Pressed(controller, SDL3Native.GamepadButton.East)) buttons |= GamepadButtonFlags.B;
            if (Pressed(controller, SDL3Native.GamepadButton.West)) buttons |= GamepadButtonFlags.X;
            if (Pressed(controller, SDL3Native.GamepadButton.North)) buttons |= GamepadButtonFlags.Y;
            if (Pressed(controller, SDL3Native.GamepadButton.Back)) buttons |= GamepadButtonFlags.Back;
            if (Pressed(controller, SDL3Native.GamepadButton.Start)) buttons |= GamepadButtonFlags.Start;
            if (Pressed(controller, SDL3Native.GamepadButton.LeftStick)) buttons |= GamepadButtonFlags.LeftThumb;
            if (Pressed(controller, SDL3Native.GamepadButton.RightStick)) buttons |= GamepadButtonFlags.RightThumb;
            if (Pressed(controller, SDL3Native.GamepadButton.LeftShoulder)) buttons |= GamepadButtonFlags.LeftShoulder;
            if (Pressed(controller, SDL3Native.GamepadButton.RightShoulder)) buttons |= GamepadButtonFlags.RightShoulder;
            if (Pressed(controller, SDL3Native.GamepadButton.DPadUp)) buttons |= GamepadButtonFlags.DPadUp;
            if (Pressed(controller, SDL3Native.GamepadButton.DPadDown)) buttons |= GamepadButtonFlags.DPadDown;
            if (Pressed(controller, SDL3Native.GamepadButton.DPadLeft)) buttons |= GamepadButtonFlags.DPadLeft;
            if (Pressed(controller, SDL3Native.GamepadButton.DPadRight)) buttons |= GamepadButtonFlags.DPadRight;
            gamepad.Buttons = buttons;

            gamepad.LeftThumbX = SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.LeftX);
            // SDL Y axes are positive-down; XInput is positive-up. Bitwise NOT == -v-1, overflow-safe.
            gamepad.LeftThumbY = (short)~SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.LeftY);
            gamepad.RightThumbX = SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.RightX);
            gamepad.RightThumbY = (short)~SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.RightY);
            // SDL triggers: 0..32767 -> XInput bytes 0..255.
            gamepad.LeftTrigger = (byte)(SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.LeftTrigger) >> 7);
            gamepad.RightTrigger = (byte)(SDL3Native.SDL_GetGamepadAxis(controller, SDL3Native.GamepadAxis.RightTrigger) >> 7);

            return gamepad;
        }

        private static bool Pressed(IntPtr controller, SDL3Native.GamepadButton button)
        {
            return SDL3Native.SDL_GetGamepadButton(controller, button) != 0;
        }
    }
}
