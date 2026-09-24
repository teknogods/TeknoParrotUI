namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Abstraction over a gamepad that presents XInput-shaped state
    /// (<see cref="State"/>). Lets <see cref="InputListenerXInput"/>'s
    /// game-specific mapping logic run against the SDL3 backend on desktop and
    /// the Android input API on Android.
    /// </summary>
    public interface IXInputSource
    {
        bool IsConnected { get; }
        State GetState();
    }

    /// <summary>
    /// SDL3-backed gamepad presented as an XInput device. Reads cached state
    /// maintained by <see cref="SDL3GamepadBackend"/>.
    /// </summary>
    public sealed class SDL3XInputSource : IXInputSource
    {
        private readonly int _slot;

        public SDL3XInputSource(int slot)
        {
            _slot = slot;
        }

        public bool IsConnected => SDL3GamepadBackend.IsConnected(_slot);
        public State GetState() => SDL3GamepadBackend.GetState(_slot);
    }
}
