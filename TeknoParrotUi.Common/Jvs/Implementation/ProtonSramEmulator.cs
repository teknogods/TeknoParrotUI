using System;
using System.IO;
using TeknoParrotUi.Common.Jvs.Abstractions;

namespace TeknoParrotUi.Common.Jvs.Implementation
{
    /// <summary>
    /// Battery-backed SRAM emulation for Ex-Board games (Arcana Heart,
    /// Daemon Bride). Holds a 64KB in-memory buffer persisted to a backing
    /// file (sram.bin) so scores/settings survive restarts. Works on both
    /// Windows and Linux+Proton.
    /// </summary>
    public class ProtonSramEmulator : ISramEmulator
    {
        public const int DefaultSize = 64 * 1024;

        private readonly byte[] _sram;
        private readonly object _lock = new object();

        public int Size { get; }
        public string BackingFilePath { get; }

        public ProtonSramEmulator(string backingFilePath, int size = DefaultSize)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            BackingFilePath = backingFilePath ?? throw new ArgumentNullException(nameof(backingFilePath));
            Size = size;
            _sram = new byte[size];
            Load();
        }

        public int Read(int offset, byte[] buffer, int bufferOffset, int count)
        {
            lock (_lock)
            {
                if (offset < 0 || offset >= Size)
                    return 0;

                var toRead = Math.Min(count, Size - offset);
                Array.Copy(_sram, offset, buffer, bufferOffset, toRead);
                return toRead;
            }
        }

        public void Write(int offset, byte[] buffer, int bufferOffset, int count)
        {
            lock (_lock)
            {
                if (offset < 0 || offset >= Size)
                    return;

                var toWrite = Math.Min(count, Size - offset);
                Array.Copy(buffer, bufferOffset, _sram, offset, toWrite);
            }
        }

        public void Load()
        {
            lock (_lock)
            {
                if (!File.Exists(BackingFilePath))
                    return;

                var data = File.ReadAllBytes(BackingFilePath);
                Array.Copy(data, _sram, Math.Min(data.Length, Size));
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(BackingFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(BackingFilePath, _sram);
            }
        }

        public void Dispose() => Save();
    }
}
