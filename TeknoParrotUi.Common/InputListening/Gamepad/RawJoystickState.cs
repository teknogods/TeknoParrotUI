using System;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>Immutable snapshot of controls SDL's GameController mapping does not expose.</summary>
    public sealed class RawJoystickState
    {
        public static readonly RawJoystickState Empty = new RawJoystickState(Array.Empty<bool>(), Array.Empty<short>(), Array.Empty<byte>());

        public bool[] Buttons { get; }
        public short[] Axes { get; }
        public byte[] Hats { get; }

        public RawJoystickState(bool[] buttons, short[] axes, byte[] hats)
        {
            Buttons = buttons;
            Axes = axes;
            Hats = hats;
        }

        public bool Button(int index) => index >= 0 && index < Buttons.Length && Buttons[index];
        public short Axis(int index) => index >= 0 && index < Axes.Length ? Axes[index] : (short)0;
        public byte Hat(int index) => index >= 0 && index < Hats.Length ? Hats[index] : (byte)0;

        public bool IsPressed(XInputButton binding)
        {
            return binding.SdlControl switch
            {
                SdlControlKind.Button => Button(binding.SdlControlIndex),
                SdlControlKind.Hat => binding.SdlDirection != 0 &&
                    (Hat(binding.SdlControlIndex) & binding.SdlDirection) == binding.SdlDirection,
                SdlControlKind.Axis => binding.SdlDirection < 0
                    ? Axis(binding.SdlControlIndex) <= -15000
                    : Axis(binding.SdlControlIndex) >= 15000,
                _ => false
            };
        }

        public bool SameControls(RawJoystickState other)
        {
            if (Buttons.Length != other.Buttons.Length || Axes.Length != other.Axes.Length || Hats.Length != other.Hats.Length)
                return false;
            for (int i = 0; i < Buttons.Length; i++)
                if (Buttons[i] != other.Buttons[i]) return false;
            for (int i = 0; i < Axes.Length; i++)
                if (Axes[i] != other.Axes[i]) return false;
            for (int i = 0; i < Hats.Length; i++)
                if (Hats[i] != other.Hats[i]) return false;
            return true;
        }
    }
}
