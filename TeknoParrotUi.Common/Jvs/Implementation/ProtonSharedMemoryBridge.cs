using System.IO;
using System.IO.MemoryMappedFiles;

namespace TeknoParrotUi.Common.Jvs.Implementation
{
    /// <summary>
    /// Linux shared memory backed by a file in /dev/shm (POSIX shared memory).
    /// .NET does not support named memory-mapped files on Unix, so we map a
    /// file at /dev/shm/&lt;name&gt; instead. This also lays the groundwork for
    /// the Proton bridge: a companion component inside the Proton prefix can
    /// mirror this region into the game's CreateFileMapping namespace (Phase 2).
    /// </summary>
    public class ProtonSharedMemoryBridge : MemoryMappedSharedMemory
    {
        private const string ShmDirectory = "/dev/shm";

        public string FilePath { get; }

        public ProtonSharedMemoryBridge(string name, int size)
            : base(name, size)
        {
            // Fall back to temp dir if /dev/shm is unavailable (e.g. containers).
            var dir = Directory.Exists(ShmDirectory) ? ShmDirectory : Path.GetTempPath();
            FilePath = Path.Combine(dir, name);

            var stream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (stream.Length < size)
                stream.SetLength(size);

            File = MemoryMappedFile.CreateFromFile(stream, null, size,
                MemoryMappedFileAccess.ReadWrite, HandleInheritability.Inheritable, leaveOpen: false);
            ViewAccessor = File.CreateViewAccessor();
        }
    }
}
