using System;

namespace TeknoParrotUi.Common.Jvs.Abstractions
{
    /// <summary>
    /// Platform-agnostic shared memory region.
    /// Abstracts Windows named memory-mapped files (existing behavior) and
    /// POSIX shared memory (/dev/shm) used for Linux+Proton bridging.
    /// </summary>
    public interface ISharedMemory : IDisposable
    {
        /// <summary>
        /// Name of the shared memory region (e.g. "TeknoParrot_JvsState").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Size of the region in bytes.
        /// </summary>
        int Size { get; }

        void Write(int offset, byte value);
        void Write(int offset, int value);
        byte ReadByte(int offset);
        int ReadInt32(int offset);
    }
}
