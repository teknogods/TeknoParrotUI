using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Keyboard/Button-as-axis engine ("Use Keyboard/Button For Axis"), ported
    /// from the deleted DirectInput listener so keyboard wheel/gas/brake work
    /// through the RawInput listener. Pure state machine: feed it button
    /// presses via <see cref="HandleButton"/>, tick it via <see cref="Tick"/>
    /// (the RawInput listener runs it on a 16 ms timer) and it ramps the
    /// analog bytes in <see cref="InputCode.AnalogBytes"/> exactly like the
    /// classic implementation (per-game byte layout and clamp values).
    /// Public + deterministic so the pipeline test can drive it directly.
    /// </summary>
    public class KeyboardAxisEngine
    {
        // Byte positions per game (-1 = not applicable)
        private int _wheelByte = -1, _p2WheelByte = -1;
        private int _gasByte = -1, _p2GasByte = -1;
        private int _brakeByte = -1, _p2BrakeByte = -1;
        private int _clutchByte = -1, _handbrakeByte = -1;
        private int _handlebarByte = -1;
        private readonly bool[] _s21Negative = new bool[16];
        private readonly bool[] _s21Positive = new bool[16];
        private readonly bool[] _s21KeyboardAxis = new bool[16];
        private readonly byte[] _s21Rest = new byte[16];
        private int _s21Step = 10;
        private bool _s21Profile;
        private bool _teknoAxisProfile;
        private readonly bool[] _analogJoystickAxis = new bool[16];
        private readonly bool[] _analogJoystickNegative = new bool[16];
        private readonly bool[] _analogJoystickPositive = new bool[16];
        private readonly bool[] _analogJoystickActive = new bool[16];
        private int _xySensitivity = 10;

        // Clamps (per game)
        private int _minWheel, _maxWheel, _cnt, _minGasBrake, _maxGasBrake;

        private int _wheelSensitivity = 15;
        private int _gasBrakeSensitivity = 15;

        // Current pressed state (RawInput gives explicit press/release)
        private bool _wheelLeft, _wheelRight, _p2WheelLeft, _p2WheelRight;
        private bool _gasDown, _p2GasDown, _brakeDown, _p2BrakeDown;
        private bool _clutchDown, _handbrakeDown;
        private bool _handlebarLeft, _handlebarRight;

        // Which axes are keyboard-driven (activated on first key event)
        private bool _wheelActive, _p2WheelActive, _gasActive, _p2GasActive, _brakeActive, _p2BrakeActive;
        private bool _clutchActive, _handbrakeActive, _handlebarActive;

        // Ramp state
        private int _wheelValue, _p2WheelValue, _gasValue, _p2GasValue, _brakeValue, _p2BrakeValue;
        private int _clutchValue, _handbrakeValue, _handlebarValue;

        public bool Enabled { get; private set; }

        public void Initialize(GameProfile profile)
        {
            Enabled = profile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");
            _wheelByte = _p2WheelByte = _gasByte = _p2GasByte = _brakeByte = _p2BrakeByte = -1;
            _clutchByte = _handbrakeByte = _handlebarByte = -1;
            _wheelLeft = _wheelRight = _p2WheelLeft = _p2WheelRight = false;
            _gasDown = _p2GasDown = _brakeDown = _p2BrakeDown = _clutchDown = _handbrakeDown = false;
            _wheelActive = _p2WheelActive = _gasActive = _p2GasActive = _brakeActive = _p2BrakeActive = false;
            _clutchActive = _handbrakeActive = _handlebarActive = false;
            _s21Profile = profile.EmulationProfile == EmulationProfile.TeknoS21;
            _teknoAxisProfile = IsTeknoAxisProfile(profile);
            Array.Clear(_s21Negative);
            Array.Clear(_s21Positive);
            Array.Clear(_s21KeyboardAxis);
            Array.Clear(_analogJoystickAxis);
            Array.Clear(_analogJoystickNegative);
            Array.Clear(_analogJoystickPositive);
            Array.Clear(_analogJoystickActive);
            if (!Enabled)
                return;

            var wheelSens = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Keyboard/Button Axis X/Y Sensitivity");
            if (wheelSens != null && int.TryParse(wheelSens.FieldValue, out var ws))
                _wheelSensitivity = ws;
            var gasSens = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Keyboard/Button Axis Throttle Sensitivity");
            if (gasSens != null && int.TryParse(gasSens.FieldValue, out var gs))
                _gasBrakeSensitivity = gs;

            if (_teknoAxisProfile)
            {
                _xySensitivity = SettingInt(profile, "Keyboard/Button Axis X/Y Sensitivity", 10);
                _wheelSensitivity = SettingInt(profile, "Keyboard/Button Axis Wheel Sensitivity", 10);
                _gasBrakeSensitivity = SettingInt(profile, "Keyboard/Button Axis Pedal Sensitivity",
                    SettingInt(profile, "Keyboard/Button Axis Throttle Sensitivity", 10));
            }

            // Clamp values (classic table)
            switch (profile.EmulationProfile)
            {
                case EmulationProfile.SegaInitialD:
                case EmulationProfile.SegaInitialDLindbergh:
                    _minWheel = 0x1F; _maxWheel = 0xE1; _cnt = 0x80; _minGasBrake = 0x00; _maxGasBrake = 0xFF;
                    break;
                case EmulationProfile.IDZ:
                    _minWheel = 0x36; _maxWheel = 0xCA; _cnt = 0x80; _minGasBrake = 0x00; _maxGasBrake = 0xFF;
                    break;
                case EmulationProfile.SegaSonicAllStarsRacing:
                    _minWheel = 0x1D; _maxWheel = 0xED; _cnt = 0x80; _minGasBrake = 0x00; _maxGasBrake = 0xFF;
                    break;
                case EmulationProfile.HummerExtreme:
                    _minWheel = 0x1D; _maxWheel = 0xE0; _cnt = 0x80; _minGasBrake = 0x20; _maxGasBrake = 0xD0;
                    break;
                case EmulationProfile.HotWheels:
                    _minWheel = 0x00; _maxWheel = 0xFE; _cnt = 0x7F; _minGasBrake = 0x05; _maxGasBrake = 0xE1;
                    break;
                default:
                    _minWheel = 0x00; _maxWheel = 0xFF; _cnt = 0x80; _minGasBrake = 0x00; _maxGasBrake = 0xFF;
                    break;
            }

            // Byte layout per game (classic table, P1/P2)
            switch (profile.EmulationProfile)
            {
                case EmulationProfile.TeknoViper:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
                case EmulationProfile.TeknoVegas:
                    switch (profile.ProfileName)
                    {
                        case "roadburn":
                            _wheelByte = 2; _gasByte = 0;
                            break;
                        case "sf2049":
                        case "sf2049se":
                        case "sf2049te":
                            _wheelByte = 14; _gasByte = 4; _brakeByte = 12;
                            break;
                        case "sfrush":
                        case "sfrushrk":
                            _wheelByte = 14; _gasByte = 8; _brakeByte = 10;
                            break;
                        case "vaportrx":
                            _wheelByte = 4;
                            break;
                        default:
                            _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                            break;
                    }
                    break;
                case EmulationProfile.TokyoCop:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
                case EmulationProfile.RingRiders:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4; _handlebarByte = 6;
                    break;
                case EmulationProfile.Harley:
                    _wheelByte = 2; _gasByte = 0; _brakeByte = 6;
                    break;
                case EmulationProfile.cxbxr:
                    // CxbxPipe's generic driving page is laid out as:
                    // Analog0 = gas, Analog2 = wheel, Analog6 = brake.
                    // Crazy Taxi High Roller uses these keyboard-axis rows for
                    // both menu selection and gameplay.
                    _wheelByte = 2; _gasByte = 0; _brakeByte = 6;
                    break;
                case EmulationProfile.RadikalBikers:
                    _handlebarByte = 0;
                    break;
                case EmulationProfile.TaitoTypeXBattleGear:
                case EmulationProfile.VirtuaRLimit:
                    // classic wrote via StateView byte 4; the wheel byte 20 is the shared-view slot
                    _wheelByte = 20; _gasByte = profile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear ? 6 : 2;
                    _brakeByte = profile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear ? 8 : 4;
                    break;
                case EmulationProfile.ChaseHq2:
                case EmulationProfile.WackyRaces:
                    _wheelByte = 4; _gasByte = 6; _brakeByte = 8;
                    break;
                case EmulationProfile.ALLSSWDC:
                case EmulationProfile.ALLSIDTA:
                    _wheelByte = 1; _gasByte = 3; _brakeByte = 5;
                    break;
                case EmulationProfile.MarioKartGP:
                case EmulationProfile.MarioKartGP2:
                    _wheelByte = 0; _gasByte = 4; _brakeByte = 6;
                    break;
                case EmulationProfile.FZeroAX:
                case EmulationProfile.FZeroAXMonster:
                    _wheelByte = 0; _gasByte = 4; _brakeByte = 6;
                    break;
                case EmulationProfile.HotWheels:
                    _wheelByte = 0; _gasByte = 2; _p2WheelByte = 4; _p2GasByte = 6;
                    break;
                case EmulationProfile.HummerExtreme:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
                case EmulationProfile.FrenzyExpress:
                case EmulationProfile.LGS:
                    _wheelByte = 0;
                    break;
                case EmulationProfile.Daytona3:
                case EmulationProfile.EuropaRFordRacing:
                case EmulationProfile.EuropaRSegaRally3:
                case EmulationProfile.FNFDrift:
                case EmulationProfile.GRID:
                case EmulationProfile.DeadHeat:
                case EmulationProfile.Nirin:
                case EmulationProfile.GtiClub3:
                case EmulationProfile.NamcoMkdx:
                case EmulationProfile.NamcoMkdxUsa:
                case EmulationProfile.NamcoWmmt5:
                case EmulationProfile.DeadHeatRiders:
                case EmulationProfile.Outrun2SPX:
                case EmulationProfile.RawThrillsFNF:
                case EmulationProfile.RawThrillsFNFH2O:
                case EmulationProfile.SegaInitialD:
                case EmulationProfile.SegaInitialDLindbergh:
                case EmulationProfile.SegaRTuned:
                case EmulationProfile.SegaRacingClassic:
                case EmulationProfile.SegaRtv:
                case EmulationProfile.SegaSonicAllStarsRacing:
                case EmulationProfile.SegaToolsIDZ:
                case EmulationProfile.NamcoWmmt3:
                case EmulationProfile.IDZ:
                case EmulationProfile.NamcoWmmt6RR:
                case EmulationProfile.PlayInput:
                case EmulationProfile.Outrun2SPXElf2:
                case EmulationProfile.KonamiAcioRacing:
                case EmulationProfile.pcsx2x6:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
            }

            if (_teknoAxisProfile)
                ConfigureTeknoAxes(profile);

            if (_s21Profile)
            {
                _s21Step = SettingInt(profile, "Keyboard/Button Axis Sensitivity",
                    SettingInt(profile, "Keyboard/Button Axis Wheel Sensitivity", 10));
                _s21Step = Math.Clamp(_s21Step, 1, 255);
                foreach (var binding in profile.JoystickButtons ?? new List<JoystickButtons>())
                {
                    var axis = (int)binding.InputMapping - (int)InputMapping.Analog0;
                    if (axis < 0 || axis >= _s21KeyboardAxis.Length ||
                        binding.AnalogType is AnalogType.Minimum or AnalogType.Maximum)
                        continue;
                    _s21Rest[axis] = binding.AnalogType is AnalogType.Gas or AnalogType.Brake
                        ? (byte)0 : (byte)128;
                    _s21KeyboardAxis[axis] = binding.AnalogType is not (AnalogType.Gas or AnalogType.Brake);
                    InputCode.AnalogBytes[axis] = _s21Rest[axis];
                }
            }

            // Ramp state starts from current byte values
            if (_wheelByte >= 0 && _wheelByte < InputCode.AnalogBytes.Length) _wheelValue = InputCode.AnalogBytes[_wheelByte];
            if (_p2WheelByte >= 0) _p2WheelValue = InputCode.AnalogBytes[_p2WheelByte];
            if (_gasByte >= 0) _gasValue = InputCode.AnalogBytes[_gasByte];
            if (_p2GasByte >= 0) _p2GasValue = InputCode.AnalogBytes[_p2GasByte];
            if (_brakeByte >= 0) _brakeValue = InputCode.AnalogBytes[_brakeByte];
            if (_p2BrakeByte >= 0) _p2BrakeValue = InputCode.AnalogBytes[_p2BrakeByte];
            if (_clutchByte >= 0) _clutchValue = InputCode.AnalogBytes[_clutchByte];
            if (_handbrakeByte >= 0) _handbrakeValue = InputCode.AnalogBytes[_handbrakeByte];
            if (_handlebarByte >= 0) _handlebarValue = InputCode.AnalogBytes[_handlebarByte];
        }

        private static int SettingInt(GameProfile profile, string name, int fallback) =>
            int.TryParse(profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue,
                out var value) ? value : fallback;

        private static bool IsTeknoAxisProfile(GameProfile profile) => !profile.GunGame &&
            profile.EmulationProfile is EmulationProfile.TeknoModel1 or EmulationProfile.TeknoModel2 or
                EmulationProfile.TeknoVegas or EmulationProfile.TeknoViper or EmulationProfile.TeknoS11 or
                EmulationProfile.TeknoTPJC or EmulationProfile.TeknoZeus or EmulationProfile.TeknoCobra or
                EmulationProfile.TeknoHNG64 or EmulationProfile.TeknoHornet or EmulationProfile.TeknoAGX or
                EmulationProfile.TeknoS22 or EmulationProfile.TeknoS21 or EmulationProfile.TeknoS23 or
                EmulationProfile.TeknoGClub or EmulationProfile.TeknoAir;

        private void ConfigureTeknoAxes(GameProfile profile)
        {
            _wheelByte = _p2WheelByte = _gasByte = _p2GasByte = _brakeByte = _p2BrakeByte = -1;
            _clutchByte = _handbrakeByte = _handlebarByte = -1;
            foreach (var binding in profile.JoystickButtons ?? new List<JoystickButtons>())
            {
                if (binding.HideWithoutKeyboardForAxis)
                    continue;
                var axis = (int)binding.InputMapping - (int)InputMapping.Analog0;
                if (axis < 0 || axis >= 16)
                    continue;
                switch (binding.ButtonName)
                {
                    case "Wheel Axis": _wheelByte = axis; break;
                    case "P2 Wheel Axis": _p2WheelByte = axis; break;
                    case "Clutch": _clutchByte = axis; break;
                    case "Handbrake Axis": _handbrakeByte = axis; break;
                    case "P2 Gas":
                    case "P2 Right": _p2GasByte = axis; break;
                    case "P2 Left": _p2BrakeByte = axis; break;
                    default:
                        if (binding.AnalogType == AnalogType.Gas) _gasByte = axis;
                        else if (binding.AnalogType == AnalogType.Brake) _brakeByte = axis;
                        break;
                }
                if (binding.AnalogType is AnalogType.AnalogJoystick or AnalogType.AnalogJoystickY or
                    AnalogType.AnalogJoystickReverse)
                    _analogJoystickAxis[axis] = true;
                InputCode.AnalogBytes[axis] = binding.AnalogType is AnalogType.Gas or AnalogType.Brake
                    ? (byte)0 : (byte)128;
            }
        }

        /// <summary>
        /// Feed a pressed/released event for a keyboard/button-bound axis row.
        /// Returns true when the row was consumed as a keyboard-axis control.
        /// Row identification matches classic: AnalogType + ButtonName.
        /// </summary>
        public bool HandleButton(JoystickButtons row, bool pressed)
        {
            if (!Enabled)
                return false;

            if (_s21Profile && row.AnalogType is AnalogType.Minimum or AnalogType.Maximum)
            {
                var axis = (int)row.InputMapping - (int)InputMapping.Analog0;
                if (axis >= 0 && axis < _s21KeyboardAxis.Length)
                {
                    if (row.AnalogType == AnalogType.Minimum) _s21Negative[axis] = pressed;
                    else _s21Positive[axis] = pressed;
                    return true;
                }
            }

            if (_teknoAxisProfile && !_s21Profile && row.HideWithoutKeyboardForAxis &&
                row.AnalogType is AnalogType.AnalogJoystick or AnalogType.AnalogJoystickY or
                    AnalogType.AnalogJoystickReverse)
            {
                var axis = (int)row.InputMapping - (int)InputMapping.Analog0;
                if (axis >= 0 && axis < _analogJoystickAxis.Length && _analogJoystickAxis[axis])
                {
                    var axisName = row.ButtonName ?? "";
                    var negative = axisName.EndsWith(" Left", StringComparison.Ordinal) ||
                                   axisName.EndsWith(" Up", StringComparison.Ordinal);
                    var positive = axisName.EndsWith(" Right", StringComparison.Ordinal) ||
                                   axisName.EndsWith(" Down", StringComparison.Ordinal);
                    if (row.AnalogType == AnalogType.AnalogJoystickReverse)
                        (negative, positive) = (positive, negative);
                    if (negative || positive)
                    {
                        _analogJoystickActive[axis] = true;
                        if (negative) _analogJoystickNegative[axis] = pressed;
                        if (positive) _analogJoystickPositive[axis] = pressed;
                        return true;
                    }
                }
            }

            var name = row.ButtonName ?? "";
            switch (row.AnalogType)
            {
                case AnalogType.Wheel:
                    if (name is "Wheel Axis" or "Leaning Axis" or "Handlebar Axis")
                        return false; // the real analog row, not a keyboard direction
                    if (name.EndsWith("Wheel Axis Left") || name.EndsWith("Leaning Axis Left"))
                    {
                        if (IsP2(name)) { _p2WheelActive = true; _p2WheelLeft = pressed; }
                        else { _wheelActive = true; _wheelLeft = pressed; }
                        return true;
                    }
                    if (name.EndsWith("Wheel Axis Right") || name.EndsWith("Leaning Axis Right"))
                    {
                        if (IsP2(name)) { _p2WheelActive = true; _p2WheelRight = pressed; }
                        else { _wheelActive = true; _wheelRight = pressed; }
                        return true;
                    }
                    if (name.EndsWith("Handlebar Axis Left")) { _handlebarActive = true; _handlebarLeft = pressed; return true; }
                    if (name.EndsWith("Handlebar Axis Right")) { _handlebarActive = true; _handlebarRight = pressed; return true; }
                    return false;

                case AnalogType.Gas:
                    if (name == "Clutch") { _clutchActive = true; _clutchDown = pressed; }
                    else if (name == "Handbrake Axis") { _handbrakeActive = true; _handbrakeDown = pressed; }
                    else if (IsP2(name)) { _p2GasActive = true; _p2GasDown = pressed; }
                    else { _gasActive = true; _gasDown = pressed; }
                    return true;

                case AnalogType.Brake:
                    if (IsP2(name)) { _p2BrakeActive = true; _p2BrakeDown = pressed; }
                    else { _brakeActive = true; _brakeDown = pressed; }
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsP2(string name) => name.StartsWith("P2 ") || name.StartsWith("Player 2 ");

        /// <summary>One ramp step (classic ran this on a 16 ms timer).</summary>
        public void Tick()
        {
            if (!Enabled)
                return;

            if (_s21Profile)
            {
                for (var axis = 0; axis < _s21KeyboardAxis.Length; axis++)
                {
                    if (!_s21KeyboardAxis[axis]) continue;
                    var target = _s21Negative[axis] == _s21Positive[axis]
                        ? _s21Rest[axis] : _s21Negative[axis] ? 0 : 255;
                    var value = InputCode.AnalogBytes[axis];
                    InputCode.AnalogBytes[axis] = (byte)(value < target
                        ? Math.Min(target, value + _s21Step)
                        : Math.Max(target, value - _s21Step));
                }
            }

            if (_teknoAxisProfile && !_s21Profile)
            {
                for (var axis = 0; axis < _analogJoystickAxis.Length; axis++)
                {
                    if (!_analogJoystickAxis[axis] || !_analogJoystickActive[axis]) continue;
                    var target = _analogJoystickNegative[axis] == _analogJoystickPositive[axis]
                        ? _cnt : _analogJoystickNegative[axis] ? _minWheel : _maxWheel;
                    var value = InputCode.AnalogBytes[axis];
                    InputCode.AnalogBytes[axis] = (byte)(value < target
                        ? Math.Min(target, value + _xySensitivity)
                        : Math.Max(target, value - _xySensitivity));
                }
            }

            if (_wheelByte >= 0 && _wheelActive)
                _wheelValue = RampWheel(_wheelByte, _wheelValue, _wheelLeft, _wheelRight);
            if (_p2WheelByte >= 0 && _p2WheelActive)
                _p2WheelValue = RampWheel(_p2WheelByte, _p2WheelValue, _p2WheelLeft, _p2WheelRight);
            if (_handlebarByte >= 0 && _handlebarActive)
                _handlebarValue = RampWheel(_handlebarByte, _handlebarValue, _handlebarLeft, _handlebarRight);

            if (_gasByte >= 0 && _gasActive)
                _gasValue = RampPedal(_gasByte, _gasValue, _gasDown);
            if (_p2GasByte >= 0 && _p2GasActive)
                _p2GasValue = RampPedal(_p2GasByte, _p2GasValue, _p2GasDown);
            if (_brakeByte >= 0 && _brakeActive)
                _brakeValue = RampPedal(_brakeByte, _brakeValue, _brakeDown);
            if (_p2BrakeByte >= 0 && _p2BrakeActive)
                _p2BrakeValue = RampPedal(_p2BrakeByte, _p2BrakeValue, _p2BrakeDown);
            if (_clutchByte >= 0 && _clutchActive)
                _clutchValue = RampPedal(_clutchByte, _clutchValue, _clutchDown);
            if (_handbrakeByte >= 0 && _handbrakeActive)
                _handbrakeValue = RampPedal(_handbrakeByte, _handbrakeValue, _handbrakeDown);
        }

        private int RampWheel(int byteIndex, int value, bool left, bool right)
        {
            int next;
            if (left && right)
                next = value;
            else if (right)
                next = Math.Min(_maxWheel, value + _wheelSensitivity);
            else if (left)
                next = Math.Max(_minWheel, value - _wheelSensitivity);
            else if (value < _cnt)
                next = Math.Min(_cnt, value + _wheelSensitivity);
            else if (value > _cnt)
                next = Math.Max(_cnt, value - _wheelSensitivity);
            else
                next = _cnt;

            WriteWheel(byteIndex, (byte)next);
            return next;
        }

        private int RampPedal(int byteIndex, int value, bool down)
        {
            int next = down
                ? Math.Min(_maxGasBrake, value + _gasBrakeSensitivity)
                : Math.Max(_minGasBrake, value - _gasBrakeSensitivity);
            InputCode.AnalogBytes[byteIndex] = (byte)next;
            return next;
        }

        private void WriteWheel(int byteIndex, byte value)
        {
            // TaitoTypeXBattleGear/VirtuaRLimit route the wheel through the JVS
            // shared view byte 4 (classic behaviour)
            if (byteIndex == 20)
                Jvs.JvsHelper.StateView.Write(4, value);
            else
                InputCode.AnalogBytes[byteIndex] = value;
        }
    }
}
