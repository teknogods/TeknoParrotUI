using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using TeknoParrotUi.Common.Jvs;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputListenerRawInput
    {
        public static bool KillMe;
        private List<JoystickButtons> _joystickButtons;
        private float _minX;
        private float _maxX;
        private float _minY;
        private float _maxY;
        private bool _invertedMouseAxis;
        private bool _isLuigisMansion = false;
        private bool _isTransformers = false;

        private bool _windowed;
        readonly List<string> _hookedWindows;
        private bool _windowFound;
        private bool _windowFocus;
        private IntPtr _windowHandle;
        private int _windowHeight;
        private int _windowWidth;
        private int _windowLocationX;
        private int _windowLocationY;

        // Unmanaged stuff
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        public InputListenerRawInput()
        {
            _hookedWindows = File.Exists(Path.Combine(Lazydata.UiPath, "HookedWindows.txt")) ? File.ReadAllLines(Path.Combine(Lazydata.UiPath, "HookedWindows.txt")).ToList() : new List<string>();
        }

        private bool isHookableWindow(string windowTitle)
        {
            for (int i = 0; i < _hookedWindows.Count; i++)
            {
                if (windowTitle == _hookedWindows[i])
                    return true;
            }

            return false;
        }

        private IntPtr GetWindowInformation()
        {
            foreach (Process pList in Process.GetProcesses())
            {
                var windowTitle = pList.MainWindowTitle;

                if (isHookableWindow(windowTitle))
                    return pList.MainWindowHandle;
            }

            return IntPtr.Zero;
        }

        public void ListenRawInput(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            _minX = gameProfile.xAxisMin;
            _maxX = gameProfile.xAxisMax;
            _minY = gameProfile.yAxisMin;
            _maxY = gameProfile.yAxisMax;
            _windowed = gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") || gameProfile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
            _invertedMouseAxis = gameProfile.InvertedMouseAxis;

            if (gameProfile.EmulationProfile == EmulationProfile.LuigisMansion)
                _isLuigisMansion = true;

            if (gameProfile.FileName.Contains("Transformers"))
                _isTransformers = true;

            if (!joystickButtons.Any())
                return;

            // Only configured buttons
            _joystickButtons = joystickButtons.Where(x => x?.RawInputButton != null).ToList();

            while (!KillMe)
            {
                if (!_windowFound)
                {
                    // Look for hookable window
                    var ptr = GetWindowInformation();
                    if (ptr != IntPtr.Zero)
                    {
                        _windowHandle = ptr;
                        _windowFound = true;
                        continue;
                    }
                }
                else
                {
                    // Check if window still exists
                    if (!IsWindow(_windowHandle))
                    {
                        _windowHandle = IntPtr.Zero;
                        _windowFound = false;
                        continue;
                    }

                    // Only update when we are on the foreground
                    if (_windowHandle == GetForegroundWindow())
                    {
                        _windowFocus = true;

                        RECT clientRect = new RECT();
                        GetClientRect(_windowHandle, ref clientRect);

                        _windowHeight = clientRect.Bottom;
                        _windowWidth = clientRect.Right;

                        RECT windowRect = new RECT();
                        GetWindowRect(_windowHandle, ref windowRect);

                        var border = (windowRect.Right - windowRect.Left - _windowWidth) / 2;
                        _windowLocationX = windowRect.Left + border;
                        _windowLocationY = windowRect.Bottom - _windowHeight - border;

                        RECT clipRect = new RECT();
                        clipRect.Left = _windowLocationX;
                        clipRect.Top = _windowLocationY;
                        clipRect.Right = _windowLocationX + _windowWidth;
                        clipRect.Bottom = _windowLocationY + _windowHeight;

                        ClipCursor(ref clipRect);
                    }
                    else
                    {
                        _windowFocus = false;
                        Thread.Sleep(100);
                        continue;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;

            if (msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(lParam);

                int vid = 0;
                int pid = 0;

                if (data != null && data.Device != null)
                {
                    vid = data.Device.VendorId;
                    pid = data.Device.ProductId;
                }

                switch (data)
                {
                    case RawInputMouseData mouse:
                        // Handle mouse button presses
                        if (mouse.Mouse.Buttons != RawMouseButtonFlags.None)
                        {
                            RawMouseButtonFlags flags = mouse.Mouse.Buttons;

                            // Multiple buttons can be pressed/released in single event so check them all
                            if (flags.HasFlag(RawMouseButtonFlags.LeftButtonDown) || flags.HasFlag(RawMouseButtonFlags.LeftButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.LeftButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.LeftButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown) || flags.HasFlag(RawMouseButtonFlags.RightButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.RightButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.RightButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown) || flags.HasFlag(RawMouseButtonFlags.MiddleButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.MiddleButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button4Down) || flags.HasFlag(RawMouseButtonFlags.Button4Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button4))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button4Down));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button5Down) || flags.HasFlag(RawMouseButtonFlags.Button5Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button5))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button5Down));
                            }
                        }

                        // Handle position
                        if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveAbsolute))
                        {
                            // Lightgun
                            foreach (var gun in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun)))
                                HandleRawInputGun(gun, mouse.Mouse.LastX, mouse.Mouse.LastY, true);
                        }
                        else if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveRelative))
                        {
                            // Windows mouse cursor
                            foreach (var gun in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == 0 && btn.RawInputButton.DevicePid == 0 && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun)))
                                HandleRawInputGun(gun, Cursor.Position.X, Cursor.Position.Y, false);
                        }

                        break;
                    case RawInputKeyboardData keyboard:
                        foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Keyboard && btn.RawInputButton.KeyboardKey == (Keys)keyboard.Keyboard.VirutalKey))
                            HandleRawInputButton(jsButton, keyboard.Keyboard.Flags == RawKeyboardFlags.Down);

                        break;
                }
            }
        }

        private void HandleRawInputButton(JoystickButtons joystickButton, bool pressed)
        {
            // Ignore when alt+tabbed
            if (!_windowFocus && pressed)
                return;

            switch (joystickButton.InputMapping)
            {
                case InputMapping.Test:
                    InputCode.PlayerDigitalButtons[0].Test = pressed;
                    break;
                case InputMapping.Service1:
                    InputCode.PlayerDigitalButtons[0].Service = pressed;
                    break;
                case InputMapping.Service2:
                    InputCode.PlayerDigitalButtons[1].Service = pressed;
                    break;
                case InputMapping.Coin1:
                    InputCode.PlayerDigitalButtons[0].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(0);
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(1);
                    break;
                // P1
                case InputMapping.P1ButtonStart:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
                case InputMapping.P1Button1:
                    InputCode.PlayerDigitalButtons[0].Button1 = pressed;
                    break;
                case InputMapping.P1Button2:
                    InputCode.PlayerDigitalButtons[0].Button2 = pressed;
                    break;
                case InputMapping.P1Button3:
                    InputCode.PlayerDigitalButtons[0].Button3 = pressed;
                    break;
                case InputMapping.P1Button4:
                    InputCode.PlayerDigitalButtons[0].Button4 = pressed;
                    break;
                case InputMapping.P1Button5:
                    InputCode.PlayerDigitalButtons[0].Button5 = pressed;
                    break;
                case InputMapping.P1Button6:
                    InputCode.PlayerDigitalButtons[0].Button6 = pressed;
                    break;
                case InputMapping.P1ButtonLeft:
                    if (pressed)
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], Direction.Left);
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], Direction.HorizontalCenter);
                    break;
                case InputMapping.P1ButtonRight:
                    if (pressed)
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], Direction.Right);
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], Direction.HorizontalCenter);
                    break;
                // P2
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = pressed;
                    break;
                case InputMapping.P2Button1:
                    InputCode.PlayerDigitalButtons[1].Button1 = pressed;
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = pressed;
                    break;
                case InputMapping.P2Button3:
                    InputCode.PlayerDigitalButtons[1].Button3 = pressed;
                    break;
                case InputMapping.P2Button4:
                    InputCode.PlayerDigitalButtons[1].Button4 = pressed;
                    break;
                case InputMapping.P2Button5:
                    InputCode.PlayerDigitalButtons[1].Button5 = pressed;
                    break;
                case InputMapping.P2Button6:
                    InputCode.PlayerDigitalButtons[1].Button6 = pressed;
                    break;
                // Ext1
                case InputMapping.ExtensionOne1:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1 = pressed;
                    break;
                case InputMapping.ExtensionOne2:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2 = pressed;
                    break;
                case InputMapping.ExtensionOne3:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = pressed;
                    break;
                case InputMapping.ExtensionOne4:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = pressed;
                    break;
                case InputMapping.ExtensionOne11:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = pressed;
                    break;
                case InputMapping.ExtensionOne12:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = pressed;
                    break;
                case InputMapping.ExtensionOne13:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = pressed;
                    break;
                case InputMapping.ExtensionOne14:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = pressed;
                    break;
                case InputMapping.ExtensionOne15:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = pressed;
                    break;
                case InputMapping.ExtensionOne16:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = pressed;
                    break;
                case InputMapping.ExtensionOne17:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.ExtensionOne18:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = pressed;
                    break;
                // Ext2
                case InputMapping.ExtensionTwo1:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 = pressed;
                    break;
                case InputMapping.ExtensionTwo2:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 = pressed;
                    break;
                case InputMapping.ExtensionTwo3:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = pressed;
                    break;
                case InputMapping.ExtensionTwo4:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = pressed;
                    break;
                case InputMapping.ExtensionTwo11:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 = pressed;
                    break;
                case InputMapping.ExtensionTwo12:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 = pressed;
                    break;
                case InputMapping.ExtensionTwo13:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 = pressed;
                    break;
                case InputMapping.ExtensionTwo14:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 = pressed;
                    break;
                case InputMapping.ExtensionTwo15:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 = pressed;
                    break;
                case InputMapping.ExtensionTwo16:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 = pressed;
                    break;
                case InputMapping.ExtensionTwo17:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.ExtensionTwo18:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = pressed;
                    break;
                default:
                    break;
            }
        }

        private void HandleRawInputGun(JoystickButtons joystickButton, int inputX, int inputY, bool moveAbsolute)
        {
            // Ignore when alt+tabbed
            if (!_windowFocus)
                return;

            // Calculate where the mouse is inside the game window
            // 0.0, 0.0 top left
            // 1.0, 1.0 right bottom
            float factorX = 0.0f;
            float factorY = 0.0f;

            // Windowed
            if (_windowed)
            {
                // Translate absolute units to pixels
                if (moveAbsolute)
                {
                    inputX = (int)((float)inputX / (float)0xFFFF * SystemParameters.PrimaryScreenWidth);
                    inputY = (int)((float)inputY / (float)0xFFFF * SystemParameters.PrimaryScreenHeight);
                }

                // X
                if (inputX <= _windowLocationX)
                    factorX = 0.0f;
                else if (inputX >= _windowLocationX + _windowWidth)
                    factorX = 1.0f;
                else
                    factorX = (float)(inputX - _windowLocationX) / (float)_windowWidth;

                // Y
                if (inputY <= _windowLocationY)
                    factorY = 0.0f;
                else if (inputY >= _windowLocationY + _windowHeight)
                    factorY = 1.0f;
                else
                    factorY = (float)(inputY - _windowLocationY) / (float)_windowHeight;
            }
            // Fullscreen
            else
            {
                if (moveAbsolute)
                {
                    factorX = (float)inputX / (float)0xFFFF;
                    factorY = (float)inputY / (float)0xFFFF;
                }
                else
                {
                    factorX = (float)inputX / (float)SystemParameters.PrimaryScreenWidth;
                    factorY = (float)inputY / (float)SystemParameters.PrimaryScreenHeight;
                }
            }

            float minX = _minX;
            float maxX = _maxX;
            float minY = _minY;
            float maxY = _maxY;

            // TODO: move this to profile
            if (_isTransformers && joystickButton.InputMapping == InputMapping.P2LightGun)
            {
                minX = 58;
                maxX = 201;
                minY = 50;
                maxY = 159;
            }

            // Convert to game specific units
            ushort x = (ushort)Math.Round(minX + factorX * (maxX - minX));
            ushort y = (ushort)Math.Round(minY + factorY * (maxY - minY));

            /*
             * InvertedMouseAxis:
             * AnalogBytes[0] = X, Left = 0, Right = 255
             * AnalogBytes[2] = Y, Top = 0,  Bottom = 255
             * 
             * NOT InvertedMouseAxis:
             * AnalogBytes[2] = X, Left = 255, Right = 0
             * AnalogBytes[0] = Y, Top = 255,  Bottom = 0
             * 
             * Luigi is different ofcourse:
             * AnalogBytes[2] = X, Left = 0, Right = 255
             * AnalogBytes[0] = Y, Top = 0,  Bottom = 255
             */

            if (joystickButton.InputMapping == InputMapping.P1LightGun)
            {
                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[0] = (byte)x;
                    InputCode.AnalogBytes[2] = (byte)y;
                }
                else if (_isLuigisMansion)
                {
                    InputCode.AnalogBytes[2] = (byte)x;
                    InputCode.AnalogBytes[0] = (byte)y;
                }
                else
                {
                    InputCode.AnalogBytes[2] = (byte)~x;
                    InputCode.AnalogBytes[0] = (byte)~y;
                }
            }
            else if (joystickButton.InputMapping == InputMapping.P2LightGun)
            {
                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[4] = (byte)x;
                    InputCode.AnalogBytes[6] = (byte)y;
                }
                else if (_isLuigisMansion)
                {
                    InputCode.AnalogBytes[6] = (byte)x;
                    InputCode.AnalogBytes[4] = (byte)y;
                }
                else
                {
                    InputCode.AnalogBytes[6] = (byte)~x;
                    InputCode.AnalogBytes[4] = (byte)~y;
                }
            }
        }
    }
}
