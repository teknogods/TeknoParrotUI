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

        // Track key states
        private Dictionary<int, bool> _previousKeyStates = new Dictionary<int, bool>();
        private List<(int key, bool pressed)> _frameChanges = new List<(int, bool)>();

        // For analog support
        private List<(int axis, float value)> _analogChanges = new List<(int, float)>();

        // Keys to monitor (0-255 virtual key codes)
        private readonly HashSet<int> _keysToMonitor = new HashSet<int>();

        // Required plugin metadata
        public string Name => "Example Keyboard Plugin";
        public Version Version => new Version(1, 0);
        public string Description => "An example keyboard input plugin for TeknoParrot";
        public string Author => "YourName";

        // Required by InputPluginManager
        public bool IsActive { get; set; }

        public ExampleKeyboardPlugin()
        {
            // Initialize all keys we want to monitor (can be expanded as needed)
            // Common keyboard keys (letters, numbers, arrows, etc.)
            for (int key = 0x01; key <= 0xFF; key++)
            {
                _keysToMonitor.Add(key);
            }
        }

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
            IsActive = true;

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
            // Initialize previous states
            foreach (var key in _keysToMonitor)
            {
                _previousKeyStates[key] = false;
            }

            while (_isRunning)
            {
                // Clear changes list at the start of each frame
                lock (_syncLock)
                {
                    _frameChanges.Clear();
                    _analogChanges.Clear();
                }

                // Check all monitored keys for changes
                foreach (var key in _keysToMonitor)
                {
                    bool isCurrentlyPressed = (GetAsyncKeyState(key) & 0x8000) != 0;

                    // Compare with previous state
                    if (!_previousKeyStates.TryGetValue(key, out bool wasPressed) || isCurrentlyPressed != wasPressed)
                    {
                        // State changed - record this change
                        lock (_syncLock)
                        {
                            _frameChanges.Add((key, isCurrentlyPressed));
                        }

                        // Update previous state
                        _previousKeyStates[key] = isCurrentlyPressed;

                        // Debug info
                        Debug.WriteLine($"[{Name}] Key 0x{key:X2} {(isCurrentlyPressed ? "pressed" : "released")}");
                    }
                }

                // Don't hammer the CPU
                Thread.Sleep(10);
            }
        }

        // Get the current frame's key changes (thread-safe)
        public List<(int key, bool pressed)> GetKeyChanges()
        {
            lock (_syncLock)
            {
                return new List<(int, bool)>(_frameChanges);
            }
        }

        // Get analog input changes (required by IInputPlugin)
        public List<(int axis, float value)> GetAnalogChanges()
        {
            lock (_syncLock)
            {
                return new List<(int, float)>(_analogChanges);
            }
        }

        public void StopListening()
        {
            _isRunning = false;
            IsActive = false;

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
            // Window message handling if needed
        }

        // Required by IInputPlugin - Update input state
        public void UpdateInputState()
        {
            // This method would be called each frame to update input state
            // For a keyboard plugin, most of the work happens in InputThreadProc
        }

        // Required by IInputPlugin - Process game-specific input
        public void ProcessGameSpecificInput()
        {
            // Process any game-specific input requirements
            // For keyboard plugin, typically empty as generic key mapping is handled elsewhere
        }
    }
}