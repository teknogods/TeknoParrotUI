using System;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Publishes TeknoParrot bindings to TeknoVegas' 64-byte shared page.
    /// The TVIN header is a renewable writer lease: TeknoVegas disables its
    /// local keyboard/mouse only while this sequence advances.
    /// </summary>
    public sealed class TeknoVegasPipe : ControlSender
    {
        private ushort _sequence;

        private static bool Down(bool? value) => value.HasValue && value.Value;

        private static bool IsProfile(string name) => string.Equals(
            InputCode.GameProfile?.ProfileName, name,
            StringComparison.OrdinalIgnoreCase);

        private static bool UsesVolumeMenuBindings() =>
            IsProfile("roadburn") || IsProfile("cartfury");

        private static byte PlayerByte(int index)
        {
            var input = InputCode.PlayerDigitalButtons[index];
            byte value = 0;
            if (input.UpPressed()) value |= 0x01;
            if (input.DownPressed()) value |= 0x02;
            if (input.LeftPressed()) value |= 0x04;
            if (input.RightPressed()) value |= 0x08;
            if (Down(input.Button2)) value |= 0x10; // Midway button 2
            if (Down(input.Button3)) value |= 0x20; // Midway button 3
            if (Down(input.Button1)) value |= 0x40; // Midway button 1
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

        private static ushort KeypadWord()
        {
            var input = InputCode.PlayerDigitalButtons[0];
            bool?[] keys =
            {
                input.ExtensionButton1_1, input.ExtensionButton1_2,
                input.ExtensionButton1_3, input.ExtensionButton1_4,
                input.ExtensionButton1_5, input.ExtensionButton1_6,
                input.ExtensionButton1_7, input.ExtensionButton1_8,
                input.ExtensionButton2_1, input.ExtensionButton2_2,
                input.ExtensionButton2_3, input.ExtensionButton2_4
            };
            ushort value = 0;
            for (var bit = 0; bit < keys.Length; ++bit)
            {
                if (Down(keys[bit]))
                    value |= (ushort)(1 << bit);
            }
            return value;
        }

        public override void Start()
        {
            JvsHelper.ResetState();
            _sequence = 0;
            // War's movement buttons and its analog aiming stick are separate
            // cabinet controls. Start the two aim channels centred so an
            // unbound or not-yet-polled stick cannot pin the sight in a corner.
            if (IsProfile("warfa"))
            {
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[2] = 0x80;
            }
            else if (IsProfile("roadburn"))
            {
                // Road's accelerator, steering and bank/lean converters all
                // power up at 0x80. Publishing JVS' generic zero here made a
                // newly opened cabinet pull hard left until the first axis
                // poll, and zero is also a valid endpoint after that poll.
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[2] = 0x80;
                InputCode.AnalogBytes[4] = 0x80;
            }
            else if (IsProfile("cartfury"))
            {
                // CART's wheel is centred while its independent gas/brake
                // pedals remain at the generic zero released position.
                InputCode.AnalogBytes[0] = 0x80;
            }
            else if (IsProfile("carnevil"))
            {
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[2] = 0x80;
                InputCode.AnalogBytes[4] = 0x80;
                InputCode.AnalogBytes[6] = 0x80;
            }
            base.Start();
        }

        public override void Stop()
        {
            // Revoke ownership before the worker exits. The emulator expires
            // an unchanging sequence too, covering abnormal UI termination.
            JvsHelper.WriteStateByte(5, 0);
            base.Stop();
        }

        public override void Transmit()
        {
            byte system = 0;
            var operatorInput = InputCode.PlayerDigitalButtons[0];
            if (Down(operatorInput.Test)) system |= 0x80;
            if (Down(operatorInput.Service)) system |= 0x40;
            if (UsesVolumeMenuBindings())
            {
                // These diagnostics use the cabinet volume switches for menu
                // motion. Their P1 digital Up/Down profile entries are
                // operator bindings, not their independent analog controls.
                if (operatorInput.UpPressed()) system |= 0x10;
                if (operatorInput.DownPressed()) system |= 0x08;
            }

            JvsHelper.WriteStateByte(8, system);
            for (var player = 0; player < 4; ++player)
            {
                byte playerByte = PlayerByte(player);
                if (player == 0 && UsesVolumeMenuBindings())
                    playerByte &= 0xfc; // operator Up/Down were routed above
                JvsHelper.WriteStateByte(9 + player, playerByte);
                JvsHelper.WriteStateByte(24 + player, ExtraByte(player));
                JvsHelper.WriteStateByte(
                    32 + player,
                    Down(InputCode.PlayerDigitalButtons[player].Coin)
                        ? (byte)1
                        : (byte)0);
            }

            for (var analog = 0; analog < 8; ++analog)
                JvsHelper.WriteStateByte(13 + analog, InputCode.AnalogBytes[analog * 2]);

            var keypad = KeypadWord();
            JvsHelper.WriteStateByte(36, (byte)keypad);
            JvsHelper.WriteStateByte(37, (byte)(keypad >> 8));

            // Publish payload first, then renew the lease. A reader may see
            // one mixed frame, which is harmless for per-frame controls.
            ++_sequence;
            JvsHelper.WriteStateByte(0, (byte)'T');
            JvsHelper.WriteStateByte(1, (byte)'V');
            JvsHelper.WriteStateByte(2, (byte)'I');
            JvsHelper.WriteStateByte(3, (byte)'N');
            JvsHelper.WriteStateByte(4, 1);
            JvsHelper.WriteStateByte(6, (byte)_sequence);
            JvsHelper.WriteStateByte(7, (byte)(_sequence >> 8));
            JvsHelper.WriteStateByte(5, 1);
        }
    }
}
