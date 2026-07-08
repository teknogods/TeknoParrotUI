using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Cross-platform input orchestrator. Resolves the requested
    /// <see cref="InputApi"/> against the current platform and starts the
    /// appropriate listeners:
    ///
    /// - Windows, legacy APIs (DirectInput/XInput/RawInput/Trackball/Merged):
    ///   delegates to the proven <see cref="InputListener"/> pipeline unchanged.
    /// - <see cref="InputApi.SDL2"/> (any platform): SDL2 gamepad listener.
    /// - Non-Windows: every gamepad API request is transparently served by SDL2
    ///   (SharpDX and Win32 RawInput are unavailable).
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
            EffectiveApi = ResolveApi(requestedApi);

            // Per-game input-method availability: generated from the GameProfile,
            // or overridden by a user-provided InputProfiles/<game>.json.
            var inputProfile = InputProfileLoader.Load(gameProfile);

            if (EffectiveApi == InputApi.SDL2)
            {
                _listeners.Add(new SDL2JoystickListener());

                // Gun/trackball games additionally get a mouse listener:
                // evdev on Linux, the legacy RawInput pipeline on Windows.
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
                    else if (OperatingSystem.IsWindows() && TryGetWindowsGunApi(gameProfile, inputProfile, out var gunApi))
                    {
                        _listeners.Add(new LegacyInputListenerAdapter(gunApi));
                        NeedsWndProcRouting = true;
                    }
                    else if (OperatingSystem.IsAndroid() && AndroidTouchListenerFactory != null &&
                             inputProfile.InputMethods.TryGetValue(InputProfile.Methods.AndroidTouch, out var touch) &&
                             touch.Enabled)
                    {
                        _listeners.Add(AndroidTouchListenerFactory());
                    }
                }
            }
            else
            {
                _listeners.Add(new LegacyInputListenerAdapter(EffectiveApi));
                NeedsWndProcRouting = EffectiveApi == InputApi.RawInput ||
                                      EffectiveApi == InputApi.RawInputTrackball ||
                                      EffectiveApi == InputApi.MergedInput;
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
        /// The RawInput API to pair with SDL2 on Windows for gun/trackball games,
        /// taken from the game's saved choice or its available input methods
        /// (InputProfile-driven, so user JSON overrides apply).
        /// </summary>
        private static bool TryGetWindowsGunApi(GameProfile gameProfile, InputProfile inputProfile, out InputApi gunApi)
        {
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

        private static InputApi ResolveApi(InputApi requested)
        {
            if (OperatingSystem.IsWindows())
                return requested;

            // Non-Windows: SharpDX (DirectInput/XInput) and Win32 RawInput do not
            // exist. SDL2 serves gamepad input; gun-game mouse listeners for
            // Linux (evdev) arrive in a later phase.
            if (requested != InputApi.SDL2)
                Debug.WriteLine($"InputListenersManager: '{requested}' not available on this platform, using SDL2");
            return InputApi.SDL2;
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
    /// Wraps the legacy Windows <see cref="InputListener"/> pipeline
    /// (DirectInput/XInput/RawInput/Trackball/MergedInput) behind
    /// <see cref="IInputListener"/> so it plugs into the manager unchanged.
    /// </summary>
    internal sealed class LegacyInputListenerAdapter : IInputListener
    {
        private readonly InputApi _api;
        private readonly InputListener _inner = new InputListener();
        private Thread _thread;

        public LegacyInputListenerAdapter(InputApi api)
        {
            _api = api;
        }

        public string Name => $"Legacy({_api})";
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
