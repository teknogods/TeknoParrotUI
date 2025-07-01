using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using TeknoParrotUi.Common.Jvs;
using Keys = System.Windows.Forms.Keys;
using System.IO.MemoryMappedFiles;
using System.Windows;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputListenerRawInputTrackball
    {
        private static GameProfile _gameProfile;
        public static bool KillMe;
        public static bool DisableTestButton;
        private List<JoystickButtons> _joystickButtons;
        readonly List<string> _hookedWindows;
        private bool _windowFound;
        private IntPtr _windowHandle;

        private bool _invertX = false;
        private bool _invertY = false;

        private static short _currentDeltaX;
        private static short _currentDeltaY;
        private readonly object _stateLock = new object();
        private const int MaxShortValue = 32767;
        private const int MinShortValue = -32768;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;

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
        static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private bool _windowFocus = false;
        private int _windowHeight;
        private int _windowWidth;
        private int _windowLocationX;
        private int _windowLocationY;
        private bool dontClip = false;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        public InputListenerRawInputTrackball()
        {
            _hookedWindows = File.Exists("HookedWindows.txt") ? File.ReadAllLines("HookedWindows.txt").ToList() : new List<string>();
            _mmf = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory", 12);
            _accessor = _mmf.CreateViewAccessor();
            _accessor.Write(0, 0); // deltaX
            _accessor.Write(4, 0); // deltaY
            _accessor.Write(8, 0); // reset flag
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

        public void ListenRawInputTrackball(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            // Reset all class members here!
            _joystickButtons = joystickButtons.Where(x => x?.RawInputButton != null).ToList(); // Only configured buttons
            _gameProfile = gameProfile;

            _windowFound = false;
            _windowHandle = IntPtr.Zero;
            _windowFocus = false;
            dontClip = false;

            while (!KillMe)
            {
                if (!_windowFound)
                {

                    var ptr = GetWindowInformation();
                    if (ptr != IntPtr.Zero)
                    {
                        Trace.WriteLine("Window found: " + ptr.ToString("X"));
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

                    if (_windowHandle == GetForegroundWindow())
                    {
                        if (!_windowFocus) // Only need to recalculate when focus changes
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
                        }

                        RECT clipRect = new RECT();
                        clipRect.Left = _windowLocationX;
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

                        _windowFocus = true;
                    }
                    else if (_windowFocus)
                    {
                        _windowFocus = false;
                        RECT freeRect = new RECT();
                        freeRect.Left = 0;
                        freeRect.Top = 0;
                        freeRect.Right = (int)SystemParameters.VirtualScreenWidth;
                        freeRect.Bottom = (int)SystemParameters.VirtualScreenHeight;

                        ClipCursor(ref freeRect);
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

                try
                {
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

                            if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveRelative))
                            {
                                foreach (var trackball in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && (btn.InputMapping == InputMapping.P1Trackball || btn.InputMapping == InputMapping.P2Trackball)))
                                {
                                    HandleRawInputTrackball(trackball, mouse.Mouse.LastX, mouse.Mouse.LastY);
                                }
                            }

                            break;
                        case RawInputKeyboardData keyboard:
                            foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Keyboard && btn.RawInputButton.KeyboardKey == (Keys)keyboard.Keyboard.VirutalKey))
                                HandleRawInputButton(jsButton, !keyboard.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up));
                            break;
                    }
                }
                catch
                {
                    // do nothing essentially
                }
            }
        }

        private void HandleRawInputButton(JoystickButtons joystickButton, bool pressed)
        {
            switch (joystickButton.InputMapping)
            {
                case InputMapping.Test:
                    if (DisableTestButton)
                    {
                        break;
                    }
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

        private void HandleRawInputTrackball(JoystickButtons joystickButton, int deltaX, int deltaY)
        {
            lock (_stateLock)
            {
                int signedDeltaX = _invertX ? -deltaX : deltaX;
                int signedDeltaY = _invertY ? -deltaY : deltaY;
                int resetFlag = _accessor.ReadInt32(8);

                if (resetFlag == 1)
                {
                    //Trace.WriteLine("Reset flag set, resetting deltas.");
                    // Game has read the accumulated delta, so we can reset and start over
                    // Note: we do also clear the delta from memory if the game does it, to get rid of leftover deltas
                    // Although we could also just read the reset flag on the game to see if there has been an update.
                    _currentDeltaX = 0;
                    _currentDeltaY = 0;
                    _accessor.Write(8, 0);
                }

                _currentDeltaX += (short)Math.Max(MinShortValue, Math.Min(MaxShortValue, signedDeltaX));
                _currentDeltaY += (short)Math.Max(MinShortValue, Math.Min(MaxShortValue, signedDeltaY));
                //Trace.WriteLine($"DeltaX: {_currentDeltaX}, DeltaY: {_currentDeltaY}");
                _accessor.Write(0, _currentDeltaX);
                _accessor.Write(4, _currentDeltaY);
            }
        }
    }
}
