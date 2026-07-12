using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using TeknoParrotUi.Common;
using Evdev = TeknoParrotUi.Common.InputListening.Mouse.EvdevInterop;
using X11 = TeknoParrotUi.Common.InputListening.Mouse.X11Interop;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Captures gun-game mouse/keyboard bindings. On Windows this uses RawInput via
/// a dedicated Win32 message-only window (semantics match the classic WPF
/// JoystickControlRawInput exactly). On Linux, mouse buttons are captured from
/// evdev devices, producing the same RawInputButton binding shape consumed by
/// EvdevMouseListener.
/// </summary>
public sealed class RawInputCaptureService : IDisposable
{
    /// <summary>Dropdown name for the X11 fallback pointer (no readable evdev mice).</summary>
    public const string X11PointerName = "System Pointer (X11)";

    private const int WM_INPUT = 0x00FF;
    private const int WM_CLOSE = 0x0010;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private Thread? _messageThread;
    private IntPtr _hwnd;
    private WndProcDelegate? _wndProcKeepAlive;
    private bool _keyboardRegistered;
    private volatile bool _running;
    private readonly List<string> _multipleMouseList = new();
    private readonly List<string> _multipleKbList = new();
    private readonly List<Thread> _evdevThreads = new();

    /// <summary>displayName, button, isEscape (cancel request)</summary>
    public event Action<string, RawInputButton, bool>? BindingCaptured;

    public void Start(bool registerKeyboard = true)
    {
        Stop();
        _keyboardRegistered = registerKeyboard;
        if (OperatingSystem.IsLinux())
        {
            StartEvdevCapture();
            return;
        }
        if (!OperatingSystem.IsWindows())
            return;

        BuildDuplicateNameLists();

        var ready = new ManualResetEventSlim();
        _running = true;
        _messageThread = new Thread(() =>
        {
            _wndProcKeepAlive = WndProc;
            var className = "TPAvaloniaRawInputCapture_" + Guid.NewGuid().ToString("N");
            var wndClass = new WNDCLASS
            {
                lpszClassName = className,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcKeepAlive)
            };
            RegisterClassW(ref wndClass);
            _hwnd = CreateWindowExW(0, className, "", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                ready.Set();
                return;
            }

            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, _hwnd);
            if (_keyboardRegistered)
                RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, _hwnd);

            ready.Set();

            while (_running && GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }

            try
            {
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                if (_keyboardRegistered)
                    RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
            }
            catch { }
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }) { IsBackground = true };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        ready.Wait(TimeSpan.FromSeconds(3));
    }

    public void Stop()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
            PostMessageW(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _messageThread?.Join(2000);
        _messageThread = null;
        foreach (var t in _evdevThreads)
            t.Join(1000);
        _evdevThreads.Clear();
    }

    public void Dispose() => Stop();

    // ---------- Linux (evdev) ----------

    private void StartEvdevCapture()
    {
        _running = true;
        bool anyMouse = false;
        foreach (var device in Evdev.EnumerateMice())
        {
            if (Evdev.CheckAccess(device.EventNode) != Evdev.DeviceAccess.Ok)
                continue;
            anyMouse = true;
            var dev = device;
            var thread = new Thread(() => EvdevCaptureLoop(dev)) { IsBackground = true };
            thread.Start();
            _evdevThreads.Add(thread);
        }
        bool anyKeyboard = false;
        if (_keyboardRegistered)
        {
            // Real typing keyboards only — power buttons and gaming-mouse macro
            // endpoints also claim the kbd handler but never emit typing keys.
            foreach (var device in Evdev.EnumerateKeyboards().Where(k => k.HasTypingKeys))
            {
                if (Evdev.CheckAccess(device.EventNode) != Evdev.DeviceAccess.Ok)
                    continue;
                anyKeyboard = true;
                var dev = device;
                var thread = new Thread(() => EvdevKeyboardCaptureLoop(dev)) { IsBackground = true };
                thread.Start();
                _evdevThreads.Add(thread);
            }
        }

        // Permission fallback: no readable evdev device → capture via X server
        // polling (same mechanism as X11FallbackInputListener). Single pointer,
        // DevicePath "X11" — served by the fallback listener at game time.
        if ((!anyMouse || (_keyboardRegistered && !anyKeyboard)) && X11.IsAvailable())
        {
            bool captureMouse = !anyMouse;
            bool captureKeys = _keyboardRegistered && !anyKeyboard;
            var thread = new Thread(() => X11CaptureLoop(captureMouse, captureKeys)) { IsBackground = true };
            thread.Start();
            _evdevThreads.Add(thread);
        }
    }

    private void X11CaptureLoop(bool captureMouse, bool captureKeys)
    {
        var display = X11.XOpenDisplay(null!);
        if (display == IntPtr.Zero)
            return;
        try
        {
            var root = X11.XDefaultRootWindow(display);
            uint prevMask = 0;
            var keymap = new byte[32];
            var prevKeymap = new byte[32];
            bool first = true;

            while (_running)
            {
                if (captureMouse &&
                    X11.XQueryPointer(display, root, out _, out _, out _, out _, out _, out _, out uint mask))
                {
                    EmitX11ButtonEdge(mask, prevMask, X11.Button1Mask, RawMouseButton.LeftButton);
                    EmitX11ButtonEdge(mask, prevMask, X11.Button3Mask, RawMouseButton.RightButton);
                    EmitX11ButtonEdge(mask, prevMask, X11.Button2Mask, RawMouseButton.MiddleButton);
                    prevMask = mask;
                }

                if (captureKeys)
                {
                    X11.XQueryKeymap(display, keymap);
                    if (!first)
                    {
                        for (int keycode = 8; keycode < 256; keycode++)
                        {
                            bool now = (keymap[keycode >> 3] & (1 << (keycode & 7))) != 0;
                            bool before = (prevKeymap[keycode >> 3] & (1 << (keycode & 7))) != 0;
                            if (!now || before)
                                continue;
                            var key = TeknoParrotUi.Common.InputListening.Keyboard.EvdevKeyMap.ToKeys((ushort)(keycode - 8));
                            if (key == Keys.None)
                                continue;
                            var button = new RawInputButton
                            {
                                DevicePath = "X11",
                                DeviceType = RawDeviceType.Keyboard,
                                MouseButton = RawMouseButton.None,
                                KeyboardKey = key
                            };
                            BindingCaptured?.Invoke($"Keyboard {key}", button, key == Keys.Escape);
                        }
                    }
                    Buffer.BlockCopy(keymap, 0, prevKeymap, 0, 32);
                    first = false;
                }

                Thread.Sleep(4);
            }
        }
        finally
        {
            X11.XCloseDisplay(display);
        }
    }

    private void EmitX11ButtonEdge(uint mask, uint prevMask, uint bit, RawMouseButton mouseButton)
    {
        if ((mask & bit) == 0 || (prevMask & bit) != 0)
            return; // capture on press edge only, like evdev capture
        var button = new RawInputButton
        {
            DevicePath = "X11",
            DeviceType = RawDeviceType.Mouse,
            MouseButton = mouseButton,
            KeyboardKey = Keys.None
        };
        BindingCaptured?.Invoke($"Mouse {mouseButton}", button, false);
    }

    /// <summary>
    /// Linux: warnings when input devices exist but are unreadable (user not in
    /// the 'input' group). Empty on other platforms or when everything is fine.
    /// Shown by binding editors so the problem is visible where the user is.
    /// </summary>
    public IReadOnlyList<string> GetAccessWarnings() =>
        OperatingSystem.IsLinux() ? Evdev.GetAccessWarnings() : new List<string>();

    private void EvdevKeyboardCaptureLoop(Evdev.MouseDevice device)
    {
        int fd = Evdev.open(device.EventNode, Evdev.O_RDONLY | Evdev.O_NONBLOCK);
        if (fd < 0)
            return;
        try
        {
            while (_running)
            {
                bool any = false;
                while (Evdev.TryReadEvent(fd, out var ev))
                {
                    any = true;
                    if (ev.Type != Evdev.EV_KEY || ev.Value != 1)
                        continue;
                    var key = TeknoParrotUi.Common.InputListening.Keyboard.EvdevKeyMap.ToKeys(ev.Code);
                    if (key == Keys.None)
                        continue;

                    var button = new RawInputButton
                    {
                        DevicePath = device.DevicePath,
                        DeviceType = RawDeviceType.Keyboard,
                        MouseButton = RawMouseButton.None,
                        KeyboardKey = key
                    };
                    BindingCaptured?.Invoke($"{device.Name} {key}", button, key == Keys.Escape);
                }
                if (!any)
                    Thread.Sleep(5);
            }
        }
        finally
        {
            Evdev.close(fd);
        }
    }

    private void EvdevCaptureLoop(Evdev.MouseDevice device)
    {
        int fd = Evdev.open(device.EventNode, Evdev.O_RDONLY | Evdev.O_NONBLOCK);
        if (fd < 0)
            return;
        try
        {
            while (_running)
            {
                bool any = false;
                while (Evdev.TryReadEvent(fd, out var ev))
                {
                    any = true;
                    if (ev.Type != Evdev.EV_KEY || ev.Value == 0)
                        continue;

                    var mouseButton = ev.Code switch
                    {
                        Evdev.BTN_LEFT => RawMouseButton.LeftButton,
                        Evdev.BTN_TOUCH => RawMouseButton.LeftButton,
                        Evdev.BTN_RIGHT => RawMouseButton.RightButton,
                        Evdev.BTN_MIDDLE => RawMouseButton.MiddleButton,
                        Evdev.BTN_SIDE => RawMouseButton.Button4,
                        Evdev.BTN_EXTRA => RawMouseButton.Button5,
                        _ => RawMouseButton.None
                    };
                    if (mouseButton == RawMouseButton.None)
                        continue;

                    var button = new RawInputButton
                    {
                        DevicePath = device.DevicePath,
                        DeviceType = RawDeviceType.Mouse,
                        MouseButton = mouseButton,
                        KeyboardKey = Keys.None
                    };
                    BindingCaptured?.Invoke($"{device.Name} {mouseButton}", button, false);
                }
                if (!any)
                    Thread.Sleep(5);
            }
        }
        finally
        {
            Evdev.close(fd);
        }
    }

    /// <summary>
    /// Fancy names of all connected RawInput mice (lightguns enumerate as mice) —
    /// used by the lightgun/trackball device dropdown, same as the classic UI.
    /// </summary>
    public List<string> GetMouseDeviceList()
    {
        if (OperatingSystem.IsLinux())
        {
            var mice = Evdev.EnumerateMice()
                .Where(m => Evdev.CheckAccess(m.EventNode) == Evdev.DeviceAccess.Ok)
                .Select(m => m.Name).ToList();
            if (mice.Count == 0 && X11.IsAvailable())
                mice.Add(X11PointerName);
            return mice;
        }
        if (!OperatingSystem.IsWindows())
            return new List<string>();
        BuildDuplicateNameLists();
        return RawInputDevice.GetDevices().OfType<RawInputMouse>()
            .Select(GetFancyDeviceName)
            .ToList();
    }

    /// <summary>Device path for a fancy device name, or null when unplugged.</summary>
    public string? GetMouseDevicePathByName(string deviceName)
    {
        if (OperatingSystem.IsLinux())
        {
            if (deviceName == X11PointerName)
                return "X11";
            return Evdev.EnumerateMice().FirstOrDefault(m => m.Name == deviceName)?.DevicePath;
        }
        if (!OperatingSystem.IsWindows())
            return null;
        BuildDuplicateNameLists();
        foreach (var device in RawInputDevice.GetDevices().OfType<RawInputMouse>())
        {
            if (GetFancyDeviceName(device) == deviceName)
                return device.DevicePath;
        }
        return null;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUT)
        {
            try
            {
                HandleInput(RawInputData.FromHandle(lParam));
            }
            catch
            {
                // malformed input packet
            }
            return IntPtr.Zero;
        }
        if (msg == WM_CLOSE)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void HandleInput(RawInputData data)
    {
        switch (data)
        {
            case RawInputMouseData mouse:
                if (mouse.Mouse.Buttons != RawMouseButtonFlags.None && !mouse.Mouse.Buttons.ToString().Contains("Up"))
                {
                    var mouseButton = GetButtonFromFlags(mouse.Mouse.Buttons);
                    if (mouseButton != RawMouseButton.None)
                    {
                        var button = new RawInputButton
                        {
                            DevicePath = mouse.Device?.DevicePath ?? "null",
                            DeviceType = RawDeviceType.Mouse,
                            MouseButton = mouseButton,
                            KeyboardKey = Keys.None
                        };
                        BindingCaptured?.Invoke($"{GetFancyDeviceName(mouse.Device)} {mouseButton}", button, false);
                    }
                }
                break;

            case RawInputKeyboardData keyboard:
                if (_keyboardRegistered)
                {
                    var key = (Keys)keyboard.Keyboard.VirutalKey;
                    var button = new RawInputButton
                    {
                        DevicePath = keyboard.Device?.DevicePath ?? "null",
                        DeviceType = RawDeviceType.Keyboard,
                        MouseButton = RawMouseButton.None,
                        KeyboardKey = key
                    };
                    BindingCaptured?.Invoke($"{GetFancyDeviceName(keyboard.Device)} {key}", button, key == Keys.Escape);
                }
                break;
        }
    }

    private static RawMouseButton GetButtonFromFlags(RawMouseButtonFlags flags)
    {
        if (flags.HasFlag(RawMouseButtonFlags.LeftButtonDown)) return RawMouseButton.LeftButton;
        if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown)) return RawMouseButton.RightButton;
        if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown)) return RawMouseButton.MiddleButton;
        if (flags.HasFlag(RawMouseButtonFlags.Button4Down)) return RawMouseButton.Button4;
        if (flags.HasFlag(RawMouseButtonFlags.Button5Down)) return RawMouseButton.Button5;
        return RawMouseButton.None;
    }

    private void BuildDuplicateNameLists()
    {
        _multipleMouseList.Clear();
        var seen = new List<string>();
        foreach (var device in RawInputDevice.GetDevices().OfType<RawInputMouse>())
        {
            var name = GetFancyDeviceName(device);
            if (seen.Contains(name)) _multipleMouseList.Add(name);
            else seen.Add(name);
        }

        _multipleKbList.Clear();
        seen.Clear();
        foreach (var device in RawInputDevice.GetDevices().OfType<RawInputKeyboard>())
        {
            var name = GetFancyDeviceName(device);
            if (seen.Contains(name)) _multipleKbList.Add(name);
            else seen.Add(name);
        }
    }

    private string GetFancyDeviceName(RawInputDevice? device)
    {
        if (device == null)
            return "Unknown Device";

        // Well-known lightgun hardware first (same as the classic UI)
        if (device.DevicePath != null)
        {
            // Aimtrak
            if (device.VendorId == 0xD209 && device.ProductId >= 0x1601 && device.ProductId <= 0x1608)
                return $"Ultimarc AimTrak #{device.ProductId - 0x1600}";
            // Sinden
            if (device.VendorId == 0x16C0)
            {
                if (device.ProductId == 0x0F01) return "Sinden Lightgun Blue";
                if (device.ProductId == 0x0F02) return "Sinden Lightgun Red";
                if (device.ProductId == 0x0F38) return "Sinden Lightgun Black";
                if (device.ProductId == 0x0F39) return "Sinden Lightgun Player 2";
            }
            // DolphinBar — always CRC-suffixed since every bar reports the same name
            if (device.VendorId == 0x0079 && device.ProductId == 0x1802)
            {
                var pathParts = device.DevicePath.Split('#');
                if (pathParts.Length > 2)
                {
                    var pathSubParts = pathParts[2].Split('&');
                    if (pathSubParts.Length > 1)
                        return "Mayflash DolphinBar " + pathSubParts[1].ToUpperInvariant();
                }
            }
        }

        string productName = "", manufacturerName = "";
        try { productName = device.ProductName?.Trim() ?? ""; } catch { }
        try { manufacturerName = device.ManufacturerName?.Trim() ?? ""; } catch { }

        if (manufacturerName == "") manufacturerName = "Unknown Manufacturer";
        if (productName == "") productName = "Unknown Product";

        string fancyName;
        if (manufacturerName == "(Standard keyboards)" || productName.Contains(manufacturerName))
            fancyName = productName;
        else if (device.DevicePath != null && device.DevicePath.Contains("Microsoft Mouse RID"))
            fancyName = "Emulated Device";
        else
            fancyName = $"{manufacturerName} {productName}";

        if (device.DevicePath != null &&
            ((device.DeviceType == RawInputDeviceType.Mouse && _multipleMouseList.Contains(fancyName)) ||
             (device.DeviceType == RawInputDeviceType.Keyboard && _multipleKbList.Contains(fancyName))))
        {
            var parts = device.DevicePath.Split('#');
            if (parts.Length > 2)
            {
                var subParts = parts[2].Split('&');
                if (subParts.Length > 1)
                    fancyName += " " + subParts[1].ToUpperInvariant();
            }
        }

        return fancyName;
    }

    // ---------- Win32 interop ----------

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);
}
