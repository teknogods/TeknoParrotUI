using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // SSIN v2: generated cabinet order and full host trackball counters.
    // Cabinet IDs and DIP settings are never published by the input bridge.
    public sealed class TeknoSS32Pipe : ControlSender
    {
        internal sealed class Axis
        {
            internal readonly int Source, Neutral, Device;
            internal readonly bool Vertical;
            internal Axis(int source, int neutral, int device = -1, bool vertical = false)
            { Source = source; Neutral = neutral; Device = device; Vertical = vertical; }
        }
        internal sealed class Layout
        {
            internal readonly uint Identity;
            internal readonly Func<bool>[] Digital;
            internal readonly Axis[] Analog, Relative;
            internal readonly int[] Toggle;
            internal Layout(uint identity, Func<bool>[] digital, Axis[] analog, Axis[] relative, int[] toggle = null)
            { Identity = identity; Digital = digital; Analog = analog; Relative = relative; Toggle = toggle ?? new int[0]; }
        }
        private readonly object _sync = new object();
        private ushort _sequence;
        private bool _active;
        private Layout _layout;
        private bool _buttonShifter;
        private bool[] _shiftDown, _shiftHigh;
        private readonly uint[] _phase = new uint[8];
        private readonly MemoryMappedFile[] _trackball = new MemoryMappedFile[3];
        private readonly MemoryMappedViewAccessor[] _view = new MemoryMappedViewAccessor[3];
        public override void Start()
        {
            lock (_sync)
            {
                _layout = TeknoSS32Bindings.Get(InputCode.GameProfile?.ProfileName ?? "");
                var shifter = InputCode.GameProfile?.ConfigValues?.FirstOrDefault(field => field.FieldName == "Shifter Mode")?.FieldValue ?? "Button";
                if (shifter != "Button" && shifter != "Lever") throw new ArgumentException("Shifter Mode must be Button or Lever");
                _buttonShifter = shifter == "Button";
                _shiftDown = new bool[_layout.Digital.Length]; _shiftHigh = new bool[_layout.Digital.Length];
                if (_layout.Relative.Length > 8 || (_layout.Relative.Length != 0 && _layout.Analog.Length != 0))
                    throw new InvalidOperationException("Unsupported mixed SS32 analog/trackball layout");
                JvsHelper.ResetState(); _sequence = 0; _active = true;
                Array.Clear(_phase, 0, _phase.Length);
                foreach (var axis in _layout.Analog.Concat(_layout.Relative)) InputCode.AnalogBytes[axis.Source] = (byte)axis.Neutral;
            }
            base.Start();
        }
        public override void Stop()
        {
            base.Stop();
            lock (_sync)
            {
                _active = false; JvsHelper.WriteStateByte(5, 0);
                for (var n = 0; n < _view.Length; ++n)
                { _view[n]?.Dispose(); _trackball[n]?.Dispose(); _view[n] = null; _trackball[n] = null; }
            }
        }
        private int Mouse(Axis axis)
        {
            if (axis.Device < 0 || axis.Device >= _view.Length) return 0;
            if (_view[axis.Device] == null)
            {
                try
                {
                    _trackball[axis.Device] = MemoryMappedFile.OpenExisting("RawInputTrackballSharedMemory" + (axis.Device == 0 ? "" : (axis.Device + 1).ToString()));
                    _view[axis.Device] = _trackball[axis.Device].CreateViewAccessor(0, 12);
                }
                catch (FileNotFoundException) { return 0; }
            }
            var view = _view[axis.Device];
            return view.ReadInt32(8) == 0 ? view.ReadInt16(axis.Vertical ? 4 : 0) : 0;
        }
        public override void Transmit()
        {
            lock (_sync)
            {
                if (!_active) return;
                var page = new byte[64];
                page[0] = page[1] = (byte)'S'; page[2] = (byte)'I'; page[3] = (byte)'N'; page[4] = 2; page[5] = 1;
                for (var n = 0; n < _layout.Digital.Length; ++n)
                {
                    var pressed = _layout.Digital[n]();
                    if (_buttonShifter && _layout.Toggle.Contains(n))
                    {
                        if (pressed && !_shiftDown[n]) _shiftHigh[n] = !_shiftHigh[n];
                        _shiftDown[n] = pressed; pressed = _shiftHigh[n];
                    }
                    if (pressed) page[8 + n / 8] |= (byte)(1 << (n % 8));
                }
                for (var n = 0; n < _layout.Analog.Length; ++n) page[24 + n] = InputCode.AnalogBytes[_layout.Analog[n].Source];
                var raw = InputCode.GameProfile?.ConfigValues?.FirstOrDefault(v => v.FieldName == "Input API")?.FieldValue == "RawInputTrackball";
                for (var n = 0; n < _layout.Relative.Length; ++n)
                {
                    var axis = _layout.Relative[n];
                    var value = InputCode.AnalogBytes[axis.Source] - 128;
                    var delta = raw ? Mouse(axis) : Math.Abs(value) > 12 ? value / 8 : 0;
                    _phase[n] = unchecked(_phase[n] + (uint)delta);
                    for (var b = 0; b < 4; ++b) page[24 + n * 4 + b] = (byte)(_phase[n] >> (b * 8));
                }
                if (raw) foreach (var view in _view) view?.Write(8, 1);
                for (var n = 0; n < 4; ++n) page[56 + n] = (byte)(_layout.Identity >> (n * 8));
                JvsHelper.StateView.Write(6, unchecked(++_sequence)); Thread.MemoryBarrier();
                for (var n = 0; n < page.Length; ++n) if (n != 6 && n != 7) JvsHelper.WriteStateByte(n, page[n]);
                Thread.MemoryBarrier(); JvsHelper.StateView.Write(6, unchecked(++_sequence));
            }
        }
    }
}
