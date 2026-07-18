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
        private static volatile bool _killMe;
        private static bool KillMe
        {
            get => _killMe;
            set => _killMe = value;
        }

        private readonly InputListenerRawInput _inputListenerRawInput = new InputListenerRawInput();
        private readonly InputListenerRawInputTrackball _inputListenerRawInputTrackball = new InputListenerRawInputTrackball();
        private readonly object _workerSync = new object();
        private readonly List<Thread> _workerThreads = new List<Thread>();
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private static GameProfile _gameprofile;
        private InputApi _inputApi;
        private bool _mergedIncludesRawInput;
        private bool _mergedIncludesRawInputTrackball;

        public void Listen(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons, InputApi inputApi, GameProfile gameProfile)
        {
            try
            {
                _stopSignal.Reset();
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

                    StartWorker(
                        () => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile),
                        "RawInput");
                    StartWorker(
                        () => _inputListenerRawInputTrackball.ListenRawInputTrackball(joystickButtons, gameProfile),
                        "RawInputTrackball");
                }
                else
                {
                    // Merged (default for every game): keyboard, mouse and gun
                    // input all through the RawInput listener. Gamepads run in
                    // the SDL2 listener alongside (started by the manager).
                    _mergedIncludesRawInput = true;
                    _mergedIncludesRawInputTrackball = false;

                    StartWorker(
                        () => _inputListenerRawInput.ListenRawInput(joystickButtons, gameProfile),
                        "RawInput");
                }
            }
            catch (Exception)
            {
                // ignored
            }
            _stopSignal.Wait();
        }

        private void StartWorker(ThreadStart action, string name)
        {
            var worker = new Thread(action)
            {
                IsBackground = true,
                Name = name
            };
            lock (_workerSync)
                _workerThreads.Add(worker);
            worker.Start();
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
            _stopSignal.Set();
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

            Thread[] workers;
            lock (_workerSync)
                workers = _workerThreads.ToArray();
            var deadline = Environment.TickCount64 + 2000;
            foreach (var worker in workers)
            {
                if (!worker.IsAlive)
                    continue;
                var remaining = Math.Max(0, deadline - Environment.TickCount64);
                if (remaining > 0)
                    worker.Join(TimeSpan.FromMilliseconds(remaining));
            }
            var allWorkersExited = false;
            lock (_workerSync)
            {
                _workerThreads.RemoveAll(worker => !worker.IsAlive);
                allWorkersExited = _workerThreads.Count == 0;
            }

            if (allWorkersExited)
            {
                // Timer callbacks may still use this session's static mapping
                // state while a worker is alive. Detach/reset them only after
                // every RawInput worker has actually stopped.
                InputListenerRawInput.StopTimers();
                // Prevent the hidden WM_INPUT window from routing into state
                // while its MMF/view handles are being released.
                _mergedIncludesRawInput = false;
                _mergedIncludesRawInputTrackball = false;
                _inputListenerRawInput.Dispose();
                _inputListenerRawInputTrackball.Dispose();
                _gameprofile = null;
            }
        }
    }
}
