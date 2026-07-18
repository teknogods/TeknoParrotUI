using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Android.OS;

namespace TeknoParrotUi.AndroidBridge;

internal sealed class MappedSharedPage : IDisposable
{
    private const int ProtRead = 0x1;
    private const int ProtWrite = 0x2;
    private const int MapShared = 0x01;

    private readonly object _sync = new();
    private ParcelFileDescriptor? _descriptor;
    private IntPtr _address;

    private MappedSharedPage(ParcelFileDescriptor descriptor, IntPtr address)
    {
        _descriptor = descriptor;
        _address = address;
    }

    public static MappedSharedPage Map(ParcelFileDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var address = Mmap(IntPtr.Zero, BridgeProtocol.PageSize, ProtRead | ProtWrite, MapShared, descriptor.Fd, 0);
        if (address == new IntPtr(-1))
            throw new InvalidOperationException($"mmap failed with errno {Marshal.GetLastPInvokeError()}.");
        return new MappedSharedPage(descriptor, address);
    }

    public byte[] ReadBytes(int offset, int length)
    {
        ValidateRange(offset, length);
        lock (_sync)
        {
            EnsureOpen();
            var value = new byte[length];
            Marshal.Copy(IntPtr.Add(_address, offset), value, 0, length);
            return value;
        }
    }

    public void WriteBytes(int offset, ReadOnlySpan<byte> value)
    {
        ValidateRange(offset, value.Length);
        lock (_sync)
        {
            EnsureOpen();
            var copy = value.ToArray();
            Marshal.Copy(copy, 0, IntPtr.Add(_address, offset), copy.Length);
        }
    }

    public ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(offset, 2));
    public uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(offset, 4));
    public ulong ReadUInt64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(offset, 8));

    public void WriteUInt16(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        WriteBytes(offset, bytes);
    }

    public void WriteUInt32(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        WriteBytes(offset, bytes);
    }

    public void WriteUInt64(int offset, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        WriteBytes(offset, bytes);
    }

    public void Clear() => WriteBytes(0, new byte[BridgeProtocol.PageSize]);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_address != IntPtr.Zero)
            {
                Munmap(_address, BridgeProtocol.PageSize);
                _address = IntPtr.Zero;
            }

            _descriptor?.Dispose();
            _descriptor = null;
        }
    }

    private static void ValidateRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > BridgeProtocol.PageSize - length)
            throw new ArgumentOutOfRangeException(nameof(offset));
    }

    private void EnsureOpen()
    {
        if (_address == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MappedSharedPage));
    }

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    private static extern IntPtr Mmap(IntPtr address, nuint length, int protection, int flags, int fd, long offset);

    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    private static extern int Munmap(IntPtr address, nuint length);
}
