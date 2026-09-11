using System;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // CBIN v1 uses Viper's 64-byte layout, with an odd/even commit sequence.
    // No allocation or IPC round trip is needed in the transmission loop.
    public sealed class TeknoCobraPipe : ControlSender
    {
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _publish;
        private static bool Down(bool? value) => value == true;

        private static bool Enabled(string name, bool fallback = false)
        {
            var value = InputCode.GameProfile?.ConfigValues?
                .FirstOrDefault(x => x.FieldName == name)?.FieldValue;
            return value == null ? fallback : value == "1" ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public override void Start()
        {
            lock (_sync)
            {
                if (Running) return;
                JvsHelper.ResetState();
                _sequence = 0;
                _publish = !(Enabled("Enable VR") && Enabled("Use VR Controls", true));
                for (int i = 0; i < 10; ++i) InputCode.AnalogBytes[i] = 0;
                InputCode.AnalogBytes[0] = 128;
                JvsHelper.WriteStateByte(0, (byte)'C');
                JvsHelper.WriteStateByte(1, (byte)'B');
                JvsHelper.WriteStateByte(2, (byte)'I');
                JvsHelper.WriteStateByte(3, (byte)'N');
                JvsHelper.WriteStateByte(4, 1);
                base.Start();
            }
        }

        public override void Stop()
        {
            lock (_sync)
            {
                _publish = false;
                base.Stop();
                JvsHelper.WriteStateByte(5, 0);
            }
        }

        public override void Transmit()
        {
            lock (_sync)
            {
                if (!_publish) return;
                _sequence = unchecked((ushort)(_sequence + 1));
                JvsHelper.StateView.Write(6, _sequence); // odd: writing
                Thread.MemoryBarrier();
                var operatorInput = InputCode.PlayerDigitalButtons[0];
                JvsHelper.WriteStateByte(8, (byte)((Down(operatorInput.Test) ? 0x80 : 0) |
                    (Down(operatorInput.Service) ? 0x40 : 0)));
                for (int player = 0; player < 4; ++player)
                {
                    var input = InputCode.PlayerDigitalButtons[player];
                    byte buttons = 0, extra = 0;
                    if (input.UpPressed()) buttons |= 1;
                    if (input.DownPressed()) buttons |= 2;
                    if (input.LeftPressed()) buttons |= 4;
                    if (input.RightPressed()) buttons |= 8;
                    if (Down(input.Button2)) buttons |= 0x10;
                    if (Down(input.Button3)) buttons |= 0x20;
                    if (Down(input.Button1)) buttons |= 0x40;
                    if (Down(input.Start)) buttons |= 0x80;
                    if (Down(input.Button4)) extra |= 1;
                    if (Down(input.Button5)) extra |= 2;
                    if (Down(input.Button6)) extra |= 4;
                    if (Down(input.ExtensionButton1)) extra |= 8; // Racing view
                    if (Down(input.Service)) extra |= 0x10;
                    if (Down(input.ExtensionButton2)) extra |= 0x20; // Handbrake
                    if (Down(input.ExtensionButton3)) extra |= 0x40; // Clutch
                    JvsHelper.WriteStateByte(9 + player, buttons);
                    JvsHelper.WriteStateByte(24 + player, extra);
                    JvsHelper.WriteStateByte(32 + player, Down(input.Coin) ? (byte)1 : (byte)0);
                }
                for (int axis = 0; axis < 8; ++axis)
                    JvsHelper.WriteStateByte(13 + axis, InputCode.AnalogBytes[axis * 2]);
                Thread.MemoryBarrier();
                _sequence = unchecked((ushort)(_sequence + 1));
                JvsHelper.StateView.Write(6, _sequence); // even: committed
                JvsHelper.WriteStateByte(5, 1);
            }
        }
    }
}
