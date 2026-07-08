using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Base contract for all input listeners (gamepad, mouse, touch).
    /// One implementation per input method; started/stopped by
    /// <see cref="InputListenersManager"/> based on the game's input configuration
    /// and the current platform.
    /// </summary>
    public interface IInputListener
    {
        /// <summary>Friendly name for logging/debugging.</summary>
        string Name { get; }

        /// <summary>Whether this listener can run on the current platform.</summary>
        bool IsSupported { get; }

        /// <summary>
        /// Start listening for input. Implementations spawn their own background
        /// threads and write state to <see cref="InputCode"/> exactly like the
        /// legacy listeners.
        /// </summary>
        void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons);

        /// <summary>
        /// Handle Win32 window messages (WM_INPUT for RawInput listeners).
        /// No-op for listeners that do not consume window messages.
        /// </summary>
        void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>Stop listening and release resources.</summary>
        void Stop();
    }
}
