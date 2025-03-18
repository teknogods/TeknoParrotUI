using System;
using System.Collections.Generic;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Plugins
{
    public abstract class InputPluginBase : IInputPlugin
    {
        protected bool ShouldStop { get; private set; } = false;
        protected Thread ListeningThread { get; private set; }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Version Version { get; }
        public bool IsActive { get; set; }

        public virtual void Initialize(GameProfile gameProfile)
        {
            ShouldStop = false;
        }

        public void StartListening(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            ShouldStop = false;
            ListeningThread = new Thread(() => ListenInternal(joystickButtons, gameProfile));
            ListeningThread.Start();
        }

        protected abstract void ListenInternal(List<JoystickButtons> joystickButtons, GameProfile gameProfile);

        public virtual void StopListening()
        {
            ShouldStop = true;
            if (ListeningThread != null && ListeningThread.IsAlive)
            {
                // Give thread time to close gracefully
                ListeningThread.Join(1000);
            }
        }

        public virtual void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Default empty implementation
        }

        public virtual List<(int key, bool pressed)> GetKeyChanges()
        {
            return new List<(int key, bool pressed)>();
        }
        public virtual List<(int axis, float value)> GetAnalogChanges()
        {
            return new List<(int axis, float value)>();
        }
    }
}