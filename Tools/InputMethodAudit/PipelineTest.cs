using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

namespace InputMethodAudit
{
    /// <summary>
    /// End-to-end pipeline test with a FAKE gamepad source: loads a real
    /// user profile, runs InputListenerXInput against scripted XInput-shaped
    /// state (no hardware needed), and verifies the presses land in InputCode
    /// and in the JVS shared-memory bytes the game-side hook reads
    /// (Pcsx2x6Pipe layout). Isolates "mapping/pipe broken" from "SDL2 device
    /// not delivering input".
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -- pipeline-test [userprofile.xml]
    /// </summary>
    internal static class PipelineTest
    {
        private sealed class ScriptedSource : IXInputSource
        {
            private readonly object _sync = new object();
            private State _state = new State { PacketNumber = 1 };

            public bool IsConnected => true;

            public State GetState()
            {
                lock (_sync) return _state;
            }

            public void Set(Action<XiGamepadRef> mutate)
            {
                lock (_sync)
                {
                    var g = new XiGamepadRef { Gamepad = _state.Gamepad };
                    mutate(g);
                    _state = new State { PacketNumber = _state.PacketNumber + 1, Gamepad = g.Gamepad };
                }
            }

            public sealed class XiGamepadRef { public XiGamepad Gamepad; }
        }

        public static int Run(string profilePath)
        {
            var profile = JoystickHelper.DeSerializeGameProfile(profilePath, true);
            if (profile == null)
            {
                Console.Error.WriteLine($"Cannot load profile: {profilePath}");
                return 1;
            }
            if (string.IsNullOrEmpty(profile.ProfileName))
                profile.ProfileName = Path.GetFileNameWithoutExtension(profilePath);
            // Controls come from InputBindings/<profile>.json when present (the
            // authoritative store) — same chain as the real game launch
            TeknoParrotUi.Common.InputListening.ProfileStorage.BindingsStore.Apply(profile);
            Console.WriteLine($"Profile: {profile.ProfileName ?? Path.GetFileNameWithoutExtension(profilePath)} ({profile.EmulationProfile})");

            InputCode.ButtonMode = profile.EmulationProfile;
            InputCode.GameProfile = profile;
            JvsPackageEmulator.Initialize(profile);

            // Control sender exactly like GameSession
            var sender = PipeFactory.CreateControlSender(profile.EmulationProfile, profile);
            sender?.Start();
            Console.WriteLine($"ControlSender: {sender?.GetType().Name ?? "(none)"}");

            // The XInput mapper with a scripted source instead of SDL2 slot 0
            var source = new ScriptedSource();
            var mapper = new InputListenerXInput();
            InputListenerXInput.KillMe = false;
            var thread = new Thread(() => mapper.ListenXInput(false, 0, profile.JoystickButtons, 0, profile, source)) { IsBackground = true };
            thread.Start();
            Thread.Sleep(300);

            int failures = 0;

            // Find XInput-bound digital buttons in the profile and fire each one
            var bound = profile.JoystickButtons
                .Where(b => b.XInputButton != null && b.XInputButton.IsButton && b.AnalogType == AnalogType.None)
                .ToList();
            Console.WriteLine($"Testing {bound.Count} digital XInput binding(s)...");

            foreach (var b in bound)
            {
                if (!CanReadMapped(b.InputMapping))
                {
                    Console.WriteLine($"  SKIP  {b.ButtonName,-26} ({b.InputMapping}) — mapping not covered by the test read-back");
                    continue;
                }
                var flag = (GamepadButtonFlags)b.XInputButton.ButtonCode;
                source.Set(g => g.Gamepad.Buttons = flag);
                Thread.Sleep(120);
                bool pressed = ReadMapped(b);
                source.Set(g => g.Gamepad.Buttons = GamepadButtonFlags.None);
                Thread.Sleep(120);
                bool released = !ReadMapped(b);
                var ok = pressed && released;
                if (!ok) failures++;
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {b.ButtonName,-26} ({b.InputMapping}) = {flag}{(pressed ? "" : "  [press not seen]")}{(released ? "" : "  [stuck]")}");
            }

            // JVS shared memory check (Pcsx2x6Pipe writes bytes 8..18) — while
            // the initial mapper thread is still alive
            if (profile.EmulationProfile == EmulationProfile.pcsx2x6 && sender != null)
            {
                var startBtn = bound.FirstOrDefault(b => b.InputMapping == InputMapping.P1Button6 && CanReadMapped(b.InputMapping))
                               ?? bound.FirstOrDefault(b => CanReadMapped(b.InputMapping));
                if (startBtn != null)
                {
                    var flag = (GamepadButtonFlags)startBtn.XInputButton.ButtonCode;
                    byte before = ReadState(8, 12);
                    source.Set(g => g.Gamepad.Buttons = flag);
                    Thread.Sleep(200);
                    byte during = ReadState(8, 12);
                    source.Set(g => g.Gamepad.Buttons = GamepadButtonFlags.None);
                    Thread.Sleep(200);
                    bool changed = during != before;
                    if (!changed) failures++;
                    Console.WriteLine($"  {(changed ? "PASS" : "FAIL")}  JVS StateView bytes changed while '{startBtn.ButtonName}' held (before=0x{before:X2} during=0x{during:X2})");
                }
            }

            failures += TestSdl2Analog(profile, source);
            failures += TestKeyboardAxis(profile);
            failures += TestRawInputDigital(profile);
            failures += TestSecondRunKeyboardAxis(profile);

            InputListenerXInput.KillMe = true;
            sender?.Stop();
            Console.WriteLine(failures == 0 ? "PIPELINE OK — mapping + control sender deliver input end-to-end." : $"{failures} FAILURE(S).");
            return failures == 0 ? 0 : 1;
        }

        private static byte ReadState(int offsetFrom, int offsetTo)
        {
            byte combined = 0;
            for (int i = offsetFrom; i <= offsetTo; i++)
            {
                JvsHelper.StateView.Read(i, out byte b);
                combined ^= b;
            }
            return combined;
        }

        /// <summary>SDL2 analog: drive stick/trigger values through XInputButton axis bindings and verify AnalogBytes move.</summary>
        private static int TestSdl2Analog(GameProfile profile, ScriptedSource source)
        {
            int failures = 0;

            // Keyboard-axis mode makes the pad yield wheel/gas/brake — turn it
            // off for this phase (restored afterwards for the keyboard test)
            var kbField = profile.ConfigValues.Find(cv => cv.FieldName == "Use Keyboard/Button For Axis");
            var savedKb = kbField?.FieldValue;
            if (kbField != null) kbField.FieldValue = "0";

            // Restart the mapper thread so it re-reads the config
            InputListenerXInput.KillMe = true;
            Thread.Sleep(150);
            InputListenerXInput.KillMe = false;
            var mapper = new InputListenerXInput();
            var thread = new Thread(() => mapper.ListenXInput(false, 0, profile.JoystickButtons, 0, profile, source)) { IsBackground = true };
            thread.Start();
            Thread.Sleep(250);

            var analogRows = profile.JoystickButtons
                .Where(b => b.XInputButton != null && !b.XInputButton.IsButton &&
                            b.AnalogType is AnalogType.Wheel or AnalogType.Gas or AnalogType.Brake or AnalogType.AnalogJoystick or AnalogType.AnalogJoystickReverse)
                .ToList();
            Console.WriteLine($"Testing {analogRows.Count} analog SDL2 binding(s)...");

            foreach (var row in analogRows)
            {
                int byteIndex = AnalogByteFor(row.InputMapping);
                if (byteIndex < 0) continue;

                // Gun aim rows only support stick axes — trigger bindings are dead
                // (classic behaviour: CalculateWheelPos returns center for them)
                if (profile.GunGame &&
                    row.AnalogType is AnalogType.AnalogJoystick or AnalogType.AnalogJoystickReverse &&
                    (row.XInputButton.IsLeftTrigger || row.XInputButton.IsRightTrigger))
                {
                    Console.WriteLine($"  SKIP  SDL2 analog {row.ButtonName,-22} — trigger bound to gun aim (never supported; rebind to a stick or use Relative Input)");
                    continue;
                }

                // With "Use Relative Input" the relative timer owns gun aim; the
                // absolute Gun X/Y rows are intentionally inert
                if (profile.GunGame &&
                    row.AnalogType is AnalogType.AnalogJoystick or AnalogType.AnalogJoystickReverse &&
                    profile.ConfigValues.Any(cv => cv.FieldName == "Use Relative Input" && cv.FieldValue == "1"))
                {
                    Console.WriteLine($"  SKIP  SDL2 analog {row.ButtonName,-22} — Use Relative Input is on (relative aim owns this byte)");
                    continue;
                }

                byte before = InputCode.AnalogBytes[byteIndex];
                source.Set(g =>
                {
                    var xi = row.XInputButton;
                    if (xi.IsLeftTrigger) g.Gamepad.LeftTrigger = 255;
                    else if (xi.IsRightTrigger) g.Gamepad.RightTrigger = 255;
                    else if (xi.IsLeftThumbX) g.Gamepad.LeftThumbX = xi.IsAxisMinus ? short.MinValue : short.MaxValue;
                    else if (xi.IsLeftThumbY) g.Gamepad.LeftThumbY = xi.IsAxisMinus ? short.MinValue : short.MaxValue;
                    else if (xi.IsRightThumbX) g.Gamepad.RightThumbX = xi.IsAxisMinus ? short.MinValue : short.MaxValue;
                    else if (xi.IsRightThumbY) g.Gamepad.RightThumbY = xi.IsAxisMinus ? short.MinValue : short.MaxValue;
                });
                Thread.Sleep(150);
                byte during = InputCode.AnalogBytes[byteIndex];
                source.Set(g => g.Gamepad = new ScriptedSource.XiGamepadRef().Gamepad);
                Thread.Sleep(150);

                bool ok = during != before;
                if (!ok) failures++;
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  SDL2 analog {row.ButtonName,-22} ({row.AnalogType}) byte[{byteIndex}] {before:X2} -> {during:X2}");
            }

            InputListenerXInput.KillMe = true;
            Thread.Sleep(150);
            if (kbField != null) kbField.FieldValue = savedKb;
            return failures;
        }

        /// <summary>Keyboard-axis engine: press wheel/gas/brake keys and verify the ramp writes the right bytes.</summary>
        private static int TestKeyboardAxis(GameProfile profile)
        {
            int failures = 0;

            var kbField = profile.ConfigValues.Find(cv => cv.FieldName == "Use Keyboard/Button For Axis");
            if (kbField == null)
            {
                Console.WriteLine("Keyboard-axis: profile has no 'Use Keyboard/Button For Axis' option — skipped.");
                return 0;
            }
            var savedKb = kbField.FieldValue;
            kbField.FieldValue = "1";

            var engine = new KeyboardAxisEngine();
            engine.Initialize(profile);

            int Check(string rowName, int byteIndex, bool expectIncrease)
            {
                var row = profile.JoystickButtons.FirstOrDefault(b => b.ButtonName == rowName);
                if (row == null)
                    return 0; // row not present in this game
                byte before = InputCode.AnalogBytes[byteIndex];
                if (!engine.HandleButton(row, true))
                {
                    Console.WriteLine($"  FAIL  kb-axis {rowName}: engine did not consume the key");
                    return 1;
                }
                for (int i = 0; i < 10; i++) engine.Tick();
                byte during = InputCode.AnalogBytes[byteIndex];
                engine.HandleButton(row, false);
                for (int i = 0; i < 30; i++) engine.Tick();
                byte after = InputCode.AnalogBytes[byteIndex];

                bool moved = expectIncrease ? during > before : during < before;
                bool returned = expectIncrease ? after < during : after > during;
                bool ok = moved && returned;
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  kb-axis {rowName,-22} byte[{byteIndex}] {before:X2} -> {during:X2} -> {after:X2}");
                return ok ? 0 : 1;
            }

            Console.WriteLine("Testing keyboard-axis (wheel/gas/brake)...");
            InputCode.AnalogBytes[0] = 0x80;
            engine.Initialize(profile); // re-seed ramp state from centered bytes
            failures += Check("Wheel Axis Left", 0, expectIncrease: false);
            failures += Check("Wheel Axis Right", 0, expectIncrease: true);
            failures += Check("Gas", 2, expectIncrease: true);
            failures += Check("Brake", 4, expectIncrease: true);

            kbField.FieldValue = savedKb;
            return failures;
        }

        /// <summary>
        /// Regression test for "axes dead on second launch": the static 16ms
        /// keyboard-axis timer used to be hooked once (static one-shot guard) to
        /// the first listener instance, so the second session's keys ramped a
        /// dead object. Simulates run → stop → run through the REAL timer chain
        /// (HandleRawInputButton → engine → timer tick → InputCode bytes).
        /// </summary>
        private static int TestSecondRunKeyboardAxis(GameProfile profile)
        {
            var kbField = profile.ConfigValues.Find(cv => cv.FieldName == "Use Keyboard/Button For Axis");
            var gasRow = profile.JoystickButtons.FirstOrDefault(b => b.ButtonName == "Gas");
            if (kbField == null || gasRow == null)
            {
                Console.WriteLine("Second-run kb-axis: no keyboard-axis option or Gas row — skipped.");
                return 0;
            }
            var savedKb = kbField.FieldValue;
            kbField.FieldValue = "1";
            int failures = 0;

            Console.WriteLine("Testing keyboard-axis across two sessions (timer rebind)...");
            for (int session = 1; session <= 2; session++)
            {
                // Fresh listener per session, exactly like a real game launch
                var listener = new InputListenerRawInput();
                listener.InitForTests(profile);
                listener.StartKeyboardAxisTimerForTests();

                InputCode.AnalogBytes[2] = 0x00;
                listener.HandleButtonForTests(gasRow, true);
                Thread.Sleep(300); // ~18 ticks of the 16ms timer
                byte during = InputCode.AnalogBytes[2];
                listener.HandleButtonForTests(gasRow, false);

                bool ok = during > 0x00;
                if (!ok) failures++;
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  session {session}: Gas ramps via the 16ms timer (byte[2] 0x00 -> 0x{during:X2})");

                // Simulate the game exiting (InputListener.StopListening path)
                InputListenerRawInput.StopTimers();
            }

            kbField.FieldValue = savedKb;
            return failures;
        }

        /// <summary>RawInput digital: drive keyboard-bound button rows through the RawInput handler.</summary>
        private static int TestRawInputDigital(GameProfile profile)
        {
            int failures = 0;
            var listener = new InputListenerRawInput();

            // Prime the listener state without spawning the window-search loop
            var rawRows = profile.JoystickButtons
                .Where(b => b.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None &&
                            b.AnalogType == AnalogType.None)
                .ToList();
            Console.WriteLine($"Testing {rawRows.Count} digital RawInput binding(s)...");
            if (rawRows.Count == 0)
                return 0;

            listener.InitForTests(profile);
            foreach (var row in rawRows)
            {
                if (!CanReadMapped(row.InputMapping))
                {
                    Console.WriteLine($"  SKIP  RawInput {row.ButtonName,-24} ({row.InputMapping}) — mapping not covered by the test read-back");
                    continue;
                }
                listener.HandleButtonForTests(row, true);
                Thread.Sleep(30);
                bool pressed = ReadMapped(row);
                listener.HandleButtonForTests(row, false);
                Thread.Sleep(30);
                bool released = !ReadMapped(row);
                bool ok = pressed && released;
                if (!ok) failures++;
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  RawInput {row.ButtonName,-24} ({row.InputMapping}){(pressed ? "" : "  [press not seen]")}{(released ? "" : "  [stuck]")}");
            }
            return failures;
        }

        private static int AnalogByteFor(InputMapping mapping)
        {
            var name = mapping.ToString();
            if (name.StartsWith("Analog") && int.TryParse(name.Substring("Analog".Length), out var n))
                return n;
            return -1;
        }

        private static bool CanReadMapped(InputMapping mapping) => ReadableMappings.Contains(mapping);

        private static readonly HashSet<InputMapping> ReadableMappings = new HashSet<InputMapping>
        {
            InputMapping.Test, InputMapping.Service1, InputMapping.Service2, InputMapping.Coin1, InputMapping.Coin2,
            InputMapping.P1Button1, InputMapping.P1Button2, InputMapping.P1Button3, InputMapping.P1Button4,
            InputMapping.P1Button5, InputMapping.P1Button6, InputMapping.P1ButtonUp, InputMapping.P1ButtonDown,
            InputMapping.P1ButtonLeft, InputMapping.P1ButtonRight, InputMapping.P1ButtonStart,
            InputMapping.P2Button1, InputMapping.P2Button2, InputMapping.P2Button3, InputMapping.P2Button4,
            InputMapping.P2Button5, InputMapping.P2Button6, InputMapping.P2ButtonUp, InputMapping.P2ButtonDown,
            InputMapping.P2ButtonLeft, InputMapping.P2ButtonRight, InputMapping.P2ButtonStart,
            InputMapping.P1RelativeUp, InputMapping.P1RelativeDown, InputMapping.P1RelativeLeft, InputMapping.P1RelativeRight,
            InputMapping.P2RelativeUp, InputMapping.P2RelativeDown, InputMapping.P2RelativeLeft, InputMapping.P2RelativeRight
        };

        private static bool ReadMapped(JoystickButtons b)
        {
            var p0 = InputCode.PlayerDigitalButtons[0];
            var p1 = InputCode.PlayerDigitalButtons[1];
            return b.InputMapping switch
            {
                InputMapping.Test => p0.Test == true,
                InputMapping.Service1 => p0.Service == true,
                InputMapping.Service2 => p1.Service == true,
                InputMapping.Coin1 => p0.Coin == true,
                InputMapping.Coin2 => p1.Coin == true,
                InputMapping.P1Button1 => p0.Button1 == true,
                InputMapping.P1Button2 => p0.Button2 == true,
                InputMapping.P1Button3 => p0.Button3 == true,
                InputMapping.P1Button4 => p0.Button4 == true,
                InputMapping.P1Button5 => p0.Button5 == true,
                InputMapping.P1Button6 => p0.Button6 == true,
                InputMapping.P1ButtonUp => p0.Up == true,
                InputMapping.P1ButtonDown => p0.Down == true,
                InputMapping.P1ButtonLeft => p0.Left == true,
                InputMapping.P1ButtonRight => p0.Right == true,
                InputMapping.P1ButtonStart => p0.Start == true,
                InputMapping.P2Button1 => p1.Button1 == true,
                InputMapping.P2Button2 => p1.Button2 == true,
                InputMapping.P2Button3 => p1.Button3 == true,
                InputMapping.P2Button4 => p1.Button4 == true,
                InputMapping.P2Button5 => p1.Button5 == true,
                InputMapping.P2Button6 => p1.Button6 == true,
                InputMapping.P2ButtonUp => p1.Up == true,
                InputMapping.P2ButtonDown => p1.Down == true,
                InputMapping.P2ButtonLeft => p1.Left == true,
                InputMapping.P2ButtonRight => p1.Right == true,
                InputMapping.P2ButtonStart => p1.Start == true,
                InputMapping.P1RelativeUp => p0.RelativeUp == true,
                InputMapping.P1RelativeDown => p0.RelativeDown == true,
                InputMapping.P1RelativeLeft => p0.RelativeLeft == true,
                InputMapping.P1RelativeRight => p0.RelativeRight == true,
                InputMapping.P2RelativeUp => p1.RelativeUp == true,
                InputMapping.P2RelativeDown => p1.RelativeDown == true,
                InputMapping.P2RelativeLeft => p1.RelativeLeft == true,
                InputMapping.P2RelativeRight => p1.RelativeRight == true,
                _ => false
            };
        }
    }
}
