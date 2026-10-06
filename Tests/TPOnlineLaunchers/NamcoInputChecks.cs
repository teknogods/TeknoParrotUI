using System;
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
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class NamcoInputChecks
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static GameProfile Load(string root, string set)
    {
        using (var stream = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles", set + ".xml")))
        {
            var profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
            profile.ProfileName = set; return profile;
        }
    }

    internal static void Run(string root, string temporary)
    {
        var previous = InputCode.GameProfile;
        var types = new[] { typeof(InputListenerDirectInput), typeof(InputListenerXInput) };
        var fields = types.SelectMany(type => new[] { "_gameProfile", "GunGame", "ReverseYAxis" }.Select(name => type.GetField(name, Static)))
            .Concat(new[] { types[0].GetField("KeyboardorButtonAxis", Static), types[0].GetField("KillMe", BindingFlags.Public | BindingFlags.Static) }).ToArray();
        var values = fields.Select(field => field.GetValue(null)).ToArray();
        var di = new InputListenerDirectInput(); var xi = new InputListenerXInput();
        var convertDi = types[0].GetMethod("ModifyAnalog", Instance); var convertXi = types[1].GetMethod("ModifyAnalog", Instance);
        var checks = 0;
        try
        {
            foreach (var set in new[] { "cybrcomm", "adillor", "alpines" })
            {
                var profile = Load(root, set); InputCode.GameProfile = profile;
                foreach (var type in types)
                {
                    type.GetField("_gameProfile", Static).SetValue(null, profile);
                    type.GetField("GunGame", Static).SetValue(null, false);
                    type.GetField("ReverseYAxis", Static).SetValue(null, false);
                }
                types[0].GetField("KeyboardorButtonAxis", Static).SetValue(null, false);
                var main = profile.JoystickButtons.Where(button => button.AnalogType == AnalogType.AnalogJoystick || button.AnalogType == AnalogType.AnalogJoystickY || button.AnalogType == AnalogType.AnalogJoystickReverse).ToArray();
                var pipe = new TeknoS22Pipe(); typeof(TeknoS22Pipe).GetField("_publishInput", Instance).SetValue(pipe, true);
                try
                {
                    foreach (var button in main)
                    {
                        var vertical = button.AnalogType == AnalogType.AnalogJoystickY;
                        var reversed = button.AnalogType == AnalogType.AnalogJoystickReverse;
                        button.DirectInputButton = new JoystickButton { IsAxis = true, Button = (int)JoystickOffset.X };
                        button.XInputButton = new XInputButton { XInputIndex = 0, IsLeftThumbX = !vertical, IsLeftThumbY = vertical };
                        var source = (int)button.InputMapping - (int)InputMapping.Analog0;
                        var d = new byte[3]; var x = new byte[3];
                        for (var n = 0; n < 3; ++n)
                        {
                            var directValue = new[] { 0, 32768, 65535 }[n];
                            d[n] = (byte)convertDi.Invoke(di, new object[] { button, new JoystickUpdate { RawOffset = (int)JoystickOffset.X, Value = directValue } });
                            var thumb = new[] { (short)-32768, (short)0, (short)32767 }[n];
                            x[n] = (byte)convertXi.Invoke(xi, new object[] { button, new State { Gamepad = new Gamepad { LeftThumbX = vertical ? (short)0 : thumb, LeftThumbY = vertical ? thumb : (short)0 } }, 0, 0 });
                            Array.Clear(InputCode.AnalogBytes, 0, InputCode.AnalogBytes.Length);
                            InputCode.AnalogBytes[source] = d[n]; pipe.Transmit();
                            Require(JvsHelper.StateView.ReadByte(13 + source / 2) == d[n], set + " axis routed to wrong ADC");
                            foreach (var other in main.Where(other => other != button))
                                Require(JvsHelper.StateView.ReadByte(13 + ((int)other.InputMapping - (int)InputMapping.Analog0) / 2) == 0, "Coupled S22 axes");
                            ++checks;
                        }
                        Require(d[0] == (reversed ? 255 : 0) && Math.Abs(d[1] - 128) <= 1 && d[2] == (reversed ? 0 : 255), "Incorrect cabinet binding polarity: " + button.ButtonName + " " + string.Join(",", d));
                        Require(Math.Abs(x[1] - 128) <= 1 && (vertical || reversed ? x[0] == 255 && x[2] == 0 : x[0] == 0 && x[2] >= 254), "XInput has reversed physical directions");
                        ++checks;
                    }
                    if (set == "cybrcomm")
                    {
                        var names = new[] { "Right Stick Y", "Left Stick Y", "Right Stick X", "Left Stick X" };
                        for (var n = 0; n < names.Length; ++n)
                            Require((int)main.Single(b => b.ButtonName == names[n]).InputMapping - (int)InputMapping.Analog0 == n * 2, "Cyber physical ADC order");
                        Require(profile.JoystickButtons.Where(b => b.ButtonName.Contains("Stick")).All(b => NamcoGameRevisions.PreviousControlName(profile, b.ButtonName).StartsWith("Analog ")), "Cyber legacy bindings cannot migrate");
                        ++checks;
                    }
                    // Exercise actual independent keyboard state/timer path.
                    types[0].GetField("KeyboardorButtonAxis", Static).SetValue(null, true);
                    types[0].GetField("KillMe", BindingFlags.Public | BindingFlags.Static).SetValue(null, false);
                    var enabled = (bool[])types[0].GetField("_keyboardAxisEnabled", Static).GetValue(null);
                    var negative = (bool[])types[0].GetField("_keyboardAxisNegative", Static).GetValue(null);
                    var positive = (bool[])types[0].GetField("_keyboardAxisPositive", Static).GetValue(null);
                    var rest = (byte[])types[0].GetField("_keyboardAxisRest", Static).GetValue(null);
                    Array.Clear(enabled, 0, enabled.Length); Array.Clear(negative, 0, negative.Length); Array.Clear(positive, 0, positive.Length);
                    foreach (var axis in main) { var i = (int)axis.InputMapping - (int)InputMapping.Analog0; enabled[i] = true; rest[i] = 128; InputCode.AnalogBytes[i] = 128; }
                    foreach (var button in profile.JoystickButtons.Where(b => b.AnalogType == AnalogType.Minimum || b.AnalogType == AnalogType.Maximum))
                    {
                        var source = (int)button.InputMapping - (int)InputMapping.Analog0;
                        button.DirectInputButton = new JoystickButton { Button = (int)JoystickOffset.Buttons0 };
                        convertDi.Invoke(di, new object[] { button, new JoystickUpdate { RawOffset = (int)JoystickOffset.Buttons0, Value = 128 } });
                        types[0].GetMethod("ListenKeyboardButton", Instance).Invoke(di, new object[] { null, null });
                        Require(button.AnalogType == AnalogType.Minimum ? InputCode.AnalogBytes[source] < 128 : InputCode.AnalogBytes[source] > 128, "Keyboard direction reversed");
                        foreach (var axis in main.Where(a => a.InputMapping != button.InputMapping)) Require(InputCode.AnalogBytes[(int)axis.InputMapping - (int)InputMapping.Analog0] == 128, "Keyboard changed another stick");
                        convertDi.Invoke(di, new object[] { button, new JoystickUpdate { RawOffset = (int)JoystickOffset.Buttons0, Value = 0 } });
                        types[0].GetMethod("ListenKeyboardButton", Instance).Invoke(di, new object[] { null, null });
                        Require(InputCode.AnalogBytes[source] == 128, "Keyboard failed to recenter"); ++checks;
                    }
                }
                finally { pipe.Stop(); }
            }
            var armadillo = Load(root, "adillor");
            Require(armadillo.ConfigValues.Single(f => f.FieldName == "Input API").FieldOptions.Contains("RawInputTrackball"), "Trackball mode missing");
            armadillo.ConfigValues.Single(f => f.FieldName == "Input API").FieldValue = "RawInputTrackball";
            InputCode.GameProfile = armadillo;
            var rawProfile = typeof(InputListenerRawInputTrackball).GetField("_gameProfile", Static); var oldRaw = rawProfile.GetValue(null);
            var listener = new InputListenerRawInputTrackball(); rawProfile.SetValue(null, armadillo);
            var raw = typeof(InputListenerRawInputTrackball).GetMethod("HandleRawInputTrackball", Instance);
            var trackball = armadillo.JoystickButtons.Single(b => b.InputMapping == InputMapping.P1Trackball);
            var bridge = new TeknoS22Pipe(); typeof(TeknoS22Pipe).GetField("_publishInput", Instance).SetValue(bridge, true);
            try
            {
                bridge.Transmit();
                raw.Invoke(listener, new object[] { trackball, 40000, -50000 }); bridge.Transmit();
                Require(JvsHelper.StateView.ReadByte(4) == 2 && JvsHelper.StateView.ReadByte(5) == 3, "VPIN relative ABI missing");
                Require(JvsHelper.StateView.ReadUInt32(40) == 40000 && JvsHelper.StateView.ReadUInt32(44) == unchecked((uint)-50000), "Raw input clipped to16bits or reversed");
                bridge.Transmit(); Require(JvsHelper.StateView.ReadUInt32(40) == 40000, "Duplicate raw motion");
                raw.Invoke(listener, new object[] { trackball, -40001, 50001 }); bridge.Transmit();
                Require(JvsHelper.StateView.ReadUInt32(40) == uint.MaxValue && JvsHelper.StateView.ReadUInt32(44) == 1, "Signed counter wrap"); checks += 4;
            }
            finally { bridge.Stop(); rawProfile.SetValue(null, oldRaw); }

            InputCode.GameProfile = Load(root, "raceonj");
            for (var player = 0; player < 4; ++player) InputCode.PlayerDigitalButtons[player] = new PlayerButtons();
            foreach (var seeded in new[] { false, true })
            {
                var pipe = new TeknoS23Pipe(seeded); pipe.Start();
                try
                {
                    Func<bool> on = () => (JvsHelper.StateView.ReadByte(8) & 128) != 0;
                    pipe.Transmit(); Require(on() == seeded, "Wrong initial Test position");
                    InputCode.PlayerDigitalButtons[0].Test = true; pipe.Transmit(); Require(on() != seeded, "Test press did not toggle");
                    pipe.Transmit(); Require(on() != seeded, "Held Test repeated");
                    InputCode.PlayerDigitalButtons[0].Test = false; pipe.Transmit(); Require(on() != seeded, "Test release reset maintained switch");
                    InputCode.PlayerDigitalButtons[0].Test = true; pipe.Transmit(); Require(on() == seeded, "Second Test press did not toggle");
                    InputCode.PlayerDigitalButtons[0].Test = false; InputCode.PlayerDigitalButtons[0].Service = true; pipe.Transmit();
                    Require((JvsHelper.StateView.ReadByte(8) & 64) != 0, "Service missing");
                    InputCode.PlayerDigitalButtons[0].Service = false; pipe.Transmit(); Require((JvsHelper.StateView.ReadByte(8) & 64) == 0, "Service latched"); checks += 7;
                }
                finally { pipe.Stop(); }
                pipe.Transmit(); Require(JvsHelper.StateView.ReadByte(5) == 0, "Stopped publisher reactivated");
            }
            var panic = Load(root, "panicprkj"); var zip = Path.Combine(temporary, "panicprk.zip"); File.WriteAllBytes(zip, new byte[0]);
            Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
            Require(TeknoS23Launcher.Build(panic, zip, null, true).Arguments.Contains("--service-dip"), "Panic Park Test startup strap missing");
            Require(!TeknoS23Launcher.Build(panic, zip, null, false).Arguments.Contains("--service-dip"), "Panic Park strap leaked into normal launch");
            Console.WriteLine("Namco input checks: " + checks + " direction, ADC isolation, keyboard, raw counter, Test toggle and startup checks passed.");
        }
        finally { InputCode.GameProfile = previous; for (var n = 0; n < fields.Length; ++n) fields[n].SetValue(null, values[n]); }
    }
}
