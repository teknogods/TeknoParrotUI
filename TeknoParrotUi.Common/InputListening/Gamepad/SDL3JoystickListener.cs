using System;
using System.Collections.Generic;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Cross-platform gamepad listener. Feeds SDL3 gamepad state through the existing
    /// XInput mapping logic — so all game-specific behaviour (WMMT gears,
    /// Initial D, rotary encoders, sto0z, gun centering) and existing user
    /// XInputButton bindings work unchanged. Generic HID controls retain their
    /// physical button/axis/hat identifiers in the same binding model.
    /// </summary>
    public class SDL3JoystickListener : IInputListener
    {
        public string Name => "SDL3Gamepad";
        public bool IsSupported => true;

        private readonly InputListenerXInput _mapper = new InputListenerXInput();
        private readonly List<Thread> _threads = new List<Thread>();
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private Thread _respawner;
        private volatile bool _stopped = true;

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            if (!_stopped)
                Stop();
            _stopped = false;
            _stopSignal.Reset();
            InputListenerXInput.KillMe = false;
            SDL3GamepadBackend.Acquire();

            // There is no game-specific mapping work to run for an empty
            // profile. The backend remains available to UI capture listeners.
            if (joystickButtons == null || joystickButtons.Count == 0)
                return;

            bool useSto0Z = Lazydata.ParrotData != null && Lazydata.ParrotData.UseSto0ZDrivingHack;
            int stoozPercent = Lazydata.ParrotData != null ? Lazydata.ParrotData.StoozPercent : 0;

            for (int slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
                _threads.Add(null);

            // Only spawn workers for connected slots. A short wait makes newly
            // plugged devices usable promptly without creating idle threads.
            _respawner = new Thread(() =>
            {
                while (!_stopped && !InputListenerXInput.KillMe)
                {
                    for (int slot = 0; slot < _threads.Count; slot++)
                    {
                        if (SDL3GamepadBackend.IsConnected(slot) &&
                            (_threads[slot] == null || !_threads[slot].IsAlive))
                            _threads[slot] = StartSlotThread(slot, useSto0Z, stoozPercent, gameProfile, joystickButtons);
                    }
                    if (_stopSignal.Wait(250))
                        break;
                }
            }) { IsBackground = true, Name = "SDL3JoystickRespawner" };
            _respawner.Start();
        }

        private Thread StartSlotThread(int slot, bool useSto0Z, int stoozPercent, GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            var thread = new Thread(() => _mapper.ListenXInput(
                useSto0Z, stoozPercent, joystickButtons, slot, gameProfile,
                new SDL3XInputSource(slot))) { IsBackground = true, Name = $"SDL3Joystick{slot}" };
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
            _stopSignal.Set();
            InputListenerXInput.KillMe = true;
            // Stop the respawner before touching its slot-thread list. The old
            // five-second sleep left it alive after Stop returned and it could
            // race a subsequent session's listener startup.
            _respawner?.Join(1000);
            _respawner = null;
            foreach (var thread in _threads)
            {
                if (thread != null && !thread.Join(1000))
                    SDL3GamepadBackend.Trace($"slot worker {thread.Name} did not stop within 1 s");
            }
            _threads.Clear();
            // Stop shared mapper timers after requesting every slot worker to
            // exit; any worker still alive is recorded in the SDL3 trace.
            InputListenerXInput.StopTimers();
            SDL3GamepadBackend.Release();
        }
    }
}
