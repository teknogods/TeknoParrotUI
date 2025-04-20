using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Plugins;

namespace ExampleKeyboardPlugin
{
    public class ExampleKeyboardPlugin : IInputPlugin
    {
        #region SDL3 Definitions

        // SDL3 constants
        private const uint SDL_INIT_EVENTS = 0x00004000;

        // SDL3 event types
        private enum SDL_EventType : uint
        {
            SDL_EVENT_FIRST = 0x100,
            SDL_EVENT_KEY_DOWN = 0x300,
            SDL_EVENT_KEY_UP = 0x301,
            SDL_EVENT_LAST = 0xFFFF
        }

        // SDL3 scancode to VK mapping
        private readonly Dictionary<int, int> _sdlScancodeToVK = new Dictionary<int, int>();

        // SDL3 native functions
        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_Init(uint flags);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_Quit();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_PollEvent(IntPtr eventPtr);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_PumpEvents();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetKeyboardState(out int numkeys);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern string SDL_GetError();

        // CRT memory functions
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr malloc(int size);

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free(IntPtr ptr);

        #endregion

        private volatile bool _isRunning;
        private Thread _inputThread;
        private GameProfile _gameProfile;
        private readonly object _syncLock = new object();

        // SDL3 resources
        private bool _sdlInitialized;
        private IntPtr _sdlEvent;

        // Track key states
        private Dictionary<int, bool> _previousKeyStates = new Dictionary<int, bool>();
        private List<(int key, bool pressed)> _frameChanges = new List<(int, bool)>();
        private List<(int axis, float value)> _analogChanges = new List<(int, float)>();

        // Keys to monitor (0-255 virtual key codes)
        private readonly HashSet<int> _keysToMonitor = new HashSet<int>();

        // Mouse buttons to ignore
        private readonly HashSet<int> _mouseButtons = new HashSet<int>
        {
            0x01, // VK_LBUTTON - Left mouse button
            0x02, // VK_RBUTTON - Right mouse button
            0x04, // VK_MBUTTON - Middle mouse button
            0x05, // VK_XBUTTON1 - X1 mouse button
            0x06  // VK_XBUTTON2 - X2 mouse button
        };

        // Required plugin metadata
        public string Name => "SDL3 Keyboard Plugin";
        public Version Version => new Version(1, 0);
        public string Description => "A cross-platform keyboard plugin using SDL3 (Windows/Linux/SteamDeck)";
        public string Author => "TeknoParrot Team";

        // Required by InputPluginManager
        public bool IsActive { get; set; }

        public ExampleKeyboardPlugin()
        {
            InitializeSDL();
            InitializeKeysToMonitor();
            InitializeScancodeMapping();
        }

        private void InitializeSDL()
        {
            try
            {
                // Initialize SDL (events only)
                int result = SDL_Init(SDL_INIT_EVENTS);
                if (result < 0)
                {
                    Debug.WriteLine($"[{Name}] Failed to initialize SDL3: {SDL_GetError()}");
                    return;
                }

                // Allocate memory for SDL events
                _sdlEvent = malloc(128); // More than enough for SDL_Event
                if (_sdlEvent == IntPtr.Zero)
                {
                    Debug.WriteLine($"[{Name}] Failed to allocate memory for SDL events");
                    SDL_Quit();
                    return;
                }

                _sdlInitialized = true;
                Debug.WriteLine($"[{Name}] SDL3 initialized successfully");

                // Dump keyboard state for testing
                DumpKeyboardState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] Error initializing SDL3: {ex.Message}");
            }
        }

        private void DumpKeyboardState()
        {
            try
            {
                // Get the keyboard state to see if SDL keyboard is working
                IntPtr keyStatePtr = SDL_GetKeyboardState(out int numKeys);
                Debug.WriteLine($"[{Name}] SDL reports {numKeys} keys available");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] Error getting keyboard state: {ex.Message}");
            }
        }

        private void InitializeKeysToMonitor()
        {
            // Add all valid virtual key codes except mouse buttons
            for (int key = 0x01; key <= 0xFF; key++)
            {
                if (!_mouseButtons.Contains(key))
                {
                    _keysToMonitor.Add(key);
                }
            }

            Debug.WriteLine($"[{Name}] Monitoring {_keysToMonitor.Count} keys");
        }

        private void InitializeScancodeMapping()
        {
            // Map SDL3 scancodes to Windows virtual key codes
            // This allows consistent handling across platforms

            #region Letters
            _sdlScancodeToVK[4] = 0x41;   // SDL_SCANCODE_A -> VK_A
            _sdlScancodeToVK[5] = 0x42;   // SDL_SCANCODE_B -> VK_B
            _sdlScancodeToVK[6] = 0x43;   // SDL_SCANCODE_C -> VK_C
            _sdlScancodeToVK[7] = 0x44;   // SDL_SCANCODE_D -> VK_D
            _sdlScancodeToVK[8] = 0x45;   // SDL_SCANCODE_E -> VK_E
            _sdlScancodeToVK[9] = 0x46;   // SDL_SCANCODE_F -> VK_F
            _sdlScancodeToVK[10] = 0x47;  // SDL_SCANCODE_G -> VK_G
            _sdlScancodeToVK[11] = 0x48;  // SDL_SCANCODE_H -> VK_H
            _sdlScancodeToVK[12] = 0x49;  // SDL_SCANCODE_I -> VK_I
            _sdlScancodeToVK[13] = 0x4A;  // SDL_SCANCODE_J -> VK_J
            _sdlScancodeToVK[14] = 0x4B;  // SDL_SCANCODE_K -> VK_K
            _sdlScancodeToVK[15] = 0x4C;  // SDL_SCANCODE_L -> VK_L
            _sdlScancodeToVK[16] = 0x4D;  // SDL_SCANCODE_M -> VK_M
            _sdlScancodeToVK[17] = 0x4E;  // SDL_SCANCODE_N -> VK_N
            _sdlScancodeToVK[18] = 0x4F;  // SDL_SCANCODE_O -> VK_O
            _sdlScancodeToVK[19] = 0x50;  // SDL_SCANCODE_P -> VK_P
            _sdlScancodeToVK[20] = 0x51;  // SDL_SCANCODE_Q -> VK_Q
            _sdlScancodeToVK[21] = 0x52;  // SDL_SCANCODE_R -> VK_R
            _sdlScancodeToVK[22] = 0x53;  // SDL_SCANCODE_S -> VK_S
            _sdlScancodeToVK[23] = 0x54;  // SDL_SCANCODE_T -> VK_T
            _sdlScancodeToVK[24] = 0x55;  // SDL_SCANCODE_U -> VK_U
            _sdlScancodeToVK[25] = 0x56;  // SDL_SCANCODE_V -> VK_V
            _sdlScancodeToVK[26] = 0x57;  // SDL_SCANCODE_W -> VK_W
            _sdlScancodeToVK[27] = 0x58;  // SDL_SCANCODE_X -> VK_X
            _sdlScancodeToVK[28] = 0x59;  // SDL_SCANCODE_Y -> VK_Y
            _sdlScancodeToVK[29] = 0x5A;  // SDL_SCANCODE_Z -> VK_Z
            #endregion

            #region Numbers
            _sdlScancodeToVK[30] = 0x31;  // SDL_SCANCODE_1 -> VK_1
            _sdlScancodeToVK[31] = 0x32;  // SDL_SCANCODE_2 -> VK_2
            _sdlScancodeToVK[32] = 0x33;  // SDL_SCANCODE_3 -> VK_3
            _sdlScancodeToVK[33] = 0x34;  // SDL_SCANCODE_4 -> VK_4
            _sdlScancodeToVK[34] = 0x35;  // SDL_SCANCODE_5 -> VK_5
            _sdlScancodeToVK[35] = 0x36;  // SDL_SCANCODE_6 -> VK_6
            _sdlScancodeToVK[36] = 0x37;  // SDL_SCANCODE_7 -> VK_7
            _sdlScancodeToVK[37] = 0x38;  // SDL_SCANCODE_8 -> VK_8
            _sdlScancodeToVK[38] = 0x39;  // SDL_SCANCODE_9 -> VK_9
            _sdlScancodeToVK[39] = 0x30;  // SDL_SCANCODE_0 -> VK_0
            #endregion

            #region Function Keys
            _sdlScancodeToVK[58] = 0x70;  // SDL_SCANCODE_F1 -> VK_F1
            _sdlScancodeToVK[59] = 0x71;  // SDL_SCANCODE_F2 -> VK_F2
            _sdlScancodeToVK[60] = 0x72;  // SDL_SCANCODE_F3 -> VK_F3
            _sdlScancodeToVK[61] = 0x73;  // SDL_SCANCODE_F4 -> VK_F4
            _sdlScancodeToVK[62] = 0x74;  // SDL_SCANCODE_F5 -> VK_F5
            _sdlScancodeToVK[63] = 0x75;  // SDL_SCANCODE_F6 -> VK_F6
            _sdlScancodeToVK[64] = 0x76;  // SDL_SCANCODE_F7 -> VK_F7
            _sdlScancodeToVK[65] = 0x77;  // SDL_SCANCODE_F8 -> VK_F8
            _sdlScancodeToVK[66] = 0x78;  // SDL_SCANCODE_F9 -> VK_F9
            _sdlScancodeToVK[67] = 0x79;  // SDL_SCANCODE_F10 -> VK_F10
            _sdlScancodeToVK[68] = 0x7A;  // SDL_SCANCODE_F11 -> VK_F11
            _sdlScancodeToVK[69] = 0x7B;  // SDL_SCANCODE_F12 -> VK_F12
            #endregion

            #region Special Keys
            _sdlScancodeToVK[41] = 0x1B;  // SDL_SCANCODE_ESCAPE -> VK_ESCAPE
            _sdlScancodeToVK[43] = 0x09;  // SDL_SCANCODE_TAB -> VK_TAB
            _sdlScancodeToVK[44] = 0x20;  // SDL_SCANCODE_SPACE -> VK_SPACE
            _sdlScancodeToVK[40] = 0x0D;  // SDL_SCANCODE_RETURN -> VK_RETURN
            _sdlScancodeToVK[42] = 0x08;  // SDL_SCANCODE_BACKSPACE -> VK_BACK
            _sdlScancodeToVK[76] = 0x2D;  // SDL_SCANCODE_INSERT -> VK_INSERT
            _sdlScancodeToVK[77] = 0x2E;  // SDL_SCANCODE_DELETE -> VK_DELETE
            _sdlScancodeToVK[74] = 0x24;  // SDL_SCANCODE_HOME -> VK_HOME
            _sdlScancodeToVK[77] = 0x23;  // SDL_SCANCODE_END -> VK_END
            _sdlScancodeToVK[75] = 0x21;  // SDL_SCANCODE_PAGEUP -> VK_PRIOR
            _sdlScancodeToVK[78] = 0x22;  // SDL_SCANCODE_PAGEDOWN -> VK_NEXT
            _sdlScancodeToVK[57] = 0x14;  // SDL_SCANCODE_CAPSLOCK -> VK_CAPITAL
            _sdlScancodeToVK[225] = 0x10;  // SDL_SCANCODE_LSHIFT -> VK_SHIFT
            _sdlScancodeToVK[229] = 0x10;  // SDL_SCANCODE_RSHIFT -> VK_SHIFT
            _sdlScancodeToVK[224] = 0x11;  // SDL_SCANCODE_LCTRL -> VK_CONTROL
            _sdlScancodeToVK[228] = 0x11;  // SDL_SCANCODE_RCTRL -> VK_CONTROL
            _sdlScancodeToVK[226] = 0x12;  // SDL_SCANCODE_LALT -> VK_MENU
            _sdlScancodeToVK[230] = 0x12;  // SDL_SCANCODE_RALT -> VK_MENU
            #endregion

            #region Arrow Keys
            _sdlScancodeToVK[80] = 0x28;  // SDL_SCANCODE_DOWN -> VK_DOWN
            _sdlScancodeToVK[82] = 0x25;  // SDL_SCANCODE_LEFT -> VK_LEFT
            _sdlScancodeToVK[79] = 0x27;  // SDL_SCANCODE_RIGHT -> VK_RIGHT 
            _sdlScancodeToVK[81] = 0x26;  // SDL_SCANCODE_UP -> VK_UP
            #endregion

            #region Numpad
            _sdlScancodeToVK[98] = 0x60;  // SDL_SCANCODE_KP_0 -> VK_NUMPAD0
            _sdlScancodeToVK[89] = 0x61;  // SDL_SCANCODE_KP_1 -> VK_NUMPAD1
            _sdlScancodeToVK[90] = 0x62;  // SDL_SCANCODE_KP_2 -> VK_NUMPAD2
            _sdlScancodeToVK[91] = 0x63;  // SDL_SCANCODE_KP_3 -> VK_NUMPAD3
            _sdlScancodeToVK[92] = 0x64;  // SDL_SCANCODE_KP_4 -> VK_NUMPAD4
            _sdlScancodeToVK[93] = 0x65;  // SDL_SCANCODE_KP_5 -> VK_NUMPAD5
            _sdlScancodeToVK[94] = 0x66;  // SDL_SCANCODE_KP_6 -> VK_NUMPAD6
            _sdlScancodeToVK[95] = 0x67;  // SDL_SCANCODE_KP_7 -> VK_NUMPAD7
            _sdlScancodeToVK[96] = 0x68;  // SDL_SCANCODE_KP_8 -> VK_NUMPAD8
            _sdlScancodeToVK[97] = 0x69;  // SDL_SCANCODE_KP_9 -> VK_NUMPAD9
            _sdlScancodeToVK[84] = 0x6F;  // SDL_SCANCODE_KP_DIVIDE -> VK_DIVIDE
            _sdlScancodeToVK[85] = 0x6A;  // SDL_SCANCODE_KP_MULTIPLY -> VK_MULTIPLY
            _sdlScancodeToVK[86] = 0x6D;  // SDL_SCANCODE_KP_MINUS -> VK_SUBTRACT
            _sdlScancodeToVK[87] = 0x6B;  // SDL_SCANCODE_KP_PLUS -> VK_ADD
            _sdlScancodeToVK[88] = 0x0D;  // SDL_SCANCODE_KP_ENTER -> VK_RETURN
            #endregion

            Debug.WriteLine($"[{Name}] Initialized {_sdlScancodeToVK.Count} scancode mappings");
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

            Debug.WriteLine($"[{Name}] Input thread started");

            // Create a timer to track how long we've been waiting for input
            Stopwatch noInputTimer = new Stopwatch();
            noInputTimer.Start();

            // SDLv2 vs SDLv3 detection
            bool triedDetectSDLVersion = false;
            bool isSDL3 = true;  // Default assuming SDL3, but we'll adjust if needed

            while (_isRunning)
            {
                // Clear changes list at the start of each frame
                lock (_syncLock)
                {
                    _frameChanges.Clear();
                    _analogChanges.Clear();
                }

                if (_sdlInitialized)
                {
                    bool hadInputs = false;

                    // If we haven't detected any inputs after 5 seconds, try auto-detecting SDL version
                    if (!triedDetectSDLVersion && noInputTimer.ElapsedMilliseconds > 5000)
                    {
                        triedDetectSDLVersion = true;
                        // Try both SDL2 and SDL3 event structures
                        Debug.WriteLine($"[{Name}] No inputs detected for 5 seconds, testing SDL version detection...");
                        TestSDLVersions();
                    }

                    // Try to process keyboard events
                    hadInputs = ProcessKeyboardWithSDL(isSDL3);

                    // If that didn't work and we're still assuming SDL3, try SDL2 format
                    if (!hadInputs && isSDL3)
                    {
                        isSDL3 = false;
                        Debug.WriteLine($"[{Name}] Switching to SDL2 event format");
                        hadInputs = ProcessKeyboardWithSDL(false);
                        if (hadInputs)
                        {
                            Debug.WriteLine($"[{Name}] SDL2 event format worked!");
                        }
                    }

                    // Try keyboard state polling as backup
                    ProcessKeyboardStateWithSDL();

                    // If we still can't get inputs after 10 seconds, fall back to Windows
                    if (noInputTimer.ElapsedMilliseconds > 10000)
                    {
                        Debug.WriteLine($"[{Name}] No SDL inputs detected after 10 seconds, using Windows fallback temporarily");
                        ProcessWindowsKeyboard();
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ProcessWindowsKeyboard();
                }

                Thread.Sleep(10);
            }
        }

        private void ProcessKeyboardStateWithSDL()
        {
            try
            {
                // Get current keyboard state
                IntPtr keyStatePtr = SDL_GetKeyboardState(out int numkeys);
                if (keyStatePtr != IntPtr.Zero)
                {
                    byte[] keyState = new byte[numkeys];
                    Marshal.Copy(keyStatePtr, keyState, 0, numkeys);

                    // Get any pressed keys for logging
                    List<int> pressedKeys = new List<int>();
                    for (int i = 0; i < keyState.Length; i++)
                    {
                        if (keyState[i] != 0)
                        {
                            pressedKeys.Add(i);
                        }
                    }

                    if (pressedKeys.Count > 0)
                    {
                        Debug.WriteLine($"[{Name}] Pressed scancodes: {string.Join(", ", pressedKeys)}");

                        // Process each pressed key
                        foreach (int scancode in pressedKeys)
                        {
                            if (_sdlScancodeToVK.TryGetValue(scancode, out int virtualKey))
                            {
                                if (!_mouseButtons.Contains(virtualKey))
                                {
                                    bool wasPressed = _previousKeyStates.TryGetValue(virtualKey, out bool prevState) && prevState;

                                    if (!wasPressed)
                                    {
                                        lock (_syncLock)
                                        {
                                            _frameChanges.Add((virtualKey, true));
                                        }

                                        _previousKeyStates[virtualKey] = true;
                                        Debug.WriteLine($"[{Name}] STATE: SDL Key {scancode} -> VK 0x{virtualKey:X2} pressed");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] Error polling keyboard state: {ex.Message}");
            }
        }

        private void TestSDLVersions()
        {
            try
            {
                // Dump the raw memory of a keyboard event for analysis
                Debug.WriteLine($"[{Name}] Waiting for a key press to detect SDL event format...");

                // Stop polling until a key is pressed
                SDL_PumpEvents();

                for (int offset = 0; offset < 40; offset += 4)
                {
                    try
                    {
                        int value = Marshal.ReadInt32(_sdlEvent, offset);
                        Debug.WriteLine($"[{Name}] SDL Event structure offset {offset}: 0x{value:X8}");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] Error testing SDL versions: {ex.Message}");
            }
        }


        // Add this method for Windows fallback
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void ProcessWindowsKeyboard()
        {
            foreach (var key in _keysToMonitor)
            {
                bool isCurrentlyPressed = (GetAsyncKeyState(key) & 0x8000) != 0;

                if (_previousKeyStates.TryGetValue(key, out bool wasPressed) && isCurrentlyPressed != wasPressed)
                {
                    lock (_syncLock)
                    {
                        _frameChanges.Add((key, isCurrentlyPressed));
                    }
                    _previousKeyStates[key] = isCurrentlyPressed;

                    Debug.WriteLine($"[{Name}] WINDOWS: Key 0x{key:X2} {(isCurrentlyPressed ? "pressed" : "released")}");
                }
            }
        }


        private bool ProcessKeyboardWithSDL(bool useSDL3Format)
        {
            bool hadEvents = false;

            try
            {
                // Make sure SDL processes events
                SDL_PumpEvents();

                // Poll for key events
                while (SDL_PollEvent(_sdlEvent) > 0)
                {
                    hadEvents = true;

                    // Read the event type (first uint32 in the event structure)
                    uint eventType = (uint)Marshal.ReadInt32(_sdlEvent, 0);

                    Debug.WriteLine($"[{Name}] SDL event received: type=0x{eventType:X}");

                    // Check if it's a keyboard event for either SDL3 or SDL2
                    bool isKeyDown = (eventType == (uint)SDL_EventType.SDL_EVENT_KEY_DOWN);
                    bool isKeyUp = (eventType == (uint)SDL_EventType.SDL_EVENT_KEY_UP);

                    if (isKeyDown || isKeyUp)
                    {
                        int scancode;

                        if (useSDL3Format)
                        {
                            // SDL3 format - scancode at offset 20 (may vary by version)
                            try
                            {
                                scancode = Marshal.ReadInt32(_sdlEvent, 20);
                            }
                            catch
                            {
                                // Fallback offset for SDL3
                                scancode = Marshal.ReadInt32(_sdlEvent, 24);
                            }
                        }
                        else
                        {
                            // SDL2 format - scancode at offset 28 as a byte
                            scancode = Marshal.ReadByte(_sdlEvent, 28);
                        }

                        bool isPressed = isKeyDown;

                        // Add debugging
                        for (int i = 0; i < 40; i += 4)
                        {
                            try
                            {
                                Debug.WriteLine($"[{Name}] Key event data at offset {i}: {Marshal.ReadInt32(_sdlEvent, i):X8}");
                            }
                            catch { }
                        }

                        Debug.WriteLine($"[{Name}] Key event - scancode {scancode}, pressed: {isPressed}");

                        // Map SDL scancode to Windows VK
                        if (_sdlScancodeToVK.TryGetValue(scancode, out int virtualKey))
                        {
                            if (!_mouseButtons.Contains(virtualKey))
                            {
                                lock (_syncLock)
                                {
                                    _frameChanges.Add((virtualKey, isPressed));
                                }

                                _previousKeyStates[virtualKey] = isPressed;
                                Debug.WriteLine($"[{Name}] SDL Key {scancode} mapped to VK 0x{virtualKey:X2} {(isPressed ? "pressed" : "released")}");
                            }
                        }
                        else
                        {
                            // Unknown scancode, let's add it to our mappings
                            Debug.WriteLine($"[{Name}] Unknown scancode: {scancode} - adding to mapping table");

                            // Map to a reasonable virtual key if it's in the normal range
                            if (scancode >= 0 && scancode < 256)
                            {
                                // Add a mapping for this unknown key - use the value directly if it's in VK range
                                int newVk = scancode < 0x100 ? scancode : 0;
                                _sdlScancodeToVK[scancode] = newVk;

                                if (newVk > 0 && !_mouseButtons.Contains(newVk))
                                {
                                    lock (_syncLock)
                                    {
                                        _frameChanges.Add((newVk, isPressed));
                                    }

                                    _previousKeyStates[newVk] = isPressed;
                                    Debug.WriteLine($"[{Name}] Added new mapping: SDL Key {scancode} -> VK 0x{newVk:X2}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] Error processing SDL events: {ex.Message}");
            }

            return hadEvents;
        }

        public List<(int key, bool pressed)> GetKeyChanges()
        {
            lock (_syncLock)
            {
                return new List<(int, bool)>(_frameChanges);
            }
        }

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

            // Clean up SDL resources
            CleanupSDL();

            Debug.WriteLine($"[{Name}] Stopped listening for keyboard input");
        }

        private void CleanupSDL()
        {
            if (_sdlInitialized)
            {
                try
                {
                    // Free event memory
                    if (_sdlEvent != IntPtr.Zero)
                    {
                        free(_sdlEvent);
                        _sdlEvent = IntPtr.Zero;
                    }

                    // Quit SDL
                    SDL_Quit();
                    _sdlInitialized = false;

                    Debug.WriteLine($"[{Name}] SDL resources cleaned up");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{Name}] Error during SDL cleanup: {ex.Message}");
                }
            }
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Not needed when using SDL3
        }

        public void UpdateInputState()
        {
            // Not needed for this plugin - input is processed in the thread
        }

        public void ProcessGameSpecificInput()
        {
            // Not needed for this plugin
        }
    }
}