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

                if (_inputApi == InputApi.RawInput)
                {
                    var thread = new Thread(() => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile));
                    thread.Start();
                }
                else if (_inputApi == InputApi.RawInputTrackball)
                {
                    var thread = new Thread(() => _inputListenerRawInputTrackball.ListenRawInputTrackball(joystickButtons, gameProfile));
                    thread.Start();
                }
                else if (_inputApi == InputApi.MergedInput)
                {
                    // Gun/trackball parts of MergedInput only (gamepads = SDL2)
                    var inputApiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
                    _mergedIncludesRawInput = inputApiField?.FieldOptions?.Contains("RawInput") == true;
                    _mergedIncludesRawInputTrackball = inputApiField?.FieldOptions?.Contains("RawInputTrackball") == true;

                    if (_mergedIncludesRawInput)
                    {
                        var riThread = new Thread(() => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile));
                        riThread.Start();
                    }

                    if (_mergedIncludesRawInputTrackball)
                    {
                        var ritThread = new Thread(() => _inputListenerRawInputTrackball.ListenRawInputTrackball(joystickButtons, gameProfile));
                        ritThread.Start();
                    }
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
            if (_inputApi == InputApi.RawInput || (_inputApi == InputApi.MergedInput && _mergedIncludesRawInput))
                _inputListenerRawInput.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);

            if (_inputApi == InputApi.RawInputTrackball || (_inputApi == InputApi.MergedInput && _mergedIncludesRawInputTrackball))
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
