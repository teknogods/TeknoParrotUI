using System;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Builds the 64-byte FastIO report for profile-specific variants which
    /// cannot use the generic Android writer unchanged.
    /// </summary>
    public static class AndroidFastIoInputEncoder
    {
        public const int ReportSize = 64;
        private const int DigitalAxisThreshold = 12_000;

        public static void BuildReport(
            string protocol,
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            if (!AndroidLaunchRecipe.IsFastIoInputProtocol(protocol))
                throw new ArgumentOutOfRangeException(nameof(protocol));
            if (buttons.Length < WinlatorForwardedInputSource.MaximumPlayers ||
                axes.Length < WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes ||
                report.Length < ReportSize)
                throw new ArgumentException("A FastIO input buffer is too small.");

            report[..ReportSize].Clear();
            for (var player = 0; player < WinlatorForwardedInputSource.MaximumPlayers; player++)
                WritePlayer(player, buttons, axes, report);

            if (Pressed(buttons[0], ForwardedInputButton.Coin)) report[4] = 1;
            if (Pressed(buttons[2], ForwardedInputButton.Coin)) report[14] = 1;
            PublishAxis(axes, 0, report, 8, 9);
            PublishAxis(axes, 2, report, 15, 16);
            PublishAxis(axes, 1, report, 17, 18);
            PublishAxis(axes, 3, report, 19, 20);

            if (protocol == AndroidLaunchRecipe.InputProtocolFastIoTheatrhythm)
            {
                if (Pressed(buttons[0], ForwardedInputButton.Button5))
                {
                    // The cabinet Right Button is ExtensionOne3, encoded at
                    // FastIO bit 0x80 rather than ordinary P1 Button5.
                    report[3] &= 0xFE;
                    report[0] |= 0x80;
                }
                if (Pressed(buttons[0], ForwardedInputButton.Button7))
                    report[0] |= 0x08; // Service2 / Select switch
                if (Pressed(buttons[0], ForwardedInputButton.Button8))
                    report[0] |= 0x20; // P2 Start / Left Button
            }
        }

        private static void WritePlayer(
            int player,
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var lane = player & 1;
            var bank = player < 2 ? 0 : 10;
            var state = buttons[player];
            if (DirectionPressed(buttons, axes, player, ForwardedInputButton.Up))
                report[bank + 1] |= (byte)(0x01 << lane);
            if (DirectionPressed(buttons, axes, player, ForwardedInputButton.Down))
                report[bank + 1] |= (byte)(0x04 << lane);
            if (DirectionPressed(buttons, axes, player, ForwardedInputButton.Left))
                report[bank + 1] |= (byte)(0x10 << lane);
            if (DirectionPressed(buttons, axes, player, ForwardedInputButton.Right))
                report[bank + 1] |= (byte)(0x40 << lane);
            if (Pressed(state, ForwardedInputButton.Start))
                report[bank] |= (byte)(0x10 << lane);
            if (Pressed(state, ForwardedInputButton.Test) && lane == 0)
                report[bank] |= 0x40;
            if (Pressed(state, ForwardedInputButton.Service))
                report[bank] |= (byte)(0x04 << lane);
            if (Pressed(state, ForwardedInputButton.Button1))
                report[bank + 2] |= (byte)(0x01 << lane);
            if (Pressed(state, ForwardedInputButton.Button2))
                report[bank + 2] |= (byte)(0x04 << lane);
            if (Pressed(state, ForwardedInputButton.Button3))
                report[bank + 2] |= (byte)(0x10 << lane);
            if (Pressed(state, ForwardedInputButton.Button4))
                report[bank + 2] |= (byte)(0x40 << lane);
            if (Pressed(state, ForwardedInputButton.Button5))
                report[bank + 3] |= (byte)(0x01 << lane);
            if (Pressed(state, ForwardedInputButton.Button6))
                report[bank + 3] |= (byte)(0x04 << lane);
        }

        private static void PublishAxis(
            ReadOnlySpan<short> axes,
            int player,
            Span<byte> report,
            int xOffset,
            int yOffset)
        {
            var source = player * WinlatorForwardedInputSource.MaximumAxes;
            if (axes[source] != 0) report[xOffset] = AxisToByte(axes[source]);
            if (axes[source + 1] != 0) report[yOffset] = AxisToByte(axes[source + 1]);
        }

        private static bool DirectionPressed(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            int player,
            ForwardedInputButton button)
        {
            if (Pressed(buttons[player], button)) return true;
            var offset = player * WinlatorForwardedInputSource.MaximumAxes;
            return button switch
            {
                ForwardedInputButton.Left =>
                    axes[offset] <= -DigitalAxisThreshold || axes[offset + 6] <= -DigitalAxisThreshold,
                ForwardedInputButton.Right =>
                    axes[offset] >= DigitalAxisThreshold || axes[offset + 6] >= DigitalAxisThreshold,
                ForwardedInputButton.Up =>
                    axes[offset + 1] <= -DigitalAxisThreshold || axes[offset + 7] <= -DigitalAxisThreshold,
                ForwardedInputButton.Down =>
                    axes[offset + 1] >= DigitalAxisThreshold || axes[offset + 7] >= DigitalAxisThreshold,
                _ => false
            };
        }

        private static bool Pressed(uint state, ForwardedInputButton button) =>
            (state & (1u << (int)button)) != 0;

        private static byte AxisToByte(short value) =>
            (byte)(((long)value - short.MinValue) * byte.MaxValue / ushort.MaxValue);
    }
}
