// ExampleKeyboardPlugin.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Plugins;

namespace ExampleKeyboardPlugin
{
    public class ExampleKeyboardPlugin : IInputPlugin
    {
        // Native methods for keyboard input
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        private volatile bool _isRunning;
        private Thread _inputThread;
        private GameProfile _gameProfile;
        private readonly object _syncLock = new object();

        // Required plugin metadata
        public string Name => "Example Keyboard Plugin";
        public Version Version => new Version(1, 0);
        public string Description => "An example keyboard input plugin for TeknoParrot";
        public string Author => "YourName";

        // Key mappings - could be loaded from config
        private readonly Dictionary<int, (int player, string button)> _keyMappings = new Dictionary<int, (int player, string button)>
        {
            // WASD for Player 1
            { 0x57, (0, "Up") },    // W key for P1 Up
            { 0x53, (0, "Down") },  // S key for P1 Down
            { 0x41, (0, "Left") },  // A key for P1 Left
            { 0x44, (0, "Right") }, // D key for P1 Right
            { 0x20, (0, "Button1") }, // Space for P1 Button1
            { 0x10, (0, "Button2") }, // Shift for P1 Button2
            
            // Arrow keys for Player 2
            { 0x26, (1, "Up") },    // Up Arrow for P2 Up
            { 0x28, (1, "Down") },  // Down Arrow for P2 Down
            { 0x25, (1, "Left") },  // Left Arrow for P2 Left
            { 0x27, (1, "Right") }, // Right Arrow for P2 Right
            { 0x31, (1, "Button1") }, // 1 key for P2 Button1
            { 0x32, (1, "Button2") }  // 2 key for P2 Button2
        };

        public void Initialize(GameProfile gameProfile)
        {
            _gameProfile = gameProfile;
            Debug.WriteLine($"[{Name}] Initialized for game: {gameProfile.GameNameInternal}");
        }

        public void StartListening(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            if (_isRunning)
                return;

            _gameProfile = gameProfile;
            _isRunning = true;

            _inputThread = new Thread(InputThreadProc)
            {
                Name = $"{Name}InputThread",
                IsBackground = true
            };
            _inputThread.Start();
            Debug.WriteLine($"[{Name}] Started listening for keyboard input");
        }

        private void InputThreadProc()
        {
            while (_isRunning)
            {
                foreach (var keyMapping in _keyMappings)
                {
                    int vKey = keyMapping.Key;
                    var (player, button) = keyMapping.Value;

                    // Check if key is pressed (most significant bit is set)
                    bool isPressed = (GetAsyncKeyState(vKey) & 0x8000) != 0;

                    // Thread-safe access to InputCode
                    lock (_syncLock)
                    {
                        // Update the appropriate button state based on mapping
                        switch (button)
                        {
                            case "Up":
                                InputCode.PlayerDigitalButtons[player].Up = isPressed;
                                break;
                            case "Down":
                                InputCode.PlayerDigitalButtons[player].Down = isPressed;
                                break;
                            case "Left":
                                InputCode.PlayerDigitalButtons[player].Left = isPressed;
                                break;
                            case "Right":
                                InputCode.PlayerDigitalButtons[player].Right = isPressed;
                                break;
                            case "Button1":
                                InputCode.PlayerDigitalButtons[player].Button1 = isPressed;
                                break;
                            case "Button2":
                                InputCode.PlayerDigitalButtons[player].Button2 = isPressed;
                                break;
                            case "Button3":
                                InputCode.PlayerDigitalButtons[player].Button3 = isPressed;
                                break;
                            case "Button4":
                                InputCode.PlayerDigitalButtons[player].Button4 = isPressed;
                                break;
                        }
                    }
                }

                // Don't hammer the CPU
                Thread.Sleep(10);
            }
        }

        public void StopListening()
        {
            _isRunning = false;

            if (_inputThread?.IsAlive == true)
            {
                try
                {
                    if (!_inputThread.Join(500))
                    {
                        Debug.WriteLine($"[{Name}] Thread did not terminate gracefully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{Name}] Error stopping input thread: {ex.Message}");
                }
                _inputThread = null;
            }

            Debug.WriteLine($"[{Name}] Stopped listening for keyboard input");
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Example: Handle raw input or other window messages if needed
            // const int WM_INPUT = 0x00FF;

            // if (msg == WM_INPUT)
            // {
            //     // Process raw input
            //     handled = true;
            // }
        }
    }
}