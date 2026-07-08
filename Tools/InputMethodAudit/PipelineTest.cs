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

            // JVS shared memory check (Pcsx2x6Pipe writes bytes 8..18)
            if (profile.EmulationProfile == EmulationProfile.pcsx2x6 && sender != null)
            {
                var startBtn = bound.FirstOrDefault(b => b.InputMapping == InputMapping.P1Button6) ?? bound.FirstOrDefault();
                if (startBtn != null)
                {
                    var flag = (GamepadButtonFlags)startBtn.XInputButton.ButtonCode;
                    byte before = ReadState(9, 12);
                    source.Set(g => g.Gamepad.Buttons = flag);
                    Thread.Sleep(200);
                    byte during = ReadState(9, 12);
                    source.Set(g => g.Gamepad.Buttons = GamepadButtonFlags.None);
                    Thread.Sleep(200);
                    bool changed = during != before;
                    if (!changed) failures++;
                    Console.WriteLine($"  {(changed ? "PASS" : "FAIL")}  JVS StateView bytes changed while '{startBtn.ButtonName}' held (before=0x{before:X2} during=0x{during:X2})");
                }
            }

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
