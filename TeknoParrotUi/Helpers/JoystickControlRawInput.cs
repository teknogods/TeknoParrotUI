using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using TeknoParrotUi.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Runtime.InteropServices;
// using Keys = System.Windows.Forms.Keys;
using Avalonia.Platform; // Add this for platform-specific APIs
namespace TeknoParrotUi.Helpers
{
    public class JoystickControlRawInput
    {
        private TextBox _lastActiveTextBox;
        private IntPtr _windowHandle;
        // Platform invoke methods for window handle management
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr _oldWndProc;
        private const int GWL_WNDPROC = -4;
        private const int WM_INPUT = 0x00FF;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate _wndProcDelegate;

        public void Listen()
        {
            // Get the window handle from Avalonia's platform-specific API
            var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            IntPtr? platformHandle = null;
            if (topLevel?.PlatformImpl != null)
            {
                // Try to get the native window handle using PlatformImpl.Handle
                var platformImpl = topLevel.PlatformImpl;
                var handleImpl = platformImpl.GetType().GetProperty("Handle")?.GetValue(platformImpl);
                if (handleImpl != null)
                {
                    var handleProp = handleImpl.GetType().GetProperty("Handle");
                    if (handleProp != null)
                    {
                        platformHandle = (IntPtr)handleProp.GetValue(handleImpl);
                    }
                }
            }

            // Alternative approach using WindowBaseImpl reflection (if needed)
            if (platformHandle == null || platformHandle == IntPtr.Zero)
            {
                try
                {
                    var windowImpl = topLevel?.PlatformImpl;
                    var handleField = windowImpl?.GetType().GetField("_handle",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (handleField != null)
                    {
                        var handle = handleField.GetValue(windowImpl);
                        if (handle != null)
                        {
                            var handleProp = handle.GetType().GetProperty("Handle");
                            if (handleProp != null)
                            {
                                platformHandle = (IntPtr)handleProp.GetValue(handle);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get window handle via reflection: {ex.Message}");
                }
            }

            if (platformHandle == IntPtr.Zero || platformHandle == null)
            {
                Console.WriteLine("Failed to get window handle");
                return;
            }

            _windowHandle = platformHandle.Value; // or just platformHandle if non-nullable

            // Set up the WndProc hook using P/Invoke
            _wndProcDelegate = new WndProcDelegate(WndProc);
            _oldWndProc = SetWindowLongPtr(_windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            // Register devices 
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, _windowHandle);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, _windowHandle);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(lParam);

                switch (data)
                {
                    case RawInputMouseData mouse:
                        if (mouse.Mouse.Buttons != RawMouseButtonFlags.None && !mouse.Mouse.Buttons.ToString().Contains("Up"))
                        {
                            var button = GetButtonFromFlags(mouse.Mouse.Buttons);

                            if (button != RawMouseButton.None)
                                SetTextBoxText(String.Format("{0} {1}", GetFancyDeviceName(mouse.Device), button), data);
                        }
                        break;
                        // TODO: FIX
                        // case RawInputKeyboardData keyboard:
                        //     SetTextBoxText(String.Format("{0} {1}", GetFancyDeviceName(keyboard.Device), (Keys)keyboard.Keyboard.VirutalKey), data);
                        //     break;
                }
            }

            return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        }

        public List<string> GetMouseDeviceList()
        {
            var mice = RawInputDevice.GetDevices().OfType<RawInputMouse>();

            List<string> deviceList = new List<string>();

            foreach (var device in mice)
                deviceList.Add(GetFancyDeviceName(device));

            return deviceList;
        }

        public RawInputDevice GetMouseDeviceByName(string deviceName)
        {
            var mice = RawInputDevice.GetDevices().OfType<RawInputMouse>();

            foreach (var device in mice)
            {
                if (GetFancyDeviceName(device) == deviceName)
                    return device;
            }

            return null;
        }

        public RawInputDevice GetMouseDeviceByBindName(string bindName)
        {
            var mice = RawInputDevice.GetDevices().OfType<RawInputMouse>();

            foreach (var device in mice)
            {
                if (bindName.StartsWith(GetFancyDeviceName(device)))
                    return device;
            }

            return null;
        }

        public RawInputDevice GetKeyboardDeviceByBindName(string bindName)
        {
            var keyboards = RawInputDevice.GetDevices().OfType<RawInputKeyboard>();

            foreach (var device in keyboards)
            {
                if (bindName.StartsWith(GetFancyDeviceName(device)))
                    return device;
            }

            return null;
        }

        private string GetFancyDeviceName(RawInputDevice device)
        {
            string fancyName = "";

            if (device == null)
                return "Unknown Device";

            if (device.DevicePath != null)
            {
                // Aimtrak
                if (device.VendorId == 0xD209 && device.ProductId >= 0x1601 && device.ProductId <= 0x1608)
                {
                    fancyName = String.Format("Ultimarc AimTrak #{0}", device.ProductId - 0x1600);
                }
                // Sinden
                else if (device.VendorId == 0x16C0)
                {
                    if (device.ProductId == 0x0F01)
                        fancyName = "Sinden Lightgun Blue";
                    else if (device.ProductId == 0x0F02)
                        fancyName = "Sinden Lightgun Red";
                    else if (device.ProductId == 0x0F38)
                        fancyName = "Sinden Lightgun Black";
                    else if (device.ProductId == 0x0F39)
                        fancyName = "Sinden Lightgun Player 2";
                }
                // DolphinBar
                else if (device.VendorId == 0x0079 && device.ProductId == 0x1802)
                {
                    fancyName = "Mayflash DolphinBar " + device.DevicePath.Split('#')[2].Split('&')[1].ToUpper(); // CRC part of path should be unique
                }
            }

            // Other
            if (fancyName == "")
            {
                string manufacturerName = "";
                string productName = "";

                // Try to get names from device
                try
                {
                    productName = device.ProductName?.Trim() ?? "";
                }
                catch
                {
                }

                try
                {
                    manufacturerName = device.ManufacturerName?.Trim() ?? "";
                }
                catch
                {
                }

                if (manufacturerName == "")
                    manufacturerName = "Unknown Manufacturer";

                if (productName == "")
                    productName = "Unknown Product";

                if (manufacturerName == "(Standard keyboards)" || productName.Contains(manufacturerName))
                    fancyName = productName; // Omit the manufacturer name
                else if (device.DevicePath != null && device.DevicePath.Contains("Microsoft Mouse RID"))
                    fancyName = "Emulated Device";
                else
                    fancyName = String.Format("{0} {1}", manufacturerName, productName); // Combined manufacturer / product name
            }

            return fancyName;
        }

        // Get first pressed button from flags
        private RawMouseButton GetButtonFromFlags(RawMouseButtonFlags flags)
        {
            if (flags.HasFlag(RawMouseButtonFlags.LeftButtonDown))
                return RawMouseButton.LeftButton;
            else if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown))
                return RawMouseButton.RightButton;
            else if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown))
                return RawMouseButton.MiddleButton;
            else if (flags.HasFlag(RawMouseButtonFlags.Button4Down))
                return RawMouseButton.Button4;
            else if (flags.HasFlag(RawMouseButtonFlags.Button5Down))
                return RawMouseButton.Button5;
            else
                return RawMouseButton.None;
        }

        private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;

            if (msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(lParam);

                switch (data)
                {
                    case RawInputMouseData mouse:
                        if (mouse.Mouse.Buttons != RawMouseButtonFlags.None && !mouse.Mouse.Buttons.ToString().Contains("Up"))
                        {
                            var button = GetButtonFromFlags(mouse.Mouse.Buttons);

                            if (button != RawMouseButton.None)
                                SetTextBoxText(String.Format("{0} {1}", GetFancyDeviceName(mouse.Device), button), data);
                        }
                        break;
                        // TODO: FIX
                        // case RawInputKeyboardData keyboard:
                        //     SetTextBoxText(String.Format("{0} {1}", GetFancyDeviceName(keyboard.Device), (Keys)keyboard.Keyboard.VirutalKey), data);
                        //     break;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Sets text box text and tag.
        /// </summary>
        /// <param name="key"></param>
        private void SetTextBoxText(string text, RawInputData data)
        {
            Dispatcher.UIThread.Post(() =>
            {
                bool save = true;
                var txt = GetActiveTextBox();

                if (txt == null)
                    return;

                // Ignore first
                if (txt == _lastActiveTextBox)
                {
                    string path = "null";

                    if (data != null && data.Device != null && data.Device.DevicePath != null)
                    {
                        path = data.Device.DevicePath;
                    }

                    var button = new RawInputButton
                    {
                        DevicePath = path,
                        DeviceType = RawDeviceType.None,
                        MouseButton = RawMouseButton.None,
                        // TODO: FIX
                        // KeyboardKey = Keys.None
                    };

                    if (data is RawInputMouseData mouse)
                    {
                        button.MouseButton = GetButtonFromFlags(mouse.Mouse.Buttons);
                        button.DeviceType = RawDeviceType.Mouse;
                    }
                    else if (data is RawInputKeyboardData kb)
                    {
                        // TODO: FIX
                        // button.KeyboardKey = (Keys)kb.Keyboard.VirutalKey;
                        button.DeviceType = RawDeviceType.Keyboard;
                        // TODO: FIX
                        // if (button.KeyboardKey == Keys.Escape)
                        //     save = false;
                    }

                    // Save?
                    if (save)
                    {
                        ToolTip.SetTip(txt, text);
                        txt.Text = text;

                        var t = txt.Tag as JoystickButtons;
                        t.RawInputButton = button;
                        t.BindNameRi = text;
                    }
                    else
                    {
                        ToolTip.SetTip(txt, "");
                        txt.Text = "";
                    }

                    // Unfocus textbox using Avalonia's API
                    txt.Focusable = false;
                    txt.Focusable = true;
                    var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    topLevel?.Focus();
                    _lastActiveTextBox = null;
                }
                else
                {
                    _lastActiveTextBox = txt;
                }
            });
        }

        /// <summary>
        /// Gets active text box.
        /// </summary>
        /// <returns></returns>
        private TextBox GetActiveTextBox()
        {
            // Get the focused element using Avalonia's API
            var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var focusedControl = topLevel?.FocusManager.GetFocusedElement();

            if (focusedControl == null)
                return null;

            if (focusedControl is TextBox txt)
            {
                var tag = txt.Tag as string;
                if (tag != "SettingsTxt")
                    return txt;
            }

            return null;
        }

        public void StopListening()
        {
            RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
            RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);

            // Restore the original WndProc
            if (_oldWndProc != IntPtr.Zero && _windowHandle != IntPtr.Zero)
            {
                SetWindowLongPtr(_windowHandle, GWL_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }
    }
}
