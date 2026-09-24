using System;
using System.Linq;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Publishes TeknoParrot bindings to TeknoZeus's 64-byte shared page.
    /// The ZPIN sequence is a renewable lease, so local emulator controls are
    /// restored if the UI closes or stops publishing input.
    /// </summary>
    public sealed class TeknoZeusPipe : ControlSender
    {
        private ushort _sequence;
        private bool _publishInput;

        private static bool Down(bool? value) => value.HasValue && value.Value;

        private static bool IsProfile(string name) => string.Equals(
            InputCode.GameProfile?.ProfileName, name,
            StringComparison.OrdinalIgnoreCase);

        private static bool SettingEnabled(string name, bool fallback = false)
        {
            var value = InputCode.GameProfile?.ConfigValues?
                .FirstOrDefault(x => x.FieldName == name)?.FieldValue;
            if (value == null)
                return fallback;
            return value == "1" || value.Equals(
                "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool GunProfile() =>
            IsProfile("invasnab");

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
            if (GunProfile() || IsProfile("thegrid") || IsProfile("mwskinst"))
            {
                InputCode.AnalogBytes[2] = 0x80;
                InputCode.AnalogBytes[4] = 0x80;
                InputCode.AnalogBytes[6] = 0x80;
            }
            base.Start();
        }

        public override void Stop()
        {
            JvsHelper.WriteStateByte(5, 0);
            base.Stop();
        }

        public override void Transmit()
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
            if (Down(operatorInput.ExtensionButton3)) system |= 0x10;
            if (Down(operatorInput.ExtensionButton4)) system |= 0x08;
            var keys = new[] { operatorInput.ExtensionButton1_1, operatorInput.ExtensionButton1_2,
                operatorInput.ExtensionButton1_3, operatorInput.ExtensionButton1_4,
                operatorInput.ExtensionButton1_5, operatorInput.ExtensionButton1_6,
                operatorInput.ExtensionButton1_7, operatorInput.ExtensionButton1_8,
                operatorInput.ExtensionButton2_1, operatorInput.ExtensionButton2_2,
                operatorInput.ExtensionButton2_3, operatorInput.ExtensionButton2_4 };
            ushort keypad = 0;
            for (int k = 0; k < keys.Length; ++k)
                if (Down(keys[k])) keypad |= (ushort)(1 << k);
            JvsHelper.WriteStateByte(36, (byte)keypad);
            JvsHelper.WriteStateByte(37, (byte)(keypad >> 8));
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
                JvsHelper.WriteStateByte(13 + analog, InputCode.AnalogBytes[analog * 2]);

            ++_sequence;
            JvsHelper.WriteStateByte(0, (byte)'Z');
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
