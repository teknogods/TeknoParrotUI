using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;

namespace TeknoParrotUi.Common.Jvs
{
    /// <summary>
    /// Initial D online chat buttons (InitialDServer docs/CHAT_VOICE.md 2.4, INTERFACES.md 4). The mapped
    /// "Chat: Quick 1..4", "Voice: Push-to-talk" and "Voice: Mode" buttons reach the DLL through the named 32-byte block
    /// "TeknoParrot_Chat", never through the JVS stream (ExtensionOne* bits would reach the game as switches).
    /// Little-endian: +0 'T','P','C','H' | +4 u16 version 1 | +6 u16 size 32 | +8 u32 beat (Environment.TickCount,
    /// refreshed every 50 ms; the DLL ignores the buttons when it is more than 500 ms old) | +12 u32 buttons (bit0-3
    /// quick 1-4, bit4 push-to-talk, bit5 voice mode; levels, the DLL detects the edges) | +16..31 zero.
    /// </summary>
    public sealed class ChatButtonBlock : IDisposable
    {
        public const string MappingName = "TeknoParrot_Chat";
        public const int Size = 32;

        private static int _buttons; // the levels, set by the input listeners
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _view;
        private readonly Implementation.MemoryMappedSharedMemory _memory;
        private readonly Proton.ProtonSharedMemoryMirror _mirror;
        private readonly Thread _mirrorThread;
        private readonly Timer _heartbeat;

        /// <summary>The bit of a chat mapping, -1 for every other mapping.</summary>
        public static int BitOf(InputMapping mapping)
        {
            switch (mapping)
            {
                case InputMapping.ChatQuick1: return 0;
                case InputMapping.ChatQuick2: return 1;
                case InputMapping.ChatQuick3: return 2;
                case InputMapping.ChatQuick4: return 3;
                case InputMapping.ChatPushToTalk: return 4;
                case InputMapping.ChatVoiceMode: return 5;
                default: return -1;
            }
        }

        /// <summary>From the input listeners: null (an update of another button) keeps the level.</summary>
        public static void Set(InputMapping mapping, bool? pressed)
        {
            var bit = BitOf(mapping);
            if (bit < 0 || pressed == null)
                return;
            int old, now;
            do
            {
                old = Volatile.Read(ref _buttons);
                now = pressed.Value ? old | (1 << bit) : old & ~(1 << bit);
            } while (Interlocked.CompareExchange(ref _buttons, now, old) != old);
        }

        public static int Buttons => Volatile.Read(ref _buttons);

        /// <summary>The block for a game whose profile has a bound chat button; null otherwise (nothing is created).</summary>
        public static ChatButtonBlock TryCreate(IEnumerable<JoystickButtons> buttons)
        {
            if (buttons == null || !buttons.Any(b => b != null && BitOf(b.InputMapping) >= 0 && IsBound(b)))
                return null;
            try
            {
                return new ChatButtonBlock();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsBound(JoystickButtons b)
        {
            return b.DirectInputButton != null || b.XInputButton != null ||
                   (b.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None);
        }

        private ChatButtonBlock()
        {
            Interlocked.Exchange(ref _buttons, 0);
            _memory = SharedMemoryFactory.CreateOrOpen(MappingName, Size);
            _file = _memory.File;
            _view = _memory.ViewAccessor;
            _view.WriteArray(0, new byte[Size], 0, Size);
            _view.WriteArray(0, new[] { (byte)'T', (byte)'P', (byte)'C', (byte)'H' }, 0, 4);
            _view.Write(4, (ushort)1);
            _view.Write(6, (ushort)Size);
            Refresh();
            if (_memory is Implementation.ProtonSharedMemoryBridge linux)
            {
                _mirror = new Proton.ProtonSharedMemoryMirror(linux);
                _mirrorThread = new Thread(() => { try { _mirror.Start(); } catch (Exception) { } })
                { IsBackground = true, Name = "ChatMemoryMirror" };
                _mirrorThread.Start();
                _heartbeat = new Timer(_ => { try { Refresh(); } catch (ObjectDisposedException) { } }, null, 50, 50);
            }
        }

        /// <summary>Writes the button levels, then the beat.</summary>
        public void Refresh()
        {
            _view.Write(12, (uint)Buttons);
            _view.Write(8, unchecked((uint)Environment.TickCount));
        }

        public void Dispose()
        {
            _heartbeat?.Dispose();
            _mirror?.Stop();
            _mirrorThread?.Join(1000);
            try
            {
                _view.Write(12, 0u);
                _view.Write(8, 0u); // a stale beat: the DLL ignores the block
            }
            catch (Exception)
            {
                // ignored
            }
            _memory.Dispose();
            Interlocked.Exchange(ref _buttons, 0);
        }
    }
}
