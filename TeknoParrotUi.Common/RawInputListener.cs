using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace TeknoParrotUi.Common
{
    public class RawInputListener
    {
        private IMouseEvents _mouseEvents;
        private IKeyboardMouseEvents _mGlobalHook;
        private int _windowHeight;
        private int _windowWidth;
        private int _windowLocationX;
        private int _windowLocationY;
        private bool _windowFound;
        private IntPtr _windowHandle;
        private bool _killListen;
        private Thread _listenThread;
        private Thread _findWindowThread;
        private int _mouseX;
        private int _mouseY;
        private float _minX;
        private float _maxX;
        private float _minY;
        private float _maxY;
        private bool _isLuigisMansion;
        private bool _isStarTrek;
        private bool _reverseAxis;
        private bool _isFullScreen;
        private GameProfile _gameProfile;
        readonly List<string> _hookedWindows;

        public RawInputListener()
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
                var windowTitle = pList.MainWindowTitle;

                //if (windowTitle != "")
                //    Console.WriteLine("Title: " + windowTitle);

                if (isHookableWindow(windowTitle))
                {
                    return pList.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }

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
        static extern long ReleaseCapture();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        private void FindWindowThread()
        {
            Thread.Sleep(2000);
            while (true)
            {
                if (_killListen)
                    return;

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

                        //Console.WriteLine("{0},{1} {2},{3}", windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom);
                    }
                    else
                    {
                        //Console.WriteLine("Not focused!");
                        //ClipCursor(null);
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private void ListenThread()
        {
            while (!_killListen)
            {
                if (!_windowFound)
                {
                    Thread.Sleep(100);
                    continue;
                }

                // Calculate where the mouse is inside the game window
                // 0.0, 0.0 top left
                // 1.0, 1.0 right bottom
                float factorX = 0.0f;
                float factorY = 0.0f;

                // X
                if (_mouseX <= _windowLocationX)
                    factorX = 0.0f;
                else if (_mouseX >= _windowLocationX + _windowWidth)
                    factorX = 1.0f;
                else
                    factorX = (float)(_mouseX - _windowLocationX) / (float)_windowWidth;

                // Y
                if (_mouseY <= _windowLocationY)
                    factorY = 0.0f;
                else if (_mouseY >= _windowLocationY + _windowHeight)
                    factorY = 1.0f;
                else
                    factorY = (float)(_mouseY - _windowLocationY) / (float)_windowHeight;

                // Convert to game specific units
                ushort x = (ushort)Math.Round(_minX + factorX * (_maxX - _minX));
                ushort y = (ushort)Math.Round(_minY + factorY * (_maxY - _minY));

                //Console.WriteLine("{0} {1}", x, y);

                // Magic
                if (_isLuigisMansion)
                {
                    InputCode.AnalogBytes[2] = (byte)~Cleanup(x);
                    InputCode.AnalogBytes[0] = (byte)~Cleanup(y);
                }
                else if (_isStarTrek)
                {
                    InputCode.AnalogBytes[0] = (byte)~Cleanup(x);
                    InputCode.AnalogBytes[2] = (byte)~Cleanup(y);
                }
                else
                {
                    if (_reverseAxis)
                    {
                        InputCode.AnalogBytes[0] = (byte) ~Cleanup(x);
                        InputCode.AnalogBytes[2] = (byte) ~Cleanup(y);
                    }
                    else
                    {
                        InputCode.AnalogBytes[2] = Cleanup(x);
                        InputCode.AnalogBytes[0] = Cleanup(y);
                    }
                }

                Thread.Sleep(10);
            }
        }

        public void ListenToDevice(bool reversedAxis, GameProfile gameProfile)
        {
            _reverseAxis = reversedAxis;
            _gameProfile = gameProfile;

            if (_gameProfile.EmulationProfile == EmulationProfile.LuigisMansion)
                _isLuigisMansion = true;
            if (_gameProfile.EmulationProfile == EmulationProfile.StarTrekVoyager)
                _isStarTrek = true;

            // TODO: move this to profile?
            if (_gameProfile.EmulationProfile == EmulationProfile.Rambo)
            {
                _minX = 15;
                _maxX = 234;
                _minY = 27;
                _maxY = 245;
            }
            /*else if (_gameProfile.EmulationProfile == EmulationProfile.TooSpicy)
            {
                _minX = 10;
                _maxX = 245;
                _minY = 6;
                _maxY = 250;
            }*/
            else if (_gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoIsland)
            {
                _minX = 27;
                _maxX = 208;
                _minY = 35;
                _maxY = 178;
            }
            else
            {
                _minX = 0;
                _maxX = 255;
                _minY = 0;
                _maxY = 255;
            }

            _isFullScreen = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0");
            _killListen = false;
            _listenThread = new Thread(ListenThread);
            _listenThread.Start();
            _findWindowThread = new Thread(FindWindowThread);
            _findWindowThread.Start();
            _mGlobalHook = Hook.GlobalEvents();
            _mGlobalHook.KeyDown += MGlobalHookOnKeyDown;
            _mGlobalHook.KeyUp += MGlobalHookOnKeyUp;
            _mouseEvents = Hook.GlobalEvents();
            _mouseEvents.MouseMove += MouseEventsOnMouseMove;
            _mouseEvents.MouseDown += MouseEventOnMouseDown;
            _mouseEvents.MouseUp += MouseEventsOnMouseUp;
        }

        void SetButton(Keys key, bool pressed)
        {
            switch (_gameProfile.EmulationProfile)
            {
                case EmulationProfile.TooSpicy:
                    SetPlayerButtons2Spicy(key, pressed);
                    break;
                case EmulationProfile.LuigisMansion:
                    SetPlayerButtonsLuigisMansion(key, pressed);
                    break;
                default:
                    SetPlayerButton(key, pressed);
                    break;
            }
        }

        private void MGlobalHookOnKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (!_windowFound)
                return;

            SetButton(keyEventArgs.KeyCode, true);
        }

        private void MGlobalHookOnKeyUp(object sender, KeyEventArgs keyEventArgs)
        {
            if (!_windowFound)
                return;

            SetButton(keyEventArgs.KeyCode, false);
        }

        void SetPlayerButtonsLuigisMansion(Keys key, bool pressed)
        {
            switch (key)
            {
                case Keys.D8:
                    InputCode.PlayerDigitalButtons[0].Test = pressed;
                    break;
                case Keys.D9:
                    InputCode.PlayerDigitalButtons[0].Service = pressed;
                    break;
                case Keys.D0:
                    InputCode.PlayerDigitalButtons[0].Button4 = pressed;
                    break;
                case Keys.D1:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
            }
        }

        void SetPlayerButtons2Spicy(Keys key, bool pressed)
        {
            switch (key)
            {
                case Keys.D8:
                    InputCode.PlayerDigitalButtons[0].Test = pressed;
                    break;
                case Keys.D9:
                    InputCode.PlayerDigitalButtons[0].Service = pressed;
                    break;
                case Keys.D0:
                    InputCode.PlayerDigitalButtons[1].Service = pressed;
                    InputCode.PlayerDigitalButtons[1].Coin = pressed;
                    break;
                case Keys.D1:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
                case Keys.Left:
                    InputCode.PlayerDigitalButtons[0].Left = pressed;
                    break;
                case Keys.Right:
                    InputCode.PlayerDigitalButtons[0].Right = pressed;
                    break;
            }
        }

        void SetPlayerButton(Keys key, bool pressed)
        {
            switch (key)
            {
                case Keys.D8:
                    InputCode.PlayerDigitalButtons[0].Test = pressed;
                    break;
                case Keys.D9:
                    InputCode.PlayerDigitalButtons[0].Service = pressed;
                    break;
                case Keys.D0:
                    InputCode.PlayerDigitalButtons[1].Service = pressed;
                    InputCode.PlayerDigitalButtons[1].Coin = pressed;
                    break;
                case Keys.D1:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
            }
        }

        private void MouseEventOnMouseDown(object sender, MouseEventArgs mouseEventArgs)
        {
            if (!_windowFound)
                return;

            if (_gameProfile.EmulationProfile == EmulationProfile.LuigisMansion)
            {
                if ((mouseEventArgs.Button & MouseButtons.Left) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button1 = true;
                }

                if ((mouseEventArgs.Button & MouseButtons.Right) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = true;
                }
            }
            else
            {
                if ((mouseEventArgs.Button & MouseButtons.Left) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button1 = true;
                }

                if ((mouseEventArgs.Button & MouseButtons.Right) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = true;
                    InputCode.PlayerDigitalButtons[0].Start = true;
                }

                if ((mouseEventArgs.Button & MouseButtons.Middle) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button3 = true;
                    InputCode.PlayerDigitalButtons[0].Button4 = true;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = true;
                }

                if ((mouseEventArgs.Button & MouseButtons.XButton1) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button4 = true;
                }
            }
        }

        private void MouseEventsOnMouseUp(object sender, MouseEventArgs mouseEventArgs)
        {
            if (!_windowFound)
                return;

            if (_gameProfile.EmulationProfile == EmulationProfile.LuigisMansion)
            {
                if ((mouseEventArgs.Button & MouseButtons.Left) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button1 = false;
                }

                if ((mouseEventArgs.Button & MouseButtons.Right) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = false;
                }
            }
            else
            {
                if ((mouseEventArgs.Button & MouseButtons.Left) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button1 = false;
                }

                if ((mouseEventArgs.Button & MouseButtons.Right) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = false;
                    InputCode.PlayerDigitalButtons[0].Start = false;
                }

                if ((mouseEventArgs.Button & MouseButtons.Middle) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button3 = false;
                    InputCode.PlayerDigitalButtons[0].Button4 = false;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = false;
                }

                if ((mouseEventArgs.Button & MouseButtons.XButton1) != 0)
                {
                    InputCode.PlayerDigitalButtons[0].Button4 = false;
                }
            }
        }

        private void MouseEventsOnMouseMove(object sender, MouseEventArgs mouseEventArgs)
        {
            if (!_windowFound)
                return;

            _mouseX = mouseEventArgs.X;
            _mouseY = mouseEventArgs.Y;
        }

        private byte Cleanup(ushort value)
        {
            if (value > 0xFF)
            {
                value = 0x00;
            }
            if (value < 0)
            {
                value = 0xFF;
            }
            value = (ushort) ~value;
            return (byte)value;
        }

        public void StopListening()
        {
            if (_mouseEvents != null)
            {
                _mouseEvents.MouseMove -= MouseEventsOnMouseMove;
                _mouseEvents.MouseDown -= MouseEventOnMouseDown;
                _mouseEvents.MouseUp -= MouseEventsOnMouseUp;
                _mouseEvents = null;
            }

            if (_mGlobalHook != null)
            {
                _mGlobalHook.KeyDown -= MGlobalHookOnKeyDown;
                _mGlobalHook.KeyUp -= MGlobalHookOnKeyUp;
                _mGlobalHook.Dispose();
                _mGlobalHook = null;
            }

            ReleaseCapture();
            _killListen = true;
        }
    }
}
