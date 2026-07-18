using System;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Builds the 16-byte report consumed by OpenParrot's APM3 input hook.
    /// The layout intentionally matches <c>APM3Pipe.GenButtonsAPM3</c>.
    /// </summary>
    public static class AndroidApm3InputEncoder
    {
        public const int ReportSize = 16;
        private const int DigitalAxisThreshold = 12_000;

        public static void BuildReport(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            if (buttons.Length < WinlatorForwardedInputSource.MaximumPlayers ||
                axes.Length < WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes ||
                report.Length < ReportSize)
                throw new ArgumentException("An APM3 input buffer is too small.");

            report[..ReportSize].Clear();
            var player = buttons[0];
            if (Pressed(player, ForwardedInputButton.Test)) report[0] = 1;
            if (Pressed(player, ForwardedInputButton.Service)) report[1] = 1;
            if (DirectionPressed(buttons, axes, ForwardedInputButton.Up)) report[2] = 1;
            if (DirectionPressed(buttons, axes, ForwardedInputButton.Down)) report[3] = 1;
            if (DirectionPressed(buttons, axes, ForwardedInputButton.Left)) report[4] = 1;
            if (DirectionPressed(buttons, axes, ForwardedInputButton.Right)) report[5] = 1;
            if (Pressed(player, ForwardedInputButton.Start)) report[6] = 1;
            if (Pressed(player, ForwardedInputButton.Button1)) report[7] = 1;
            if (Pressed(player, ForwardedInputButton.Button2)) report[8] = 1;
            if (Pressed(player, ForwardedInputButton.Button3)) report[9] = 1;
            if (Pressed(player, ForwardedInputButton.Button4)) report[10] = 1;
            if (Pressed(player, ForwardedInputButton.Button5)) report[11] = 1;
            if (Pressed(player, ForwardedInputButton.Button6)) report[12] = 1;
            if (Pressed(player, ForwardedInputButton.Button7) ||
                axes[4] >= DigitalAxisThreshold) report[13] = 1;
            if (Pressed(player, ForwardedInputButton.Button8) ||
                axes[5] >= DigitalAxisThreshold) report[14] = 1;
        }

        private static bool Pressed(uint state, ForwardedInputButton button) =>
            (state & (1u << (int)button)) != 0;

        private static bool DirectionPressed(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ForwardedInputButton button)
        {
            if (Pressed(buttons[0], button))
                return true;

            return button switch
            {
                ForwardedInputButton.Left =>
                    axes[0] <= -DigitalAxisThreshold || axes[6] <= -DigitalAxisThreshold,
                ForwardedInputButton.Right =>
                    axes[0] >= DigitalAxisThreshold || axes[6] >= DigitalAxisThreshold,
                ForwardedInputButton.Up =>
                    axes[1] <= -DigitalAxisThreshold || axes[7] <= -DigitalAxisThreshold,
                ForwardedInputButton.Down =>
                    axes[1] >= DigitalAxisThreshold || axes[7] >= DigitalAxisThreshold,
                _ => false
            };
        }
    }
}
