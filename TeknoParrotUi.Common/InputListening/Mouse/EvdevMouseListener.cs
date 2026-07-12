using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    /// <summary>
    /// Linux gun-game mouse listener (evdev). Ports the fullscreen semantics of
    /// InputListenerRawInput to /dev/input/event* devices:
    /// - relative mice accumulate position over a virtual canvas
    /// - absolute devices (light guns, tablets) map their reported range directly
    /// - positions are converted to the game's analog range with the same
    ///   inverted/Luigi/Gunslinger byte layouts as the Windows listener.
    ///
    /// Device-to-player assignment: JoystickButtons entries whose RawInputButton
    /// DevicePath matches an event node are honoured; unbound mice are assigned
    /// to players in discovery order with a default button map
    /// (left=Button1, right=Button2, middle=Button3). Keyboard RawInputButton
    /// bindings are serviced from evdev keyboard devices via EvdevKeyMap.
    ///
    /// Not yet supported (Windows-only for now): windowed-mode cursor clipping,
    /// per-game special cases (Primeval Hunt split screen, Play canvas metrics),
    /// trackball games (blocked on the named-MMF game-side bridge).
    /// </summary>
    public class EvdevMouseListener : IInputListener
    {
        // Virtual canvas for relative-motion accumulation; any size works since
        // positions are normalized before conversion to game units.
        private const int CanvasWidth = 1920;
        private const int CanvasHeight = 1080;

        public string Name => "EvdevMouse";
        public bool IsSupported => OperatingSystem.IsLinux();

        private GameProfile _gameProfile;
        private List<JoystickButtons> _gunMappings;      // P{n}LightGun entries
        private List<JoystickButtons> _boundButtons;     // entries with mouse RawInputButton bindings
        private List<JoystickButtons> _keyboardBindings; // entries with keyboard RawInputButton bindings

        private float _minX, _maxX, _minY, _maxY;
        private bool _invertedMouseAxis;
        private bool _is16Bit;
        private bool _isLuigisMansion;
        private bool _isGunslinger;
        private bool _isGunGame;

        private readonly List<Thread> _threads = new List<Thread>();
        private volatile bool _killMe;

        // Keyboard/button wheel-gas-brake ramping ("Use Keyboard/Button For
        // Axis") — same engine the Windows RawInput listener runs. Without it
        // keyboards are completely dead in wheel games (Sega Rally 3 etc.):
        // their steering/pedal rows are analog, not digital mappings.
        private readonly KeyboardAxisEngine _keyboardAxis = new KeyboardAxisEngine();

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            _killMe = false;
            _gameProfile = gameProfile;
            MappingDispatch.ResetSessionState();
            _keyboardAxis.Initialize(gameProfile);
            if (_keyboardAxis.Enabled)
            {
                // Classic 16 ms ramp timer — must tick even with no key events
                // (axes ramp back to center on release).
                var tickThread = new Thread(() =>
                {
                    while (!_killMe)
                    {
                        _keyboardAxis.Tick();
                        Thread.Sleep(16);
                    }
                }) { IsBackground = true, Name = "EvdevKbdAxis" };
                tickThread.Start();
                _threads.Add(tickThread);
            }
            _minX = gameProfile.xAxisMin;
            _maxX = gameProfile.xAxisMax;
            _minY = gameProfile.yAxisMin;
            _maxY = gameProfile.yAxisMax;
            _invertedMouseAxis = gameProfile.InvertedMouseAxis;
            _is16Bit = gameProfile.Use16BitAnalog;
            _isLuigisMansion = gameProfile.EmulationProfile == EmulationProfile.LuigisMansion;
            _isGunslinger = gameProfile.EmulationProfile == EmulationProfile.GunslingerStratos3;

            var buttons = joystickButtons ?? new List<JoystickButtons>();
            _gunMappings = buttons.Where(b => b != null &&
                (b.InputMapping == InputMapping.P1LightGun || b.InputMapping == InputMapping.P2LightGun ||
                 b.InputMapping == InputMapping.P3LightGun || b.InputMapping == InputMapping.P4LightGun)).ToList();
            _boundButtons = buttons.Where(b => b?.RawInputButton != null &&
                b.RawInputButton.DeviceType == RawDeviceType.Mouse).ToList();
            _keyboardBindings = buttons.Where(b => b?.RawInputButton != null &&
                b.RawInputButton.DeviceType == RawDeviceType.Keyboard &&
                b.RawInputButton.KeyboardKey != Keys.None).ToList();

            // Merged always: this listener runs for every game, but gun analog
            // writes (centering + aim) must only happen for games that actually
            // have light-gun mappings — otherwise mouse movement would fight the
            // SDL2 gamepad listener over the same analog bytes (wheel games etc).
            _isGunGame = _gunMappings.Count > 0 || gameProfile.GunGame;

            if (_isGunGame)
                CenterCrosshairs();

            var mice = EvdevInterop.EnumerateMice();
            if (mice.Count == 0)
            {
                Debug.WriteLine("EvdevMouseListener: no mouse devices found");
            }
            else if (!_isGunGame && _boundButtons.Count == 0)
            {
                // Non-gun game without explicit mouse bindings: nothing for mice
                // to do — don't hold the devices open. Keyboards still run below.
                mice.Clear();
            }

            // Bound devices keep their player; unbound mice fill remaining players in order.
            var assignedPlayers = new HashSet<int>();
            var pending = new List<(EvdevInterop.MouseDevice Device, int Player)>();

            foreach (var mouse in mice)
            {
                int player = PlayerForBoundDevice(mouse.DevicePath);
                if (player >= 0)
                {
                    pending.Add((mouse, player));
                    assignedPlayers.Add(player);
                }
            }
            int nextFree = 0;
            foreach (var mouse in mice)
            {
                if (pending.Any(p => p.Device.EventNode == mouse.EventNode))
                    continue;
                while (nextFree < 4 && assignedPlayers.Contains(nextFree))
                    nextFree++;
                if (nextFree >= 4)
                    break;
                pending.Add((mouse, nextFree));
                assignedPlayers.Add(nextFree);
            }

            foreach (var (device, player) in pending)
            {
                var thread = new Thread(() => DeviceLoop(device, player))
                {
                    IsBackground = true,
                    Name = $"Evdev:{device.EventNode}"
                };
                thread.Start();
                _threads.Add(thread);
            }

            StartKeyboardThreads();
        }

        /// <summary>One reader thread per keyboard when keyboard bindings exist.</summary>
        private void StartKeyboardThreads()
        {
            if (_keyboardBindings.Count == 0)
                return;

            // Only real typing keyboards (letter keys in the capability bitmap);
            // devices that merely claim the kbd handler (power buttons, gaming-
            // mouse macro endpoints) are skipped unless a binding explicitly
            // targets them by device path.
            var allKeyboards = EvdevInterop.EnumerateKeyboards();
            var keyboards = allKeyboards.Where(k => k.HasTypingKeys ||
                _keyboardBindings.Any(b => b.RawInputButton.DevicePath == k.DevicePath)).ToList();
            var knownPaths = new HashSet<string>(keyboards.Select(k => k.DevicePath));

            int opened = 0;
            foreach (var keyboard in keyboards)
            {
                var device = keyboard;
                // Bindings for this exact device, plus bindings whose stored device is
                // not present (Windows-captured or stale paths) — any keyboard serves those.
                var bindings = _keyboardBindings.Where(b =>
                    b.RawInputButton.DevicePath == device.DevicePath ||
                    !knownPaths.Contains(b.RawInputButton.DevicePath)).ToList();
                if (bindings.Count == 0)
                    continue;

                var thread = new Thread(() => KeyboardLoop(device, bindings))
                {
                    IsBackground = true,
                    Name = $"EvdevKbd:{device.EventNode}"
                };
                thread.Start();
                _threads.Add(thread);
                opened++;
            }

            if (opened == 0)
                Debug.WriteLine("EvdevMouseListener: keyboard bindings exist but no usable keyboard device was started — check /dev/input permissions ('input' group)");
        }

        private void KeyboardLoop(EvdevInterop.MouseDevice device, List<JoystickButtons> bindings)
        {
            int fd = EvdevInterop.open(device.EventNode, EvdevInterop.O_RDONLY | EvdevInterop.O_NONBLOCK);
            if (fd < 0)
            {
                // Almost always EACCES: the user is not in the 'input' group.
                // (Mice can still work through per-vendor udev ACLs, which makes
                // this failure easy to miss — also surfaced in the launch console.)
                Debug.WriteLine($"EvdevMouseListener: cannot open keyboard {device.EventNode} " +
                                $"({EvdevInterop.CheckAccess(device.EventNode)}) — add user to 'input' group");
                return;
            }

            try
            {
                while (!_killMe)
                {
                    bool any = false;
                    while (EvdevInterop.TryReadEvent(fd, out var ev))
                    {
                        any = true;
                        // value: 0=up, 1=down, 2=auto-repeat (ignore repeats)
                        if (ev.Type != EvdevInterop.EV_KEY || ev.Value == 2)
                            continue;
                        var key = Keyboard.EvdevKeyMap.ToKeys(ev.Code);
                        if (key == Keys.None)
                            continue;
                        foreach (var binding in bindings)
                        {
                            if (binding.RawInputButton.KeyboardKey != key)
                                continue;
                            // Wheel/gas/brake rows are ramped by the axis engine,
                            // not dispatched as digital buttons.
                            if (_keyboardAxis.HandleButton(binding, ev.Value != 0))
                                continue;
                            ApplyMapping(binding.InputMapping, ev.Value != 0);
                        }
                    }
                    if (!any)
                        Thread.Sleep(2);
                }
            }
            finally
            {
                EvdevInterop.close(fd);
            }
        }

        private int PlayerForBoundDevice(string devicePath)
        {
            var gun = _gunMappings.FirstOrDefault(g => g.RawInputButton != null && g.RawInputButton.DevicePath == devicePath);
            if (gun == null)
                return -1;
            return gun.InputMapping switch
            {
                InputMapping.P2LightGun => 1,
                InputMapping.P3LightGun => 2,
                InputMapping.P4LightGun => 3,
                _ => 0
            };
        }

        private void DeviceLoop(EvdevInterop.MouseDevice device, int player)
        {
            int fd = EvdevInterop.open(device.EventNode, EvdevInterop.O_RDONLY | EvdevInterop.O_NONBLOCK);
            if (fd < 0)
            {
                Debug.WriteLine($"EvdevMouseListener: cannot open {device.EventNode} (add user to 'input' group?)");
                return;
            }

            Debug.WriteLine($"EvdevMouseListener: {device.Name} ({device.EventNode}) -> Player {player + 1}");

            bool hasAbsX = EvdevInterop.TryGetAbsInfo(fd, EvdevInterop.ABS_X, out var absX) && absX.Maximum > absX.Minimum;
            bool hasAbsY = EvdevInterop.TryGetAbsInfo(fd, EvdevInterop.ABS_Y, out var absY) && absY.Maximum > absY.Minimum;

            // Start centered on the virtual canvas.
            float posX = CanvasWidth / 2f;
            float posY = CanvasHeight / 2f;
            bool moved = false;

            try
            {
                while (!_killMe)
                {
                    bool any = false;
                    while (EvdevInterop.TryReadEvent(fd, out var ev))
                    {
                        any = true;
                        switch (ev.Type)
                        {
                            case EvdevInterop.EV_REL when ev.Code == EvdevInterop.REL_X:
                                posX = Math.Clamp(posX + ev.Value, 0, CanvasWidth);
                                moved = true;
                                break;
                            case EvdevInterop.EV_REL when ev.Code == EvdevInterop.REL_Y:
                                posY = Math.Clamp(posY + ev.Value, 0, CanvasHeight);
                                moved = true;
                                break;
                            case EvdevInterop.EV_ABS when ev.Code == EvdevInterop.ABS_X && hasAbsX:
                                posX = (ev.Value - absX.Minimum) / (float)(absX.Maximum - absX.Minimum) * CanvasWidth;
                                moved = true;
                                break;
                            case EvdevInterop.EV_ABS when ev.Code == EvdevInterop.ABS_Y && hasAbsY:
                                posY = (ev.Value - absY.Minimum) / (float)(absY.Maximum - absY.Minimum) * CanvasHeight;
                                moved = true;
                                break;
                            case EvdevInterop.EV_KEY:
                                HandleButtonEvent(device, player, ev.Code, ev.Value != 0);
                                break;
                        }
                    }

                    if (moved)
                    {
                        // Aim analog writes only for gun games — in other games
                        // the SDL2 listener owns the analog bytes.
                        if (_isGunGame)
                            UpdateGunPosition(player, posX / CanvasWidth, posY / CanvasHeight);
                        moved = false;
                    }

                    if (!any)
                        Thread.Sleep(2);
                }
            }
            finally
            {
                EvdevInterop.close(fd);
            }
        }

        // ---------- buttons ----------

        private void HandleButtonEvent(EvdevInterop.MouseDevice device, int player, ushort code, bool pressed)
        {
            var mouseButton = code switch
            {
                EvdevInterop.BTN_LEFT => RawMouseButton.LeftButton,
                EvdevInterop.BTN_TOUCH => RawMouseButton.LeftButton, // light gun triggers report BTN_TOUCH
                EvdevInterop.BTN_RIGHT => RawMouseButton.RightButton,
                EvdevInterop.BTN_MIDDLE => RawMouseButton.MiddleButton,
                EvdevInterop.BTN_SIDE => RawMouseButton.Button4,
                EvdevInterop.BTN_EXTRA => RawMouseButton.Button5,
                _ => RawMouseButton.None
            };
            if (mouseButton == RawMouseButton.None)
                return;

            // Explicit bindings for this device first.
            var bound = _boundButtons.Where(b =>
                b.RawInputButton.DevicePath == device.DevicePath &&
                b.RawInputButton.MouseButton == mouseButton).ToList();

            if (bound.Count > 0)
            {
                foreach (var jsButton in bound)
                {
                    // Mouse-button-bound axis rows also go to the ramp engine.
                    if (_keyboardAxis.HandleButton(jsButton, pressed))
                        continue;
                    ApplyMapping(jsButton.InputMapping, pressed);
                }
                return;
            }

            // Default map for unbound mice (gun games only — in other games a
            // mouse click must not fire buttons the user never bound):
            // left=Button1 (trigger), right=Button2, middle=Button3.
            if (!_isGunGame)
                return;
            var mapping = mouseButton switch
            {
                RawMouseButton.LeftButton => PlayerMapping(player, 1),
                RawMouseButton.RightButton => PlayerMapping(player, 2),
                RawMouseButton.MiddleButton => PlayerMapping(player, 3),
                _ => (InputMapping?)null
            };
            if (mapping.HasValue)
                ApplyMapping(mapping.Value, pressed);
        }

        private static InputMapping? PlayerMapping(int player, int button)
        {
            return (player, button) switch
            {
                (0, 1) => InputMapping.P1Button1,
                (0, 2) => InputMapping.P1Button2,
                (0, 3) => InputMapping.P1Button3,
                (1, 1) => InputMapping.P2Button1,
                (1, 2) => InputMapping.P2Button2,
                (1, 3) => InputMapping.P2Button3,
                _ => null
            };
        }

        /// <summary>Full RawInput-parity dispatch (shared with the X11 fallback).</summary>
        private void ApplyMapping(InputMapping mapping, bool pressed) => MappingDispatch.Apply(mapping, pressed, _gameProfile);

        // ---------- gun position ----------

        /// <summary>
        /// Convert a normalized (0..1) position into the game's analog bytes,
        /// using the same layouts as InputListenerRawInput.HandleRawInputGun
        /// (math extracted to <see cref="GunAnalogMath"/> for verifiability).
        /// </summary>
        private void UpdateGunPosition(int player, float factorX, float factorY)
        {
            var cfg = new GunAnalogMath.GunConfig(
                _minX, _maxX, _minY, _maxY,
                _is16Bit, _invertedMouseAxis,
                _isLuigisMansion || _isGunslinger, _isGunslinger);
            GunAnalogMath.Write(InputCode.AnalogBytes, player, factorX, factorY, cfg);
        }

        /// <summary>Write centered analog values on startup (fullscreen semantics).</summary>
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
            foreach (var thread in _threads)
                thread.Join(1000);
            _threads.Clear();
        }
    }
}
