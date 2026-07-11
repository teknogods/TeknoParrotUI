using System;
using System.IO.MemoryMappedFiles;
using TeknoParrotUi.Common.Jvs.Abstractions;

namespace TeknoParrotUi.Common.Jvs.Implementation
{
    /// <summary>
    /// Common base for <see cref="ISharedMemory"/> implementations backed by a
    /// <see cref="MemoryMappedFile"/>. Exposes the underlying file and view
    /// accessor so existing code (JvsHelper.StateView / StateSection) keeps
    /// working unchanged.
    /// </summary>
    public abstract class MemoryMappedSharedMemory : ISharedMemory
    {
        public string Name { get; }
        public int Size { get; }

        /// <summary>Underlying memory-mapped file (backward compatibility).</summary>
        public MemoryMappedFile File { get; protected set; }

        /// <summary>Underlying view accessor (backward compatibility).</summary>
        public MemoryMappedViewAccessor ViewAccessor { get; protected set; }

        protected MemoryMappedSharedMemory(string name, int size)
        {
            Name = name;
            Size = size;
        }

        public void Write(int offset, byte value) => ViewAccessor.Write(offset, value);
        public void Write(int offset, int value) => ViewAccessor.Write(offset, value);
        public byte ReadByte(int offset) => ViewAccessor.ReadByte(offset);
        public int ReadInt32(int offset) => ViewAccessor.ReadInt32(offset);

        public virtual void Dispose()
        {
            ViewAccessor?.Dispose();
            File?.Dispose();
        }
    }
}
