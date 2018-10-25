using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common.InputListening;

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
        private static GameProfile _gameprofile;

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

        public void Listen(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons, bool isXinput, GameProfile gameProfile, IntPtr hWnd)
        {
            try
            {
                KillMe = false;
                InputListenerXInput.KillMe = false;
                InputListenerDirectInput.KillMe = false;
                _gameprofile = gameProfile;

                if (isXinput)
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
                else
                {
                    var thread = new Thread(() =>_inputListenerDirectInput.ListenDirectInput(joystickButtons, gameProfile, hWnd));
                    thread.Start();
                }
            }
            catch (Exception)
            {
                // ignored
            }
            while (!KillMe)
                Thread.Sleep(1000);
        }

        public void StopListening()
        {
            KillMe = true;
            InputListenerXInput.KillMe = true;
            InputListenerDirectInput.KillMe = true;
        }


    }
}
