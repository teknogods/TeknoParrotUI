using System;
using System.Buffers.Binary;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace InputMethodAudit
{
    internal static class AndroidSharedStateInputTest
    {
        public static int Run()
        {
            try
            {
                var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
                var axes = new short[
                    WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes];
                var report = new byte[AndroidSharedStateInputEncoder.ReportSize];
                var pointers = new ForwardedPointerState[
                    WinlatorForwardedInputSource.MaximumPlayers];

                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Coin) |
                             Mask(ForwardedInputButton.Button1);
                Build(AndroidLaunchRecipe.InputProtocolSharedExBoard, buttons, axes, report);
                Equal(0x182, ReadInt32(report, 8), "ExBoard buttons");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button1);
                axes[5] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrills, buttons, axes, report);
                Equal(0x108, ReadInt32(report, 8), "Raw Thrills buttons");
                Equal(127, ReadInt32(report, 12), "Raw Thrills centered wheel");
                Equal(255, ReadInt32(report, 16), "Raw Thrills gas");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                axes[0] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrillsSuperBikes,
                    buttons, axes, report);
                Equal(255, ReadInt32(report, 12), "Super Bikes wheel");
                Equal(0, ReadInt32(report, 8) & 0xC000,
                    "Super Bikes wheel does not adjust volume");

                axes[5] = 0;
                axes[4] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrillsH2O, buttons, axes, report);
                Equal(-255, ReadInt32(report, 16), "H2Overdrive combined brake");
                Equal(0, ReadInt32(report, 20), "H2Overdrive second pedal slot");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Left) |
                             Mask(ForwardedInputButton.Button1);
                Build(AndroidLaunchRecipe.InputProtocolSharedFrenzyExpress, buttons, axes, report);
                Equal(0x50, ReadInt32(report, 8), "Frenzy Express buttons");

                Array.Clear(buttons, 0, buttons.Length);
                Build(AndroidLaunchRecipe.InputProtocolSharedFrenzyExpress, buttons, axes, report);
                Equal(0x80, ReadInt32(report, 12), "Frenzy Express centered wheel");

                Array.Clear(buttons, 0, buttons.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button5);
                Build(AndroidLaunchRecipe.InputProtocolSharedGrid, buttons, axes, report);
                Equal(0x03, ReadInt32(report, 8), "GRID start and shift-up");
                Equal(0, ReadInt32(report, 4) & 0x4000, "GRID shift does not select reverse");

                Build(AndroidLaunchRecipe.InputProtocolSharedGtiClub3, buttons, axes, report);
                Equal(0x110, ReadInt32(report, 8), "GTI Club start and shift-up");

                Array.Clear(buttons, 0, buttons.Length);
                buttons[1] = Mask(ForwardedInputButton.Button6);
                Build(AndroidLaunchRecipe.InputProtocolSharedGrid, buttons, axes, report);
                Equal(0x2000, ReadInt32(report, 4), "GRID gear 6 does not also select reverse");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Coin) |
                             Mask(ForwardedInputButton.Right) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button4);
                Build(AndroidLaunchRecipe.InputProtocolSharedTaiko, buttons, axes, report);
                Equal(0x261, ReadInt32(report, 8), "Taiko menu, coin, and drum buttons");

                Array.Clear(buttons, 0, buttons.Length);
                buttons[0] = Mask(ForwardedInputButton.Start);
                Build(AndroidLaunchRecipe.InputProtocolSharedTaiko, buttons, axes, report);
                Equal(0x20, ReadInt32(report, 8), "Taiko explicit start maps to cabinet enter");

                Array.Clear(buttons, 0, buttons.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button1);
                axes[0] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedGaelco, buttons, axes, report);
                Equal(0x1002, ReadInt32(report, 8), "Gaelco start and accelerator");
                Equal(255, report[12], "Gaelco handlebar");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button4);
                axes[0] = short.MinValue;
                axes[1] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedJusticeLeague,
                    buttons, axes, report);
                Equal(0x808, ReadInt32(report, 8), "Justice League start and button 4");
                Equal(0, ReadInt32(report, 12), "Justice League X axis");
                Equal(255, ReadInt32(report, 16), "Justice League Y axis");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = new ForwardedPointerState(
                    7, ushort.MaxValue, 32_768, ushort.MaxValue, 1, 1);
                Build(AndroidLaunchRecipe.InputProtocolSharedEadp,
                    buttons, axes, pointers, report);
                Equal(0x01, ReadInt32(report, 8), "EADP touch trigger");
                Equal(255, report[12], "EADP gun X");
                Equal(127, report[16], "EADP gun Y");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = default;
                Build(AndroidLaunchRecipe.InputProtocolSharedWonderlandWars,
                    buttons, axes, pointers, report);
                Equal(0, ReadInt32(report, 8), "Wonderland neutral buttons");
                Equal(0x80, ReadInt32(report, 12), "Wonderland centered X axis");
                Equal(0x80, ReadInt32(report, 16), "Wonderland centered Y axis");
                Equal(0, ReadInt32(report, 32), "Wonderland neutral Aime");

                buttons[0] = Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button4);
                axes[0] = short.MinValue;
                axes[1] = short.MaxValue;
                pointers[0] = new ForwardedPointerState(
                    8, 32_768, 32_768, ushort.MaxValue, 1, 1);
                Build(AndroidLaunchRecipe.InputProtocolSharedWonderlandWars,
                    buttons, axes, pointers, report);
                Equal(0x03, ReadInt32(report, 8),
                    "Wonderland touch pen and dodge switches");
                Equal(0, ReadInt32(report, 12), "Wonderland minimum X axis");
                Equal(255, ReadInt32(report, 16), "Wonderland maximum Y axis");
                Equal(1, ReadInt32(report, 32), "Wonderland Aime switch");

                buttons[0] = Mask(ForwardedInputButton.Test);
                Build(AndroidLaunchRecipe.InputProtocolSharedFriction,
                    buttons, axes, pointers, report);
                Equal(0x05, ReadInt32(report, 8), "Friction test and touch trigger");

                Array.Clear(buttons, 0, buttons.Length);
                Build(AndroidLaunchRecipe.InputProtocolSharedTaitoGun,
                    buttons, axes, pointers, report);
                Equal(0x01, ReadInt32(report, 8), "Taito gun touch trigger");
                Build(AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic,
                    buttons, axes, pointers, report);
                Equal(0x01, ReadInt32(report, 8),
                    "Music Gun Gun 2 touch trigger");
                Build(AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2,
                    buttons, axes, pointers, report);
                Equal(0x01, ReadInt32(report, 8),
                    "Haunted Museum II touch trigger");

                Array.Clear(buttons, 0, buttons.Length);
                pointers[0] = new ForwardedPointerState(
                    9, ushort.MaxValue, 32_768, ushort.MaxValue, 1, 1);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button3);
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrillsGun,
                    buttons, axes, pointers, report);
                Equal(0xF0, ReadInt32(report, 8),
                    "Raw Thrills gun start, touch trigger, grenade, and reload");
                Equal(255, report[12], "Raw Thrills gun X");
                Equal(127, report[16], "Raw Thrills gun Y");

                Array.Clear(buttons, 0, buttons.Length);
                pointers[0] = default;
                buttons[0] = Mask(ForwardedInputButton.Button4);
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrillsGun,
                    buttons, axes, pointers, report);
                Equal(0x0800, ReadInt32(report, 8),
                    "Jurassic Park P1 overlay menu-down alias");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = default;
                buttons[0] = Mask(ForwardedInputButton.Coin) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button2);
                axes[0] = short.MaxValue;
                axes[1] = short.MinValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedRawThrillsGoGoStrike,
                    buttons, axes, pointers, report);
                Equal(0x2C, ReadInt32(report, 8),
                    "Go Go Strike setup, coin, and trackball click");
                Equal(255, report[12], "Go Go Strike trackball X");
                Equal(0, report[16], "Go Go Strike trackball Y");
                Equal(0, ReadInt32(report, 8) & 0xF000,
                    "Raw Thrills analog axes do not synthesize digital cabinet switches");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = new ForwardedPointerState(
                    10, ushort.MaxValue, 32_768, ushort.MaxValue, 1, 1);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button2);
                Build(AndroidLaunchRecipe.InputProtocolSharedWartran,
                    buttons, axes, pointers, report);
                Equal(0x1C0, ReadInt32(report, 8),
                    "Wartran start, touch trigger, and option");
                Equal(255, report[12], "Wartran P1 X");
                Equal(127, report[13], "Wartran P1 Y");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = default;
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button2);
                axes[0] = short.MaxValue;
                axes[5] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedDeadHeat,
                    buttons, axes, pointers, report);
                Equal(0x06, ReadInt32(report, 8), "Dead Heat enter and view");
                Equal(255, report[12], "Dead Heat wheel");
                Equal(255, report[16], "Dead Heat gas");

                pointers[0] = default;
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Up) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button5);
                Build(AndroidLaunchRecipe.InputProtocolSharedGha,
                    buttons, axes, pointers, report);
                Equal(0x8B, ReadInt32(report, 8), "Guitar Hero left guitar controls");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = new ForwardedPointerState(
                    8, ushort.MaxValue, 32_768, ushort.MaxValue, 1, 1);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button3) |
                             Mask(ForwardedInputButton.Button4) |
                             Mask(ForwardedInputButton.Test) |
                             Mask(ForwardedInputButton.Service);
                Build(AndroidLaunchRecipe.InputProtocolSharedLuigiMansion,
                    buttons, axes, pointers, report);
                Equal(0x10707, ReadInt32(report, 8), "Luigi P1 cabinet and vacuum controls");
                Equal(127, report[12], "Luigi packed P1 gun Y");
                Equal(255, report[13], "Luigi packed P1 gun X");

                pointers[0] = default;
                axes[0] = short.MinValue;
                axes[1] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedLuigiMansion,
                    buttons, axes, pointers, report);
                Equal(255, report[12], "Luigi controller P1 gun Y fallback");
                Equal(0, report[13], "Luigi controller P1 gun X fallback");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = default;
                try
                {
                    Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                        buttons, axes, pointers, report);
                    throw new InvalidOperationException(
                        "CXBXR WMMT accepted a report without session shifter state.");
                }
                catch (ArgumentNullException)
                {
                    // Required: each game session owns and resets its shifter.
                }

                var cxbxrWmmtState = new AndroidCxbxrWmmtInputState();
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Coin);
                axes[0] = short.MaxValue;
                axes[5] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                    buttons, axes, pointers, report, cxbxrWmmtState);
                Equal(0x06, ReadInt32(report, 8),
                    "CXBXR WMMT starts in persistent gear 1");
                Equal(255, report[12], "CXBXR WMMT wheel");
                Equal(255, report[13], "CXBXR WMMT gas");
                Equal(0, report[14], "CXBXR WMMT brake");
                Equal(1, ReadInt32(report, 32), "CXBXR WMMT coin");

                buttons[0] = Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button3) |
                             Mask(ForwardedInputButton.Button4);
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                    buttons, axes, pointers, report, cxbxrWmmtState);
                Equal(0x08400020, ReadInt32(report, 8),
                    "CXBXR WMMT shift-up, view, and interruption");

                buttons[0] = 0;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                    buttons, axes, pointers, report, cxbxrWmmtState);
                Equal(0x20, ReadInt32(report, 8),
                    "CXBXR WMMT gear 2 persists after button release");

                buttons[0] = Mask(ForwardedInputButton.Button2);
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                    buttons, axes, pointers, report, cxbxrWmmtState);
                Equal(0x200, ReadInt32(report, 8),
                    "CXBXR WMMT second shift-up selects gear 3");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button3);
                axes[0] = short.MinValue;
                axes[4] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun,
                    buttons, axes, pointers, report);
                Equal(0x52002, ReadInt32(report, 8),
                    "CXBXR OutRun view and sequential shifter sensors");
                Equal(0, report[12], "CXBXR OutRun gas page slot");
                Equal(0, report[13], "CXBXR OutRun wheel page slot");
                Equal(255, report[15], "CXBXR OutRun brake page slot");

                Array.Clear(buttons, 0, buttons.Length);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Up) |
                             Mask(ForwardedInputButton.Down) |
                             Mask(ForwardedInputButton.Button1) |
                             Mask(ForwardedInputButton.Button2);
                axes[0] = short.MinValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrDriving,
                    buttons, axes, pointers, report);
                Equal(0x2826, ReadInt32(report, 8),
                    "CXBXR Crazy Taxi gears and jump buttons");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = new ForwardedPointerState(
                    11, ushort.MaxValue, 32_768, ushort.MaxValue, 1, 1);
                buttons[0] = Mask(ForwardedInputButton.Start) |
                             Mask(ForwardedInputButton.Service) |
                             Mask(ForwardedInputButton.Test) |
                             Mask(ForwardedInputButton.Button2) |
                             Mask(ForwardedInputButton.Button3) |
                             Mask(ForwardedInputButton.Button4);
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrGun,
                    buttons, axes, pointers, report);
                Equal(0x80267, ReadInt32(report, 8),
                    "CXBXR gun cabinet, trigger, reload, weapon, and special");
                Equal(0, report[12], "CXBXR inverted gun X");
                Equal(128, report[13], "CXBXR inverted gun Y");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                pointers[0] = default;
                axes[1] = short.MaxValue;
                axes[2] = short.MinValue;
                axes[3] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrOllie,
                    buttons, axes, pointers, report);
                Equal(127, report[12], "CXBXR Ollie centered inverted axis");
                Equal(127, report[13], "CXBXR Ollie unused analog 1 stays neutral");
                Equal(127, report[14], "CXBXR Ollie unused analog 2 stays neutral");
                Equal(127, report[15], "CXBXR Ollie unused analog 3 stays neutral");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                buttons[0] = Mask(ForwardedInputButton.Button3) |
                             Mask(ForwardedInputButton.Button4) |
                             Mask(ForwardedInputButton.Button5);
                axes[0] = short.MinValue;
                axes[1] = short.MaxValue;
                axes[2] = short.MaxValue;
                axes[3] = short.MinValue;
                axes[5] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrGundam,
                    buttons, axes, pointers, report);
                Equal(0x32690, ReadInt32(report, 8),
                    "CXBXR Gundam dual sticks, card, and fire buttons");
                Equal(255, report[12], "CXBXR Gundam analog pedal");

                Array.Clear(buttons, 0, buttons.Length);
                Array.Clear(axes, 0, axes.Length);
                axes[5] = short.MaxValue;
                Build(AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf,
                    buttons, axes, pointers, report);
                Equal(255, report[12], "CXBXR Sega Golf swing");
                Equal(0, report[13], "CXBXR Sega Golf unused analog 1");
                Equal(0, report[14], "CXBXR Sega Golf unused analog 2");
                Equal(0, report[15], "CXBXR Sega Golf unused analog 3");

                Console.WriteLine("Android OpenParrot/CXBXR shared-state layouts: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android shared-state test failed: " + error.Message);
                return 1;
            }
        }

        private static void Build(
            string protocol,
            uint[] buttons,
            short[] axes,
            byte[] report) =>
            AndroidSharedStateInputEncoder.BuildReport(protocol, buttons, axes, report);

        private static void Build(
            string protocol,
            uint[] buttons,
            short[] axes,
            ForwardedPointerState[] pointers,
            byte[] report) =>
            AndroidSharedStateInputEncoder.BuildReport(
                protocol, buttons, axes, pointers, report);

        private static void Build(
            string protocol,
            uint[] buttons,
            short[] axes,
            ForwardedPointerState[] pointers,
            byte[] report,
            AndroidCxbxrWmmtInputState cxbxrWmmtState) =>
            AndroidSharedStateInputEncoder.BuildReport(
                protocol, buttons, axes, pointers, report, cxbxrWmmtState);

        private static uint Mask(ForwardedInputButton button) => 1u << (int)button;

        private static int ReadInt32(byte[] report, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(report.AsSpan(offset, sizeof(int)));

        private static void Equal(int expected, int actual, string name)
        {
            if (expected != actual)
                throw new InvalidOperationException(
                    $"{name}: expected 0x{expected:X}, got 0x{actual:X}.");
        }
    }
}
