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
        private static short _currentDeltaX2;
        private static short _currentDeltaY2;
        private static short _currentDeltaX3;
        private static short _currentDeltaY3;
        private static short _currentDeltaX4;
        private static short _currentDeltaY4;
        private static short _currentDeltaXHost;
        private static short _currentDeltaYHost;
        private readonly object _stateLock = new object();
        private const int MaxShortValue = 32767;
        private const int MinShortValue = -32768;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private MemoryMappedFile _mmf2;
        private MemoryMappedViewAccessor _accessor2;
        private MemoryMappedFile _mmf3;
        private MemoryMappedViewAccessor _accessor3;
        private MemoryMappedFile _mmf4;
        private MemoryMappedViewAccessor _accessor4;
        private MemoryMappedFile _mmfHost;
        private MemoryMappedViewAccessor _accessorHost;

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

            // Local Online Mode only: P2/P3/P4 each get their own independent region, same
            // layout as above, so the game side can read all four independently and decide
            // which one applies based on its own current-player state. Cheap to always create
            // regardless of whether Remote Local Play is actually on for this game - unused
            // memory-mapped files cost nothing at runtime if nothing ever reads/writes them.
            _mmf2 = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory2", 12);
            _accessor2 = _mmf2.CreateViewAccessor();
            _accessor2.Write(0, 0);
            _accessor2.Write(4, 0);
            _accessor2.Write(8, 0);

            _mmf3 = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory3", 12);
            _accessor3 = _mmf3.CreateViewAccessor();
            _accessor3.Write(0, 0);
            _accessor3.Write(4, 0);
            _accessor3.Write(8, 0);

            _mmf4 = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory4", 12);
            _accessor4 = _mmf4.CreateViewAccessor();
            _accessor4.Write(0, 0);
            _accessor4.Write(4, 0);
            _accessor4.Write(8, 0);

            _mmfHost = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemoryHost", 12);
            _accessorHost = _mmfHost.CreateViewAccessor();
            _accessorHost.Write(0, 0);
            _accessorHost.Write(4, 0);
            _accessorHost.Write(8, 0);

            SunshinePlayerInput.InputReceived += OnSunshineInputReceived;
            SunshinePlayerInput.Start();
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

                // See InputListenerRawInput.WndProcReceived for why anonymous (no real device)
                // events are dropped: they're an echo of Sunshine's own synthetic injection, and
                // we already get that same press properly tagged via the pipe.
                if (data == null || data.Device == null)
                {
                    return;
                }

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
                                    ProcessMouseButton(path, RawMouseButton.LeftButton, flags.HasFlag(RawMouseButtonFlags.LeftButtonDown));

                                if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown) || flags.HasFlag(RawMouseButtonFlags.RightButtonUp))
                                    ProcessMouseButton(path, RawMouseButton.RightButton, flags.HasFlag(RawMouseButtonFlags.RightButtonDown));

                                if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown) || flags.HasFlag(RawMouseButtonFlags.MiddleButtonUp))
                                    ProcessMouseButton(path, RawMouseButton.MiddleButton, flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown));

                                if (flags.HasFlag(RawMouseButtonFlags.Button4Down) || flags.HasFlag(RawMouseButtonFlags.Button4Up))
                                    ProcessMouseButton(path, RawMouseButton.Button4, flags.HasFlag(RawMouseButtonFlags.Button4Down));

                                if (flags.HasFlag(RawMouseButtonFlags.Button5Down) || flags.HasFlag(RawMouseButtonFlags.Button5Up))
                                    ProcessMouseButton(path, RawMouseButton.Button5, flags.HasFlag(RawMouseButtonFlags.Button5Down));
                            }

                            if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveRelative))
                            {
                                ProcessTrackballMove(path, mouse.Mouse.LastX, mouse.Mouse.LastY);
                            }

                            break;
                        case RawInputKeyboardData keyboard:
                            ProcessKeyboardKey(path, (Keys)keyboard.Keyboard.VirutalKey, !keyboard.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up));
                            break;
                    }
                }
                catch
                {
                    // do nothing essentially
                }
            }
        }

        /// <summary>
        /// Handles a player-tagged event forwarded from Sunshine over the identity-bridge pipe.
        /// Fires on a background thread, so this only feeds the same matching/dispatch path real
        /// RawInput events use, keyed by the synthetic "SUNSHINE#PLAYERn" device path.
        /// </summary>
        private void OnSunshineInputReceived(object sender, SunshineInputEventArgs e)
        {
            if (e.Player < SunshinePlayerInput.MinPlayer || e.Player > SunshinePlayerInput.MaxPlayer)
            {
                return;
            }

            // _joystickButtons is only populated once ListenRawInputTrackball() actually runs.
            // When the game's Input API is plain RawInput (not RawInputTrackball), this class is
            // constructed and stays subscribed to the Sunshine pipe, but its own listen method is
            // never called - only InputListenerRawInput's does. See the matching guard in
            // InputListenerRawInput.OnSunshineInputReceived for why this must not be skipped.
            if (_joystickButtons == null)
            {
                return;
            }

            string path = SunshinePlayerInput.DevicePathForPlayer(e.Player);

            try
            {
                switch (e.EventType)
                {
                    case SunshineInputEventType.KeyDown:
                        ProcessKeyboardKey(path, (Keys)e.KeyCode, true);
                        break;
                    case SunshineInputEventType.KeyUp:
                        ProcessKeyboardKey(path, (Keys)e.KeyCode, false);
                        break;
                    case SunshineInputEventType.MouseButtonDown:
                        ProcessMouseButton(path, SunshinePlayerInput.MapMouseButton(e.MouseButton), true);
                        break;
                    case SunshineInputEventType.MouseButtonUp:
                        ProcessMouseButton(path, SunshinePlayerInput.MapMouseButton(e.MouseButton), false);
                        break;
                    case SunshineInputEventType.MouseMove:
                        ProcessTrackballMove(path, e.DeltaX, e.DeltaY);
                        break;
                }
            }
            catch
            {
                // do nothing essentially
            }
        }

        /// <summary>
        /// Matches and dispatches a mouse button state change for the given device path. Shared
        /// by real WM_INPUT events and Sunshine pipe events.
        /// </summary>
        private void ProcessMouseButton(string path, RawMouseButton button, bool pressed)
        {
            foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == button))
                HandleRawInputButton(jsButton, pressed);
        }

        /// <summary>
        /// Matches and dispatches a keyboard key state change for the given device path. Shared
        /// by real WM_INPUT events and Sunshine pipe events.
        /// </summary>
        private void ProcessKeyboardKey(string path, Keys key, bool pressed)
        {
            foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DevicePath == path && btn.RawInputButton.DeviceType == RawDeviceType.Keyboard && btn.RawInputButton.KeyboardKey == key))
                HandleRawInputButton(jsButton, pressed);
        }

        /// <summary>
        /// Matches and dispatches a relative trackball delta for the given device path. Shared by
        /// real WM_INPUT events and Sunshine pipe events.
        /// </summary>
        private void ProcessTrackballMove(string path, int deltaX, int deltaY)
        {
            bool isRemoteLocalPlayMode = _gameProfile != null && _gameProfile.ConfigValues != null &&
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Remote Local Play" && x.FieldValue != "Off");
            bool isRemoteLocalPlayHostMode = _gameProfile != null && _gameProfile.ConfigValues != null &&
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Remote Local Play" && x.FieldValue == "Host Only");

            foreach (var trackball in _joystickButtons.Where(btn =>
                btn.RawInputButton.DevicePath == path &&
                btn.RawInputButton.DeviceType == RawDeviceType.Mouse &&
                (btn.InputMapping == InputMapping.P1Trackball || btn.InputMapping == InputMapping.P2Trackball || btn.InputMapping == InputMapping.P3Trackball || btn.InputMapping == InputMapping.P4Trackball || btn.InputMapping == InputMapping.HostTrackball) &&
                !(isRemoteLocalPlayMode && btn.HideWithRemoteLocalPlayMode) &&
                !(!isRemoteLocalPlayMode && btn.HideWithoutRemoteLocalPlayMode) &&
                !(!isRemoteLocalPlayHostMode && btn.HideWithoutRemoteLocalPlayHost)))
            {
                HandleRawInputTrackball(trackball, deltaX, deltaY);
            }
        }

        private void HandleRawInputButton(JoystickButtons joystickButton, bool pressed)
        {
            bool isRemoteLocalPlayMode = _gameProfile != null && _gameProfile.ConfigValues != null &&
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Remote Local Play" && x.FieldValue != "Off");
            bool isRemoteLocalPlayHostMode = _gameProfile != null && _gameProfile.ConfigValues != null &&
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Remote Local Play" && x.FieldValue == "Host Only");

            if ((isRemoteLocalPlayMode && joystickButton.HideWithRemoteLocalPlayMode) ||
                (!isRemoteLocalPlayMode && joystickButton.HideWithoutRemoteLocalPlayMode) ||
                (!isRemoteLocalPlayHostMode && joystickButton.HideWithoutRemoteLocalPlayHost))
            {
                return;
            }

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
                case InputMapping.StreamHostP1ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[6].Start = pressed;
                    break;
                case InputMapping.StreamHostP1Button1:
                    InputCode.StreamingPlayerDigitalButtons[6].Button1 = pressed;
                    break;
                case InputMapping.StreamHostP1Button2:
                    InputCode.StreamingPlayerDigitalButtons[6].Button2 = pressed;
                    break;
                case InputMapping.Stream2P1ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[0].Start = pressed;
                    break;
                case InputMapping.Stream2P2Button1:
                    InputCode.StreamingPlayerDigitalButtons[1].Button1 = pressed;
                    break;
                case InputMapping.Stream2P2Button2:
                    InputCode.StreamingPlayerDigitalButtons[1].Button2 = pressed;
                    break;
                case InputMapping.Stream2P1Button1:
                    InputCode.StreamingPlayerDigitalButtons[0].Button1 = pressed;
                    break;
                case InputMapping.Stream2P1Button2:
                    InputCode.StreamingPlayerDigitalButtons[0].Button2 = pressed;
                    break;
                case InputMapping.Stream2P1Button3:
                    InputCode.StreamingPlayerDigitalButtons[0].Button3 = pressed;
                    break;
                case InputMapping.Stream2P1Button4:
                    InputCode.StreamingPlayerDigitalButtons[0].Button4 = pressed;
                    break;
                case InputMapping.Stream2P1Button6:
                    InputCode.StreamingPlayerDigitalButtons[0].Button6 = pressed;
                    break;
                case InputMapping.Stream2P1ButtonLeft:
                    InputCode.StreamingPlayerDigitalButtons[0].Left = pressed;
                    break;
                case InputMapping.Stream2P1ButtonRight:
                    InputCode.StreamingPlayerDigitalButtons[0].Right = pressed;
                    break;
                case InputMapping.Stream2P2ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[1].Start = pressed;
                    break;
                case InputMapping.Stream3P1ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[2].Start = pressed;
                    break;
                case InputMapping.Stream3P2Button1:
                    InputCode.StreamingPlayerDigitalButtons[3].Button1 = pressed;
                    break;
                case InputMapping.Stream3P2Button2:
                    InputCode.StreamingPlayerDigitalButtons[3].Button2 = pressed;
                    break;
                case InputMapping.Stream3P1Button1:
                    InputCode.StreamingPlayerDigitalButtons[2].Button1 = pressed;
                    break;
                case InputMapping.Stream3P1Button2:
                    InputCode.StreamingPlayerDigitalButtons[2].Button2 = pressed;
                    break;
                case InputMapping.Stream3P1Button3:
                    InputCode.StreamingPlayerDigitalButtons[2].Button3 = pressed;
                    break;
                case InputMapping.Stream3P1Button4:
                    InputCode.StreamingPlayerDigitalButtons[2].Button4 = pressed;
                    break;
                case InputMapping.Stream3P1Button6:
                    InputCode.StreamingPlayerDigitalButtons[2].Button6 = pressed;
                    break;
                case InputMapping.Stream3P1ButtonLeft:
                    InputCode.StreamingPlayerDigitalButtons[2].Left = pressed;
                    break;
                case InputMapping.Stream3P1ButtonRight:
                    InputCode.StreamingPlayerDigitalButtons[2].Right = pressed;
                    break;
                case InputMapping.Stream3P2ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[3].Start = pressed;
                    break;
                case InputMapping.Stream4P1ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[4].Start = pressed;
                    break;
                case InputMapping.Stream4P2Button1:
                    InputCode.StreamingPlayerDigitalButtons[5].Button1 = pressed;
                    break;
                case InputMapping.Stream4P2Button2:
                    InputCode.StreamingPlayerDigitalButtons[5].Button2 = pressed;
                    break;
                case InputMapping.Stream4P1Button1:
                    InputCode.StreamingPlayerDigitalButtons[4].Button1 = pressed;
                    break;
                case InputMapping.Stream4P1Button2:
                    InputCode.StreamingPlayerDigitalButtons[4].Button2 = pressed;
                    break;
                case InputMapping.Stream4P1Button3:
                    InputCode.StreamingPlayerDigitalButtons[4].Button3 = pressed;
                    break;
                case InputMapping.Stream4P1Button4:
                    InputCode.StreamingPlayerDigitalButtons[4].Button4 = pressed;
                    break;
                case InputMapping.Stream4P1Button6:
                    InputCode.StreamingPlayerDigitalButtons[4].Button6 = pressed;
                    break;
                case InputMapping.Stream4P1ButtonLeft:
                    InputCode.StreamingPlayerDigitalButtons[4].Left = pressed;
                    break;
                case InputMapping.Stream4P1ButtonRight:
                    InputCode.StreamingPlayerDigitalButtons[4].Right = pressed;
                    break;
                case InputMapping.Stream4P2ButtonStart:
                    InputCode.StreamingPlayerDigitalButtons[5].Start = pressed;
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
            int signedDeltaX = _invertX ? -deltaX : deltaX;
            int signedDeltaY = _invertY ? -deltaY : deltaY;

            lock (_stateLock)
            {
                // Local Online Mode only: route each player's trackball to its own independent
                // buffer, so the game can tell them apart and apply only whichever one is
                // currently active. Outside this mode, every trackball binding - P1, P2, or
                // anything else - always goes to the single original buffer, exactly matching
                // behavior from before this feature existed.
                if (_gameProfile != null && _gameProfile.ConfigValues != null && _gameProfile.ConfigValues.Any(x => x.FieldName == "Remote Local Play" && x.FieldValue != "Off"))
                {
                    switch (joystickButton.InputMapping)
                    {
                        case InputMapping.HostTrackball:
                            WriteTrackballDelta(_accessorHost, ref _currentDeltaXHost, ref _currentDeltaYHost, signedDeltaX, signedDeltaY);
                            return;
                        case InputMapping.P2Trackball:
                            WriteTrackballDelta(_accessor2, ref _currentDeltaX2, ref _currentDeltaY2, signedDeltaX, signedDeltaY);
                            return;
                        case InputMapping.P3Trackball:
                            WriteTrackballDelta(_accessor3, ref _currentDeltaX3, ref _currentDeltaY3, signedDeltaX, signedDeltaY);
                            return;
                        case InputMapping.P4Trackball:
                            WriteTrackballDelta(_accessor4, ref _currentDeltaX4, ref _currentDeltaY4, signedDeltaX, signedDeltaY);
                            return;
                        // P1Trackball (and anything unexpected) falls through to the original
                        // buffer below, same as always.
                    }
                }

                WriteTrackballDelta(_accessor, ref _currentDeltaX, ref _currentDeltaY, signedDeltaX, signedDeltaY);
            }
        }

        /// <summary>
        /// Accumulates a delta into the given buffer's state and writes it out. Shared by all
        /// four (P1-P4) trackball targets - identical logic, just pointed at different backing
        /// state, to avoid quadruplicating this by hand.
        /// </summary>
        private static void WriteTrackballDelta(MemoryMappedViewAccessor accessor, ref short currentDeltaX, ref short currentDeltaY, int signedDeltaX, int signedDeltaY)
        {
            int resetFlag = accessor.ReadInt32(8);

            if (resetFlag == 1)
            {
                // Game has read the accumulated delta, so we can reset and start over.
                currentDeltaX = 0;
                currentDeltaY = 0;
                accessor.Write(8, 0);
            }

            currentDeltaX += (short)Math.Max(MinShortValue, Math.Min(MaxShortValue, signedDeltaX));
            currentDeltaY += (short)Math.Max(MinShortValue, Math.Min(MaxShortValue, signedDeltaY));
            accessor.Write(0, currentDeltaX);
            accessor.Write(4, currentDeltaY);
        }
    }
}
