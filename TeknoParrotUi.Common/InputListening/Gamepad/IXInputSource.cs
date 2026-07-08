using SharpDX.XInput;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Abstraction over a gamepad that presents XInput-shaped state
    /// (<see cref="State"/>). Lets <see cref="InputListenerXInput"/>'s
    /// game-specific mapping logic run against any backend: real XInput on
    /// Windows (SharpDX) or SDL2 on all platforms.
    /// </summary>
    public interface IXInputSource
    {
        bool IsConnected { get; }
        State GetState();
    }

    /// <summary>Real XInput device via SharpDX (Windows only).</summary>
    public sealed class SharpDXXInputSource : IXInputSource
    {
        private readonly Controller _controller;

        public SharpDXXInputSource(UserIndex index)
        {
            _controller = new Controller(index);
        }

        public bool IsConnected => _controller.IsConnected;
        public State GetState() => _controller.GetState();
    }

    /// <summary>
    /// SDL2-backed gamepad presented as an XInput device. Reads cached state
    /// maintained by <see cref="SDL2GamepadBackend"/>.
    /// </summary>
    public sealed class SDL2XInputSource : IXInputSource
    {
        private readonly int _slot;

        public SDL2XInputSource(int slot)
        {
            _slot = slot;
        }

        public bool IsConnected => SDL2GamepadBackend.IsConnected(_slot);
        public State GetState() => SDL2GamepadBackend.GetState(_slot);
    }
}
