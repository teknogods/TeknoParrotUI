using System;
using System.Collections.Generic;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Cross-platform gamepad listener. Replaces InputListenerXInput +
    /// InputListenerDirectInput on non-Windows platforms (and optionally on
    /// Windows) by feeding SDL2 controller state through the existing
    /// XInput mapping logic — so all game-specific behaviour (WMMT gears,
    /// Initial D, rotary encoders, sto0z, gun centering) and existing user
    /// XInputButton bindings work unchanged.
    /// </summary>
    public class SDL2JoystickListener : IInputListener
    {
        public string Name => "SDL2Gamepad";
        public bool IsSupported => true;

        private readonly InputListenerXInput _mapper = new InputListenerXInput();
        private readonly List<Thread> _threads = new List<Thread>();
        private Thread _respawner;
        private volatile bool _stopped;

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            _stopped = false;
            InputListenerXInput.KillMe = false;
            SDL2GamepadBackend.Acquire();

            bool useSto0Z = Lazydata.ParrotData != null && Lazydata.ParrotData.UseSto0ZDrivingHack;
            int stoozPercent = Lazydata.ParrotData != null ? Lazydata.ParrotData.StoozPercent : 0;

            for (int slot = 0; slot < SDL2GamepadBackend.MaxSlots; slot++)
                _threads.Add(StartSlotThread(slot, useSto0Z, stoozPercent, gameProfile, joystickButtons));

            // Like ThreadRespawnerXInput: a slot thread exits when its pad is not
            // connected; respawn so hot-plugged pads start working.
            _respawner = new Thread(() =>
            {
                while (!_stopped && !InputListenerXInput.KillMe)
                {
                    for (int slot = 0; slot < _threads.Count; slot++)
                    {
                        if (!_threads[slot].IsAlive)
                            _threads[slot] = StartSlotThread(slot, useSto0Z, stoozPercent, gameProfile, joystickButtons);
                    }
                    Thread.Sleep(5000);
                }
            }) { IsBackground = true, Name = "SDL2JoystickRespawner" };
            _respawner.Start();
        }

        private Thread StartSlotThread(int slot, bool useSto0Z, int stoozPercent, GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            var thread = new Thread(() => _mapper.ListenXInput(
                useSto0Z, stoozPercent, joystickButtons, slot, gameProfile,
                new SDL2XInputSource(slot))) { IsBackground = true, Name = $"SDL2Joystick{slot}" };
            thread.Start();
            return thread;
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Gamepad input does not consume window messages.
        }

        public void Stop()
        {
            if (_stopped)
                return;
            _stopped = true;
            InputListenerXInput.KillMe = true;
            foreach (var thread in _threads)
                thread.Join(1000);
            _threads.Clear();
            _respawner?.Join(100);
            SDL2GamepadBackend.Release();
        }
    }
}
