using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace InputMethodAudit;

/// <summary>
/// Test-only SDL3 virtual device. SDL3 changed attachment from a device index
/// to an instance ID and requires an initialized descriptor structure.
/// </summary>
internal sealed class Sdl3VirtualJoystick : IDisposable
{
    private const string Library = "SDL3";
    private uint _instanceId;
    private IntPtr _joystick;

    static Sdl3VirtualJoystick()
    {
        NativeLibrary.SetDllImportResolver(typeof(Sdl3VirtualJoystick).Assembly, ResolveLibrary);
    }

    public Sdl3VirtualJoystick(string name, ushort type, ushort axes, ushort buttons, ushort hats)
    {
        var nativeName = Marshal.StringToCoTaskMemUTF8(name);
        try
        {
            var descriptor = new VirtualJoystickDesc
            {
                Version = (uint)Marshal.SizeOf<VirtualJoystickDesc>(),
                Type = type,
                Axes = axes,
                Buttons = buttons,
                Hats = hats,
                Name = nativeName
            };
            _instanceId = AttachVirtualJoystick(ref descriptor);
            if (_instanceId == 0)
                throw new InvalidOperationException("SDL3 virtual joystick attach failed: " + Error());

            _joystick = OpenJoystick(_instanceId);
            if (_joystick == IntPtr.Zero)
            {
                DetachVirtualJoystick(_instanceId);
                _instanceId = 0;
                throw new InvalidOperationException("SDL3 virtual joystick open failed: " + Error());
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(nativeName);
        }
    }

    public void SetButton(int button, bool pressed)
    {
        if (!SetJoystickVirtualButton(_joystick, button, pressed))
            throw new InvalidOperationException("SDL3 virtual button update failed: " + Error());
    }

    public void Dispose()
    {
        if (_joystick != IntPtr.Zero)
        {
            CloseJoystick(_joystick);
            _joystick = IntPtr.Zero;
        }
        if (_instanceId != 0)
        {
            DetachVirtualJoystick(_instanceId);
            _instanceId = 0;
        }
    }

    private static string Error() => Marshal.PtrToStringUTF8(GetError()) ?? "unknown SDL3 error";

    private static IntPtr ResolveLibrary(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
            return IntPtr.Zero;

        if (OperatingSystem.IsWindows())
        {
            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => "win-x86",
                Architecture.X64 => "win-x64",
                _ => null
            };
            if (architecture != null)
            {
                var packaged = Path.Combine(AppContext.BaseDirectory, "Native", "SDL3", architecture, "SDL3.dll");
                if (NativeLibrary.TryLoad(packaged, out var library))
                    return library;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => null
            };
            if (architecture != null)
            {
                var packaged = Path.Combine(AppContext.BaseDirectory, "runtimes", architecture,
                    "native", "libSDL3.so.0");
                if (NativeLibrary.TryLoad(packaged, out var bundled))
                    return bundled;
            }
            if (NativeLibrary.TryLoad("libSDL3.so.0", out var system))
                return system;
        }
        else if (OperatingSystem.IsMacOS())
        {
            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => null
            };
            if (architecture != null)
            {
                var packaged = Path.Combine(AppContext.BaseDirectory, "runtimes", architecture,
                    "native", "libSDL3.dylib");
                if (NativeLibrary.TryLoad(packaged, out var bundled))
                    return bundled;
            }
            if (NativeLibrary.TryLoad("libSDL3.dylib", out var system))
                return system;
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VirtualJoystickDesc
    {
        public uint Version;
        public ushort Type;
        public ushort Padding;
        public ushort VendorId;
        public ushort ProductId;
        public ushort Axes;
        public ushort Buttons;
        public ushort Balls;
        public ushort Hats;
        public ushort Touchpads;
        public ushort Sensors;
        public ushort Padding2A;
        public ushort Padding2B;
        public uint ButtonMask;
        public uint AxisMask;
        public IntPtr Name;
        public IntPtr TouchpadDescriptions;
        public IntPtr SensorDescriptions;
        public IntPtr Userdata;
        public IntPtr Update;
        public IntPtr SetPlayerIndex;
        public IntPtr Rumble;
        public IntPtr RumbleTriggers;
        public IntPtr SetLed;
        public IntPtr SendEffect;
        public IntPtr SetSensorsEnabled;
        public IntPtr Cleanup;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_AttachVirtualJoystick")]
    private static extern uint AttachVirtualJoystick(ref VirtualJoystickDesc descriptor);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_OpenJoystick")]
    private static extern IntPtr OpenJoystick(uint instanceId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_SetJoystickVirtualButton")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SetJoystickVirtualButton(IntPtr joystick, int button, [MarshalAs(UnmanagedType.I1)] bool down);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CloseJoystick")]
    private static extern void CloseJoystick(IntPtr joystick);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_DetachVirtualJoystick")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool DetachVirtualJoystick(uint instanceId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetError")]
    private static extern IntPtr GetError();
}
