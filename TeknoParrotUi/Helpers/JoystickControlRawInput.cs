using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Helpers
{
    public class JoystickControlRawInput
    {
        private TextBox _lastActiveTextBox;
        private HwndSource _source;
        private readonly List<string> _multipleMouseList = new List<string>();
        private readonly List<string> _multipleKBList = new List<string>();

        public void Listen()
        {
            var hWnd = new WindowInteropHelper(Application.Current.MainWindow ?? throw new InvalidOperationException()).EnsureHandle();

            _source = HwndSource.FromHwnd(hWnd);
            _source.AddHook(WndProcHook);

            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, hWnd);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, hWnd);

            // Create a list of devices that have the same name.
            // These will be checked in GetFancyName() and get a unique CRC added.
            // So, only devices that have the same name will get modified with a CRC, leaving all unique devices without a CRC.
            // This way game configs before this change will not be affected.
            // Mayflash DolphinBar will retain its current behavior of always having a unique CRC added.
            _multipleMouseList.Clear();
            var mice = RawInputDevice.GetDevices().OfType<RawInputMouse>();
            List<string> deviceList = new List<string>();
            foreach (var device in mice)
            {
                string name = GetFancyDeviceName(device);
                if (deviceList.Contains(name))
                    _multipleMouseList.Add(name);
                else
                    deviceList.Add(name);
            }
            _multipleKBList.Clear();
            var kb = RawInputDevice.GetDevices().OfType<RawInputKeyboard>();
            deviceList.Clear(); ;
            foreach (var device in kb)
            {
                string name = GetFancyDeviceName(device);
                if (deviceList.Contains(name))
                    _multipleKBList.Add(name);
                else
                    deviceList.Add(name);
            }

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

                if (device.DevicePath != null)
                {
                    if ((device.DeviceType == RawInputDeviceType.Mouse && _multipleMouseList.Contains(fancyName)) || (device.DeviceType == RawInputDeviceType.Keyboard && _multipleKBList.Contains(fancyName)))
                        fancyName += " " + device.DevicePath.Split('#')[2].Split('&')[1].ToUpper(); // CRC part of path should be unique
                }

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
                    case RawInputKeyboardData keyboard:
                        SetTextBoxText(String.Format("{0} {1}", GetFancyDeviceName(keyboard.Device), (Keys)keyboard.Keyboard.VirutalKey), data);
                        break;
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
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
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
                            KeyboardKey = Keys.None
                        };

                        if (data is RawInputMouseData)
                        {
                            RawInputMouseData mouse = data as RawInputMouseData;
                            button.MouseButton = GetButtonFromFlags(mouse.Mouse.Buttons);
                            button.DeviceType = RawDeviceType.Mouse;
                        }
                        else if (data is RawInputKeyboardData)
                        {
                            RawInputKeyboardData kb = data as RawInputKeyboardData;
                            button.KeyboardKey = (Keys)kb.Keyboard.VirutalKey;
                            button.DeviceType = RawDeviceType.Keyboard;

                            if (button.KeyboardKey == Keys.Escape)
                                save = false;
                        }

                        // Save?
                        if (save)
                        {
                            txt.ToolTip = text;
                            txt.Text = text;

                            var t = txt.Tag as JoystickButtons;
                            t.RawInputButton = button;
                            t.BindNameRi = text;
                        }
                        else
                        {
                            txt.ToolTip = "";
                            txt.Text = "";
                        }
                        
                        // Unfocus textbox
                        Keyboard.ClearFocus();
                        FocusManager.SetFocusedElement(Application.Current.Windows[0], null);
                        _lastActiveTextBox = null;
                    }
                    else
                    {
                        _lastActiveTextBox = txt;
                    }
                }));
        }

        /// <summary>
        /// Gets active text box.
        /// </summary>
        /// <returns></returns>
        private TextBox GetActiveTextBox()
        {
            IInputElement focusedControl = FocusManager.GetFocusedElement(Application.Current.Windows[0]);

            if (focusedControl == null)
                return null;

            if (focusedControl.GetType() == typeof(TextBox))
            {
                var txt = (TextBox)focusedControl;
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
            _source?.RemoveHook(WndProcHook);
        }
    }
}
