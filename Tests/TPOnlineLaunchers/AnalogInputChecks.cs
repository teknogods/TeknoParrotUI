using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

internal static class AnalogInputChecks
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Run(string root)
    {
        var types = new[] { typeof(InputListenerDirectInput), typeof(InputListenerXInput) };
        var names = new[] { "_gameProfile", "GunGame", "ReverseYAxis", "RelativeInput", "_invertedMouseAxis", "_minX", "_minY", "_DivideX", "_DivideY" };
        var fields = types.SelectMany(type => names.Select(name => type.GetField(name, Static))).ToArray();
        var values = fields.Select(field => field.GetValue(null)).ToArray();
        var keyboard = types[0].GetField("KeyboardorButtonAxis", Static);
        var oldKeyboard = keyboard.GetValue(null);
        var di = new InputListenerDirectInput(); var xi = new InputListenerXInput();
        var convertD = types[0].GetMethod("ModifyAnalog", Instance);
        var convertX = types[1].GetMethod("ModifyAnalog", Instance);
        var serializer = new XmlSerializer(typeof(GameProfile));
        var axes = 0; var samples = 0; var games = 0;
        var gunSamples = 0;
        try
        {
            keyboard.SetValue(null, false);
            foreach (var prefix in new[] { "cps_", "mvs_", "ss32_" })
            foreach (var file in Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), prefix + "*.xml"))
            {
                GameProfile profile;
                using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                var controls = profile.JoystickButtons.Where(button => button.InputMapping.ToString().StartsWith("Analog", StringComparison.Ordinal) &&
                    (button.AnalogType == AnalogType.AnalogJoystick || button.AnalogType == AnalogType.AnalogJoystickY)).ToArray();
                if (controls.Length == 0) continue;
                ++games;
                foreach (var type in types)
                {
                    type.GetField("_gameProfile", Static).SetValue(null, profile);
                    type.GetField("GunGame", Static).SetValue(null, profile.GunGame);
                    type.GetField("ReverseYAxis", Static).SetValue(null, false);
                    type.GetField("RelativeInput", Static).SetValue(null, false);
                    type.GetField("_invertedMouseAxis", Static).SetValue(null, profile.InvertedMouseAxis);
                    type.GetField("_minX", Static).SetValue(null, (short)0);
                    type.GetField("_minY", Static).SetValue(null, (short)0);
                    type.GetField("_DivideX", Static).SetValue(null, 1.0);
                    type.GetField("_DivideY", Static).SetValue(null, 1.0);
                }
                foreach (var field in profile.ConfigValues.Where(field => field.FieldName == "Input API" || field.FieldName == "Rotary Input"))
                    field.FieldValue = field.FieldName == "Rotary Input" ? "Analog" : "XInput";
                InputCode.GameProfile = profile;
                ControlSender pipe = prefix == "cps_" ? (ControlSender)new TeknoCPSPipe() : prefix == "mvs_" ? new TeknoMVSPipe() : new TeknoSS32Pipe();
                pipe.Start(); pipe.Running = false; Thread.Sleep(40);
                try
                {
                    var phases = new int[controls.Length];
                    for (var index = 0; index < controls.Length; ++index)
                    {
                        var control = controls[index]; ++axes;
                        control.DirectInputButton = new JoystickButton { Button = (int)JoystickOffset.X, IsAxis = true };
                        control.XInputButton = new XInputButton { XInputIndex = 0, IsLeftThumbX = control.AnalogType == AnalogType.AnalogJoystick,
                            IsLeftThumbY = control.AnalogType == AnalogType.AnalogJoystickY };
                        var source = int.Parse(control.InputMapping.ToString().Substring(6));
                        var relative = prefix != "ss32_" || profile.ProfileName.StartsWith("ss32_sonic", StringComparison.Ordinal);
                        foreach (var api in new[] { "DirectInput", "XInput" })
                        {
                            var converted = new byte[5]; var n = 0;
                            foreach (var raw in api == "DirectInput" ? new[] { 0, 16384, 32768, 49152, 65535 } : new[] { -32768, -16384, 0, 16384, 32767 })
                            {
                                byte value;
                                if (api == "DirectInput") value = (byte)convertD.Invoke(di, new object[] { control, new JoystickUpdate { RawOffset = (int)JoystickOffset.X, Value = raw } });
                                else value = (byte)convertX.Invoke(xi, new object[] { control, new State { Gamepad = new Gamepad {
                                    LeftThumbX = (short)(control.AnalogType == AnalogType.AnalogJoystick ? raw : 0),
                                    LeftThumbY = (short)(control.AnalogType == AnalogType.AnalogJoystickY ? raw : 0) } }, 0, 0 });
                                converted[n++] = value;
                                for (var clear = 0; clear < InputCode.AnalogBytes.Length; ++clear) InputCode.AnalogBytes[clear] = 128;
                                InputCode.AnalogBytes[source] = value; pipe.Transmit(); ++samples;
                                if (relative)
                                {
                                    var delta = value - 128; if (Math.Abs(delta) > 12) phases[index] += delta / 8;
                                    for (var other = 0; other < controls.Length; ++other)
                                    {
                                        var offset = prefix == "ss32_" ? 24 + other * 4 : 16 + other * 4;
                                        Require(JvsHelper.StateView.ReadInt32(offset) == (prefix == "mvs_" ? phases[other] / 4 : phases[other]),
                                            profile.ProfileName + " " + api + " lost relative direction, range or device isolation: " + control.ButtonName);
                                    }
                                }
                                else
                                {
                                    for (var other = 0; other < controls.Length; ++other)
                                        Require(JvsHelper.StateView.ReadByte(24 + other) == (other == index ? value : 128),
                                            profile.ProfileName + " " + api + " lost absolute range or player isolation: " + control.ButtonName);
                                }
                            }
                            Require(Math.Min(converted[0], converted[4]) <= 1 && Math.Max(converted[0], converted[4]) >= 254 &&
                                converted[2] >= 126 && converted[2] <= 129, profile.ProfileName + " axis did not reach both ends and neutral");
                            if (profile.GunGame)
                            {
                                var upwards = api == "XInput" && control.AnalogType == AnalogType.AnalogJoystickY;
                                Require(upwards ? converted[0] >= 254 && converted[4] == 0 : converted[0] == 0 && converted[4] >= 254,
                                    profile.ProfileName + " gun direction must follow screen coordinates: " + api + " " + control.ButtonName);
                            }
                            for (var step = 1; step < converted.Length; ++step)
                                Require(converted[0] < converted[4] ? converted[step] > converted[step - 1] : converted[step] < converted[step - 1],
                                    profile.ProfileName + " non-monotonic axis " + control.ButtonName);
                        }
                    }
                }
                finally { pipe.Stop(); }
                if (profile.GunGame)
                {
                    foreach (var type in types)
                    {
                        type.GetField("_minX", Static).SetValue(null, (short)20);
                        type.GetField("_minY", Static).SetValue(null, (short)20);
                        type.GetField("_DivideX", Static).SetValue(null, 255.0 / 200);
                        type.GetField("_DivideY", Static).SetValue(null, 255.0 / 200);
                    }
                    foreach (var control in controls)
                    foreach (var corner in new[] { 0, 1, 2 })
                    {
                        var raw = new[] { 0, 32768, 65535 }[corner];
                        var actualD = (byte)convertD.Invoke(di, new object[] { control, new JoystickUpdate { RawOffset = (int)JoystickOffset.X, Value = raw } });
                        var y = control.AnalogType == AnalogType.AnalogJoystickY;
                        var thumb = (short)(new[] { -32768, 0, 32767 }[y ? 2 - corner : corner]);
                        var actualX = (byte)convertX.Invoke(xi, new object[] { control, new State { Gamepad = new Gamepad {
                            LeftThumbX = y ? (short)0 : thumb, LeftThumbY = y ? thumb : (short)0 } }, 0, 0 });
                        var expected = new[] { 20, 120, 220 }[corner];
                        Require(Math.Abs(actualD - expected) <= 1 && Math.Abs(actualX - expected) <= 1,
                            profile.ProfileName + " calibrated gun bounds changed direction or shifted range");
                        gunSamples += 2;
                    }
                    gunSamples += CheckRawGun(profile);
                }
            }
            Require(axes == 109, "Expected every remaining rotary, trackball, flight, gun and miscellaneous analog source");
            Console.WriteLine("Other analog TPUI checks: " + games + " profiles, " + axes + " axes, " + samples + " actual DirectInput/XInput conversion and input-bridge samples.");
            Console.WriteLine("SS32 guns: " + gunSamples + " actual RawInput viewport/device, calibrated controller bounds, direction and player-isolation samples.");
        }
        finally
        {
            for (var n = 0; n < fields.Length; ++n) fields[n].SetValue(null, values[n]);
            keyboard.SetValue(null, oldKeyboard);
        }
    }

    private static int CheckRawGun(GameProfile profile)
    {
        Require(profile.InvertedMouseAxis, "SS32 guns require separate screen X/Y coordinates");
        var listener = new InputListenerRawInput();
        var type = typeof(InputListenerRawInput);
        Action<string, object> set = (name, value) => type.GetField(name, Instance).SetValue(listener, value);
        set("_windowFocus", true); set("_isTeknoSS32", true);
        set("_invertedMouseAxis", profile.InvertedMouseAxis);
        set("_minX", 0.0f); set("_maxX", 255.0f); set("_minY", 0.0f); set("_maxY", 255.0f);
        set("_windowLocationX", 100); set("_windowLocationY", 100);
        set("_windowWidth", 1000); set("_windowHeight", 800);
        var canvas = type.GetField("canvasInfo", Instance).GetValue(listener);
        foreach (var pair in new[] { new { Name = "windowLocationX", Value = 0 }, new { Name = "windowLocationY", Value = 0 },
            new { Name = "windowWidth", Value = 1200 }, new { Name = "windowHeight", Value = 1000 } })
            canvas.GetType().GetField(pair.Name).SetValue(canvas, pair.Value);
        set("canvasInfo", canvas);
        var handle = type.GetMethod("HandleRawInputGun", Instance);
        InputCode.GameProfile = profile;
        var pipe = new TeknoSS32Pipe(); pipe.Start(); pipe.Running = false; Thread.Sleep(40);
        var samples = 0;
        try
        {
            foreach (var windowed in new[] { false, true })
            foreach (var source in new[] { 0, 1, 2 })
            foreach (var gun in profile.JoystickButtons.Where(button => button.InputMapping.ToString().EndsWith("LightGun", StringComparison.Ordinal)))
            foreach (var point in new[] { new[] { 100, 100, 0, 0 }, new[] { 1100, 900, 255, 255 }, new[] { 600, 500, 128, 128 },
                new[] { 100, 500, 0, 128 }, new[] { 1100, 500, 255, 128 }, new[] { 600, 100, 128, 0 }, new[] { 600, 900, 128, 255 } })
            {
                set("_windowed", windowed);
                for (var n = 0; n < InputCode.AnalogBytes.Length; ++n) InputCode.AnalogBytes[n] = 128;
                var absolute = source != 0; var virtualDesktop = source == 2;
                var metrics = type.GetMethod("GetSystemMetrics", Static);
                var left = virtualDesktop ? (int)metrics.Invoke(null, new object[] { 76 }) : 0;
                var top = virtualDesktop ? (int)metrics.Invoke(null, new object[] { 77 }) : 0;
                var width = (int)metrics.Invoke(null, new object[] { virtualDesktop ? 78 : 0 });
                var height = (int)metrics.Invoke(null, new object[] { virtualDesktop ? 79 : 1 });
                // RAWMOUSE units describe the physical display, independently of the fake game viewport.
                var x = absolute ? (int)Math.Round((point[0] - left) * 65535.0 / width) : point[0];
                var y = absolute ? (int)Math.Round((point[1] - top) * 65535.0 / height) : point[1];
                handle.Invoke(listener, new object[] { gun, x, y, absolute, virtualDesktop }); pipe.Transmit(); ++samples;
                var player = gun.InputMapping == InputMapping.P1LightGun ? 0 : 1;
                for (var p = 0; p < 2; ++p)
                {
                    Require(Math.Abs(JvsHelper.StateView.ReadByte(24 + p * 2) - (p == player ? point[2] : 128)) <= 1 &&
                        Math.Abs(JvsHelper.StateView.ReadByte(25 + p * 2) - (p == player ? point[3] : 128)) <= 1,
                        profile.ProfileName + " gun coordinates lost viewport bounds, direction or player isolation");
                }
            }
            var absolutePosition = type.GetMethod("AbsoluteDesktopPosition", Static);
            Require((int)absolutePosition.Invoke(null, new object[] { 0, -1920, 5760 }) == -1920 &&
                (int)absolutePosition.Invoke(null, new object[] { 65535, -1920, 5760 }) == 3840 &&
                (int)absolutePosition.Invoke(null, new object[] { 32768, -1920, 5760 }) == 960,
                "Absolute device coordinates lost a negative virtual desktop origin");
        }
        finally { pipe.Stop(); }
        return samples;
    }
}
