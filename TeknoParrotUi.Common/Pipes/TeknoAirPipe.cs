using System;
using System.Linq;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Publishes TeknoParrot bindings to TeknoAir's 64-byte shared page.
    /// The VPIN sequence is a renewable lease, so local emulator controls are
    /// restored if the UI closes or stops publishing input.
    /// </summary>
    public sealed class TeknoAirPipe : ControlSender
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
                JvsHelper.WriteStateByte(5, 0);
                base.Start();
                return;
            }

            InputCode.AnalogBytes[0] = 0x80;
            InputCode.AnalogBytes[2] = 0x80;
            InputCode.AnalogBytes[4] = 0;
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
            lock (_sync) TransmitSnapshot();
        }

        private void TransmitSnapshot()
        {
            if (!_publishInput)
            {
                JvsHelper.WriteStateByte(5, 0);
                return;
            }

            var operatorInput = InputCode.PlayerDigitalButtons[0];
            byte system = 0;
            if (Down(operatorInput.Test)) system |= 0x80;
            if (Down(operatorInput.Service)) system |= 0x40;
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

            var xinput = InputCode.GameProfile?.ConfigValues?.Any(x =>
                x.FieldName == "Input API" && x.FieldValue == "XInput") == true;
            for (var analog = 0; analog < 8; ++analog)
            {
                var value = InputCode.AnalogBytes[analog * 2];
                // XInput thumbstick Y grows upward; the cabinet and DirectInput grow downward.
                if (analog == 1 && xinput) value = (byte)(255 - value);
                JvsHelper.WriteStateByte(13 + analog, value);
            }

            ++_sequence;
            JvsHelper.WriteStateByte(0, (byte)'V');
            JvsHelper.WriteStateByte(1, (byte)'P');
            JvsHelper.WriteStateByte(2, (byte)'I');
            JvsHelper.WriteStateByte(3, (byte)'N');
            JvsHelper.WriteStateByte(4, 1);
            JvsHelper.WriteStateByte(6, (byte)_sequence);
            JvsHelper.WriteStateByte(7, (byte)(_sequence >> 8));
            JvsHelper.WriteStateByte(5, 1);
        }
    }
}
