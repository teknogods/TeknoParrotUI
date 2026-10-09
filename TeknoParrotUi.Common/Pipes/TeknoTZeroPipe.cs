using System;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>Single-writer TTZIN v1 publisher; see TeknoTZero/docs/TPUI_INPUT.md.</summary>
    public sealed class TeknoTZeroPipe : ControlSender
    {
        private readonly object _sync = new object();
        private readonly byte[] _page = new byte[64];
        private bool _publishInput;
        private string _set;
        private int _notch;
        private int _previousNotches;
        private Thread _worker;
        private long _generation;

        private static bool Down(bool? value) => value == true;
        private static bool Enabled(string name, bool fallback = false)
        {
            var value = InputCode.GameProfile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue;
            return value == null ? fallback : value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public override void Start()
        {
            lock (_sync)
            {
                if (Running) return;
                _set = InputCode.GameProfile?.ProfileName ?? "";
                _notch = _previousNotches = 0;
                _publishInput = !(Enabled("Enable VR") && Enabled("Use VR Controls", true));
                for (var axis = 0; axis < 8; ++axis)
                    InputCode.AnalogBytes[axis * 2] = Neutral(axis);
                TransmitInput();
                Running = true;
                var generation = ++_generation;
                _worker = new Thread(() => PublishLoop(generation)) { IsBackground = true };
                _worker.Start();
            }
        }

        private byte Neutral(int axis)
        {
            if (_set.StartsWith("pwrshovl", StringComparison.Ordinal)) return 128;
            if (_set.StartsWith("landhigh", StringComparison.Ordinal)) return axis < 3 ? (byte)128 : (byte)0;
            if (_set.StartsWith("raizpin", StringComparison.Ordinal)) return axis == 3 || axis == 7 ? (byte)0 : (byte)128;
            if (axis == 0 && (_set.StartsWith("batl", StringComparison.Ordinal) ||
                              _set.StartsWith("styphp", StringComparison.Ordinal))) return 128;
            return 0;
        }

        public override void Stop()
        {
            Thread worker;
            lock (_sync)
            {
                _publishInput = false;
                Running = false;
                ++_generation;
                worker = _worker;
                _worker = null;
                TransmitInput();
            }
            if (worker != null && worker != Thread.CurrentThread) worker.Join();
        }

        private void PublishLoop(long generation)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (!Running || generation != _generation) return;
                    TransmitInput();
                }
                Thread.Sleep(Math.Max(1, (int)SleepTime));
            }
        }

        public override void Transmit()
        {
            lock (_sync) TransmitInput();
        }

        internal static byte ReverseSteering(byte value)
        {
            // Piecewise normalization preserves center=128 and both endpoints.
            return (byte)(value <= 128 ? 128 + ((128 - value) * 127 + 64) / 128
                                      : 128 - ((value - 128) * 128 + 63) / 127);
        }

        private void TransmitInput()
        {
            Array.Clear(_page, 0, _page.Length);
            _page[0] = (byte)'T'; _page[1] = (byte)'T'; _page[2] = (byte)'Z';
            _page[3] = (byte)'I'; _page[4] = (byte)'N'; _page[5] = 1;
            _page[6] = (byte)(_publishInput ? 1 : 0);
            if (_publishInput)
            {
                var system = InputCode.PlayerDigitalButtons[0];
                if (Down(system.Test)) _page[12] |= 0x80;
                if (Down(system.Service)) _page[12] |= 0x40;
                for (var player = 0; player < 4; ++player)
                {
                    var input = InputCode.PlayerDigitalButtons[player];
                    byte buttons = 0, extra = 0;
                    if (input.UpPressed()) buttons |= 1;
                    if (input.DownPressed()) buttons |= 2;
                    if (input.LeftPressed()) buttons |= 4;
                    if (input.RightPressed()) buttons |= 8;
                    if (Down(input.Button1)) buttons |= 0x40;
                    if (Down(input.Button2)) buttons |= 0x10;
                    if (Down(input.Button3)) buttons |= 0x20;
                    if (Down(input.Start)) buttons |= 0x80;
                    if (Down(input.Button4)) extra |= 1;
                    if (Down(input.Button5)) extra |= 2;
                    if (Down(input.Button6)) extra |= 4;
                    if (Down(input.ExtensionButton1)) extra |= 8;
                    if (Down(input.ExtensionButton2)) extra |= 16;
                    if (player == 0 && _set.StartsWith("dendego3", StringComparison.Ordinal))
                    {
                        var notches = (Down(input.Button1) ? 1 : 0) | (Down(input.Button2) ? 2 : 0) |
                            (Down(input.Button3) ? 4 : 0) | (Down(input.Button4) ? 8 : 0) |
                            (Down(input.Button5) ? 16 : 0) | (Down(input.Button6) ? 32 : 0);
                        var rising = notches & ~_previousNotches;
                        for (var n = 0; n < 6; ++n) if ((rising & (1 << n)) != 0) _notch = n;
                        _previousNotches = notches;
                        buttons &= 0x8f; extra &= 0x18;
                        if (_notch < 3) buttons |= new byte[] { 0x40, 0x10, 0x20 }[_notch];
                        else extra |= (byte)(1 << (_notch - 3));
                    }
                    _page[13 + player] = buttons;
                    _page[28 + player] = extra;
                    _page[32 + player] = (byte)(Down(input.Coin) ? 1 : 0);
                }
                var channels = _set.StartsWith("pwrshovl", StringComparison.Ordinal) ? 8 :
                    _set.StartsWith("landhigh", StringComparison.Ordinal) ? 6 :
                    _set.StartsWith("dendego3", StringComparison.Ordinal) ? 1 :
                    _set.StartsWith("raizpin", StringComparison.Ordinal) ? 7 : 3;
                for (var axis = 0; axis < channels; ++axis)
                    if (axis != 3 || !(_set.StartsWith("raizpin", StringComparison.Ordinal) ||
                                      _set.StartsWith("landhigh", StringComparison.Ordinal)))
                        _page[20 + axis] = InputCode.AnalogBytes[axis * 2];
                if (_set.StartsWith("styphp", StringComparison.Ordinal))
                    _page[20] = ReverseSteering(_page[20]);
            }
            Publish(_page);
        }

        private static void Publish(byte[] page)
        {
            var view = JvsHelper.StateView;
            // Calls are serialized by _sync and TTZIN permits only one writer.
            // The mapping starts at offset zero: the UInt32 at byte 8 is aligned,
            // so accessor writes are atomic on Windows x86/x64. Explicit barriers
            // keep payload stores between the odd/in-progress and even/done stores.
            // Recover from an interrupted odd publication and allow UInt32 wrap.
            var sequence = unchecked((view.ReadUInt32(8) & ~1u) + 1u);
            view.Write(8, sequence);
            Thread.MemoryBarrier();
            view.WriteArray(0, page, 0, 8);
            view.WriteArray(12, page, 12, 52);
            Thread.MemoryBarrier();
            view.Write(8, unchecked(sequence + 1u));
        }
    }
}
