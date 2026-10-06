using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

internal static class DrivingChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static readonly BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static byte ConvertX(InputListenerXInput listener, JoystickButtons binding, State state) =>
        (byte)typeof(InputListenerXInput).GetMethod("ModifyAnalog", Instance).Invoke(listener, new object[] { binding, state, 0, 0 });
    private static byte ConvertD(InputListenerDirectInput listener, JoystickButtons binding, int value) =>
        (byte)typeof(InputListenerDirectInput).GetMethod("ModifyAnalog", Instance).Invoke(listener,
            new object[] { binding, new JoystickUpdate { RawOffset = (int)JoystickOffset.X, Value = value } });

    internal static void Run(string root)
    {
        var oldData = Lazydata.ParrotData;
        var fields = new[] {
            typeof(InputListenerDirectInput).GetField("_gameProfile", Static),
            typeof(InputListenerDirectInput).GetField("minValWheel", Static),
            typeof(InputListenerDirectInput).GetField("maxValWheel", Static),
            typeof(InputListenerDirectInput).GetField("KeyboardorButtonAxis", Static),
            typeof(InputListenerXInput).GetField("_gameProfile", Static) };
        var values = fields.Select(field => field.GetValue(null)).ToArray();
        var xi = new InputListenerXInput(); var di = new InputListenerDirectInput();
        var games = 0; var axes = 0; var samples = 0; var shifts = 0;
        try
        {
            Lazydata.ParrotData = new ParrotData { FullAxisGas = true, FullAxisBrake = true, ReverseAxisGas = false, ReverseAxisBrake = false, UseSto0ZDrivingHack = false };
            fields[1].SetValue(null, 0); fields[2].SetValue(null, 255); fields[3].SetValue(null, false);
            var serializer = new XmlSerializer(typeof(GameProfile));
            foreach (var file in Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), "ss32_*.xml"))
            {
                GameProfile profile;
                using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                var controls = profile.JoystickButtons.Where(button => button.ButtonName.EndsWith("Steering wheel") || button.ButtonName.EndsWith("Handlebar") ||
                    button.ButtonName.EndsWith("Gas pedal") || button.ButtonName.EndsWith("Accelerator") || button.ButtonName.EndsWith("Brake pedal")).ToArray();
                if (controls.Length == 0) continue;
                var oldProfile = profile.Clone(); oldProfile.GameProfileRevision = 3;
                foreach (var control in oldProfile.JoystickButtons.Where(button => button.InputMapping.ToString().StartsWith("Analog")))
                {
                    control.AnalogType = AnalogType.AnalogJoystick;
                    control.XInputButton = new XInputButton { IsRightTrigger = true, XInputIndex = 2 };
                }
                var migrated = ArcadeGameRevisions.CreateLaunchProfile(oldProfile);
                foreach (var control in controls)
                {
                    var updated = migrated.JoystickButtons.Single(button => button.ButtonName == control.ButtonName);
                    Require(updated.AnalogType == control.AnalogType && updated.XInputButton?.XInputIndex == 2,
                        "Old driving profile failed role migration or lost its controller binding");
                }
                ++games; fields[0].SetValue(null, profile); fields[4].SetValue(null, profile);
                InputCode.GameProfile = profile;
                var pipe = new TeknoSS32Pipe(); pipe.Start();
                try
                {
                    foreach (var control in controls)
                    {
                        ++axes;
                        var wheel = control.ButtonName.EndsWith("Steering wheel") || control.ButtonName.EndsWith("Handlebar");
                        var expected = wheel ? AnalogType.Wheel : control.ButtonName.EndsWith("Brake pedal") ? AnalogType.Brake : AnalogType.Gas;
                        Require(control.AnalogType == expected, profile.ProfileName + " incorrect analog role: " + control.ButtonName);
                        control.DirectInputButton = new JoystickButton { Button = (int)JoystickOffset.X, IsAxis = true };
                        control.XInputButton = wheel ? new XInputButton { XInputIndex = 0, IsLeftThumbX = true } :
                            new XInputButton { XInputIndex = 0, IsRightTrigger = expected == AnalogType.Gas, IsLeftTrigger = expected == AnalogType.Brake };
                        var source = int.Parse(control.InputMapping.ToString().Substring(6));
                        var analogOrder = profile.JoystickButtons.Where(button => button.InputMapping.ToString().StartsWith("Analog", StringComparison.Ordinal)).ToArray();
                        var offset = 24 + Array.IndexOf(analogOrder, control);
                        byte previous = 0;
                        foreach (var raw in new[] { 0, 16384, 32768, 49152, 65535 })
                        {
                            var value = ConvertD(di, control, raw);
                            Require(raw == 0 ? value == 0 : value > previous, "DirectInput axis direction/range " + profile.ProfileName + " " + control.ButtonName);
                            Require(raw != 65535 || value == 255, "DirectInput full travel");
                            InputCode.AnalogBytes[source] = value; pipe.Transmit();
                            Require(JvsHelper.StateView.ReadByte(offset) == value, "Driving axis lost in SSIN bridge");
                            previous = value; ++samples;
                        }
                        foreach (var raw in wheel ? new[] { -32768, 0, 32767 } : new[] { 0, 64, 128, 255 })
                        {
                            var state = new State { Gamepad = new Gamepad { LeftThumbX = (short)(wheel ? raw : 0), LeftTrigger = (byte)(expected == AnalogType.Brake ? raw : 0), RightTrigger = (byte)(expected == AnalogType.Gas ? raw : 0) } };
                            var value = ConvertX(xi, control, state);
                            Require(wheel ? raw == 0 ? value >= 126 && value <= 129 : raw < 0 ? value <= 1 : value >= 254 : value == raw,
                                "XInput neutral/pedal direction " + profile.ProfileName + " " + control.ButtonName);
                            InputCode.AnalogBytes[source] = value; pipe.Transmit();
                            Require(JvsHelper.StateView.ReadByte(offset) == value, "XInput axis lost in SSIN bridge"); ++samples;
                        }
                        if (!wheel)
                        {
                            Lazydata.ParrotData.ReverseAxisBrake = true;
                            var independentReverse = ConvertD(di, control, 65535);
                            Require(independentReverse == (expected == AnalogType.Brake ? 0 : 255), "Gas/brake reversal settings were mixed");
                            ++samples;
                            Lazydata.ParrotData.ReverseAxisBrake = false;
                            Lazydata.ParrotData.FullAxisBrake = false;
                            var independentRange = ConvertD(di, control, 49151);
                            Require(independentRange == (expected == AnalogType.Brake ? 128 : 191), "Gas/brake full-axis settings were mixed");
                            ++samples; Lazydata.ParrotData.FullAxisBrake = true;
                            foreach (var stick in new[] { 0, 1, 2, 3 })
                            foreach (var negative in new[] { false, true })
                            {
                                control.XInputButton = new XInputButton { XInputIndex = 0, IsAxisMinus = negative,
                                    IsLeftThumbX = stick == 0, IsLeftThumbY = stick == 1, IsRightThumbX = stick == 2, IsRightThumbY = stick == 3 };
                                foreach (var travel in new[] { 0, 8192, 16384, 24576, 32767, -16384 })
                                {
                                    var position = (short)(negative ? -travel : travel);
                                    var state = new State { Gamepad = new Gamepad {
                                        LeftThumbX = stick == 0 ? position : (short)0, LeftThumbY = stick == 1 ? position : (short)0,
                                        RightThumbX = stick == 2 ? position : (short)0, RightThumbY = stick == 3 ? position : (short)0 } };
                                    var value = ConvertX(xi, control, state);
                                    Require(value == (travel < 0 ? 0 : travel == 32767 ? 255 : travel / 128),
                                        "Stick pedal must release at neutral and reach full travel in its selected direction");
                                    InputCode.AnalogBytes[source] = value; pipe.Transmit();
                                    Require(JvsHelper.StateView.ReadByte(offset) == value, "Stick pedal lost in SSIN bridge"); ++samples;
                                }
                            }
                        }
                    }
                    if (profile.ProfileName.StartsWith("ss32_slipstrm", StringComparison.Ordinal))
                        Require(profile.JoystickButtons.Any(button => button.ButtonName == "Player 1 Gear change" && !button.InputMapping.ToString().StartsWith("Analog")), "Slip Stream shifter missing");
                }
                finally { pipe.Stop(); }
                var shifter = profile.ConfigValues.FirstOrDefault(field => field.FieldName == "Shifter Mode");
                if (shifter != null)
                {
                    var gear = profile.JoystickButtons.Single(button => button.ButtonName == "Player 1 Gear change");
                    var digital = profile.JoystickButtons.Where(button => !button.InputMapping.ToString().StartsWith("Analog") &&
                        !button.InputMapping.ToString().Contains("Trackball") && !button.InputMapping.ToString().Contains("LightGun")).ToArray();
                    var index = Array.IndexOf(digital, gear);
                    Func<bool> high = () => (JvsHelper.StateView.ReadByte(8 + index / 8) & (1 << (index % 8))) != 0;
                    Action clear = () => { for (var player = 0; player < 4; ++player) InputCode.PlayerDigitalButtons[player] = new PlayerButtons(); };
                    Action press = () => typeof(Ss32Checks).GetMethod("Press", Static).Invoke(null, new object[] { gear.InputMapping.ToString() });
                    foreach (var mode in new[] { "Button", "Lever" })
                    {
                        shifter.FieldValue = mode; clear(); pipe.Start();
                        try
                        {
                            pipe.Transmit(); Require(!high(), "Shifter must start in low gear");
                            press(); pipe.Transmit(); Require(high(), "Gear press not transmitted");
                            pipe.Transmit(); Require(high(), "Held gear button toggled repeatedly");
                            clear(); pipe.Transmit(); Require(high() == (mode == "Button"), "Gear button/lever release semantics");
                            press(); pipe.Transmit(); Require(high() == (mode == "Lever"), "Second gear press semantics");
                            clear(); pipe.Transmit(); Require(!high(), "Gear failed to return low");
                            shifts += 6;
                        }
                        finally { pipe.Stop(); }
                        clear(); pipe.Start(); pipe.Transmit(); Require(!high(), "Previous game shifter state leaked into next launch"); pipe.Stop(); ++shifts;
                    }
                }
            }
            Require(games == 23 && axes == 100, "Expected all 19 driving revisions plus 4 linked modes and 100 exposed axes");
            foreach (var reverse in new[] { false, true })
            {
                byte previous = reverse ? (byte)255 : (byte)0;
                foreach (var raw in new[] { 32767, 40959, 49151, 57343, 65535 })
                {
                    var value = di.HandleGasBrakeForJvs(raw, false, reverse, false, true);
                    Require(reverse ? value <= previous : value >= previous, "Half-axis pedal must remain monotonic");
                    if (raw == 40959) Require(value >= 60 && value <= 195, "Half-axis pedal reached full travel prematurely");
                    previous = value; ++samples;
                }
                previous = reverse ? (byte)255 : (byte)0;
                foreach (var raw in new[] { 32767, 24575, 16383, 8191, 0 })
                {
                    var value = di.HandleGasBrakeForJvs(raw, true, reverse, false, true);
                    Require(reverse ? value <= previous : value >= previous, "Negative half-axis pedal must remain monotonic");
                    previous = value; ++samples;
                }
            }
            Console.WriteLine("Driving TPUI checks: " + games + " SS32 profiles, " + axes + " axes, " + samples + " actual DirectInput/XInput conversion and input-bridge samples; " + shifts + " Button/Lever shifter checks.");
        }
        finally
        {
            Lazydata.ParrotData = oldData;
            for (var n = 0; n < fields.Length; ++n) fields[n].SetValue(null, values[n]);
        }
    }
}
