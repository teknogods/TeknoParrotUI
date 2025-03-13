using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Common.InputListening.Plugins
{
    public interface IInputPlugin
    {
        // Plugin metadata
        string Name { get; }
        string Description { get; }
        Version Version { get; }

        // Lifecycle methods
        void Initialize(GameProfile gameProfile);
        void StartListening(List<JoystickButtons> joystickButtons, GameProfile gameProfile);
        void StopListening();

        // Optional window message handling
        void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    }
}