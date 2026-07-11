using System.IO.MemoryMappedFiles;

namespace TeknoParrotUi.Common.Jvs.Implementation
{
    /// <summary>
    /// Windows named shared memory (existing behavior wrapped).
    /// Creates or opens a named memory-mapped file in the Windows kernel
    /// namespace so OpenParrot inside the game process can open it by name.
    /// </summary>
    public class WindowsMemoryMappedFile : MemoryMappedSharedMemory
    {
        public WindowsMemoryMappedFile(string name, int size)
            : base(name, size)
        {
            File = MemoryMappedFile.CreateOrOpen(name, size);
            ViewAccessor = File.CreateViewAccessor();
        }
    }
}
