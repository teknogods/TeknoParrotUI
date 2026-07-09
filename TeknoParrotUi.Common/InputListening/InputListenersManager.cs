using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Cross-platform input orchestrator. Every game always runs MERGED input:
    ///
    /// - Gamepad input: SDL2 listener, always (the only gamepad backend).
    /// - Keyboard/mouse/gun input: Win32 RawInput on Windows, always; evdev on
    ///   Linux and touch on Android for gun games.
    ///
    /// The game's "Input API" value no longer switches input systems — it only
    /// selects the gun flavour (RawInput vs RawInputTrackball) for the games
    /// that offer both.
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

            if (OperatingSystem.IsWindows())
            {
                // Keyboard/mouse (and gun) input always runs via RawInput — every
                // game is merged. The saved Input API only picks the gun flavour.
                _listeners.Add(new RawInputListenerHost(ResolveGunFlavour(gameProfile)));
                NeedsWndProcRouting = true;
            }
            else if (OperatingSystem.IsLinux() &&
                     (!inputProfile.InputMethods.TryGetValue(InputProfile.Methods.EvdevMouse, out var evdev) || evdev.Enabled))
            {
                // Merged always, like Windows: evdev services keyboards and mice
                // for every game (gun analog writes only activate when the game
                // has light-gun mappings). A user InputProfiles/<game>.json can
                // still disable it explicitly.
                _listeners.Add(new Mouse.EvdevMouseListener());
            }
            else if (OperatingSystem.IsAndroid() && AndroidTouchListenerFactory != null &&
                     inputProfile.InputMethods.TryGetValue(InputProfile.Methods.AndroidTouch, out var touch) &&
                     touch.Enabled)
            {
                _listeners.Add(AndroidTouchListenerFactory());
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
        /// The RawInput flavour for this game: RawInputTrackball when the game
        /// offers it and the user selected it (or it is the only gun option),
        /// plain merged RawInput otherwise.
        /// </summary>
        private static InputApi ResolveGunFlavour(GameProfile gameProfile)
        {
            var apiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
            bool offersTrackball = apiField?.FieldOptions?.Contains("RawInputTrackball") == true;
            bool offersRawInput = apiField?.FieldOptions?.Contains("RawInput") == true;

            if (offersTrackball && (apiField.FieldValue == "RawInputTrackball" || !offersRawInput))
                return InputApi.RawInputTrackball;
            return InputApi.MergedInput;
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
