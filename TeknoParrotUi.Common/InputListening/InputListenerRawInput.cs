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
        private static GameProfile _gameProfile;
        public static bool KillMe;
        private List<JoystickButtons> _joystickButtons;
        private float _minX;
        private float _maxX;
        private float _minY;
        private float _maxY;
        private bool _invertedMouseAxis;
        private bool _isLuigisMansion;
        private bool _isPrimevalHunt;
        private bool _swapdisplay;
        private bool _onedisplay;

        private bool _windowed;
        readonly List<string> _hookedWindows;
        private bool _windowFound;
        private bool _windowFocus;
        private IntPtr _windowHandle;
        private int _windowHeight;
        private int _windowWidth;
        private int _windowLocationX;
        private int _windowLocationY;

        private bool _centerCrosshairs;
        private int[] _lastPosX = new int[4];
        private int[] _lastPosY = new int[4];
        private bool dontClip;

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
            _hookedWindows = File.Exists("HookedWindows.txt") ? File.ReadAllLines("HookedWindows.txt").ToList() : new List<string>();
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
                // TODO: Find a better way to find game window handle
                if (isHookableWindow(pList.MainWindowTitle) && pList.ProcessName != "explorer")
                    return pList.MainWindowHandle;
            }

            return IntPtr.Zero;
        }

        public void ListenRawInput(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            // Reset all class members here!
            _joystickButtons = joystickButtons.Where(x => x?.RawInputButton != null).ToList(); // Only configured buttons
            _minX = gameProfile.xAxisMin;
            _maxX = gameProfile.xAxisMax;
            _minY = gameProfile.yAxisMin;
            _maxY = gameProfile.yAxisMax;
            _invertedMouseAxis = gameProfile.InvertedMouseAxis;
            _isLuigisMansion = gameProfile.EmulationProfile == EmulationProfile.LuigisMansion;
            _isPrimevalHunt = gameProfile.EmulationProfile == EmulationProfile.PrimevalHunt;
            _gameProfile = gameProfile;

            if (_isPrimevalHunt)
            {
                _onedisplay = gameProfile.ConfigValues.Any(x => x.FieldName == "OneDisplay" && x.FieldValue == "1");
                _swapdisplay = gameProfile.ConfigValues.Any(x => x.FieldName == "SwapDisplay" && x.FieldValue == "1");
            }

            _windowed = gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") || gameProfile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
            _windowFound = false;
            _windowFocus = false;
            _windowHandle = IntPtr.Zero;
            _windowHeight = 0;
            _windowWidth = 0;
            _windowLocationX = 0;
            _windowLocationY = 0;

            _centerCrosshairs = true;
            _lastPosX = new int[4];
            _lastPosY = new int[4];
            dontClip = false;

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
                        _windowFocus = false;
                        Thread.Sleep(100);
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
                        _windowFocus = false;
                        Thread.Sleep(100);
                        continue;
                    }

                    // Only update when we are on the foreground
                    if (_windowHandle == GetForegroundWindow())
                    {
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

                        if (_isPrimevalHunt && !_swapdisplay && !_onedisplay)
                            clipRect.Left = (int)(_windowLocationX + _windowWidth / 2.0);
                        else
                            clipRect.Left = _windowLocationX;

                        if (_isPrimevalHunt && _swapdisplay && !_onedisplay)
                            clipRect.Right = (int)(_windowLocationX + _windowWidth / 2.0);
                        else
                            clipRect.Right = _windowLocationX + _windowWidth;

                        clipRect.Top = _windowLocationY;
                        clipRect.Bottom = _windowLocationY + _windowHeight;

                        if (!dontClip)
                        {
                            ClipCursor(ref clipRect);
                        }
                        else
                        {
                            RECT freeRect = new RECT();
                            freeRect.Left = 0;
                            freeRect.Top = 0;
                            freeRect.Right = (int)SystemParameters.VirtualScreenWidth;
                            freeRect.Bottom = (int)SystemParameters.VirtualScreenHeight;

                            ClipCursor(ref freeRect);
                        }

                        // First time we see the window lets center the crosshairs
                        if (_centerCrosshairs)
                        {
                            _lastPosX[0] = _lastPosX[1] = _lastPosX[2] = _lastPosX[3] = _windowWidth / 2 + _windowLocationX;
                            _lastPosY[0] = _lastPosY[1] = _lastPosY[2] = _lastPosY[3] = _windowHeight / 2 + _windowLocationY;

                            if (_invertedMouseAxis)
                            {
                                InputCode.AnalogBytes[0]  = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[2]  = (byte)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[4]  = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[6]  = (byte)((_minY + _maxY) / 2.0);

                                InputCode.AnalogBytes[8]  = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[10] = (byte)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[12] = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[14] = (byte)((_minY + _maxY) / 2.0);
                            }
                            else if (_isLuigisMansion)
                            {
                                InputCode.AnalogBytes[2] = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[0] = (byte)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[6] = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[4] = (byte)((_minY + _maxY) / 2.0);

                                InputCode.AnalogBytes[10] = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[8]  = (byte)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[14] = (byte)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[12] = (byte)((_minY + _maxY) / 2.0);
                            }
                            else
                            {
                                InputCode.AnalogBytes[2]  = (byte)~(int)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[0]  = (byte)~(int)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[6]  = (byte)~(int)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[4]  = (byte)~(int)((_minY + _maxY) / 2.0);

                                InputCode.AnalogBytes[10] = (byte)~(int)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[8]  = (byte)~(int)((_minY + _maxY) / 2.0);
                                InputCode.AnalogBytes[14] = (byte)~(int)((_minX + _maxX) / 2.0);
                                InputCode.AnalogBytes[12] = (byte)~(int)((_minY + _maxY) / 2.0);
                            }

                            _centerCrosshairs = false;
                        }

                        _windowFocus = true;
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

                string path = "null";

                if (data != null && data.Device != null && data.Device.DevicePath != null)
                {
                    path = data.Device.DevicePath;
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
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.LeftButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.LeftButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown) || flags.HasFlag(RawMouseButtonFlags.RightButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.RightButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.RightButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown) || flags.HasFlag(RawMouseButtonFlags.MiddleButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.MiddleButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button4Down) || flags.HasFlag(RawMouseButtonFlags.Button4Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button4))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button4Down));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button5Down) || flags.HasFlag(RawMouseButtonFlags.Button5Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button5))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button5Down));
                            }
                        }

                        // Handle position
                        if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveAbsolute))
                        {
                            // Lightgun
                            foreach (var gun in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun || btn.InputMapping == InputMapping.P3LightGun || btn.InputMapping == InputMapping.P4LightGun)))
                                HandleRawInputGun(gun, mouse.Mouse.LastX, mouse.Mouse.LastY, true);
                        }
                        else if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveRelative))
                        {
                            // Windows mouse cursor
                            foreach (var gun in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == "Windows Mouse Cursor" && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun || btn.InputMapping == InputMapping.P3LightGun || btn.InputMapping == InputMapping.P4LightGun)))
                                HandleRawInputGun(gun, Cursor.Position.X, Cursor.Position.Y, false);

                            // Other relative movement mouse like device
                            foreach (var gun in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun || btn.InputMapping == InputMapping.P3LightGun || btn.InputMapping == InputMapping.P4LightGun)))
                            {
                                byte player = 0;

                                if (gun.InputMapping == InputMapping.P1LightGun)
                                    player = 0;
                                else if (gun.InputMapping == InputMapping.P2LightGun)
                                    player = 1;
                                else if (gun.InputMapping == InputMapping.P3LightGun)
                                    player = 2;
                                else if (gun.InputMapping == InputMapping.P4LightGun)
                                    player = 3;

                                _lastPosX[player] = Math.Min(Math.Max(_lastPosX[player] + mouse.Mouse.LastX, _windowLocationX), _windowLocationX + _windowWidth);
                                _lastPosY[player] = Math.Min(Math.Max(_lastPosY[player] + mouse.Mouse.LastY, _windowLocationY), _windowLocationY + _windowHeight);

                                HandleRawInputGun(gun, _lastPosX[player], _lastPosY[player], false);
                            }
                        }

                        break;
                    case RawInputKeyboardData keyboard:
                        if ((Keys)keyboard.Keyboard.VirutalKey == Keys.ControlKey && !keyboard.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up))
                            dontClip = true;
                        else
                            dontClip = false;

                        foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Keyboard && btn.RawInputButton.KeyboardKey == (Keys)keyboard.Keyboard.VirutalKey))
                            HandleRawInputButton(jsButton, !keyboard.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up));

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
                    if (_gameProfile.EmulationProfile == EmulationProfile.EADP)
                    {
                        if (InputCode.PlayerDigitalButtons[0].Coin.Value)
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = true;
                        else
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = false;
                    }
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
                case InputMapping.P1ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.P1ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.P1ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.P1ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Right : Direction.HorizontalCenter);
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
                case InputMapping.P2ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.P2ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.P2ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.P2ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;
                // Jvs Board 2
                case InputMapping.JvsTwoService1:
                    InputCode.PlayerDigitalButtons[2].Service = pressed;
                    break;
                case InputMapping.JvsTwoService2:
                    InputCode.PlayerDigitalButtons[3].Service = pressed;
                    break;
                case InputMapping.JvsTwoCoin1:
                    InputCode.PlayerDigitalButtons[2].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(2);
                    break;
                case InputMapping.JvsTwoCoin2:
                    InputCode.PlayerDigitalButtons[3].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(3);
                    break;
                case InputMapping.JvsTwoP1Button1:
                    InputCode.PlayerDigitalButtons[2].Button1 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button2:
                    InputCode.PlayerDigitalButtons[2].Button2 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button3:
                    InputCode.PlayerDigitalButtons[2].Button3 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button4:
                    InputCode.PlayerDigitalButtons[2].Button4 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button5:
                    InputCode.PlayerDigitalButtons[2].Button5 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button6:
                    InputCode.PlayerDigitalButtons[2].Button6 = pressed;
                    break;
                case InputMapping.JvsTwoP1ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonStart:
                    InputCode.PlayerDigitalButtons[2].Start = pressed;
                    break;
                case InputMapping.JvsTwoP2Button1:
                    InputCode.PlayerDigitalButtons[3].Button1 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button2:
                    InputCode.PlayerDigitalButtons[3].Button2 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button3:
                    InputCode.PlayerDigitalButtons[3].Button3 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button4:
                    InputCode.PlayerDigitalButtons[3].Button4 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button5:
                    InputCode.PlayerDigitalButtons[3].Button5 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button6:
                    InputCode.PlayerDigitalButtons[3].Button6 = pressed;
                    break;
                case InputMapping.JvsTwoP2ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonStart:
                    InputCode.PlayerDigitalButtons[3].Start = pressed;
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
                    {
                        if (_gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum || _gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum2)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = !pressed;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = pressed;
                        }     
                    }
                    break;
                case InputMapping.ExtensionOne18:
                    {
                        if (_gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum || _gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum2)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = !pressed;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = pressed;
                        }
                    }
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

            // Convert to game specific units
            ushort x;

            if (_isPrimevalHunt && !_onedisplay && !_swapdisplay)
                x = (ushort)Math.Round(1.0 + factorX * 2.0 * (maxX - minX));
            else if (_isPrimevalHunt && !_onedisplay && _swapdisplay)
                x = (ushort)Math.Round(minX + factorX * 2.0 * (maxX - minX));
            else
                x = (ushort)Math.Round(minX + factorX * (maxX - minX));

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

            byte indexA = 0;
            byte indexB = 0;

            if (joystickButton.InputMapping == InputMapping.P1LightGun)
            {
                indexA = 0;
                indexB = 2;
            }
            else if (joystickButton.InputMapping == InputMapping.P2LightGun)
            {
                indexA = 4;
                indexB = 6;
            }
            else if (joystickButton.InputMapping == InputMapping.P3LightGun)
            {
                indexA = 8;
                indexB = 10;
            }
            else if (joystickButton.InputMapping == InputMapping.P4LightGun)
            {
                indexA = 12;
                indexB = 14;
            }


            if (_isLuigisMansion)
            {
                InputCode.AnalogBytes[indexB] = (byte)x;
                InputCode.AnalogBytes[indexA] = (byte)y;
            }
            else if (_invertedMouseAxis)
            {
                InputCode.AnalogBytes[indexA] = (byte)x;
                InputCode.AnalogBytes[indexB] = (byte)y;
            }  
            else
            {
                InputCode.AnalogBytes[indexB] = (byte)~x;
                InputCode.AnalogBytes[indexA] = (byte)~y;
            }
        }
    }
}
