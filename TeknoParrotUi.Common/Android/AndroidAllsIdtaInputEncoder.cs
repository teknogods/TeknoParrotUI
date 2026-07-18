using System;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Reproduces the 64-byte SWDCALLSUsbIoPipe report consumed by Initial D
    /// The Arcade's injected AMDaemon. The input stream carries semantic
    /// Android controls; this encoder applies the cabinet-specific byte and bit
    /// layout used by the desktop launcher.
    /// </summary>
    public sealed class AndroidAllsIdtaInputEncoder
    {
        public const int ReportSize = 64;

        private bool _coinWasPressed;
        private bool _shiftUpWasPressed;
        private bool _shiftDownWasPressed;
        private ushort _coinCount;
        private int _gear;

        public void BuildReport(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            if (buttons.Length < WinlatorForwardedInputSource.MaximumPlayers ||
                axes.Length < WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes ||
                report.Length < ReportSize)
                throw new ArgumentException("An ALLS Initial D input buffer is too small.");

            report[..ReportSize].Clear();
            var player = buttons[0];
            var wheel = AxisToByte(axes[0]);
            if (Pressed(player, ForwardedInputButton.Left)) wheel = 0;
            if (Pressed(player, ForwardedInputButton.Right)) wheel = byte.MaxValue;

            // The desktop ALLSIDTA listener writes the three driving controls
            // at AnalogBytes 1, 3, and 5 before SWDCALLSUsbIoPipe copies them.
            report[1] = wheel;
            report[3] = TriggerToByte(axes[5]);
            report[5] = TriggerToByte(axes[4]);

            // IDTAS5.xml deliberately maps the named cabinet controls onto
            // P1Button slots rather than the generic Start/Test/Service slots:
            // Start=P1Button3, Service=P1Button2, Test=P1Button5 and
            // View=P1Button1. Preserve that profile mapping here instead of
            // serializing the semantic Android controls as generic switches.
            if (Pressed(player, ForwardedInputButton.Start)) report[28] |= 0x80;
            if (Pressed(player, ForwardedInputButton.Button1)) report[28] |= 0x02;
            if (Pressed(player, ForwardedInputButton.Right)) report[28] |= 0x04;
            if (Pressed(player, ForwardedInputButton.Left)) report[28] |= 0x08;
            if (Pressed(player, ForwardedInputButton.Down)) report[28] |= 0x10;
            if (Pressed(player, ForwardedInputButton.Up)) report[28] |= 0x20;
            if (Pressed(player, ForwardedInputButton.Service)) report[28] |= 0x40;
            if (Pressed(player, ForwardedInputButton.Test)) report[29] |= 0x02;

            UpdateGear(player);
            WriteGear(report);

            var coinPressed = Pressed(player, ForwardedInputButton.Coin);
            if (_coinWasPressed && !coinPressed)
                _coinCount++;
            _coinWasPressed = coinPressed;

            // Desktop SWDCALLSUsbIoPipe serializes CoinCount * 256 as a
            // little-endian value, placing the count in byte 25.
            report[24] = 0;
            report[25] = (byte)_coinCount;
        }

        private void UpdateGear(uint buttons)
        {
            var shiftUpPressed = Pressed(buttons, ForwardedInputButton.Button2);
            var shiftDownPressed = Pressed(buttons, ForwardedInputButton.Button3);

            // Desktop InputListenerXInput changes IDZ gear once on each button
            // edge and keeps reporting the selected H-pattern until it changes.
            if (shiftUpPressed && !_shiftUpWasPressed && _gear < 6)
                _gear++;
            if (shiftDownPressed && !_shiftDownWasPressed && _gear > 0)
                _gear--;

            _shiftUpWasPressed = shiftUpPressed;
            _shiftDownWasPressed = shiftDownPressed;
        }

        private void WriteGear(Span<byte> report)
        {
            // DigitalHelper.ChangeIDZGear writes the virtual shifter through
            // player two's direction switches in SWDCALLSUsbIoPipe.
            switch (_gear)
            {
                case 1:
                    report[30] |= 0x28; // up + left
                    break;
                case 2:
                    report[30] |= 0x18; // down + left
                    break;
                case 3:
                    report[30] |= 0x20; // up
                    break;
                case 4:
                    report[30] |= 0x10; // down
                    break;
                case 5:
                    report[30] |= 0x24; // up + right
                    break;
                case 6:
                    report[30] |= 0x14; // down + right
                    break;
            }
        }

        private static bool Pressed(uint buttons, ForwardedInputButton button) =>
            (buttons & (1u << (int)button)) != 0;

        private static byte AxisToByte(short value)
        {
            // Desktop ALLSIDTA initializes AnalogBytes[1] to the exact USB-I/O
            // center value (0x80). A truncating Q15 conversion maps an idle
            // Android axis to 0x7F, which leaves Initial D's steering setup
            // waiting even when START is pressed. Round the conversion so the
            // neutral point and both endpoints match the desktop listener.
            return (byte)((
                ((long)value - short.MinValue) * byte.MaxValue +
                (ushort.MaxValue / 2L)) /
                ushort.MaxValue);
        }

        private static byte TriggerToByte(short value) =>
            (byte)(Math.Clamp((int)value, 0, short.MaxValue) * byte.MaxValue /
                short.MaxValue);
    }
}
