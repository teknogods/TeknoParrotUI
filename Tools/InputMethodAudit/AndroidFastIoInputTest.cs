using System;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace InputMethodAudit
{
    internal static class AndroidFastIoInputTest
    {
        public static int Run()
        {
            try
            {
                var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
                var axes = new short[
                    WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes];
                var report = new byte[AndroidFastIoInputEncoder.ReportSize];
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button5) |
                             Mask(ForwardedInputButton.Button7) |
                             Mask(ForwardedInputButton.Button8);

                AndroidFastIoInputEncoder.BuildReport(
                    AndroidLaunchRecipe.InputProtocolFastIo, buttons, axes, report);
                Equal(0x10, report[0], "generic FastIO start");
                Equal(0x01, report[2], "generic FastIO button 1");
                Equal(0x01, report[3], "generic FastIO button 5");

                AndroidFastIoInputEncoder.BuildReport(
                    AndroidLaunchRecipe.InputProtocolFastIoTheatrhythm,
                    buttons, axes, report);
                Equal(0xB8, report[0],
                    "Theatrhythm enter, select, left, and right buttons");
                Equal(0x01, report[2], "Theatrhythm right-stick up");
                Equal(0x00, report[3], "Theatrhythm does not leak P1 button 5");

                Console.WriteLine("Android FastIO profile variants: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android FastIO input test failed: " + error.Message);
                return 1;
            }
        }

        private static uint Mask(ForwardedInputButton button) => 1u << (int)button;

        private static void Equal(byte expected, byte actual, string name)
        {
            if (expected != actual)
                throw new InvalidOperationException(
                    $"{name}: expected 0x{expected:X2}, got 0x{actual:X2}.");
        }
    }
}
