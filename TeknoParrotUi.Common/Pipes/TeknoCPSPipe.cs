using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // CPIN v1: four independent 14-contact panels, no cabinet configuration.
    public sealed class TeknoCPSPipe : ControlSender
    {
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _active;
        private static bool Down(bool? value) => value == true;

        public override void Start()
        {
            lock (_sync) { JvsHelper.ResetState(); _sequence = 0; _active = true; }
            base.Start();
        }
        public override void Stop()
        {
            base.Stop();
            lock (_sync) { _active = false; JvsHelper.WriteStateByte(5, 0); }
        }
        public override void Transmit()
        {
            lock (_sync)
            {
                if (!_active) return;
                var page = new byte[64];
                page[0] = (byte)'C'; page[1] = (byte)'P'; page[2] = (byte)'I'; page[3] = (byte)'N'; page[4] = page[5] = 1;
                for (var p = 0; p < 4; ++p)
                {
                    var input = InputCode.PlayerDigitalButtons[p];
                    var buttons = new[] { input.RightPressed(), input.LeftPressed(), input.DownPressed(), input.UpPressed(),
                        Down(input.Button1), Down(input.Button2), Down(input.Button3), Down(input.Button4), Down(input.Button5), Down(input.Button6),
                        Down(input.Start), Down(input.Coin), Down(input.Service), Down(input.Test) };
                    for (var bit = 0; bit < buttons.Length; ++bit)
                        if (buttons[bit]) page[8 + p * 2 + bit / 8] |= (byte)(1 << (bit % 8));
                }
                JvsHelper.StateView.Write(6, unchecked(++_sequence)); Thread.MemoryBarrier();
                for (var n = 0; n < page.Length; ++n) if (n != 6 && n != 7) JvsHelper.WriteStateByte(n, page[n]);
                Thread.MemoryBarrier(); JvsHelper.StateView.Write(6, unchecked(++_sequence));
            }
        }
    }
}
