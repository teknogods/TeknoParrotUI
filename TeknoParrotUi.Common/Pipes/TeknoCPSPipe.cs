using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // CPIN v3: four 16-contact panels and optional 32-bit host rotary counters.
    // Cabinet configuration stays under the native TPO lobby's control.
    public sealed class TeknoCPSPipe : ControlSender
    {
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _active;
        private readonly uint[] _phase = new uint[4];
        private readonly MemoryMappedFile[] _trackball = new MemoryMappedFile[2];
        private readonly MemoryMappedViewAccessor[] _view = new MemoryMappedViewAccessor[2];
        private static bool Down(bool? value) => value == true;

        public override void Start()
        {
            lock (_sync)
            {
                JvsHelper.ResetState(); _sequence = 0; _active = true; Array.Clear(_phase, 0, _phase.Length);
                InputCode.AnalogBytes[0] = InputCode.AnalogBytes[2] = 128;
            }
            base.Start();
        }
        public override void Stop()
        {
            base.Stop();
            lock (_sync)
            {
                _active = false; JvsHelper.WriteStateByte(5, 0);
                for (var n = 0; n < 2; ++n) { _view[n]?.Dispose(); _trackball[n]?.Dispose(); _view[n] = null; _trackball[n] = null; }
            }
        }
        private int Mouse(int player)
        {
            if (_view[player] == null)
            {
                try
                {
                    _trackball[player] = MemoryMappedFile.OpenExisting("RawInputTrackballSharedMemory" + (player == 0 ? "" : "2"));
                    _view[player] = _trackball[player].CreateViewAccessor(0, 12);
                }
                catch (FileNotFoundException) { return 0; }
            }
            var view = _view[player];
            return view.ReadInt32(8) == 0 ? view.ReadInt16(0) : 0;
        }
        public override void Transmit()
        {
            lock (_sync)
            {
                if (!_active) return;
                var page = new byte[64];
                page[0] = (byte)'C'; page[1] = (byte)'P'; page[2] = (byte)'I'; page[3] = (byte)'N'; page[4] = 3; page[5] = 1;
                for (var p = 0; p < 4; ++p)
                {
                    var input = InputCode.PlayerDigitalButtons[p];
                    var buttons = new[] { input.RightPressed(), input.LeftPressed(), input.DownPressed(), input.UpPressed(),
                        Down(input.Button1), Down(input.Button2), Down(input.Button3), Down(input.Button4), Down(input.Button5), Down(input.Button6),
                        Down(input.Start), Down(input.Coin), Down(input.Service), Down(input.Test),
                        Down(input.ExtensionButton1), Down(input.ExtensionButton2) };
                    for (var bit = 0; bit < buttons.Length; ++bit)
                        if (buttons[bit]) page[8 + p * 2 + bit / 8] |= (byte)(1 << (bit % 8));
                }
                var rotary = InputCode.GameProfile?.ConfigValues?.FirstOrDefault(v => v.FieldName == "Rotary Input")?.FieldValue;
                if (rotary == "Analog" || rotary == "Trackball")
                {
                    for (var p = 0; p < 2; ++p)
                    {
                        var axis = InputCode.AnalogBytes[p * 2] - 128;
                        var delta = rotary == "Trackball" ? Mouse(p) : Math.Abs(axis) > 12 ? axis / 8 : 0;
                        _phase[p] = unchecked(_phase[p] + (uint)delta);
                        for (var n = 0; n < 4; ++n) page[16 + p * 4 + n] = (byte)(_phase[p] >> (n * 8));
                        page[32] |= (byte)(1 << p);
                    }
                    if (rotary == "Trackball") foreach (var view in _view) view?.Write(8, 1);
                }
                else Array.Clear(_phase, 0, _phase.Length);
                JvsHelper.StateView.Write(6, unchecked(++_sequence)); Thread.MemoryBarrier();
                for (var n = 0; n < page.Length; ++n) if (n != 6 && n != 7) JvsHelper.WriteStateByte(n, page[n]);
                Thread.MemoryBarrier(); JvsHelper.StateView.Write(6, unchecked(++_sequence));
            }
        }
    }
}
