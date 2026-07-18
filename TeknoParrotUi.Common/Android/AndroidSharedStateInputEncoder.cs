using System;
using System.Buffers.Binary;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Per-session sequential shifter state for the CXBXR WMMT1/2 cabinet.
    /// The Android layout exposes shift-down/up buttons, while the game reads
    /// six mutually-exclusive gear switches.
    /// </summary>
    public sealed class AndroidCxbxrWmmtInputState
    {
        private bool _shiftDownWasPressed;
        private bool _shiftUpWasPressed;
        private int _gear = 1;

        internal int Update(bool shiftDown, bool shiftUp)
        {
            if (shiftDown && !_shiftDownWasPressed)
                _gear = Math.Max(1, _gear - 1);
            if (shiftUp && !_shiftUpWasPressed)
                _gear = Math.Min(6, _gear + 1);
            _shiftDownWasPressed = shiftDown;
            _shiftUpWasPressed = shiftUp;
            return _gear;
        }
    }

    /// <summary>
    /// Reproduces the legacy 64-byte TeknoParrot_JvsState layouts used by
    /// OpenParrot games whose controls are not carried in their named pipe.
    /// Winlator maps this byte range directly into the Windows process.
    /// </summary>
    public static class AndroidSharedStateInputEncoder
    {
        public const int ReportSize = 64;
        private const int DigitalAxisThreshold = 12_000;

        public static void BuildReport(
            string protocol,
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            Span<ForwardedPointerState> pointers = stackalloc ForwardedPointerState[
                WinlatorForwardedInputSource.MaximumPlayers];
            BuildReport(protocol, buttons, axes, pointers, report, null);
        }

        public static void BuildReport(
            string protocol,
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            BuildReport(protocol, buttons, axes, pointers, report, null);
        }

        public static void BuildReport(
            string protocol,
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report,
            AndroidCxbxrWmmtInputState cxbxrWmmtState)
        {
            if (!AndroidLaunchRecipe.IsSharedStateInputProtocol(protocol))
                throw new ArgumentOutOfRangeException(nameof(protocol));
            if (buttons.Length < WinlatorForwardedInputSource.MaximumPlayers ||
                axes.Length < WinlatorForwardedInputSource.MaximumPlayers *
                    WinlatorForwardedInputSource.MaximumAxes ||
                pointers.Length < WinlatorForwardedInputSource.MaximumPlayers ||
                report.Length < ReportSize)
                throw new ArgumentException("A shared-state input buffer is too small.");

            report[..ReportSize].Clear();
            switch (protocol)
            {
                case AndroidLaunchRecipe.InputProtocolSharedExBoard:
                    WriteInt32(report, 8, BuildExBoardButtons(buttons, axes));
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedRawThrills:
                    BuildRawThrills(buttons, axes, report, combineGasBrake: false);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedRawThrillsH2O:
                    BuildRawThrills(buttons, axes, report, combineGasBrake: true);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedRawThrillsGun:
                    BuildRawThrillsGun(buttons, axes, pointers, report, false);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedRawThrillsGoGoStrike:
                    BuildRawThrillsGun(buttons, axes, pointers, report, true);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedWartran:
                    BuildWartran(buttons, axes, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedDeadHeat:
                    BuildDeadHeat(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedFrenzyExpress:
                    BuildFrenzyExpress(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedGrid:
                    BuildGrid(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedGtiClub3:
                    BuildGtiClub3(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedTaiko:
                    WriteInt32(report, 8, BuildTaikoButtons(buttons, axes));
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedGaelco:
                    BuildGaelco(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedJusticeLeague:
                    BuildJusticeLeague(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedEadp:
                    BuildEadp(buttons, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedWonderlandWars:
                    BuildWonderlandWars(buttons, axes, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedFriction:
                    BuildFriction(buttons, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedTaitoGun:
                    BuildTaitoGun(buttons, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedGha:
                    BuildGha(buttons, axes, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedLuigiMansion:
                    BuildLuigiMansion(buttons, axes, pointers, report);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrDriving:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Driving, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Outrun, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt:
                    if (cxbxrWmmtState == null)
                        throw new ArgumentNullException(
                            nameof(cxbxrWmmtState),
                            "CXBXR WMMT input requires per-session shifter state.");
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Wmmt, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrGun:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Gun, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrOllie:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Ollie, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrGundam:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Gundam, cxbxrWmmtState);
                    return;
                case AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf:
                    BuildCxbxr(
                        buttons, axes, pointers, report,
                        CxbxrAnalogLayout.Golf, cxbxrWmmtState);
                    return;
            }
        }

        private enum CxbxrAnalogLayout
        {
            Driving,
            Outrun,
            Wmmt,
            Gun,
            Ollie,
            Gundam,
            Golf
        }

        private static void BuildCxbxr(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report,
            CxbxrAnalogLayout layout,
            AndroidCxbxrWmmtInputState cxbxrWmmtState)
        {
            var control = BuildCxbxrButtons(
                buttons, axes, layout, cxbxrWmmtState);
            if (layout == CxbxrAnalogLayout.Gun)
            {
                if (PointerPressed(pointers[0])) control |= 0x00000004;
                if (PointerPressed(pointers[1])) control |= 0x00000010;
            }

            WriteInt32(report, 8, unchecked((int)control));
            switch (layout)
            {
                case CxbxrAnalogLayout.Driving:
                case CxbxrAnalogLayout.Outrun:
                    // CXBXR driving profiles use TP Analog2 for the wheel,
                    // Analog0 for gas, and Analog6 for brake. The generic
                    // Chihiro JVS adapter swaps these page slots into the
                    // game's order.
                    report[12] = PedalByte(axes, 5);
                    report[13] = WheelByte(axes, buttons);
                    report[14] = 0;
                    report[15] = PedalByte(axes, 4);
                    break;
                case CxbxrAnalogLayout.Wmmt:
                    // WMMT1/2 consume the page directly as wheel, gas, brake.
                    report[12] = WheelByte(axes, buttons);
                    report[13] = PedalByte(axes, 5);
                    report[14] = PedalByte(axes, 4);
                    report[15] = 0;
                    break;
                case CxbxrAnalogLayout.Gun:
                    // HOTD3 and VC3 use the inverted coordinates produced by
                    // the existing desktop CxbxPipe implementation.
                    report[12] = InvertByte(PointerAxisOrController(
                        pointers[0], true, axes[0]));
                    report[13] = InvertByte(PointerAxisOrController(
                        pointers[0], false, axes[1]));
                    report[14] = InvertByte(PointerAxisOrController(
                        pointers[1], true,
                        axes[WinlatorForwardedInputSource.MaximumAxes]));
                    report[15] = InvertByte(PointerAxisOrController(
                        pointers[1], false,
                        axes[WinlatorForwardedInputSource.MaximumAxes + 1]));
                    break;
                case CxbxrAnalogLayout.Ollie:
                    // Ollie King is the non-pointer title which uses the same
                    // inverted TP Analog0 page convention. Its remaining TP
                    // analogs are unassigned, so keep them at desktop neutral
                    // instead of leaking physical right-stick motion.
                    report[12] = InvertByte(WheelByte(axes, buttons));
                    report[13] = InvertByte(0x80);
                    report[14] = InvertByte(0x80);
                    report[15] = InvertByte(0x80);
                    break;
                case CxbxrAnalogLayout.Gundam:
                    // The cabinet exposes its pedal as TP Analog0. The fork's
                    // generic adapter consumes that value from page slot 0.
                    report[12] = PedalByte(axes, 5);
                    report[13] = 0;
                    report[14] = 0;
                    report[15] = 0;
                    break;
                case CxbxrAnalogLayout.Golf:
                    // Sega Golf's CXBXR patches read TP Analog0 from the first
                    // shared-page slot, then mirror it to every native swing
                    // channel used by the 2005 and 2006 revisions. Android R2
                    // is the physical and on-screen swing control.
                    report[12] = PedalByte(axes, 5);
                    report[13] = 0;
                    report[14] = 0;
                    report[15] = 0;
                    break;
            }

            WriteInt32(
                report,
                32,
                Pressed(buttons[0], ForwardedInputButton.Coin) ||
                Pressed(buttons[1], ForwardedInputButton.Coin) ? 1 : 0);
        }

        private static uint BuildCxbxrButtons(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            CxbxrAnalogLayout layout,
            AndroidCxbxrWmmtInputState cxbxrWmmtState)
        {
            uint control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x00000001;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x00000002;
            if (Pressed(buttons[1], ForwardedInputButton.Start)) control |= 0x00000008;
            if (Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x00000010;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x00000040;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x00000080;
            if (Pressed(buttons[1], ForwardedInputButton.Service)) control |= 0x00000100;
            AddCxbxrButtonDirections(ref control, buttons, 0, 0x00000400);
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x00004000;
            AddCxbxrButtonDirections(ref control, buttons, 1, 0x00008000);

            if (layout == CxbxrAnalogLayout.Wmmt)
            {
                var gear = cxbxrWmmtState.Update(
                    Pressed(buttons[0], ForwardedInputButton.Button1),
                    Pressed(buttons[0], ForwardedInputButton.Button2));
                control |= gear switch
                {
                    1 => 0x00000004u,
                    2 => 0x00000020u,
                    3 => 0x00000200u,
                    4 => 0x00080000u,
                    5 => 0x00100000u,
                    6 => 0x00200000u,
                    _ => throw new InvalidOperationException(
                        "CXBXR WMMT shifter left its valid gear range.")
                };
                if (Pressed(buttons[0], ForwardedInputButton.Button3))
                    control |= 0x08000000; // view / ExtensionButton1_2
                if (Pressed(buttons[0], ForwardedInputButton.Button4))
                    control |= 0x00400000; // interruption / ExtensionButton1_1
            }
            else if (layout == CxbxrAnalogLayout.Outrun)
            {
                // The XML routes OutRun's view switch through P1 Down and its
                // sequential shifter through P2 Up/Down. Translate the three
                // intuitively-labelled Android face buttons to those sensors.
                if (Pressed(buttons[0], ForwardedInputButton.Button1))
                    control |= 0x00002000; // view
                if (Pressed(buttons[0], ForwardedInputButton.Button2))
                    control |= 0x00040000; // shift down
                if (Pressed(buttons[0], ForwardedInputButton.Button3))
                    control |= 0x00010000; // shift up
            }
            else
            {
                if (Pressed(buttons[0], ForwardedInputButton.Button1))
                    control |= 0x00000004;
                if (Pressed(buttons[0], ForwardedInputButton.Button2))
                    control |= 0x00000020;
                if (Pressed(buttons[0], ForwardedInputButton.Button3))
                    control |= 0x00000200;
            }

            if (layout != CxbxrAnalogLayout.Gundam &&
                layout != CxbxrAnalogLayout.Wmmt &&
                layout != CxbxrAnalogLayout.Outrun)
            {
                if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x00080000;
                if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x00100000;
                if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x00200000;
                if (Pressed(buttons[0], ForwardedInputButton.Button7)) control |= 0x00400000;
                if (Pressed(buttons[0], ForwardedInputButton.Button8)) control |= 0x08000000;
            }
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x00800000;
            if (Pressed(buttons[1], ForwardedInputButton.Button5)) control |= 0x01000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button6)) control |= 0x02000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button7)) control |= 0x04000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button8)) control |= 0x20000000;

            if (layout == CxbxrAnalogLayout.Gundam)
            {
                // A single Android controller represents the cabinet's two
                // sticks: left stick is P1, right stick is P2. Face/shoulder
                // buttons 4 and 5 provide the right-stick fire buttons.
                SetCxbxrDirection(
                    ref control, axes[0], axes[1],
                    0x00000400, 0x00001000, 0x00000800, 0x00002000);
                SetCxbxrDirection(
                    ref control, axes[2], axes[3],
                    0x00008000, 0x00020000, 0x00010000, 0x00040000);
                if (Pressed(buttons[0], ForwardedInputButton.Button4))
                    control |= 0x00000010;
                if (Pressed(buttons[0], ForwardedInputButton.Button5))
                    control |= 0x00000080;
            }

            return control;
        }

        private static void AddCxbxrButtonDirections(
            ref uint control,
            ReadOnlySpan<uint> buttons,
            int player,
            uint leftBit)
        {
            // CXBXR's page has separate digital cabinet switches and analog
            // channels. Do not turn a steering/gun axis into a menu switch.
            if (Pressed(buttons[player], ForwardedInputButton.Left))
                control |= leftBit;
            if (Pressed(buttons[player], ForwardedInputButton.Up))
                control |= leftBit << 1;
            if (Pressed(buttons[player], ForwardedInputButton.Right))
                control |= leftBit << 2;
            if (Pressed(buttons[player], ForwardedInputButton.Down))
                control |= leftBit << 3;
        }

        private static void SetCxbxrDirection(
            ref uint control,
            short x,
            short y,
            uint left,
            uint right,
            uint up,
            uint down)
        {
            if (x <= -DigitalAxisThreshold) control |= left;
            if (x >= DigitalAxisThreshold) control |= right;
            if (y <= -DigitalAxisThreshold) control |= up;
            if (y >= DigitalAxisThreshold) control |= down;
        }

        private static byte WheelByte(
            ReadOnlySpan<short> axes,
            ReadOnlySpan<uint> buttons)
        {
            if (Pressed(buttons[0], ForwardedInputButton.Left)) return 0;
            if (Pressed(buttons[0], ForwardedInputButton.Right)) return byte.MaxValue;
            return AxisToCenteredByte(axes[0]);
        }

        private static byte PedalByte(ReadOnlySpan<short> axes, int axis) =>
            (byte)(Math.Clamp((int)axes[axis], 0, short.MaxValue) *
                byte.MaxValue / short.MaxValue);

        private static byte InvertByte(byte value) => (byte)~value;

        private static void BuildLuigiMansion(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x000001;
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x000002;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x000004;
            if (Pressed(buttons[1], ForwardedInputButton.Start)) control |= 0x000008;
            if (PointerPressed(pointers[1]) ||
                Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x000010;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x000020;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x000100;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x000200;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x000400;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x010000;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x020000;
            WriteInt32(report, 8, control);

            // Luigi reads one packed DWORD: P1 Y, P1 X, P2 Y, P2 X.
            // Touch is authoritative while present; sticks provide a centered
            // physical-controller fallback when no pointer has been reported.
            report[12] = PointerAxisOrController(pointers[0], false, axes[1]);
            report[13] = PointerAxisOrController(pointers[0], true, axes[0]);
            report[14] = PointerAxisOrController(pointers[1], false,
                axes[WinlatorForwardedInputSource.MaximumAxes + 1]);
            report[15] = PointerAxisOrController(pointers[1], true,
                axes[WinlatorForwardedInputSource.MaximumAxes]);
        }

        private static byte PointerAxisOrController(
            ForwardedPointerState pointer,
            bool horizontal,
            short controllerAxis)
        {
            var hasPointer = pointer.X != 0 || pointer.Y != 0 ||
                             pointer.Pressure != 0 || pointer.Buttons != 0;
            return hasPointer
                ? PointerToByte(horizontal ? pointer.X : pointer.Y)
                : AxisToByte(controllerAxis);
        }

        private static void BuildEadp(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x02;
            if (PointerPressed(pointers[1]) ||
                Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x04;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x08;
            if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x10;
            if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x20;
            WriteInt32(report, 8, control);
            WritePointer(report, 12, 16, pointers[0]);
            WritePointer(report, 20, 24, pointers[1]);
        }

        private static void BuildWonderlandWars(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            // The cabinet pen switch follows the native 3M touch press as well
            // as a physical controller's primary face button.
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x02;
            WriteInt32(report, 8, control);

            // Desktop TP initializes these two analog channels to 0x80. Keep
            // that exact neutral value instead of AxisToByte's floored 0x7f.
            WriteInt32(report, 12, AxisToCenteredByte(axes[0]));
            WriteInt32(report, 16, AxisToCenteredByte(axes[1]));

            // ExtensionButton1_3 is the desktop Aime/card switch. Android's
            // cabinet overlays consistently expose that extension as button 4.
            WriteInt32(report, 32,
                Pressed(buttons[0], ForwardedInputButton.Button4) ? 1 : 0);
        }

        private static void BuildFriction(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x02;
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x04;
            if (PointerPressed(pointers[1]) ||
                Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x08;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x10;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x20;
            if (Pressed(buttons[1], ForwardedInputButton.Start)) control |= 0x40;
            if (Pressed(buttons[0], ForwardedInputButton.Up)) control |= 0x80;
            if (Pressed(buttons[0], ForwardedInputButton.Down)) control |= 0x100;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x200;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x400;
            if (Pressed(buttons[1], ForwardedInputButton.Coin)) control |= 0x800;
            WriteInt32(report, 8, control);
            WritePointer(report, 12, 16, pointers[0]);
            WritePointer(report, 20, 24, pointers[1]);
        }

        private static void BuildTaitoGun(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x01;
            if (PointerPressed(pointers[1]) ||
                Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x04;
            if (Pressed(buttons[2], ForwardedInputButton.Button1)) control |= 0x40;
            if (Pressed(buttons[3], ForwardedInputButton.Button1)) control |= 0x80;
            if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x10;
            if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x20;
            WriteInt32(report, 8, control);
            for (var player = 0; player < WinlatorForwardedInputSource.MaximumPlayers; player++)
                WritePointer(report, 12 + player * 8, 16 + player * 8, pointers[player]);
        }

        private static void BuildGha(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            for (var player = 0; player < 2; player++)
            {
                var shift = player * 8;
                if (Pressed(buttons[player], ForwardedInputButton.Start)) control |= 0x01 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Up)) control |= 0x02 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Down)) control |= 0x04 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Button1)) control |= 0x08 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Button2)) control |= 0x10 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Button3)) control |= 0x20 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Button4)) control |= 0x40 << shift;
                if (Pressed(buttons[player], ForwardedInputButton.Button5)) control |= 0x80 << shift;
            }
            WriteInt32(report, 8, control);
            report[12] = AxisToByte(axes[0]);
            report[16] = AxisToByte(axes[2]);
        }

        private static void WritePointer(
            Span<byte> report,
            int xOffset,
            int yOffset,
            ForwardedPointerState pointer)
        {
            var isDefault = pointer.X == 0 && pointer.Y == 0 &&
                            pointer.Pressure == 0 && pointer.Buttons == 0;
            report[xOffset] = isDefault ? (byte)127 : PointerToByte(pointer.X);
            report[yOffset] = isDefault ? (byte)127 : PointerToByte(pointer.Y);
        }

        private static bool PointerPressed(ForwardedPointerState pointer) =>
            pointer.Pressure != 0 || (pointer.Buttons & 1u) != 0;

        private static byte PointerToByte(ushort value) =>
            (byte)((uint)value * byte.MaxValue / ushort.MaxValue);

        private static int BuildTaikoButtons(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x04;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x08;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x10;
            // OpenParrot exposes Taiko's cabinet Enter action at 0x20. Keep
            // D-pad Right for test-menu navigation and make the overlay's
            // explicit START control useful during ordinary game flow.
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right) ||
                Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x20;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x40;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x80;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x100;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x200;
            if (Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x400;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x800;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x1000;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x2000;
            return control;
        }

        private static void BuildGaelco(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x0100;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x0200;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0400;
            if (Pressed(buttons[1], ForwardedInputButton.Coin)) control |= 0x0800;
            if (Pressed(buttons[0], ForwardedInputButton.Button4) ||
                Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x1000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up)) control |= 0x2000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down)) control |= 0x4000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x8000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Button1) ||
                axes[5] >= DigitalAxisThreshold) control |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Button2) ||
                axes[4] >= DigitalAxisThreshold) control |= 0x04;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x08;

            WriteInt32(report, 8, control);
            report[12] = (byte)Wheel(axes, buttons);
            report[16] = (byte)Pedal(axes, 5);
            report[20] = (byte)Pedal(axes, 4);
        }

        private static void BuildJusticeLeague(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x0001;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x0002;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0004;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x0008;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x0100;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x0200;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x0400;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x0800;
            WriteInt32(report, 8, control);
            WriteInt32(report, 12, AxisToByte(axes[0]));
            WriteInt32(report, 16, AxisToByte(axes[1]));
        }

        private static int BuildExBoardButtons(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes)
        {
            uint control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x00400000;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x00000100;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x00000040;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x20;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x10;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) control |= 0x08;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) control |= 0x04;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x80;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x00008000;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x00004000;
            if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x00002000;
            if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x00001000;

            if (Pressed(buttons[1], ForwardedInputButton.Service)) control |= 0x40;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up)) control |= 0x00200000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down)) control |= 0x00100000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Left)) control |= 0x00080000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Right)) control |= 0x00040000;
            if (Pressed(buttons[1], ForwardedInputButton.Start)) control |= 0x00800000;
            if (Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x00020000;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x00010000;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x80000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x40000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button5)) control |= 0x20000000;
            if (Pressed(buttons[1], ForwardedInputButton.Button6)) control |= 0x10000000;
            return unchecked((int)control);
        }

        private static void BuildRawThrills(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report,
            bool combineGasBrake)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x0001;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x0002;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0004;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x0008;
            if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x0010;
            if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x0020;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up)) control |= 0x0040;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down)) control |= 0x0080;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x0100;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x0200;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x0400;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control |= 0x0800;
            // Cruis'n Blast exposes its brake pedal as P1Button4 even though
            // Android and physical controllers naturally report L2 as an
            // analog pedal. Preserve the ordinary button mapping while also
            // making the pedal usable from either input source.
            if (axes[4] >= DigitalAxisThreshold) control |= 0x0800;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x1000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x2000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) control |= 0x4000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) control |= 0x8000;

            var gears = 0;
            if (Pressed(buttons[2], ForwardedInputButton.Button1)) gears |= 0x01;
            if (Pressed(buttons[2], ForwardedInputButton.Button2)) gears |= 0x02;
            if (Pressed(buttons[2], ForwardedInputButton.Button3)) gears |= 0x04;
            if (Pressed(buttons[2], ForwardedInputButton.Button4)) gears |= 0x08;

            var wheel = Wheel(axes, buttons);
            var gas = Pedal(axes, 5);
            var brake = Pedal(axes, 4);
            WriteInt32(report, 8, control);
            WriteInt32(report, 12, wheel);
            if (combineGasBrake)
            {
                WriteInt32(report, 16, brake > 0 ? -brake : gas);
                WriteInt32(report, 20, 0);
            }
            else
            {
                WriteInt32(report, 16, gas);
                WriteInt32(report, 20, brake);
            }
            WriteInt32(report, 24, gears);
        }

        private static void BuildRawThrillsGun(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report,
            bool goGoStrike)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x0001;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x0002;
            // Go Go Strike uses the first coin channel as its cabinet Setup
            // key and the second as the actual coin input. A single-player
            // Android overlay cannot emit a P2 coin event, so its dedicated
            // protocol maps B to Setup and Select to the real coin channel.
            if (goGoStrike)
            {
                if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x0004;
                if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0008;
            }
            else if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0004;
            if (Pressed(buttons[1], ForwardedInputButton.Coin)) control |= 0x0008;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x0010;
            if (PointerPressed(pointers[0]) ||
                Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x0020;
            if (!goGoStrike && Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x0040;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x0080;
            if (Pressed(buttons[1], ForwardedInputButton.Start)) control |= 0x0100;
            if (PointerPressed(pointers[1]) ||
                Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x0200;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x0400;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x0800;
            // These are independent digital cabinet inputs in RawThrillsGUN.cs.
            // Never derive them from the analog gun/trackball/controller axes.
            if (Pressed(buttons[0], ForwardedInputButton.Up)) control |= 0x1000;
            if (Pressed(buttons[0], ForwardedInputButton.Down)) control |= 0x2000;
            if (Pressed(buttons[0], ForwardedInputButton.Left)) control |= 0x4000;
            if (Pressed(buttons[0], ForwardedInputButton.Right)) control |= 0x8000;

            WriteInt32(report, 8, control);
            report[12] = PointerAxisOrController(pointers[0], true, axes[0]);
            report[16] = PointerAxisOrController(pointers[0], false, axes[1]);
            report[20] = PointerAxisOrController(
                pointers[1], true, axes[WinlatorForwardedInputSource.MaximumAxes]);
            report[24] = PointerAxisOrController(
                pointers[1], false, axes[WinlatorForwardedInputSource.MaximumAxes + 1]);
        }

        private static void BuildWartran(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            ReadOnlySpan<ForwardedPointerState> pointers,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x000001;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x000002;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x000004;
            if (Pressed(buttons[1], ForwardedInputButton.Coin)) control |= 0x000008;
            if (Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x000010;
            if (Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x000020;

            for (var player = 0; player < WinlatorForwardedInputSource.MaximumPlayers; player++)
            {
                var shift = player == 0 ? 6 : 11 + (player - 1) * 5;
                if (Pressed(buttons[player], ForwardedInputButton.Start)) control |= 1 << shift;
                if (PointerPressed(pointers[player]) ||
                    Pressed(buttons[player], ForwardedInputButton.Button1)) control |= 1 << (shift + 1);
                if (Pressed(buttons[player], ForwardedInputButton.Button2)) control |= 1 << (shift + 2);
                if (Pressed(buttons[player], ForwardedInputButton.Button3)) control |= 1 << (shift + 3);
                if (Pressed(buttons[player], ForwardedInputButton.Button4)) control |= 1 << (shift + 4);

                var axisOffset = player * WinlatorForwardedInputSource.MaximumAxes;
                report[12 + player * 2] = PointerAxisOrController(
                    pointers[player], true, axes[axisOffset]);
                report[13 + player * 2] = PointerAxisOrController(
                    pointers[player], false, axes[axisOffset + 1]);
            }

            WriteInt32(report, 8, control);
        }

        private static void BuildDeadHeat(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x0100;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x0200;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x0400;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up) ||
                Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x1000;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down) ||
                Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x2000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x4000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x0001;
            if (Pressed(buttons[0], ForwardedInputButton.Button1) ||
                Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x0002;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x0004;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x0008;
            if (Pressed(buttons[1], ForwardedInputButton.Button1)) control |= 0x8000;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control |= 0x10000;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control |= 0x20000;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control |= 0x40000;

            WriteInt32(report, 8, control);
            report[12] = (byte)Wheel(axes, buttons);
            report[16] = (byte)Pedal(axes, 5);
            report[20] = (byte)Pedal(axes, 4);
            report[28] = AxisToByte(axes[3]);
        }

        private static void BuildFrenzyExpress(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x04;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x08;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) control |= 0x10;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) control |= 0x20;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x40;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control |= 0x80;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control |= 0x100;
            WriteInt32(report, 8, control);
            // Desktop TPUI initializes Frenzy Express' wheel byte to exactly
            // 0x80. The generic signed-axis conversion maps zero to 0x7F,
            // which makes this title fail its boot-time center calibration.
            // Preserve the full-range mapping away from neutral while matching
            // the cabinet's exact center value when no direction is asserted.
            var wheel = !Pressed(buttons[0], ForwardedInputButton.Left) &&
                        !Pressed(buttons[0], ForwardedInputButton.Right) &&
                        axes[0] == 0
                ? 0x80
                : Wheel(axes, buttons);
            WriteInt32(report, 12, wheel);
        }

        private static void BuildGrid(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            var control2 = 0;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x01;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up) ||
                Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x02;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down) ||
                Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x04;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control2 |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Button2)) control2 |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Button3)) control2 |= 0x04;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) control2 |= 0x08;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) control2 |= 0x10;
            if (Pressed(buttons[0], ForwardedInputButton.Button4)) control2 |= 0x20;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control2 |= 0x40;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control2 |= 0x80;
            if (Pressed(buttons[1], ForwardedInputButton.Button1)) control2 |= 0x0100;
            if (Pressed(buttons[1], ForwardedInputButton.Button2)) control2 |= 0x0200;
            if (Pressed(buttons[1], ForwardedInputButton.Button3)) control2 |= 0x0400;
            if (Pressed(buttons[1], ForwardedInputButton.Button4)) control2 |= 0x0800;
            if (Pressed(buttons[1], ForwardedInputButton.Button5)) control2 |= 0x1000;
            if (Pressed(buttons[1], ForwardedInputButton.Button6)) control2 |= 0x2000;
            WriteInt32(report, 4, control2);
            WriteInt32(report, 8, control);
            WriteInt32(report, 12, Wheel(axes, buttons));
            WriteInt32(report, 16, Pedal(axes, 5));
            WriteInt32(report, 20, Pedal(axes, 4));
        }

        private static void BuildGtiClub3(
            ReadOnlySpan<uint> buttons,
            ReadOnlySpan<short> axes,
            Span<byte> report)
        {
            var control = 0;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up) ||
                Pressed(buttons[0], ForwardedInputButton.Button5)) control |= 0x0100;
            if (Pressed(buttons[0], ForwardedInputButton.Button1)) control |= 0x0800;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) control |= 0x1000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) control |= 0x2000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) control |= 0x4000;
            if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) control |= 0x8000;
            if (Pressed(buttons[0], ForwardedInputButton.Test)) control |= 0x02;
            if (Pressed(buttons[0], ForwardedInputButton.Service)) control |= 0x01;
            if (Pressed(buttons[0], ForwardedInputButton.Coin)) control |= 0x04;
            if (Pressed(buttons[0], ForwardedInputButton.Start)) control |= 0x10;
            if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down) ||
                Pressed(buttons[0], ForwardedInputButton.Button6)) control |= 0x80;
            WriteInt32(report, 8, control);
            WriteInt32(report, 12, 0xFF + Wheel(axes, buttons) * 0x100);
            WriteInt32(report, 16, 0xFF + Pedal(axes, 5) * 0x100);
            WriteInt32(report, 20, 0xFF + Pedal(axes, 4) * 0x100);
        }

        private static int Wheel(ReadOnlySpan<short> axes, ReadOnlySpan<uint> buttons)
        {
            if (Pressed(buttons[0], ForwardedInputButton.Left)) return 0;
            if (Pressed(buttons[0], ForwardedInputButton.Right)) return byte.MaxValue;
            return AxisToByte(axes[0]);
        }

        private static int Pedal(ReadOnlySpan<short> axes, int axis) =>
            Math.Clamp((int)axes[axis], 0, short.MaxValue) * byte.MaxValue / short.MaxValue;

        private static byte AxisToByte(short value) =>
            (byte)(((long)value - short.MinValue) * byte.MaxValue / ushort.MaxValue);

        private static byte AxisToCenteredByte(short value) =>
            value == 0 ? (byte)0x80 : AxisToByte(value);

        private static bool Pressed(uint state, ForwardedInputButton button) =>
            (state & (1u << (int)button)) != 0;

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

        private static void WriteInt32(Span<byte> report, int offset, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(report.Slice(offset, sizeof(int)), value);
    }
}
