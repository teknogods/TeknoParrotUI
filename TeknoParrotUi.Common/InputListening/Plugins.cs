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
        bool IsActive { get; set; }

        // Lifecycle methods
        void Initialize(GameProfile gameProfile);
        void StartListening(List<JoystickButtons> joystickButtons, GameProfile gameProfile);
        void StopListening();

        // Optional window message handling
        void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        // For digital inputs (buttons/keys)
        List<(int key, bool pressed)> GetKeyChanges();

        // For analog inputs (axes, triggers)
        List<(int axis, float value)> GetAnalogChanges();
    }
}