using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // MVIN v2 publishes contact closures and full host counters, never cabinet DIP settings.
    public sealed class TeknoMVSPipe : ControlSender
    {
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _active;
        private readonly long[] _phase = new long[2];
        private readonly MemoryMappedFile[] _trackball = new MemoryMappedFile[2];
        private readonly MemoryMappedViewAccessor[] _trackballView = new MemoryMappedViewAccessor[2];
        private static bool Down(bool? value) => value == true;
        private static string Setting(string name) => InputCode.GameProfile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue;

        public override void Start()
        {
            lock (_sync)
            {
                JvsHelper.ResetState(); _sequence = 0; _active = true;
                _phase[0] = _phase[1] = 0;
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
                for (var n = 0; n < 2; ++n) { _trackballView[n]?.Dispose(); _trackball[n]?.Dispose(); _trackballView[n] = null; _trackball[n] = null; }
            }
        }
        private int MouseDelta(int player, bool y)
        {
            if (_trackballView[player] == null)
            {
                try
                {
                    _trackball[player] = MemoryMappedFile.OpenExisting("RawInputTrackballSharedMemory" + (player == 0 ? "" : "2"));
                    _trackballView[player] = _trackball[player].CreateViewAccessor(0, 12);
                }
                catch (FileNotFoundException) { return 0; }
            }
            var view = _trackballView[player];
            return view.ReadInt32(8) == 0 ? view.ReadInt16(y ? 4 : 0) : 0;
        }
        public override void Transmit()
        {
            lock (_sync)
            {
                if (!_active) return;
                var page = new byte[64]; page[0] = (byte)'M'; page[1] = (byte)'V'; page[2] = (byte)'I'; page[3] = (byte)'N'; page[4] = 2; page[5] = 1;
                void Contact(int contact, bool down) { if (down) page[8 + contact / 8] |= (byte)(1 << (contact % 8)); }
                var set = InputCode.GameProfile?.ProfileName ?? "";
                for (var p = 0; p < (set == "mvs_kizuna4p" ? 4 : 2); ++p)
                {
                    var input = InputCode.PlayerDigitalButtons[p];
                    var c = p < 2 ? p * 8 : 30 + (p - 2) * 8;
                    Contact(c, input.UpPressed()); Contact(c + 1, input.DownPressed()); Contact(c + 2, input.LeftPressed()); Contact(c + 3, input.RightPressed());
                    Contact(c + 4, Down(input.Button1)); Contact(c + 5, Down(input.Button2)); Contact(c + 6, Down(input.Button3)); Contact(c + 7, Down(input.Button4));
                    Contact(p < 2 ? 16 + p : 46 + p - 2, Down(input.Start));
                    Contact(18 + p % 2, Down(input.Coin));
                }
                var op = InputCode.PlayerDigitalButtons[0];
                Contact(20, Down(op.Service)); Contact(21, Down(op.Test));
                if (set == "mvs_janshin" || set == "mvs_minasan" || set == "mvs_bakatono")
                {
                    var keys = new[] { op.ExtensionButton1, op.ExtensionButton2, op.ExtensionButton3, op.ExtensionButton4,
                        op.ExtensionButton1_1, op.ExtensionButton1_2, op.ExtensionButton1_3, op.ExtensionButton1_4,
                        op.ExtensionButton1_5, op.ExtensionButton1_6, op.ExtensionButton1_7, op.ExtensionButton1_8,
                        op.ExtensionButton2_1, op.ExtensionButton2_2, op.ExtensionButton2_3, op.ExtensionButton2_4,
                        op.ExtensionButton2_5, op.ExtensionButton2_6, op.ExtensionButton2_7 };
                    for (var n = 0; n < keys.Length; ++n) Contact(30 + n, Down(keys[n]));
                }
                if (set.StartsWith("mvs_vliner", StringComparison.Ordinal))
                { Contact(30, Down(op.ExtensionButton1)); Contact(31, Down(op.ExtensionButton2)); Contact(32, Down(op.ExtensionButton3)); }
                if (set == "mvs_irrmaze" || set == "mvs_popbounc")
                {
                    if (Setting("Input API") == "RawInputTrackball")
                    {
                        _phase[0] += MouseDelta(0, false);
                        _phase[1] += MouseDelta(set == "mvs_popbounc" ? 1 : 0, set == "mvs_irrmaze");
                        foreach (var view in _trackballView) view?.Write(8, 1);
                    }
                    else
                    {
                        for (var n = 0; n < 2; ++n) { var axis = InputCode.AnalogBytes[n * 2] - 128; if (Math.Abs(axis) > 12) _phase[n] += axis / 8; }
                    }
                    for (var p = 0; p < 2; ++p)
                    {
                        var counter = unchecked((uint)(_phase[p] / 4));
                        for (var n = 0; n < 4; ++n) page[16 + p * 4 + n] = (byte)(counter >> (n * 8));
                    }
                }
                // Odd while writing, even after publication. The reader checks
                // both copies and expires an unchanged sequence after 250 ms.
                JvsHelper.StateView.Write(6, unchecked(++_sequence)); Thread.MemoryBarrier();
                for (var n = 0; n < page.Length; ++n) if (n != 6 && n != 7) JvsHelper.WriteStateByte(n, page[n]);
                Thread.MemoryBarrier(); JvsHelper.StateView.Write(6, unchecked(++_sequence));
            }
        }
    }
}
