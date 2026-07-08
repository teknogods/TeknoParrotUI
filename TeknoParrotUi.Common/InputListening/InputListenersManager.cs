using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Cross-platform input orchestrator. SDL2 is the only gamepad backend on
    /// every platform (XInput/DirectInput are gone); requested APIs resolve to:
    ///
    /// - Gamepad input: SDL2 listener, always.
    /// - Gun/trackball input: paired platform mouse/keyboard listener —
    ///   Win32 RawInput on Windows, evdev on Linux, touch on Android.
    /// Existing XInputButton bindings are read unchanged by the SDL2 listener.
    /// </summary>
    public class InputListenersManager
    {
        private readonly List<IInputListener> _listeners = new List<IInputListener>();

        /// <summary>The API actually in use after platform resolution.</summary>
        public InputApi EffectiveApi { get; private set; }

        /// <summary>
        /// True when an active listener consumes WM_INPUT and the host must run a
        /// RawInput forward window (Windows only).
        /// </summary>
        public bool NeedsWndProcRouting { get; private set; }

        /// <summary>
        /// Set by the Android head at startup (Common cannot reference Android
        /// APIs): returns an AndroidTouchListener for gun games.
        /// </summary>
        public static Func<IInputListener> AndroidTouchListenerFactory { get; set; }

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons, InputApi requestedApi)
        {
            Stop();
            EffectiveApi = InputApi.SDL2; // the one and only gamepad backend

            // Per-game input-method availability: generated from the GameProfile,
            // or overridden by a user-provided InputProfiles/<game>.json.
            var inputProfile = InputProfileLoader.Load(gameProfile);

            _listeners.Add(new SDL2JoystickListener());

            // Gun/trackball games additionally get a mouse listener:
            // evdev on Linux, the Win32 RawInput pipeline on Windows.
            bool gunIntent = requestedApi == InputApi.RawInput ||
                             requestedApi == InputApi.RawInputTrackball ||
                             requestedApi == InputApi.MergedInput ||
                             gameProfile.GunGame;
            if (gunIntent)
            {
                if (OperatingSystem.IsLinux() &&
                    inputProfile.InputMethods.TryGetValue(InputProfile.Methods.EvdevMouse, out var evdev) &&
                    evdev.Enabled)
                {
                    _listeners.Add(new Mouse.EvdevMouseListener());
                }
                else if (OperatingSystem.IsWindows() && TryGetWindowsGunApi(gameProfile, inputProfile, requestedApi, out var gunApi))
                {
                    _listeners.Add(new RawInputListenerHost(gunApi));
                    NeedsWndProcRouting = true;
                }
                else if (OperatingSystem.IsAndroid() && AndroidTouchListenerFactory != null &&
                         inputProfile.InputMethods.TryGetValue(InputProfile.Methods.AndroidTouch, out var touch) &&
                         touch.Enabled)
                {
                    _listeners.Add(AndroidTouchListenerFactory());
                }
            }

            foreach (var listener in _listeners)
            {
                if (!listener.IsSupported)
                {
                    Debug.WriteLine($"InputListenersManager: {listener.Name} unsupported on this platform, skipping");
                    continue;
                }
                listener.Start(gameProfile, joystickButtons);
                Debug.WriteLine($"InputListenersManager: started {listener.Name}");
            }
        }

        /// <summary>
        /// The RawInput flavour to pair with SDL2 on Windows for gun/trackball
        /// games. An explicit RawInput/Trackball/Merged selection is honoured
        /// directly; otherwise the choice comes from the game's saved value or
        /// its available input methods (InputProfile-driven, so user JSON
        /// overrides apply).
        /// </summary>
        private static bool TryGetWindowsGunApi(GameProfile gameProfile, InputProfile inputProfile, InputApi requestedApi, out InputApi gunApi)
        {
            if (requestedApi is InputApi.RawInput or InputApi.RawInputTrackball or InputApi.MergedInput)
            {
                gunApi = requestedApi;
                return true;
            }

            bool hasRawInput = inputProfile.InputMethods.TryGetValue(InputProfile.Methods.RawInput, out var ri) && ri.Enabled;
            bool hasTrackball = inputProfile.InputMethods.TryGetValue(InputProfile.Methods.RawInputTrackball, out var rit) && rit.Enabled;

            var apiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");

            // Respect a previously saved gun choice (before the user switched to SDL2)
            if (apiField?.FieldValue == "RawInputTrackball" && hasTrackball)
            {
                gunApi = InputApi.RawInputTrackball;
                return true;
            }
            if (hasRawInput)
            {
                gunApi = InputApi.RawInput;
                return true;
            }
            if (hasTrackball)
            {
                gunApi = InputApi.RawInputTrackball;
                return true;
            }
            gunApi = default;
            return false;
        }

        /// <summary>Route Win32 window messages (WM_INPUT) to RawInput listeners.</summary>
        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            foreach (var listener in _listeners)
                listener.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
        }

        public void Stop()
        {
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.Stop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InputListenersManager: error stopping {listener.Name}: {ex.Message}");
                }
            }
            _listeners.Clear();
            NeedsWndProcRouting = false;
        }
    }

    /// <summary>
    /// Hosts the Windows gun/mouse/keyboard pipeline (<see cref="InputListener"/>:
    /// RawInput / RawInputTrackball / their MergedInput combination) behind
    /// <see cref="IInputListener"/> so it plugs into the manager.
    /// </summary>
    internal sealed class RawInputListenerHost : IInputListener
    {
        private readonly InputApi _api;
        private readonly InputListener _inner = new InputListener();
        private Thread _thread;

        public RawInputListenerHost(InputApi api)
        {
            _api = api;
        }

        public string Name => $"RawInput({_api})";
        public bool IsSupported => OperatingSystem.IsWindows();

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            bool useSto0Z = Lazydata.ParrotData != null && Lazydata.ParrotData.UseSto0ZDrivingHack;
            int stoozPercent = Lazydata.ParrotData != null ? Lazydata.ParrotData.StoozPercent : 0;
            _thread = new Thread(() => _inner.Listen(useSto0Z, stoozPercent, joystickButtons, _api, gameProfile))
            {
                IsBackground = true,
                Name = "LegacyInputListener"
            };
            _thread.Start();
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            _inner.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
        }

        public void Stop()
        {
            _inner.StopListening();
            _thread?.Join(2000);
        }
    }
}
