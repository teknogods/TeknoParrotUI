using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputProfiles.Helpers;

namespace TeknoParrotUi.Common
{
    public class InputListener
    {
        /// <summary>
        /// This is so we can easily kill the thread.
        /// </summary>
        private static bool KillMe { get; set; }

        private static Thread _xi1;
        private static Thread _xi2;
        private static Thread _xi3;
        private static Thread _xi4;
        private readonly InputListenerXInput _inputListenerXInput = new InputListenerXInput();
        private readonly InputListenerDirectInput _inputListenerDirectInput = new InputListenerDirectInput();
        private readonly InputListenerRawInput _inputListenerRawInput = new InputListenerRawInput();
        private readonly InputListenerRawInputTrackball _inputListenerRawInputTrackball = new InputListenerRawInputTrackball();
        private static GameProfile _gameprofile;
        private InputApi _inputApi;
        private bool _mergedIncludesRawInput;
        private bool _mergedIncludesRawInputTrackball;

        public void ThreadRespawnerXInput(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons)
        {
            while (!KillMe)
            {
                if (!_xi1.IsAlive)
                {
                    _xi1 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.One, _gameprofile));
                    _xi1.Start();
                }
                if (!_xi2.IsAlive)
                {
                    _xi2 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Two, _gameprofile));
                    _xi2.Start();
                }
                if (!_xi3.IsAlive)
                {
                    _xi3 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Three, _gameprofile));
                    _xi3.Start();
                }
                if (!_xi4.IsAlive)
                {
                    _xi4 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Four, _gameprofile));
                    _xi4.Start();
                }
                Thread.Sleep(5000);
            }
        }

        public void Listen(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons, InputApi inputApi, GameProfile gameProfile)
        {
            try
            {
                KillMe = false;
                InputListenerXInput.KillMe = false;
                InputListenerDirectInput.KillMe = false;
                InputListenerRawInput.KillMe = false;
                InputListenerRawInputTrackball.KillMe = false;
                _gameprofile = gameProfile;
                _inputApi = inputApi;

                if (_inputApi == InputApi.DirectInput)
                {
                    var thread = new Thread(() => _inputListenerDirectInput.ListenDirectInput(joystickButtons, gameProfile));
                    thread.Start();
                }
                else if (_inputApi == InputApi.XInput)
                {
                    _xi1 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.One, gameProfile));
                    _xi1.Start();

                    _xi2 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Two, gameProfile));
                    _xi2.Start();

                    _xi3 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Three, gameProfile));
                    _xi3.Start();

                    _xi4 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Four, gameProfile));
                    _xi4.Start();
                    var thread = new Thread(() => ThreadRespawnerXInput(useSto0Z, stoozPercent, joystickButtons));
                    thread.Start();
                }
                else if (_inputApi == InputApi.RawInput)
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
                    // Check if the game profile supports RawInput
                    var inputApiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
                    _mergedIncludesRawInput = inputApiField?.FieldOptions?.Contains("RawInput") == true;
                    _mergedIncludesRawInputTrackball = inputApiField?.FieldOptions?.Contains("RawInputTrackball") == true;

                    // Detect XInput device GUIDs so DirectInput skips them
                    var xinputGuids = XInputDeviceHelper.GetXInputDeviceGuids();

                    // Run both XInput and DirectInput listeners simultaneously
                    _xi1 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.One, gameProfile));
                    _xi1.Start();

                    _xi2 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Two, gameProfile));
                    _xi2.Start();

                    _xi3 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Three, gameProfile));
                    _xi3.Start();

                    _xi4 = new Thread(() => _inputListenerXInput.ListenXInput(useSto0Z, stoozPercent, joystickButtons, UserIndex.Four, gameProfile));
                    _xi4.Start();

                    var respawnerThread = new Thread(() => ThreadRespawnerXInput(useSto0Z, stoozPercent, joystickButtons));
                    respawnerThread.Start();

                    // DirectInput excludes XInput controllers to avoid double-polling
                    var diThread = new Thread(() => _inputListenerDirectInput.ListenDirectInput(joystickButtons, gameProfile, xinputGuids));
                    diThread.Start();

                    // RawInput for mouse/keyboard (only if the game profile supports it)
                    if (_mergedIncludesRawInput)
                    {
                        var riThread = new Thread(() => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile));
                        riThread.Start();
                    }

                    // RawInputTrackball for trackball devices (only if the game profile supports it)
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
            InputListenerXInput.KillMe = true;
            InputListenerDirectInput.KillMe = true;
            InputListenerRawInput.KillMe = true;
            InputListenerRawInputTrackball.KillMe = true;

            if (_gameprofile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameprofile.EmulationProfile == EmulationProfile.NamcoWmmt6RR)
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
