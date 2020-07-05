﻿using System;
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
        private bool _killListen;
        private Thread _listenThread;
        private Thread _findWindowThread;
        private int _mouseX;
        private bool isLuigisMansion;
        private bool _isStarTrek;
        private int _mouseY;
        private bool _reverseAxis;
        private bool _isFullScreen;
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
                Console.WriteLine(windowTitle);
                if (isHookableWindow(windowTitle))
                {
                    return pList.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
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

        [DllImport("user32.dll")]
        static extern long ReleaseCapture();
        
        private GameProfile _gameProfile;

        private void FindWindowThread()
        {
            Thread.Sleep(2000);
            while (true)
            {
                if (_killListen)
                    return;
                if (!_windowFound)
                {
                    var ptr = GetWindowInformation();
                    if (ptr != IntPtr.Zero)
                    {
                        RECT rct = new RECT();
                        GetWindowRect(ptr, ref rct);
                        _windowHeight = rct.Bottom - rct.Top;
                        _windowWidth = rct.Right - rct.Left;
                        _windowLocationX = rct.Top;
                        _windowLocationY = rct.Left;
                        ClipCursor(ref rct);
                        _windowFound = true;
                    }
                }
                else
                    return;
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
                var width = _windowWidth;
                var height = _windowHeight;
                var minX = _windowLocationX;
                var minY = _windowLocationY;
                var xArgs = CleanMouse(_mouseX - minX);
                var yArgs = CleanMouse(_mouseY - minY);
                if (yArgs < 0)
                    yArgs = 0;
                if (xArgs < 0)
                    xArgs = 0;
                var x = (ushort)(xArgs / (width / 255));
                var y = (ushort)(yArgs / (height / 255));
                if (isLuigisMansion)
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
                isLuigisMansion = true;
            if (_gameProfile.EmulationProfile == EmulationProfile.StarTrekVoyager)
                _isStarTrek = true;
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

        private int CleanMouse(int mouseLocation)
        {
            if (mouseLocation < 0)
                return 0;
            //Console.WriteLine("Mouse location ok");
            return mouseLocation;
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
