using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    /// <summary>
    /// Minimal libX11 interop for the permission-free Linux input fallback.
    /// Polls global cursor position (XQueryPointer) and keyboard state
    /// (XQueryKeymap) — neither requires /dev/input access, root, or 'input'
    /// group membership, and both work under XWayland (Wine games are always
    /// X11/XWayland clients).
    ///
    /// Wayland compatibility: On pure Wayland desktops (no XWayland), there is
    /// no X server to query, so IsAvailable() returns false and the fallback
    /// is skipped. This is OK because pure Wayland setups are still rare;
    /// Wine games require XWayland anyway. The udev rule approach is the
    /// recommended solution for Wayland-first systems.
    ///
    /// Steam Deck: Gamescope micro-compositor bundles XWayland, so games
    /// always have an X server available. XQueryPointer adds ~10-15ms latency
    /// due to IPC overhead (vs 2-5ms for evdev), but works out of the box
    /// without any setup. For best performance, users should install the udev
    /// rule once in Desktop Mode.
    /// </summary>
    public static class X11Interop
    {
        private const string LibX11 = "libX11.so.6";

        // XQueryPointer button masks
        public const uint Button1Mask = 1 << 8;  // left
        public const uint Button2Mask = 1 << 9;  // middle
        public const uint Button3Mask = 1 << 10; // right

        [DllImport(LibX11)]
        public static extern IntPtr XOpenDisplay(string display);

        [DllImport(LibX11)]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11)]
        public static extern int XDisplayWidth(IntPtr display, int screen);

        [DllImport(LibX11)]
        public static extern int XDisplayHeight(IntPtr display, int screen);

        [DllImport(LibX11)]
        public static extern int XDefaultScreen(IntPtr display);

        [DllImport(LibX11)]
        public static extern bool XQueryPointer(IntPtr display, IntPtr window,
            out IntPtr rootReturn, out IntPtr childReturn,
            out int rootX, out int rootY, out int winX, out int winY,
            out uint maskReturn);

        /// <summary>keys = 32-byte bitmap indexed by X keycode (evdev code + 8).</summary>
        [DllImport(LibX11)]
        public static extern int XQueryKeymap(IntPtr display, byte[] keys);

        // ==================== XInput2 raw events (rootless, event-driven) ====================
        // Raw events are the strongest no-permission input source on X11:
        // per-physical-device button/key/motion events delivered regardless of
        // window focus, including side buttons — no /dev/input access needed.

        private const string LibXi = "libXi.so.6";

        public const int GenericEvent = 35;
        public const int XIAllDevices = 0;
        public const int XIAllMasterDevices = 1;
        public const int XISlavePointer = 3;
        public const int XISlaveKeyboard = 4;

        // XI2 raw event types
        public const int XI_RawKeyPress = 13;
        public const int XI_RawKeyRelease = 14;
        public const int XI_RawButtonPress = 15;
        public const int XI_RawButtonRelease = 16;
        public const int XI_RawMotion = 17;

        [StructLayout(LayoutKind.Sequential)]
        public struct XIEventMask
        {
            public int DeviceId;
            public int MaskLen;
            public IntPtr Mask;
        }

        /// <summary>First 56 bytes of XEvent when type == GenericEvent (the xcookie view).</summary>
        [StructLayout(LayoutKind.Explicit, Size = 192)]
        public struct XEvent
        {
            [FieldOffset(0)] public int Type;
            [FieldOffset(8)] public ulong Serial;
            [FieldOffset(16)] public int SendEvent;
            [FieldOffset(24)] public IntPtr Display;
            [FieldOffset(32)] public int Extension;   // cookie: XI opcode
            [FieldOffset(36)] public int EvType;      // cookie: XI_Raw*
            [FieldOffset(40)] public uint Cookie;
            [FieldOffset(48)] public IntPtr Data;     // cookie: XIRawEvent* after XGetEventData
        }

        /// <summary>struct XIRawEvent (64-bit layout), read from XEvent.Data.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct XIRawEvent
        {
            public int Type;
            public ulong Serial;
            public int SendEvent;
            public IntPtr Display;
            public int Extension;
            public int EvType;
            public ulong Time;
            public int DeviceId;
            public int SourceId;   // physical slave device
            public int Detail;     // button number / X keycode
            public int Flags;
            public int ValuatorsMaskLen;
            public IntPtr ValuatorsMask;
            public IntPtr ValuatorsValues; // double[popcount(mask)]
            public IntPtr RawValues;
        }

        /// <summary>struct XIDeviceInfo (64-bit layout).</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct XIDeviceInfo
        {
            public int DeviceId;
            public IntPtr Name;
            public int Use;        // XISlavePointer / XISlaveKeyboard / ...
            public int Attachment;
            public int Enabled;
            public int NumClasses;
            public IntPtr Classes;
        }

        [DllImport(LibX11)]
        public static extern int XQueryExtension(IntPtr display, string name, out int opcode, out int eventBase, out int errorBase);

        [DllImport(LibXi)]
        public static extern int XIQueryVersion(IntPtr display, ref int major, ref int minor);

        [DllImport(LibXi)]
        public static extern int XISelectEvents(IntPtr display, IntPtr window, ref XIEventMask masks, int numMasks);

        [DllImport(LibXi)]
        public static extern IntPtr XIQueryDevice(IntPtr display, int deviceId, out int deviceCount);

        [DllImport(LibXi)]
        public static extern void XIFreeDeviceInfo(IntPtr info);

        [DllImport(LibX11)]
        public static extern int XPending(IntPtr display);

        [DllImport(LibX11)]
        public static extern int XNextEvent(IntPtr display, ref XEvent ev);

        [DllImport(LibX11)]
        public static extern int XGetEventData(IntPtr display, ref XEvent cookie);

        [DllImport(LibX11)]
        public static extern void XFreeEventData(IntPtr display, ref XEvent cookie);

        [DllImport(LibX11)]
        public static extern int XFlush(IntPtr display);

        public sealed class PointerDevice
        {
            public int SourceId;
            public string Name;
        }

        /// <summary>
        /// Physical (slave) pointer devices, XTEST virtuals excluded. On native
        /// X11 each mouse/gun appears separately (rootless multi-gun); XWayland
        /// merges everything into one virtual pointer.
        /// </summary>
        public static List<PointerDevice> EnumeratePointerDevices(IntPtr display)
        {
            var result = new List<PointerDevice>();
            var infos = XIQueryDevice(display, XIAllDevices, out int count);
            if (infos == IntPtr.Zero)
                return result;
            try
            {
                int size = Marshal.SizeOf<XIDeviceInfo>();
                for (int i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<XIDeviceInfo>(infos + i * size);
                    if (info.Use != XISlavePointer || info.Enabled == 0)
                        continue;
                    var name = Marshal.PtrToStringAnsi(info.Name) ?? $"pointer-{info.DeviceId}";
                    if (name.Contains("XTEST"))
                        continue;
                    // XWayland exposes helper slaves (relative-pointer, pointer-
                    // gestures, touch) alongside the real merged pointer — only
                    // "xwayland-pointer:N" is the actual cursor device.
                    if (name.StartsWith("xwayland-", StringComparison.Ordinal) &&
                        !name.StartsWith("xwayland-pointer:", StringComparison.Ordinal))
                        continue;
                    result.Add(new PointerDevice { SourceId = info.DeviceId, Name = name });
                }
            }
            finally
            {
                XIFreeDeviceInfo(infos);
            }
            return result;
        }

        /// <summary>
        /// Select XI2 raw button/key/motion events on the root window.
        /// Returns the XI opcode to match in GenericEvent cookies, or -1 when
        /// XInput2 is unavailable (fall back to polling).
        /// </summary>
        public static int SelectRawEvents(IntPtr display)
        {
            try
            {
                if (XQueryExtension(display, "XInputExtension", out int opcode, out _, out _) == 0)
                    return -1;
                int major = 2, minor = 0;
                if (XIQueryVersion(display, ref major, ref minor) != 0) // != Success
                    return -1;

                // mask bits 13..17 → 3 bytes
                var maskBytes = Marshal.AllocHGlobal(3);
                try
                {
                    Marshal.WriteByte(maskBytes, 0, 0);
                    Marshal.WriteByte(maskBytes, 1, (byte)((1 << (XI_RawKeyPress - 8)) | (1 << (XI_RawKeyRelease - 8)) |
                                                           (1 << (XI_RawButtonPress - 8))));
                    Marshal.WriteByte(maskBytes, 2, (byte)((1 << (XI_RawButtonRelease - 16)) | (1 << (XI_RawMotion - 16))));
                    var mask = new XIEventMask { DeviceId = XIAllMasterDevices, MaskLen = 3, Mask = maskBytes };
                    if (XISelectEvents(display, XDefaultRootWindow(display), ref mask, 1) != 0)
                        return -1;
                    XFlush(display);
                    return opcode;
                }
                finally
                {
                    Marshal.FreeHGlobal(maskBytes);
                }
            }
            catch (DllNotFoundException)
            {
                return -1; // libXi missing → polling fallback
            }
        }

        /// <summary>
        /// Read valuator deltas (valuator 0 = X, 1 = Y) from a raw motion event.
        /// </summary>
        public static void GetMotionDeltas(in XIRawEvent ev, out double dx, out double dy)
        {
            dx = 0;
            dy = 0;
            if (ev.ValuatorsMask == IntPtr.Zero || ev.ValuatorsValues == IntPtr.Zero || ev.ValuatorsMaskLen < 1)
                return;
            byte mask0 = Marshal.ReadByte(ev.ValuatorsMask, 0);
            int valueIndex = 0;
            if ((mask0 & 1) != 0)
            {
                dx = ReadDouble(ev.ValuatorsValues, valueIndex++);
            }
            if ((mask0 & 2) != 0)
            {
                dy = ReadDouble(ev.ValuatorsValues, valueIndex);
            }
        }

        private static double ReadDouble(IntPtr basePtr, int index)
        {
            long bits = Marshal.ReadInt64(basePtr, index * sizeof(double));
            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>True when an X display is reachable (DISPLAY set and connectable).</summary>
        public static bool IsAvailable()
        {
            if (!OperatingSystem.IsLinux())
                return false;
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
                return false;
            try
            {
                var display = XOpenDisplay(null);
                if (display == IntPtr.Zero)
                    return false;
                XCloseDisplay(display);
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }
}
