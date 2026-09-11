using System;
using System.Linq;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Publishes TPUI bindings to TeknoModel2's conventional 64-byte input
    /// page. The M2IN header is a renewable lease, allowing the emulator to
    /// return to native keyboard/mouse/XInput when TPUI stops publishing.
    /// </summary>
    public sealed class TeknoModel2Pipe : ControlSender
    {
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _publishInput;

        private static bool Down(bool? value) => value.HasValue && value.Value;

        private static bool SettingEnabled(string name, bool fallback = false)
        {
            var value = InputCode.GameProfile?.ConfigValues?
                .FirstOrDefault(x => x.FieldName == name)?.FieldValue;
            if (value == null)
                return fallback;
            return value == "1" || value.Equals(
                "true", StringComparison.OrdinalIgnoreCase);
        }

        private static byte PlayerByte(int index)
        {
            var input = InputCode.PlayerDigitalButtons[index];
            byte value = 0;
            if (input.UpPressed()) value |= 0x01;
            if (input.DownPressed()) value |= 0x02;
            if (input.LeftPressed()) value |= 0x04;
            if (input.RightPressed()) value |= 0x08;
            if (Down(input.Button2)) value |= 0x10;
            if (Down(input.Button3)) value |= 0x20;
            if (Down(input.Button1)) value |= 0x40;
            if (Down(input.Start)) value |= 0x80;
            return value;
        }

        private static byte ExtraByte(int index)
        {
            var input = InputCode.PlayerDigitalButtons[index];
            byte value = 0;
            if (Down(input.Button4)) value |= 0x01;
            if (Down(input.Button5)) value |= 0x02;
            if (Down(input.Button6)) value |= 0x04;
            if (Down(input.ExtensionButton1)) value |= 0x08;
            return value;
        }

        public override void Start()
        {
            JvsHelper.ResetState();
            _sequence = 0;
            _publishInput = !(SettingEnabled("Enable VR") &&
                              SettingEnabled("Use VR Controls", true));

            if (!_publishInput)
            {
                // OpenXR owns cabinet controls in this mode. Keeping the lease
                // revoked makes the two complete input snapshots exclusive.
                JvsHelper.WriteStateByte(5, 0);
                base.Start();
                return;
            }

            // Neutral values follow the profile's logical axes, before the
            // emulator applies cabinet channel ordering and calibration.
            for (var axis = 0; axis < 8; ++axis)
                InputCode.AnalogBytes[axis * 2] = 0x80;
            foreach (var binding in InputCode.GameProfile.JoystickButtons)
            {
                var mapping = binding.InputMapping.ToString();
                if (mapping.StartsWith("Analog", StringComparison.Ordinal) &&
                    int.TryParse(mapping.Substring(6), out var offset) &&
                    offset >= 0 && offset < 16 &&
                    (binding.AnalogType == AnalogType.Gas ||
                     binding.AnalogType == AnalogType.Brake))
                    InputCode.AnalogBytes[offset] = 0;
            }

            base.Start();
        }

        public override void Stop()
        {
            lock (_sync)
            {
                _publishInput = false;
                base.Stop();
                JvsHelper.WriteStateByte(5, 0);
            }
        }

        public override void Transmit()
        {
            lock (_sync) TransmitInput();
        }

        private void TransmitInput()
        {
            if (!_publishInput)
            {
                JvsHelper.WriteStateByte(5, 0);
                return;
            }

            byte system = 0;
            var operatorInput = InputCode.PlayerDigitalButtons[0];
            if (Down(operatorInput.Test)) system |= 0x80;
            if (Down(operatorInput.Service)) system |= 0x40;
            if (Down(InputCode.PlayerDigitalButtons[1].Service)) system |= 0x20;
            JvsHelper.WriteStateByte(8, system);

            for (var player = 0; player < 4; ++player)
            {
                JvsHelper.WriteStateByte(9 + player, PlayerByte(player));
                JvsHelper.WriteStateByte(24 + player, ExtraByte(player));
                JvsHelper.WriteStateByte(
                    32 + player,
                    Down(InputCode.PlayerDigitalButtons[player].Coin)
                        ? (byte)1
                        : (byte)0);
            }

            for (var analog = 0; analog < 8; ++analog)
                JvsHelper.WriteStateByte(
                    13 + analog, InputCode.AnalogBytes[analog * 2]);

            ++_sequence;
            JvsHelper.WriteStateByte(0, (byte)'M');
            JvsHelper.WriteStateByte(1, (byte)'2');
            JvsHelper.WriteStateByte(2, (byte)'I');
            JvsHelper.WriteStateByte(3, (byte)'N');
            JvsHelper.WriteStateByte(4, 1);
            JvsHelper.WriteStateByte(6, (byte)_sequence);
            JvsHelper.WriteStateByte(7, (byte)(_sequence >> 8));
            JvsHelper.WriteStateByte(5, 1);
        }
    }
}
