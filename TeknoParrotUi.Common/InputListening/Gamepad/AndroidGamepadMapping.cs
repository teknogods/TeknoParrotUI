using System;

namespace TeknoParrotUi.Common.InputListening.Gamepad;

/// <summary>Android key and axis values translated to the shared XInput-shaped state.</summary>
public static class AndroidGamepadMapping
{
    public static GamepadButtonFlags Button(int keyCode) => keyCode switch
    {
        19 => GamepadButtonFlags.DPadUp,
        20 => GamepadButtonFlags.DPadDown,
        21 => GamepadButtonFlags.DPadLeft,
        22 => GamepadButtonFlags.DPadRight,
        96 => GamepadButtonFlags.A,
        97 => GamepadButtonFlags.B,
        99 => GamepadButtonFlags.X,
        100 => GamepadButtonFlags.Y,
        102 => GamepadButtonFlags.LeftShoulder,
        103 => GamepadButtonFlags.RightShoulder,
        106 => GamepadButtonFlags.LeftThumb,
        107 => GamepadButtonFlags.RightThumb,
        108 => GamepadButtonFlags.Start,
        109 => GamepadButtonFlags.Back,
        _ => GamepadButtonFlags.None
    };

    public static short Stick(float value, float min, float max, bool invert = false)
    {
        if (max <= min) return 0;
        // Android stick axes are generally -1..1. Normalize unusual ranges as well.
        var centered = (2f * (value - min) / (max - min)) - 1f;
        if (invert) centered = -centered;
        return (short)Math.Clamp((int)Math.Round(centered * 32767f), short.MinValue, short.MaxValue);
    }

    public static byte Trigger(float value, float min, float max)
    {
        if (max <= min) return 0;
        return (byte)Math.Clamp((int)Math.Round((value - min) / (max - min) * 255f), 0, 255);
    }

    public static GamepadButtonFlags Hat(float x, float y)
    {
        var flags = GamepadButtonFlags.None;
        if (x <= -0.5f) flags |= GamepadButtonFlags.DPadLeft;
        if (x >= 0.5f) flags |= GamepadButtonFlags.DPadRight;
        if (y <= -0.5f) flags |= GamepadButtonFlags.DPadUp;
        if (y >= 0.5f) flags |= GamepadButtonFlags.DPadDown;
        return flags;
    }
}
