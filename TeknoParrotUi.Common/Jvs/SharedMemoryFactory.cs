using System;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common.Jvs.Abstractions;
using TeknoParrotUi.Common.Jvs.Implementation;

namespace TeknoParrotUi.Common.Jvs
{
    /// <summary>
    /// Creates the correct <see cref="ISharedMemory"/> for the current platform.
    /// Windows: named memory-mapped file (existing behavior).
    /// Linux: /dev/shm-backed region (Proton bridge groundwork).
    /// </summary>
    public static class SharedMemoryFactory
    {
        public static MemoryMappedSharedMemory CreateOrOpen(string name, int size)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsMemoryMappedFile(name, size);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new ProtonSharedMemoryBridge(name, size);

            throw new PlatformNotSupportedException("Shared memory is only supported on Windows and Linux.");
        }
    }
}
