using System;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// XInput-shaped gamepad state types, previously provided by SharpDX.XInput.
    /// SDL2 is the only gamepad backend on all platforms; these types keep the
    /// battle-tested XInput mapping logic (and every existing user XInputButton
    /// binding) working unchanged without any SharpDX dependency.
    /// Field names, bit values and ranges are identical to XINPUT_GAMEPAD.
    /// </summary>
    [Flags]
    public enum GamepadButtonFlags : ushort
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    /// <summary>XINPUT_GAMEPAD-shaped snapshot (same ranges: sticks -32768..32767, triggers 0..255).</summary>
    public struct XiGamepad : IEquatable<XiGamepad>
    {
        /// <summary>Recommended dead zones / thresholds (same values as XInput headers).</summary>
        public const short LeftThumbDeadZone = 7849;
        public const short RightThumbDeadZone = 8689;
        public const byte TriggerThreshold = 30;

        public GamepadButtonFlags Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;

        public bool Equals(XiGamepad other) =>
            Buttons == other.Buttons &&
            LeftTrigger == other.LeftTrigger &&
            RightTrigger == other.RightTrigger &&
            LeftThumbX == other.LeftThumbX &&
            LeftThumbY == other.LeftThumbY &&
            RightThumbX == other.RightThumbX &&
            RightThumbY == other.RightThumbY;

        public override bool Equals(object obj) => obj is XiGamepad other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Buttons, LeftTrigger, RightTrigger, LeftThumbX, LeftThumbY, RightThumbX, RightThumbY);
    }

    /// <summary>XINPUT_STATE-shaped snapshot: packet number + gamepad.</summary>
    public struct State
    {
        public uint PacketNumber;
        public XiGamepad Gamepad;
    }
}
