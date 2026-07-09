using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    /// <summary>
    /// Minimal Linux evdev interop: raw <c>input_event</c> reading from
    /// <c>/dev/input/event*</c> plus device discovery via
    /// <c>/proc/bus/input/devices</c>. No libevdev dependency — the event
    /// structs are read directly, which is all a mouse/light-gun listener needs.
    /// </summary>
    public static class EvdevInterop
    {
        // event types
        public const ushort EV_KEY = 0x01;
        public const ushort EV_REL = 0x02;
        public const ushort EV_ABS = 0x03;

        // relative axes
        public const ushort REL_X = 0x00;
        public const ushort REL_Y = 0x01;

        // absolute axes (light guns / tablets)
        public const ushort ABS_X = 0x00;
        public const ushort ABS_Y = 0x01;

        // button codes
        public const ushort BTN_LEFT = 0x110;
        public const ushort BTN_RIGHT = 0x111;
        public const ushort BTN_MIDDLE = 0x112;
        public const ushort BTN_SIDE = 0x113;
        public const ushort BTN_EXTRA = 0x114;
        public const ushort BTN_TOUCH = 0x14a;

        public const int O_RDONLY = 0x0000;
        public const int O_NONBLOCK = 0x0800;

        private const int EACCES = 13;

        // Typing-key codes used to distinguish real keyboards from devices that
        // merely claim the kbd handler (power buttons, mouse macro endpoints).
        private const int KEY_A = 30;
        private const int KEY_Z = 44;
        private const int KEY_SPACE = 57;

        /// <summary>struct input_event on 64-bit Linux (struct timeval = 2×long).</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct InputEvent
        {
            public long TimeSeconds;
            public long TimeMicroseconds;
            public ushort Type;
            public ushort Code;
            public int Value;
        }

        public static readonly int InputEventSize = Marshal.SizeOf<InputEvent>();

        /// <summary>struct input_absinfo (for EVIOCGABS).</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct AbsInfo
        {
            public int Value;
            public int Minimum;
            public int Maximum;
            public int Fuzz;
            public int Flat;
            public int Resolution;
        }

        [DllImport("libc", SetLastError = true)]
        public static extern int open(string pathname, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern IntPtr read(int fd, IntPtr buf, IntPtr count);

        [DllImport("libc", SetLastError = true)]
        private static extern int ioctl(int fd, ulong request, ref AbsInfo absInfo);

        /// <summary>EVIOCGABS(axis) = _IOR('E', 0x40 + axis, struct input_absinfo)</summary>
        private static ulong Eviocgabs(ushort axis) =>
            0x80000000UL | ((ulong)Marshal.SizeOf<AbsInfo>() << 16) | ((ulong)'E' << 8) | (0x40UL + axis);

        /// <summary>Query the min/max range of an absolute axis. Returns false if unsupported.</summary>
        public static bool TryGetAbsInfo(int fd, ushort axis, out AbsInfo info)
        {
            info = default;
            return ioctl(fd, Eviocgabs(axis), ref info) >= 0;
        }

        /// <summary>Read one input_event; returns false when no event is pending (non-blocking fd).</summary>
        public static bool TryReadEvent(int fd, out InputEvent ev)
        {
            ev = default;
            var buf = Marshal.AllocHGlobal(InputEventSize);
            try
            {
                var got = (long)read(fd, buf, (IntPtr)InputEventSize);
                if (got != InputEventSize)
                    return false;
                ev = Marshal.PtrToStructure<InputEvent>(buf);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        public sealed class MouseDevice
        {
            public string Name { get; set; }
            public string EventNode { get; set; }   // e.g. /dev/input/event5 (open this)
            /// <summary>
            /// Stable identifier stored in bindings: the /dev/input/by-id symlink
            /// when available (survives reboots), otherwise the event node.
            /// </summary>
            public string DevicePath { get; set; }
            /// <summary>Supports letter keys — a real typing keyboard, not a
            /// power button / macro endpoint that only claims the kbd handler.</summary>
            public bool HasTypingKeys { get; set; }
        }

        public enum DeviceAccess
        {
            Ok,
            PermissionDenied,
            Error
        }

        /// <summary>Probe whether the current user can read an event node.</summary>
        public static DeviceAccess CheckAccess(string eventNode)
        {
            int fd = open(eventNode, O_RDONLY | O_NONBLOCK);
            if (fd >= 0)
            {
                close(fd);
                return DeviceAccess.Ok;
            }
            return Marshal.GetLastWin32Error() == EACCES ? DeviceAccess.PermissionDenied : DeviceAccess.Error;
        }

        /// <summary>
        /// Human-readable warnings when input devices exist but cannot be read
        /// (the classic failure: user not in the 'input' group — some devices
        /// still work through per-vendor udev ACLs, e.g. gaming mice, which is
        /// why "mouse works but keyboard doesn't" happens). Empty when fine.
        /// </summary>
        public static List<string> GetAccessWarnings()
        {
            var warnings = new List<string>();
            if (!OperatingSystem.IsLinux())
                return warnings;

            var deniedKeyboards = new List<string>();
            var deniedMice = new List<string>();

            foreach (var kb in EnumerateKeyboards())
            {
                if (kb.HasTypingKeys && CheckAccess(kb.EventNode) == DeviceAccess.PermissionDenied)
                    deniedKeyboards.Add(kb.Name);
            }
            foreach (var m in EnumerateMice())
            {
                if (CheckAccess(m.EventNode) == DeviceAccess.PermissionDenied)
                    deniedMice.Add(m.Name);
            }

            if (deniedKeyboards.Count > 0)
                warnings.Add($"Keyboard(s) not readable ({string.Join(", ", deniedKeyboards)}) — keyboard input will NOT work.");
            if (deniedMice.Count > 0)
                warnings.Add($"Mouse/gun device(s) not readable ({string.Join(", ", deniedMice)}).");
            if (warnings.Count > 0)
                warnings.Add("Fix: add your user to the 'input' group:  sudo usermod -aG input $USER  — then log out and back in.");
            return warnings;
        }

        /// <summary>
        /// Enumerate mouse-capable input devices by parsing
        /// /proc/bus/input/devices (devices whose handlers include a mouseN).
        /// </summary>
        public static List<MouseDevice> EnumerateMice() => EnumerateDevices("mouse");

        /// <summary>
        /// Enumerate keyboard-capable input devices (handlers include kbd).
        /// </summary>
        public static List<MouseDevice> EnumerateKeyboards() => EnumerateDevices("kbd");

        private static List<MouseDevice> EnumerateDevices(string handlerKeyword)
        {
            var result = new List<MouseDevice>();
            const string procPath = "/proc/bus/input/devices";
            if (!File.Exists(procPath))
                return result;

            var stablePaths = BuildStablePathMap();

            string currentName = null;
            MouseDevice currentDevice = null;
            foreach (var line in File.ReadAllLines(procPath))
            {
                if (line.StartsWith("N: Name="))
                {
                    currentName = line.Substring("N: Name=".Length).Trim('"');
                }
                else if (line.StartsWith("H: Handlers=") && line.Contains(handlerKeyword))
                {
                    foreach (var handler in line.Substring("H: Handlers=".Length).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (handler.StartsWith("event"))
                        {
                            var eventNode = "/dev/input/" + handler;
                            currentDevice = new MouseDevice
                            {
                                Name = currentName ?? handler,
                                EventNode = eventNode,
                                DevicePath = stablePaths.TryGetValue(eventNode, out var stable) ? stable : eventNode
                            };
                            result.Add(currentDevice);
                            break;
                        }
                    }
                }
                else if (line.StartsWith("B: KEY=") && currentDevice != null)
                {
                    // The KEY capability bitmap follows the Handlers line in each
                    // block. Real typing keyboards support letter keys; power
                    // buttons and mouse macro endpoints do not.
                    currentDevice.HasTypingKeys = KeyBitmapHasTypingKeys(line.Substring("B: KEY=".Length));
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    currentName = null;
                    currentDevice = null;
                }
            }
            return result;
        }

        /// <summary>
        /// True when the /proc KEY= capability bitmap contains the letter keys
        /// A, Z and Space (codes 30/44/57 — all within the lowest 64-bit word,
        /// which is the LAST hex group on the line).
        /// </summary>
        private static bool KeyBitmapHasTypingKeys(string bitmap)
        {
            var words = bitmap.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return false;
            if (!ulong.TryParse(words[^1], System.Globalization.NumberStyles.HexNumber, null, out var low))
                return false;
            const ulong mask = (1UL << KEY_A) | (1UL << KEY_Z) | (1UL << KEY_SPACE);
            return (low & mask) == mask;
        }

        /// <summary>Resolve an event node from a stored DevicePath (by-id symlink or event node).</summary>
        public static string ResolveEventNode(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return null;
            try
            {
                if (File.Exists(devicePath) || Directory.Exists(devicePath))
                {
                    var info = new FileInfo(devicePath);
                    var target = info.LinkTarget;
                    if (target != null)
                        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(devicePath) ?? "/dev/input", target));
                }
            }
            catch { }
            return devicePath;
        }

        /// <summary>Map event nodes to their stable /dev/input/by-id symlinks.</summary>
        private static Dictionary<string, string> BuildStablePathMap()
        {
            var map = new Dictionary<string, string>();
            const string byIdDir = "/dev/input/by-id";
            if (!Directory.Exists(byIdDir))
                return map;
            try
            {
                foreach (var link in Directory.GetFiles(byIdDir))
                {
                    var target = new FileInfo(link).LinkTarget;
                    if (target == null)
                        continue;
                    var resolved = Path.GetFullPath(Path.Combine(byIdDir, target));
                    if (resolved.Contains("/event") && !map.ContainsKey(resolved))
                        map[resolved] = link;
                }
            }
            catch { }
            return map;
        }
    }
}
