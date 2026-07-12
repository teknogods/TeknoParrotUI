using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    /// <summary>
    /// Permission-free Linux input fallback. Used when /dev/input event nodes
    /// are not readable (no udev rule installed, user not in 'input' group):
    /// reads input from the X server instead of evdev — no root, ever.
    ///
    /// Primary path — XInput2 raw events (event-driven, zero permissions):
    /// - per-physical-device button/key/motion events → multi-gun works
    ///   rootless on native X11 (XWayland merges pointers: single gun there)
    /// - side buttons (Button4/Button5) supported
    /// - X keycode = evdev code + 8 → EvdevKeyMap gives identical Keys values,
    ///   so bindings stay compatible with Windows RawInput and evdev
    /// - aim: single pointer → absolute cursor via XQueryPointer (exactly what
    ///   the Wine game sees); multiple pointers → per-device raw-delta canvas
    ///   accumulation with the same GunAnalogMath as evdev/RawInput
    ///
    /// Last resort — XQueryPointer/XQueryKeymap polling at ~250 Hz when the
    /// server lacks XInput2 (single pointer, three buttons).
    ///
    /// Works on X11 and XWayland (Wine games are always X11 clients). The
    /// only setup that beats this is the udev rule (setup/install-udev-rules.sh),
    /// which unlocks direct evdev: dedicated light-gun hardware and per-device
    /// binding paths.
    /// </summary>
    public class X11FallbackInputListener : IInputListener
    {
        public string Name => "X11Fallback";
        public bool IsSupported => OperatingSystem.IsLinux() && X11Interop.IsAvailable();

        private readonly bool _handleMouse;
        private readonly bool _handleKeyboard;

        private GameProfile _gameProfile;
        private List<JoystickButtons> _mouseBindings;
        private List<(JoystickButtons Binding, ushort[] EvdevCodes)> _keyBindings;
        private bool _isGunGame;
        private float _minX, _maxX, _minY, _maxY;
        private bool _is16Bit, _invertedMouseAxis, _isLuigisMansion, _isGunslinger;

        private Thread _thread;
        private Thread _axisTickThread;
        private volatile bool _killMe;

        // Keyboard/button wheel-gas-brake ramping ("Use Keyboard/Button For
        // Axis") — same engine as the Windows RawInput listener and evdev.
        private readonly KeyboardAxisEngine _keyboardAxis = new KeyboardAxisEngine();

        /// <param name="handleMouse">Serve aim + mouse buttons (no readable evdev mouse).</param>
        /// <param name="handleKeyboard">Serve keyboard bindings (no readable evdev keyboard).</param>
        public X11FallbackInputListener(bool handleMouse, bool handleKeyboard)
        {
            _handleMouse = handleMouse;
            _handleKeyboard = handleKeyboard;
        }

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            _killMe = false;
            _gameProfile = gameProfile;
            MappingDispatch.ResetSessionState();
            _keyboardAxis.Initialize(gameProfile);
            _minX = gameProfile.xAxisMin;
            _maxX = gameProfile.xAxisMax;
            _minY = gameProfile.yAxisMin;
            _maxY = gameProfile.yAxisMax;
            _is16Bit = gameProfile.Use16BitAnalog;
            _invertedMouseAxis = gameProfile.InvertedMouseAxis;
            _isLuigisMansion = gameProfile.EmulationProfile == EmulationProfile.LuigisMansion;
            _isGunslinger = gameProfile.EmulationProfile == EmulationProfile.GunslingerStratos3;

            var buttons = joystickButtons ?? new List<JoystickButtons>();
            var gunMappings = buttons.Where(b => b != null &&
                (b.InputMapping == InputMapping.P1LightGun || b.InputMapping == InputMapping.P2LightGun ||
                 b.InputMapping == InputMapping.P3LightGun || b.InputMapping == InputMapping.P4LightGun)).ToList();
            _isGunGame = gunMappings.Count > 0 || gameProfile.GunGame;

            _mouseBindings = buttons.Where(b => b?.RawInputButton != null &&
                b.RawInputButton.DeviceType == RawDeviceType.Mouse).ToList();
            _keyBindings = buttons
                .Where(b => b?.RawInputButton != null &&
                            b.RawInputButton.DeviceType == RawDeviceType.Keyboard &&
                            b.RawInputButton.KeyboardKey != Keys.None)
                .Select(b => (b, Keyboard.EvdevKeyMap.CodesFor(b.RawInputButton.KeyboardKey).ToArray()))
                .Where(t => t.Item2.Length > 0)
                .ToList();

            if (_isGunGame && _handleMouse)
                CenterCrosshairs();

            bool anythingToDo = (_handleMouse && (_isGunGame || _mouseBindings.Count > 0)) ||
                                (_handleKeyboard && _keyBindings.Count > 0) ||
                                (_handleKeyboard && _keyboardAxis.Enabled);
            if (!anythingToDo)
                return;

            if (_keyboardAxis.Enabled)
            {
                // Classic 16 ms ramp timer — ticks even without key events
                // (axes ramp back to center on release).
                _axisTickThread = new Thread(() =>
                {
                    while (!_killMe)
                    {
                        _keyboardAxis.Tick();
                        Thread.Sleep(16);
                    }
                }) { IsBackground = true, Name = "X11KbdAxis" };
                _axisTickThread.Start();
            }

            _thread = new Thread(PollLoop) { IsBackground = true, Name = "X11FallbackInput" };
            _thread.Start();
        }

        private void PollLoop()
        {
            var display = X11Interop.XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                Debug.WriteLine("X11FallbackInputListener: cannot open X display");
                return;
            }

            try
            {
                // Preferred: XInput2 raw events — event-driven, per-physical-
                // device (rootless multi-gun on native X11), side buttons,
                // still zero permissions. XQueryPointer polling only when XI2
                // is unavailable (ancient servers / stripped-down XWayland).
                int opcode = X11Interop.SelectRawEvents(display);
                if (opcode >= 0)
                {
                    Debug.WriteLine($"X11FallbackInputListener: XInput2 raw events (mouse={_handleMouse}, keyboard={_handleKeyboard})");
                    RawEventLoop(display, opcode);
                }
                else
                {
                    Debug.WriteLine($"X11FallbackInputListener: XI2 unavailable, polling (mouse={_handleMouse}, keyboard={_handleKeyboard})");
                    LegacyPollLoop(display);
                }
            }
            finally
            {
                X11Interop.XCloseDisplay(display);
            }
        }

        // ---------- XInput2 raw-event path ----------

        private void RawEventLoop(IntPtr display, int opcode)
        {
            var root = X11Interop.XDefaultRootWindow(display);
            int screen = X11Interop.XDefaultScreen(display);
            float width = Math.Max(1, X11Interop.XDisplayWidth(display, screen));
            float height = Math.Max(1, X11Interop.XDisplayHeight(display, screen));

            // Physical pointers → players, in device order. Native X11 lists
            // each mouse/light-gun separately; XWayland shows one merged pointer.
            var pointers = X11Interop.EnumeratePointerDevices(display);
            var playerBySource = new Dictionary<int, int>();
            for (int i = 0; i < pointers.Count && i < 4; i++)
            {
                playerBySource[pointers[i].SourceId] = i;
                Debug.WriteLine($"X11FallbackInputListener: {pointers[i].Name} (XI id {pointers[i].SourceId}) -> Player {i + 1}");
            }
            bool multiPointer = playerBySource.Count > 1;

            // Multi-pointer aim: per-player virtual canvas fed by raw deltas
            // (same approach as the evdev listener). Single pointer: absolute
            // cursor position — exactly what the Wine game sees.
            var posX = new float[4];
            var posY = new float[4];
            for (int i = 0; i < 4; i++)
            {
                posX[i] = width / 2f;
                posY[i] = height / 2f;
            }

            var ev = new X11Interop.XEvent();
            while (!_killMe)
            {
                if (X11Interop.XPending(display) == 0)
                {
                    Thread.Sleep(2);
                    continue;
                }

                X11Interop.XNextEvent(display, ref ev);
                if (ev.Type != X11Interop.GenericEvent || ev.Extension != opcode)
                    continue;
                if (X11Interop.XGetEventData(display, ref ev) == 0)
                    continue;
                try
                {
                    var raw = System.Runtime.InteropServices.Marshal.PtrToStructure<X11Interop.XIRawEvent>(ev.Data);
                    int player = playerBySource.TryGetValue(raw.SourceId, out var p) ? p : 0;

                    switch (ev.EvType)
                    {
                        case X11Interop.XI_RawMotion when _handleMouse && _isGunGame:
                            if (multiPointer)
                            {
                                X11Interop.GetMotionDeltas(in raw, out double dx, out double dy);
                                posX[player] = Math.Clamp(posX[player] + (float)dx, 0, width);
                                posY[player] = Math.Clamp(posY[player] + (float)dy, 0, height);
                                UpdateGunPosition(player, posX[player] / width, posY[player] / height);
                            }
                            else if (X11Interop.XQueryPointer(display, root, out _, out _,
                                         out int rootX, out int rootY, out _, out _, out _))
                            {
                                UpdateGunPosition(player,
                                    Math.Clamp(rootX / width, 0f, 1f),
                                    Math.Clamp(rootY / height, 0f, 1f));
                            }
                            break;

                        case X11Interop.XI_RawButtonPress when _handleMouse:
                        case X11Interop.XI_RawButtonRelease when _handleMouse:
                            var mouseButton = raw.Detail switch
                            {
                                1 => RawMouseButton.LeftButton,
                                2 => RawMouseButton.MiddleButton,
                                3 => RawMouseButton.RightButton,
                                8 => RawMouseButton.Button4,
                                9 => RawMouseButton.Button5,
                                _ => RawMouseButton.None // 4-7 = scroll
                            };
                            if (mouseButton != RawMouseButton.None)
                                RouteMouseButton(mouseButton, player, ev.EvType == X11Interop.XI_RawButtonPress);
                            break;

                        case X11Interop.XI_RawKeyPress when _handleKeyboard:
                        case X11Interop.XI_RawKeyRelease when _handleKeyboard:
                            if (raw.Detail >= 8)
                            {
                                var evdevCode = (ushort)(raw.Detail - 8);
                                bool pressed = ev.EvType == X11Interop.XI_RawKeyPress;
                                foreach (var (binding, codes) in _keyBindings)
                                {
                                    if (Array.IndexOf(codes, evdevCode) < 0)
                                        continue;
                                    bool wasPressed = _keyState.TryGetValue(binding, out var s) && s;
                                    if (pressed != wasPressed) // filter X auto-repeat
                                    {
                                        _keyState[binding] = pressed;
                                        if (_keyboardAxis.HandleButton(binding, pressed))
                                            continue;
                                        MappingDispatch.Apply(binding.InputMapping, pressed, _gameProfile);
                                    }
                                }
                            }
                            break;
                    }
                }
                finally
                {
                    X11Interop.XFreeEventData(display, ref ev);
                }
            }
        }

        /// <summary>Explicit bindings first, then the per-player default gun map.</summary>
        private void RouteMouseButton(RawMouseButton mouseButton, int player, bool pressed)
        {
            var bound = _mouseBindings.Where(b => b.RawInputButton.MouseButton == mouseButton).ToList();
            if (bound.Count > 0)
            {
                foreach (var jsButton in bound)
                {
                    if (_keyboardAxis.HandleButton(jsButton, pressed))
                        continue;
                    MappingDispatch.Apply(jsButton.InputMapping, pressed, _gameProfile);
                }
                return;
            }

            if (!_isGunGame)
                return;
            var mapping = (player, mouseButton) switch
            {
                (0, RawMouseButton.LeftButton) => InputMapping.P1Button1,
                (0, RawMouseButton.RightButton) => InputMapping.P1Button2,
                (0, RawMouseButton.MiddleButton) => InputMapping.P1Button3,
                (1, RawMouseButton.LeftButton) => InputMapping.P2Button1,
                (1, RawMouseButton.RightButton) => InputMapping.P2Button2,
                (1, RawMouseButton.MiddleButton) => InputMapping.P2Button3,
                _ => (InputMapping?)null
            };
            if (mapping.HasValue)
                MappingDispatch.Apply(mapping.Value, pressed, _gameProfile);
        }

        // ---------- legacy XQueryPointer/XQueryKeymap polling (no XI2) ----------

        private void LegacyPollLoop(IntPtr display)
        {
            var root = X11Interop.XDefaultRootWindow(display);
            int screen = X11Interop.XDefaultScreen(display);
            float width = Math.Max(1, X11Interop.XDisplayWidth(display, screen));
            float height = Math.Max(1, X11Interop.XDisplayHeight(display, screen));

            uint prevMask = 0;
            var keymap = new byte[32];

            while (!_killMe)
            {
                if (_handleMouse)
                {
                    if (X11Interop.XQueryPointer(display, root, out _, out _,
                            out int rootX, out int rootY, out _, out _, out uint mask))
                    {
                        if (_isGunGame)
                        {
                            UpdateGunPosition(0,
                                Math.Clamp(rootX / width, 0f, 1f),
                                Math.Clamp(rootY / height, 0f, 1f));
                        }

                        HandleButtonEdge(mask, prevMask, X11Interop.Button1Mask, RawMouseButton.LeftButton, 1);
                        HandleButtonEdge(mask, prevMask, X11Interop.Button3Mask, RawMouseButton.RightButton, 2);
                        HandleButtonEdge(mask, prevMask, X11Interop.Button2Mask, RawMouseButton.MiddleButton, 3);
                        prevMask = mask;
                    }
                }

                if (_handleKeyboard && _keyBindings.Count > 0)
                {
                    X11Interop.XQueryKeymap(display, keymap);
                    foreach (var (binding, codes) in _keyBindings)
                    {
                        bool pressed = false;
                        foreach (var code in codes)
                        {
                            int keycode = code + 8; // X keycode offset over evdev
                            if (keycode < 256 && (keymap[keycode >> 3] & (1 << (keycode & 7))) != 0)
                            {
                                pressed = true;
                                break;
                            }
                        }
                        bool wasPressed = _keyState.TryGetValue(binding, out var s) && s;
                        if (pressed != wasPressed)
                        {
                            _keyState[binding] = pressed;
                            if (_keyboardAxis.HandleButton(binding, pressed))
                                continue;
                            MappingDispatch.Apply(binding.InputMapping, pressed, _gameProfile);
                        }
                    }
                }

                Thread.Sleep(4); // ~250 Hz
            }
        }

        private readonly Dictionary<JoystickButtons, bool> _keyState = new Dictionary<JoystickButtons, bool>();

        private void HandleButtonEdge(uint mask, uint prevMask, uint bit, RawMouseButton button, int defaultButtonNumber)
        {
            bool now = (mask & bit) != 0;
            bool before = (prevMask & bit) != 0;
            if (now == before)
                return;

            // Explicit mouse bindings first (single pointer — any stored device path matches).
            var bound = _mouseBindings.Where(b => b.RawInputButton.MouseButton == button).ToList();
            if (bound.Count > 0)
            {
                foreach (var jsButton in bound)
                {
                    if (_keyboardAxis.HandleButton(jsButton, now))
                        continue;
                    MappingDispatch.Apply(jsButton.InputMapping, now, _gameProfile);
                }
                return;
            }

            // Default gun map for unbound buttons (gun games only), P1.
            if (!_isGunGame)
                return;
            var mapping = defaultButtonNumber switch
            {
                1 => InputMapping.P1Button1,
                2 => InputMapping.P1Button2,
                3 => InputMapping.P1Button3,
                _ => (InputMapping?)null
            };
            if (mapping.HasValue)
                MappingDispatch.Apply(mapping.Value, now, _gameProfile);
        }

        private void UpdateGunPosition(int player, float factorX, float factorY)
        {
            var cfg = new GunAnalogMath.GunConfig(
                _minX, _maxX, _minY, _maxY,
                _is16Bit, _invertedMouseAxis,
                _isLuigisMansion || _isGunslinger, _isGunslinger);
            GunAnalogMath.Write(InputCode.AnalogBytes, player, factorX, factorY, cfg);
        }

        /// <summary>Same startup centering as EvdevMouseListener.</summary>
        private void CenterCrosshairs()
        {
            for (int player = 0; player < 4; player++)
                UpdateGunPosition(player, 0.5f, 0.5f);

            if (_isGunslinger)
            {
                for (int i = 0; i < 14; i += 2)
                    InputCode.AnalogBytes[i] = 0x80;
            }

            if (_gameProfile.ProfileName == "DarkEscape4D")
            {
                InputCode.AnalogBytes[8] = 60;
                InputCode.AnalogBytes[10] = 60;
            }
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Not applicable on Linux.
        }

        public void Stop()
        {
            _killMe = true;
            _thread?.Join(1000);
            _thread = null;
            _axisTickThread?.Join(1000);
            _axisTickThread = null;
            _keyState.Clear();
        }
    }
}
