using System;
using System.Runtime.InteropServices;
using System.Threading;
using Linearstar.Windows.RawInput;
using TeknoParrotUi.Common.InputListening;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// A hidden Win32 message-only window that registers for RawInput and forwards
    /// WM_INPUT messages to an InputListener. Replaces the WPF HwndSource hook for
    /// frontends that do not expose a Win32 WndProc (e.g. Avalonia).
    /// </summary>
    public sealed class RawInputForwardWindow : IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const int WM_CLOSE = 0x0010;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private readonly InputListener _listener;
        private Thread _messageThread;
        private IntPtr _hwnd;
        private WndProcDelegate _wndProcKeepAlive;
        private volatile bool _running;

        public RawInputForwardWindow(InputListener listener)
        {
            _listener = listener;
        }

        public void Start()
        {
            Stop();
            var ready = new ManualResetEventSlim();
            _running = true;
            _messageThread = new Thread(() =>
            {
                _wndProcKeepAlive = WndProc;
                var className = "TPRawInputForward_" + Guid.NewGuid().ToString("N");
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

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                bool handled = false;
                _listener?.WndProcReceived(hWnd, (int)msg, wParam, lParam, ref handled);
                return IntPtr.Zero;
            }
            if (msg == WM_CLOSE)
            {
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

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
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
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
}
