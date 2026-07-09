using System;
using System.Collections.Generic;
using System.Threading;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputProfiles.Helpers;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Windows gun/mouse/keyboard listener host (RawInput + RawInputTrackball).
    /// Gamepad input is handled exclusively by the SDL2 listener on all
    /// platforms — the old XInput/DirectInput polling paths are gone.
    /// For MergedInput selections this starts whichever RawInput flavours the
    /// game's Input API options declare (the SDL2 gamepad listener runs
    /// alongside, started separately by <see cref="InputListening.InputListenersManager"/>).
    /// </summary>
    public class InputListener
    {
        /// <summary>
        /// This is so we can easily kill the thread.
        /// </summary>
        private static bool KillMe { get; set; }

        private readonly InputListenerRawInput _inputListenerRawInput = new InputListenerRawInput();
        private readonly InputListenerRawInputTrackball _inputListenerRawInputTrackball = new InputListenerRawInputTrackball();
        private static GameProfile _gameprofile;
        private InputApi _inputApi;
        private bool _mergedIncludesRawInput;
        private bool _mergedIncludesRawInputTrackball;

        public void Listen(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons, InputApi inputApi, GameProfile gameProfile)
        {
            try
            {
                KillMe = false;
                InputListenerRawInput.KillMe = false;
                InputListenerRawInputTrackball.KillMe = false;
                _gameprofile = gameProfile;
                _inputApi = inputApi;

                if (_inputApi == InputApi.RawInputTrackball)
                {
                    // Trackball flavour: trackball deltas via the trackball
                    // listener, keyboard/mouse buttons still via RawInput.
                    _mergedIncludesRawInput = true;
                    _mergedIncludesRawInputTrackball = true;

                    var riThread = new Thread(() => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile));
                    riThread.Start();

                    var ritThread = new Thread(() => _inputListenerRawInputTrackball.ListenRawInputTrackball(joystickButtons, gameProfile));
                    ritThread.Start();
                }
                else
                {
                    // Merged (default for every game): keyboard, mouse and gun
                    // input all through the RawInput listener. Gamepads run in
                    // the SDL2 listener alongside (started by the manager).
                    _mergedIncludesRawInput = true;
                    _mergedIncludesRawInputTrackball = false;

                    var riThread = new Thread(() => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile));
                    riThread.Start();
                }
            }
            catch (Exception)
            {
                // ignored
            }
            while (!KillMe)
                Thread.Sleep(1000);
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_mergedIncludesRawInput)
                _inputListenerRawInput.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);

            if (_mergedIncludesRawInputTrackball)
                _inputListenerRawInputTrackball.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
        }

        public void StopListening()
        {
            KillMe = true;
            InputListenerRawInput.KillMe = true;
            InputListenerRawInputTrackball.KillMe = true;

            if (_gameprofile != null && (_gameprofile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameprofile.EmulationProfile == EmulationProfile.NamcoWmmt6RR))
            {
                DigitalHelper.CurrentWmmt5Gear = 1;
                InputCode.PlayerDigitalButtons[0].Button1 = false;
                InputCode.PlayerDigitalButtons[0].Button2 = false;
                InputCode.PlayerDigitalButtons[0].Button3 = false;
                InputCode.PlayerDigitalButtons[0].Button4 = false;
                InputCode.PlayerDigitalButtons[0].Button5 = false;
                InputCode.PlayerDigitalButtons[0].Button6 = false;
            }
        }
    }
}
