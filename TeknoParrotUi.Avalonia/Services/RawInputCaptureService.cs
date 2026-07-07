using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Captures RawInput keyboard/mouse bindings using a dedicated Win32 message-only
/// window (independent of the UI framework). Binding semantics match the classic
/// WPF JoystickControlRawInput exactly.
/// </summary>
public sealed class RawInputCaptureService : IDisposable
{
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

    /// <summary>displayName, button, isEscape (cancel request)</summary>
    public event Action<string, RawInputButton, bool>? BindingCaptured;

    public void Start(bool registerKeyboard = true)
    {
        Stop();
        if (!OperatingSystem.IsWindows())
            return;

        _keyboardRegistered = registerKeyboard;
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
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Fancy names of all connected RawInput mice (lightguns enumerate as mice) —
    /// used by the lightgun/trackball device dropdown, same as the classic UI.
    /// </summary>
    public List<string> GetMouseDeviceList()
    {
        BuildDuplicateNameLists();
        return RawInputDevice.GetDevices().OfType<RawInputMouse>()
            .Select(GetFancyDeviceName)
            .ToList();
    }

    /// <summary>Device path for a fancy device name, or null when unplugged.</summary>
    public string? GetMouseDevicePathByName(string deviceName)
    {
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
