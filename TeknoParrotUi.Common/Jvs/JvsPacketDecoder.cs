using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Common.Jvs
{
    /// <summary>
    /// Incrementally decodes the JVS byte-stuffed stream used by the
    /// TeknoParrot_JVS named pipe. Returned packets include the logical 0xE0
    /// sync byte and have 0xD0/0xE0 payload escapes removed.
    /// </summary>
    public sealed class JvsPacketDecoder
    {
        private readonly List<byte> _packet = new List<byte>(260);
        private int _expectedLength;
        private bool _escapePending;

        public bool TryPush(byte value, out byte[] packet)
        {
            packet = Array.Empty<byte>();
            if (_escapePending)
            {
                _escapePending = false;
                if (value == 0xCF)
                    return PushLogical(0xD0, escaped: true, out packet);
                if (value == 0xDF)
                    return PushLogical(0xE0, escaped: true, out packet);
                Reset();
                return false;
            }
            if (value == 0xD0)
            {
                _escapePending = true;
                return false;
            }
            return PushLogical(value, escaped: false, out packet);
        }

        public void Reset()
        {
            _packet.Clear();
            _expectedLength = 0;
            _escapePending = false;
        }

        private bool PushLogical(byte value, bool escaped, out byte[] packet)
        {
            packet = Array.Empty<byte>();
            if (!escaped && value == 0xE0)
            {
                _packet.Clear();
                _expectedLength = 0;
            }
            else if (_packet.Count == 0)
            {
                return false;
            }

            _packet.Add(value);
            if (_packet.Count == 3)
            {
                _expectedLength = 3 + _packet[2];
                if (_expectedLength < 4 || _expectedLength > 258)
                {
                    Reset();
                    return false;
                }
            }
            if (_expectedLength == 0 || _packet.Count < _expectedLength)
                return false;
            if (_packet.Count != _expectedLength)
            {
                Reset();
                return false;
            }

            packet = _packet.ToArray();
            Reset();
            return true;
        }
    }
}
