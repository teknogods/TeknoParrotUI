using System;

namespace TeknoParrotUi.Common.Jvs.Abstractions
{
    /// <summary>
    /// Emulated battery-backed SRAM used by Ex-Board games (Arcana Heart,
    /// Daemon Bride). Contents persist across game restarts.
    /// </summary>
    public interface ISramEmulator : IDisposable
    {
        /// <summary>
        /// Size of the SRAM in bytes (Ex-Board uses 64KB).
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Path of the backing file used for persistence (e.g. sram.bin).
        /// </summary>
        string BackingFilePath { get; }

        int Read(int offset, byte[] buffer, int bufferOffset, int count);
        void Write(int offset, byte[] buffer, int bufferOffset, int count);

        /// <summary>
        /// Loads SRAM contents from the backing file (no-op if it does not exist).
        /// </summary>
        void Load();

        /// <summary>
        /// Flushes SRAM contents to the backing file.
        /// </summary>
        void Save();
    }
}
