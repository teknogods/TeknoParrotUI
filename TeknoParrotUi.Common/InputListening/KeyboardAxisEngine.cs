using System;
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
        private int _brakeByte = -1;
        private int _handlebarByte = -1;

        // Clamps (per game)
        private int _minWheel, _maxWheel, _cnt, _minGasBrake, _maxGasBrake;

        private int _wheelSensitivity = 15;
        private int _gasBrakeSensitivity = 15;

        // Current pressed state (RawInput gives explicit press/release)
        private bool _wheelLeft, _wheelRight, _p2WheelLeft, _p2WheelRight;
        private bool _gasDown, _p2GasDown, _brakeDown;
        private bool _handlebarLeft, _handlebarRight;

        // Which axes are keyboard-driven (activated on first key event)
        private bool _wheelActive, _p2WheelActive, _gasActive, _p2GasActive, _brakeActive, _handlebarActive;

        // Ramp state
        private int _wheelValue, _p2WheelValue, _gasValue, _p2GasValue, _brakeValue, _handlebarValue;

        public bool Enabled { get; private set; }

        public void Initialize(GameProfile profile)
        {
            Enabled = profile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");
            if (!Enabled)
                return;

            var wheelSens = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Keyboard/Button Axis X/Y Sensitivity");
            if (wheelSens != null && int.TryParse(wheelSens.FieldValue, out var ws))
                _wheelSensitivity = ws;
            var gasSens = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Keyboard/Button Axis Throttle Sensitivity");
            if (gasSens != null && int.TryParse(gasSens.FieldValue, out var gs))
                _gasBrakeSensitivity = gs;

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
                case EmulationProfile.TokyoCop:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
                case EmulationProfile.RingRiders:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4; _handlebarByte = 6;
                    break;
                case EmulationProfile.Harley:
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
                case EmulationProfile.KonamiAcio:
                case EmulationProfile.pcsx2x6:
                    _wheelByte = 0; _gasByte = 2; _brakeByte = 4;
                    break;
            }

            // Ramp state starts from current byte values
            if (_wheelByte >= 0 && _wheelByte < InputCode.AnalogBytes.Length) _wheelValue = InputCode.AnalogBytes[_wheelByte];
            if (_p2WheelByte >= 0) _p2WheelValue = InputCode.AnalogBytes[_p2WheelByte];
            if (_gasByte >= 0) _gasValue = InputCode.AnalogBytes[_gasByte];
            if (_p2GasByte >= 0) _p2GasValue = InputCode.AnalogBytes[_p2GasByte];
            if (_brakeByte >= 0) _brakeValue = InputCode.AnalogBytes[_brakeByte];
            if (_handlebarByte >= 0) _handlebarValue = InputCode.AnalogBytes[_handlebarByte];
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
                    if (IsP2(name)) { _p2GasActive = true; _p2GasDown = pressed; }
                    else { _gasActive = true; _gasDown = pressed; }
                    return true;

                case AnalogType.Brake:
                    _brakeActive = true; _brakeDown = pressed;
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
