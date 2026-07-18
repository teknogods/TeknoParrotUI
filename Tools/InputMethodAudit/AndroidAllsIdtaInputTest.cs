using System;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace InputMethodAudit
{
    internal static class AndroidAllsIdtaInputTest
    {
        public static int Run()
        {
            try
            {
                var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
                var axes = new short[
                    WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes];
                var report = new byte[AndroidAllsIdtaInputEncoder.ReportSize];
                var encoder = new AndroidAllsIdtaInputEncoder();

                encoder.BuildReport(buttons, axes, report);
                Equal(0x80, report[1], "centered wheel");

                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Up) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Test) |
                             Mask(ForwardedInputButton.Service) |
                             Mask(ForwardedInputButton.Coin);
                axes[0] = short.MinValue;
                axes[5] = short.MaxValue;
                encoder.BuildReport(buttons, axes, report);
                Equal(0, report[1], "wheel");
                Equal(255, report[3], "gas");
                Equal(0xE2, report[28], "profile-mapped start, view and service");
                Equal(0x02, report[29], "profile-mapped test");
                Equal(0, report[25], "coin press does not increment");

                buttons[0] &= ~Mask(ForwardedInputButton.Coin);
                encoder.BuildReport(buttons, axes, report);
                Equal(1, report[25], "coin release increments counter");

                buttons[0] = Mask(ForwardedInputButton.Button2);
                encoder.BuildReport(buttons, axes, report);
                Equal(0x28, report[30], "first gear");
                encoder.BuildReport(buttons, axes, report);
                Equal(0x28, report[30], "held shift up changes once");

                buttons[0] = 0;
                encoder.BuildReport(buttons, axes, report);
                buttons[0] = Mask(ForwardedInputButton.Button2);
                encoder.BuildReport(buttons, axes, report);
                Equal(0x18, report[30], "second gear");

                buttons[0] = 0;
                encoder.BuildReport(buttons, axes, report);
                buttons[0] = Mask(ForwardedInputButton.Button3);
                encoder.BuildReport(buttons, axes, report);
                Equal(0x28, report[30], "shift down returns to first gear");

                Console.WriteLine("Android Initial D The Arcade ALLS report: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android ALLS IDTA test failed: " + error.Message);
                return 1;
            }
        }

        private static uint Mask(ForwardedInputButton button) => 1u << (int)button;

        private static void Equal(int expected, int actual, string name)
        {
            if (expected != actual)
                throw new InvalidOperationException(
                    $"{name}: expected 0x{expected:X}, got 0x{actual:X}.");
        }
    }
}
