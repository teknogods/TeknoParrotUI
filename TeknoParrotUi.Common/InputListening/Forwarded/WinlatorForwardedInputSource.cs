using System;
using System.Collections.Generic;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Mouse;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputListening.Forwarded
{
    public enum ForwardedInputApplyResult
    {
        Applied,
        SequenceGap,
        StaleSequence,
        InvalidFrame,
        UnsupportedFrame
    }

    /// <summary>
    /// Thread-safe state boundary between a Winlator socket reader and the JVS
    /// sampling thread. The socket thread updates device-owned state; consumers
    /// copy into caller-owned spans or publish digital controls on their thread.
    /// </summary>
    public sealed class WinlatorForwardedInputSource
    {
        public const int MaximumPlayers = ForwardedInputProtocol.MaximumPlayers;
        public const int MaximumAxes = ForwardedInputProtocol.MaximumAxes;

        private sealed class DeviceState
        {
            public readonly uint[] Buttons = new uint[MaximumPlayers];
            public readonly short[] Axes = new short[MaximumPlayers * MaximumAxes];
            public readonly ushort[] Flats = new ushort[MaximumPlayers * MaximumAxes];
            public readonly ForwardedPointerState[] Pointers = new ForwardedPointerState[MaximumPlayers];
            public readonly bool[] HasPointers = new bool[MaximumPlayers];
            public uint LastSequence;
            public bool HasSequence;

            public void Clear()
            {
                Array.Clear(Buttons, 0, Buttons.Length);
                Array.Clear(Axes, 0, Axes.Length);
                Array.Clear(Flats, 0, Flats.Length);
                Array.Clear(Pointers, 0, Pointers.Length);
                Array.Clear(HasPointers, 0, HasPointers.Length);
            }
        }

        private readonly object _sync = new object();
        private readonly Dictionary<uint, DeviceState> _devices = new Dictionary<uint, DeviceState>();
        private readonly bool _latchTestSwitch;
        private readonly bool _reverseYAxis;
        private bool _testSwitchLatched;
        private int _wmmtGear = 1;
        private bool _wmmtShiftDownWasPressed;
        private bool _wmmtShiftUpWasPressed;
        private int _srcGear = 1;
        private bool _srcShiftDownWasPressed;
        private bool _srcShiftUpWasPressed;
        // Battle Gear's JVS key sensor is active-low. Desktop XInput starts
        // Right=true so the entry key is absent during the cabinet boot check.
        private bool _battleGearKeySensorOff = true;
        private bool _battleGearKeyWasPressed;

        public WinlatorForwardedInputSource(
            bool latchTestSwitch = false,
            bool reverseYAxis = false)
        {
            _latchTestSwitch = latchTestSwitch;
            _reverseYAxis = reverseYAxis;
        }

        public ForwardedInputApplyResult ApplyFrame(ReadOnlySpan<byte> packet)
        {
            if (!ForwardedInputProtocol.TryReadHeader(packet, out var header))
                return ForwardedInputApplyResult.InvalidFrame;
            if (!IsSupported(header.Type))
                return ForwardedInputApplyResult.UnsupportedFrame;
            if (!IsValidPayload(packet, header.Type))
                return ForwardedInputApplyResult.InvalidFrame;

            lock (_sync)
            {
                if (!_devices.TryGetValue(header.DeviceStableId, out var device))
                {
                    device = new DeviceState();
                    _devices.Add(header.DeviceStableId, device);
                }

                var sequenceResult = CheckSequence(device, header.Sequence);
                if (sequenceResult == ForwardedInputApplyResult.StaleSequence)
                    return sequenceResult;
                if (sequenceResult == ForwardedInputApplyResult.SequenceGap)
                    device.Clear();

                ApplyValidatedFrame(packet, header, device);

                device.LastSequence = header.Sequence;
                device.HasSequence = true;
                return sequenceResult;
            }
        }

        public bool TryCopyDeviceState(
            uint deviceStableId,
            Span<uint> buttonMasks,
            Span<short> axes,
            Span<ushort> flats,
            Span<ForwardedPointerState> pointers)
        {
            if (buttonMasks.Length < MaximumPlayers ||
                axes.Length < MaximumPlayers * MaximumAxes ||
                flats.Length < MaximumPlayers * MaximumAxes ||
                pointers.Length < MaximumPlayers)
                throw new ArgumentException("A forwarded-input snapshot buffer is too small.");

            lock (_sync)
            {
                if (!_devices.TryGetValue(deviceStableId, out var device))
                    return false;
                device.Buttons.AsSpan().CopyTo(buttonMasks);
                device.Axes.AsSpan().CopyTo(axes);
                device.Flats.AsSpan().CopyTo(flats);
                device.Pointers.AsSpan().CopyTo(pointers);
                return true;
            }
        }

        /// <summary>
        /// Copies a deterministic all-device snapshot for the arcade bridge.
        /// Digital controls are ORed. For each analog axis, the device furthest
        /// from center wins so an idle controller cannot mask an active one.
        /// </summary>
        public void CopyAggregateState(Span<uint> buttonMasks, Span<short> axes)
        {
            Span<ForwardedPointerState> pointers = stackalloc ForwardedPointerState[MaximumPlayers];
            CopyAggregateState(buttonMasks, axes, pointers);
        }

        public void CopyAggregateState(
            Span<uint> buttonMasks,
            Span<short> axes,
            Span<ForwardedPointerState> pointers)
        {
            if (buttonMasks.Length < MaximumPlayers ||
                axes.Length < MaximumPlayers * MaximumAxes ||
                pointers.Length < MaximumPlayers)
                throw new ArgumentException("A forwarded-input aggregate buffer is too small.");

            buttonMasks.Clear();
            axes.Clear();
            pointers.Clear();
            lock (_sync)
            {
                foreach (var device in _devices.Values)
                {
                    for (var player = 0; player < MaximumPlayers; player++)
                        buttonMasks[player] |= device.Buttons[player];
                    for (var index = 0; index < MaximumPlayers * MaximumAxes; index++)
                    {
                        if (Math.Abs((int)device.Axes[index]) > Math.Abs((int)axes[index]))
                            axes[index] = device.Axes[index];
                    }
                    for (var player = 0; player < MaximumPlayers; player++)
                    {
                        if (!device.HasPointers[player])
                            continue;
                        var candidate = device.Pointers[player];
                        var current = pointers[player];
                        if (candidate.Buttons != 0 || candidate.Pressure > current.Pressure ||
                            (current.X == 0 && current.Y == 0 &&
                             (candidate.X != 0 || candidate.Y != 0)))
                            pointers[player] = candidate;
                    }
                }
                ApplyLatchedTestSwitch(buttonMasks);
            }
        }

        public void PublishDigitalButtonsToInputCode()
        {
            PublishControlsToInputCode(digitalizeStickDirections: false);
        }

        /// <summary>
        /// Publishes the forwarded controls using arcade-stick direction
        /// digitalization and applies the same coin edge accounting as the
        /// desktop JVS input listeners.
        /// </summary>
        public void PublishControlsToJvsInputCode()
        {
            PublishControlsToInputCode(digitalizeStickDirections: true);
            for (var player = 0; player < MaximumPlayers; player++)
                JvsPackageEmulator.UpdateCoinCount(player);
        }

        /// <summary>
        /// Publishes controls for the small number of JVS driving boards whose
        /// XML profiles route cabinet actions through extension switches and
        /// non-default analog channels.
        /// </summary>
        public void PublishControlsToJvsInputCode(string inputProtocol)
        {
            PublishControlsToJvsInputCode();
            if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvs)
                return;

            var target = InputCode.PlayerDigitalButtons[0];
            var start = target.Start == true;
            var service = target.Service == true;
            var coin = target.Coin == true;
            var button1 = target.Button1 == true;
            var button2 = target.Button2 == true;
            var button3 = target.Button3 == true;
            var button4 = target.Button4 == true;
            var button5 = target.Button5 == true;
            var button6 = target.Button6 == true;

            switch (inputProtocol)
            {
                case AndroidLaunchRecipe.InputProtocolSharedEadp:
                    // EADP has no ordinary Start input: its cabinet uses
                    // extension switch 4 for Select and switch 3 for Enter.
                    // Keep the familiar bottom Start position as Enter and
                    // expose Select as B on the single-player overlay.
                    target.Start = false;
                    target.ExtensionButton4 = button2;
                    target.ExtensionButton3 = start;
                    target.Button2 = false;
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsBattleGear:
                    target.Up = button1;       // view change
                    target.Down = button2;     // hazard
                    target.Left = button3;     // overtake
                    PublishBattleGearKey(target);
                    target.Button1 = button4;  // side brake
                    target.Button2 = button5;  // shift up
                    target.Button3 = button6;  // shift down
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolSharedTaitoGun:
                case AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2:
                    // Haunted Museum, Haunted Museum II and Gaia Attack 4 do
                    // not expose their labelled cabinet Start/Service/Coin
                    // controls on the ordinary JVS bits. Preserve the XML
                    // assignments used by the desktop input listeners:
                    // P1 Start=P1 Up, P2 Start=P1 Down,
                    // Service=extension switch 4 and Coin=extension switch 1.
                    // Keep the directional overlay controls usable as aliases.
                    var playerTwo = InputCode.PlayerDigitalButtons[1];
                    target.Start = false;
                    target.Up = target.Up == true || start;
                    target.Down = target.Down == true || playerTwo.Start == true;
                    playerTwo.Start = false;
                    target.Service = false;
                    target.Coin = false;
                    target.ExtensionButton4 = service;
                    target.ExtensionButton1 = coin;
                    if (inputProtocol ==
                        AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2)
                    {
                        // Haunted Museum II exposes its labelled P1 Action as
                        // desktop mapping ExtensionOne18. The shared gun page
                        // independently consumes Button1 as the P1 trigger, so
                        // route the dedicated X overlay button only to Action.
                        target.ExtensionButton1_8 = button3;
                        target.Button3 = false;
                    }
                    break;
                case AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic:
                    // Music Gun Gun 2 uses ordinary P1 Start as Decision,
                    // unlike Haunted Museum/Gaia where Start is P1 Up. Route
                    // only the extension switches that its XML assigns to the
                    // cabinet and keep the labelled directions intact.
                    target.Service = false;
                    target.Coin = false;
                    target.ExtensionButton4 = service;
                    target.ExtensionButton1 = coin;
                    target.ExtensionButton3 = button2; // Select
                    target.ExtensionButton2 = button4; // Enter
                    target.Button2 = false;
                    target.Button4 = false;
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsChaseHq2:
                    target.Start = false;
                    target.ExtensionButton4 = start;
                    target.Up = button1;        // shift low
                    target.Down = button2;      // shift/nitro
                    target.ExtensionButton3 = button3; // pursuit/nitro
                    target.Button1 = false;
                    target.Button2 = false;
                    target.Button3 = false;
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit:
                    target.Up = button1;        // nitro
                    target.Down = button2;      // view change
                    target.Left = button3;      // side brake
                    target.Right = button4;     // shift up
                    target.Button1 = button5;   // shift down
                    target.Button2 = false;
                    target.Button3 = false;
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsWackyRaces:
                    target.Start = false;
                    target.ExtensionButton3 = start;
                    target.ExtensionButton4 = button1; // view change
                    target.Down = button2;             // lever
                    target.Button1 = false;
                    target.Button2 = false;
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsWmmt:
                    PublishWmmtControls(
                        target, button1, button2, button3, button4, button5);
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsMachStorm:
                    target.Button1 = button1;              // menu enter
                    target.ExtensionButton1_2 = button2;  // machine gun / weapon trigger
                    target.ExtensionButton1_1 = button3;  // missile / weapon button
                    target.Button2 = false;
                    target.Button3 = button4;              // Star Wars view change
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    PublishMachStormAnalogs();
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsMkdx:
                    // MKDX's profile uses ordinary JVS for the cabinet and a
                    // separate shared-page bit for the Banapass reader. Keep
                    // the overlay labels intuitive, then translate them to
                    // the profile's non-sequential switch assignments.
                    target.Button1 = button2;              // enter switch
                    target.Button2 = button4;              // Banapass entry
                    target.Button3 = false;
                    target.Button4 = false;
                    target.Button5 = button1;              // item
                    target.Button6 = false;
                    target.ExtensionButton1_2 = button3;  // Mario button
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsInitialD:
                    // Initial D exposes view/menu on player 1, while its
                    // sequential shift switches are JVS player-2 directions.
                    // Keep the Android face buttons labelled for the cabinet,
                    // then publish the exact XML assignments.
                    var shifter = InputCode.PlayerDigitalButtons[1];
                    target.Button1 = button1; // view change
                    target.Button2 = false;
                    target.Button3 = false;
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    shifter.Up = button2;     // shift up
                    shifter.Down = button3;   // shift down
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsSegaRacingClassic:
                    // SRC's four coloured view switches are P1 directions. Its
                    // four-speed shifter is a pair of persistent sensors on the
                    // P2 direction byte, exactly as DigitalHelper.ChangeSrcGear
                    // publishes for desktop input listeners.
                    target.Up = button1;
                    target.Down = button2;
                    target.Left = button3;
                    target.Right = button4;
                    target.Button1 = false;
                    target.Button2 = false;
                    target.Button3 = false;
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    PublishSrcGear(button5, button6);
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsSegaSonic:
                    // Ko Drive has a single Start/Item cabinet switch rather
                    // than a distinct JVS Start input. Accept both the labelled
                    // action button and a physical controller's Start button.
                    target.Start = false;
                    target.Button1 = start || button1;
                    target.Button2 = false;
                    target.Button3 = false;
                    target.Button4 = false;
                    target.Button5 = false;
                    target.Button6 = false;
                    PublishDrivingAnalogs(inputProtocol);
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsSegaDreamRaiders:
                case AndroidLaunchRecipe.InputProtocolJvsSegaGoldenGun:
                case AndroidLaunchRecipe.InputProtocolJvsSegaLetsGoIsland:
                    PublishSegaGunControls(inputProtocol, target, button1, button2, button3);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inputProtocol));
            }
        }

        private void PublishBattleGearKey(PlayerButtons target)
        {
            Span<uint> buttons = stackalloc uint[MaximumPlayers];
            Span<short> axes = stackalloc short[MaximumPlayers * MaximumAxes];
            CopyAggregateState(buttons, axes);
            var keyPressed = IsPressed(buttons[0], ForwardedInputButton.Right);
            if (keyPressed && !_battleGearKeyWasPressed)
                _battleGearKeySensorOff = !_battleGearKeySensorOff;
            _battleGearKeyWasPressed = keyPressed;
            target.Right = _battleGearKeySensorOff;
        }

        private void PublishSegaGunControls(
            string inputProtocol,
            PlayerButtons target,
            bool button1,
            bool button2,
            bool button3)
        {
            Span<uint> buttons = stackalloc uint[MaximumPlayers];
            Span<short> axes = stackalloc short[MaximumPlayers * MaximumAxes];
            CopyAggregateState(buttons, axes);

            var factorX = ((long)axes[0] - short.MinValue) / (float)ushort.MaxValue;
            var factorY = ((long)axes[1] - short.MinValue) / (float)ushort.MaxValue;
            var pointerTrigger = false;
            if (TryGetAggregatePointer(0, out var pointer))
            {
                factorX = pointer.X / (float)ushort.MaxValue;
                factorY = pointer.Y / (float)ushort.MaxValue;
                pointerTrigger = pointer.Buttons != 0 || pointer.Pressure != 0;
            }

            GunAnalogMath.GunConfig config;
            switch (inputProtocol)
            {
                case AndroidLaunchRecipe.InputProtocolJvsSegaDreamRaiders:
                    config = new GunAnalogMath.GunConfig(
                        63, 207, 63, 191,
                        is16Bit: false,
                        invertedMouseAxis: false,
                        luigiLayout: false,
                        gunslinger: false);
                    target.Button1 = button1 || pointerTrigger;
                    target.Button2 = false;
                    target.Button3 = false;
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsSegaGoldenGun:
                    config = new GunAnalogMath.GunConfig(
                        6, 250, 1, 254,
                        is16Bit: false,
                        invertedMouseAxis: true,
                        luigiLayout: false,
                        gunslinger: false);
                    target.Button1 = button1 || pointerTrigger;
                    target.Button2 = button2;
                    target.Button3 = false;
                    break;
                case AndroidLaunchRecipe.InputProtocolJvsSegaLetsGoIsland:
                    config = new GunAnalogMath.GunConfig(
                        27, 208, 35, 178,
                        is16Bit: false,
                        invertedMouseAxis: false,
                        luigiLayout: false,
                        gunslinger: false);
                    target.Button1 = button1 || pointerTrigger;
                    target.Button2 = button2;
                    target.Button3 = false;
                    target.ExtensionButton1_3 = button3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inputProtocol));
            }

            target.Button4 = false;
            target.Button5 = false;
            target.Button6 = false;
            GunAnalogMath.Write(InputCode.AnalogBytes, 0, factorX, factorY, config);
        }

        private bool TryGetAggregatePointer(int player, out ForwardedPointerState pointer)
        {
            pointer = default;
            var hasPointer = false;
            lock (_sync)
            {
                foreach (var device in _devices.Values)
                {
                    if (!device.HasPointers[player])
                        continue;
                    var candidate = device.Pointers[player];
                    if (!hasPointer || candidate.Buttons != 0 ||
                        candidate.Pressure > pointer.Pressure)
                    {
                        pointer = candidate;
                        hasPointer = true;
                    }
                }
            }
            return hasPointer;
        }

        private void PublishSrcGear(bool shiftDown, bool shiftUp)
        {
            if (shiftDown && !_srcShiftDownWasPressed)
                _srcGear = Math.Max(1, _srcGear - 1);
            if (shiftUp && !_srcShiftUpWasPressed)
                _srcGear = Math.Min(4, _srcGear + 1);
            _srcShiftDownWasPressed = shiftDown;
            _srcShiftUpWasPressed = shiftUp;

            var shifter = InputCode.PlayerDigitalButtons[1];
            shifter.Right = false;
            switch (_srcGear)
            {
                case 2:
                    shifter.Up = false;
                    shifter.Left = true;
                    shifter.Down = true;
                    break;
                case 3:
                    shifter.Up = true;
                    shifter.Left = false;
                    shifter.Down = false;
                    break;
                case 4:
                    shifter.Up = false;
                    shifter.Left = false;
                    shifter.Down = true;
                    break;
                default:
                    shifter.Up = true;
                    shifter.Left = true;
                    shifter.Down = false;
                    break;
            }
        }

        private void PublishWmmtControls(
            PlayerButtons target,
            bool shiftDown,
            bool shiftUp,
            bool perspective,
            bool interruption,
            bool menuEnter)
        {
            if (shiftDown && !_wmmtShiftDownWasPressed)
                _wmmtGear = Math.Max(1, _wmmtGear - 1);
            if (shiftUp && !_wmmtShiftUpWasPressed)
                _wmmtGear = Math.Min(6, _wmmtGear + 1);
            _wmmtShiftDownWasPressed = shiftDown;
            _wmmtShiftUpWasPressed = shiftUp;

            // WMMT uses P1 button 1 for the test-menu Enter switch. Its
            // six-speed shifter is encoded by the cabinet's four persistent
            // sensors on buttons 3-6; mirror DigitalHelper.ChangeWmmt5Gear so
            // Android and desktop publish the exact same JVS switch pattern.
            target.Button1 = menuEnter;
            target.Button2 = false;
            target.Button3 = (_wmmtGear & 1) != 0;
            target.Button4 = (_wmmtGear & 1) == 0;
            target.Button5 = _wmmtGear <= 2;
            target.Button6 = _wmmtGear >= 5;
            target.ExtensionButton2 = perspective;
            target.ExtensionButton1 = interruption;
        }

        private void PublishDrivingAnalogs(string inputProtocol)
        {
            Span<uint> buttons = stackalloc uint[MaximumPlayers];
            Span<short> axes = stackalloc short[MaximumPlayers * MaximumAxes];
            CopyAggregateState(buttons, axes);
            var wheel = AxisToByte(axes[0]);
            if (IsPressed(buttons[0], ForwardedInputButton.Left)) wheel = 0;
            if (IsPressed(buttons[0], ForwardedInputButton.Right)) wheel = byte.MaxValue;
            var gas = TriggerToByte(axes[5]);
            var brake = TriggerToByte(axes[4]);

            if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsWmmt ||
                inputProtocol == AndroidLaunchRecipe.InputProtocolJvsMkdx ||
                inputProtocol == AndroidLaunchRecipe.InputProtocolJvsInitialD ||
                inputProtocol == AndroidLaunchRecipe.InputProtocolJvsSegaRacingClassic ||
                inputProtocol == AndroidLaunchRecipe.InputProtocolJvsSegaSonic)
            {
                // WMMT requests the first three JVS analog channels in the
                // conventional wheel/gas/brake order. The desktop listeners
                // publish the same values at byte offsets 0, 2, and 4.
                if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsInitialD)
                    wheel = (byte)(0x1F + wheel * (0xE1 - 0x1F) / byte.MaxValue);
                else if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsSegaSonic)
                    wheel = (byte)(0x1D + wheel * (0xED - 0x1D) / byte.MaxValue);
                InputCode.AnalogBytes[0] = wheel;
                InputCode.AnalogBytes[2] = gas;
                InputCode.AnalogBytes[4] = brake;
                JvsHelper.WriteStateByte(4, wheel);
            }
            else if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsBattleGear ||
                     inputProtocol == AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit)
            {
                InputCode.AnalogBytes[20] = wheel;
                JvsHelper.WriteStateByte(4, wheel);
                if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit)
                {
                    InputCode.AnalogBytes[2] = gas;
                    InputCode.AnalogBytes[4] = brake;
                }
                else
                {
                    InputCode.AnalogBytes[6] = gas;
                    InputCode.AnalogBytes[8] = brake;
                }
            }
            else
            {
                InputCode.AnalogBytes[4] = wheel;
                InputCode.AnalogBytes[6] = gas;
                InputCode.AnalogBytes[8] = brake;
            }
        }

        private void PublishMachStormAnalogs()
        {
            Span<uint> buttons = stackalloc uint[MaximumPlayers];
            Span<short> axes = stackalloc short[MaximumPlayers * MaximumAxes];
            CopyAggregateState(buttons, axes);
            var accelerator = Math.Clamp((int)axes[5], 0, short.MaxValue);
            var brake = Math.Clamp((int)axes[4], 0, short.MaxValue);
            var throttle = 128 +
                (accelerator * 127 / short.MaxValue) -
                (brake * 128 / short.MaxValue);
            InputCode.AnalogBytes[2] = (byte)Math.Clamp(throttle, 0, byte.MaxValue);
            InputCode.AnalogBytes[4] = AxisToByte(axes[0]);
            var aimY = AxisToByte(axes[1]);
            InputCode.AnalogBytes[6] = _reverseYAxis
                ? (byte)(byte.MaxValue - aimY)
                : aimY;
        }

        private static byte AxisToByte(short value) =>
            (byte)(((long)value - short.MinValue) * byte.MaxValue / ushort.MaxValue);

        private static byte TriggerToByte(short value) =>
            (byte)(Math.Clamp((int)value, 0, short.MaxValue) * byte.MaxValue / short.MaxValue);

        /// <summary>
        /// Publishes the aggregate TPI1 state into the existing JVS input
        /// model. Android gamepads commonly expose the primary stick and hat
        /// as axes rather than D-pad key events, so arcade-stick profiles can
        /// opt into a dead-zone conversion without changing desktop listeners.
        /// </summary>
        public void PublishControlsToInputCode(bool digitalizeStickDirections)
        {
            Span<uint> aggregate = stackalloc uint[MaximumPlayers];
            Span<short> axes = stackalloc short[MaximumPlayers * MaximumAxes];
            lock (_sync)
            {
                foreach (var device in _devices.Values)
                {
                    for (var player = 0; player < MaximumPlayers; player++)
                        aggregate[player] |= device.Buttons[player];
                    for (var index = 0; index < axes.Length; index++)
                    {
                        if (Math.Abs((int)device.Axes[index]) > Math.Abs((int)axes[index]))
                            axes[index] = device.Axes[index];
                    }
                }
                ApplyLatchedTestSwitch(aggregate);
            }

            for (var player = 0; player < MaximumPlayers; player++)
            {
                var target = InputCode.PlayerDigitalButtons[player];
                var state = aggregate[player];
                var axisOffset = player * MaximumAxes;
                target.Up = IsPressed(state, ForwardedInputButton.Up) ||
                            (digitalizeStickDirections &&
                             IsNegativeDirection(axes[axisOffset + 1], axes[axisOffset + 7]));
                target.Down = IsPressed(state, ForwardedInputButton.Down) ||
                              (digitalizeStickDirections &&
                               IsPositiveDirection(axes[axisOffset + 1], axes[axisOffset + 7]));
                target.Left = IsPressed(state, ForwardedInputButton.Left) ||
                              (digitalizeStickDirections &&
                               IsNegativeDirection(axes[axisOffset], axes[axisOffset + 6]));
                target.Right = IsPressed(state, ForwardedInputButton.Right) ||
                               (digitalizeStickDirections &&
                                IsPositiveDirection(axes[axisOffset], axes[axisOffset + 6]));
                target.Start = IsPressed(state, ForwardedInputButton.Start);
                target.Service = IsPressed(state, ForwardedInputButton.Service);
                target.Test = IsPressed(state, ForwardedInputButton.Test);
                target.Coin = IsPressed(state, ForwardedInputButton.Coin);
                target.Button1 = IsPressed(state, ForwardedInputButton.Button1);
                target.Button2 = IsPressed(state, ForwardedInputButton.Button2);
                target.Button3 = IsPressed(state, ForwardedInputButton.Button3);
                target.Button4 = IsPressed(state, ForwardedInputButton.Button4);
                target.Button5 = IsPressed(state, ForwardedInputButton.Button5);
                target.Button6 = IsPressed(state, ForwardedInputButton.Button6);
                target.ExtensionButton1 = IsPressed(state, ForwardedInputButton.Button7);
                target.ExtensionButton2 = IsPressed(state, ForwardedInputButton.Button8);
            }
        }

        public void ReleaseAll()
        {
            lock (_sync)
            {
                foreach (var device in _devices.Values)
                    device.Clear();
            }
        }

        private static ForwardedInputApplyResult CheckSequence(DeviceState device, uint sequence)
        {
            if (!device.HasSequence)
                return ForwardedInputApplyResult.Applied;
            if (!ForwardedInputProtocol.IsNewerSequence(sequence, device.LastSequence))
                return ForwardedInputApplyResult.StaleSequence;
            return sequence == unchecked(device.LastSequence + 1)
                ? ForwardedInputApplyResult.Applied
                : ForwardedInputApplyResult.SequenceGap;
        }

        private void ApplyValidatedFrame(
            ReadOnlySpan<byte> packet,
            ForwardedInputFrameHeader header,
            DeviceState device)
        {
            switch (header.Type)
            {
                case ForwardedInputFrameType.Button:
                    if (!ForwardedInputProtocol.TryReadButton(
                            packet, out _, out var buttonPlayer, out var button, out var pressed))
                        throw new InvalidOperationException("Validated TPI1 button payload changed.");
                    var mask = 1u << (int)button;
                    if (_latchTestSwitch &&
                        buttonPlayer == 0 &&
                        button == ForwardedInputButton.Test &&
                        pressed &&
                        (device.Buttons[buttonPlayer] & mask) == 0)
                        _testSwitchLatched = !_testSwitchLatched;
                    if (pressed)
                        device.Buttons[buttonPlayer] |= mask;
                    else
                        device.Buttons[buttonPlayer] &= ~mask;
                    return;

                case ForwardedInputFrameType.Axis:
                    if (!ForwardedInputProtocol.TryReadAxis(
                            packet, out _, out var axisPlayer, out var axisId,
                            out var valueQ15, out var flatQ15))
                        throw new InvalidOperationException("Validated TPI1 axis payload changed.");
                    var axisIndex = axisPlayer * MaximumAxes + axisId;
                    device.Axes[axisIndex] = valueQ15;
                    device.Flats[axisIndex] = flatQ15;
                    return;

                case ForwardedInputFrameType.PointerAbsolute:
                    if (!ForwardedInputProtocol.TryReadPointerAbsolute(
                            packet, out _, out var pointerPlayer, out var pointer))
                        throw new InvalidOperationException("Validated TPI1 pointer payload changed.");
                    device.Pointers[pointerPlayer] = pointer;
                    device.HasPointers[pointerPlayer] = true;
                    return;

                case ForwardedInputFrameType.Focus:
                    if (!ForwardedInputProtocol.TryReadFocus(packet, out _, out var focused))
                        throw new InvalidOperationException("Validated TPI1 focus payload changed.");
                    if (!focused)
                        ReleaseAllLocked();
                    return;

                case ForwardedInputFrameType.Suspend:
                    ReleaseAllLocked();
                    return;

                case ForwardedInputFrameType.DeviceRemoved:
                    _devices.Remove(header.DeviceStableId);
                    return;

                default:
                    throw new InvalidOperationException("Unsupported TPI1 frame reached the state boundary.");
            }
        }

        private static bool IsSupported(ForwardedInputFrameType type) =>
            type == ForwardedInputFrameType.Button ||
            type == ForwardedInputFrameType.Axis ||
            type == ForwardedInputFrameType.PointerAbsolute ||
            type == ForwardedInputFrameType.Focus ||
            type == ForwardedInputFrameType.Suspend ||
            type == ForwardedInputFrameType.DeviceRemoved;

        private static bool IsValidPayload(ReadOnlySpan<byte> packet, ForwardedInputFrameType type)
        {
            switch (type)
            {
                case ForwardedInputFrameType.Button:
                    return ForwardedInputProtocol.TryReadButton(
                        packet, out _, out _, out _, out _);
                case ForwardedInputFrameType.Axis:
                    return ForwardedInputProtocol.TryReadAxis(
                        packet, out _, out _, out _, out _, out _);
                case ForwardedInputFrameType.PointerAbsolute:
                    return ForwardedInputProtocol.TryReadPointerAbsolute(
                        packet, out _, out _, out _);
                case ForwardedInputFrameType.Focus:
                    return ForwardedInputProtocol.TryReadFocus(packet, out _, out _);
                case ForwardedInputFrameType.Suspend:
                case ForwardedInputFrameType.DeviceRemoved:
                    return packet.Length == ForwardedInputProtocol.HeaderBytes;
                default:
                    return false;
            }
        }

        private void ReleaseAllLocked()
        {
            foreach (var state in _devices.Values)
                state.Clear();
        }

        private void ApplyLatchedTestSwitch(Span<uint> buttonMasks)
        {
            if (!_latchTestSwitch)
                return;
            var mask = 1u << (int)ForwardedInputButton.Test;
            buttonMasks[0] = _testSwitchLatched
                ? buttonMasks[0] | mask
                : buttonMasks[0] & ~mask;
        }

        private static bool IsPressed(uint state, ForwardedInputButton button) =>
            (state & (1u << (int)button)) != 0;

        private const int DigitalAxisThreshold = 12_000;

        private static bool IsNegativeDirection(short primary, short hat) =>
            primary <= -DigitalAxisThreshold || hat <= -DigitalAxisThreshold;

        private static bool IsPositiveDirection(short primary, short hat) =>
            primary >= DigitalAxisThreshold || hat >= DigitalAxisThreshold;
    }
}
