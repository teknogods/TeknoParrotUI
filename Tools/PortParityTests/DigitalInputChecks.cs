using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

internal static class DigitalInputChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Clear() { for (var player = 0; player < 4; ++player) InputCode.PlayerDigitalButtons[player] = new PlayerButtons(); }
    private static byte[] Page(ControlSender pipe) { pipe.Transmit(); return Enumerable.Range(8, 16).Select(index => JvsHelper.StateView.ReadByte(index)).ToArray(); }

    internal static void Run(string root)
    {
        const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Static;
        var xi = new InputListenerXInput();
        var xiProfile = typeof(InputListenerXInput).GetField("_gameProfile", statics);
        var oldXi = xiProfile.GetValue(null);
        var xiInput = typeof(InputListenerXInput).GetMethod("HandleXinput", instance);
        var press = new State { Gamepad = new XiGamepad { Buttons = GamepadButtonFlags.A } }; var release = new State();
        var profiles = 0; var contacts = 0;
        try
        {
            foreach (var prefix in new[] { "mvs_", "cps_", "ss32_" })
            {
                ControlSender pipe = prefix == "mvs_" ? (ControlSender)new TeknoMVSPipe() : prefix == "cps_" ? new TeknoCPSPipe() : new TeknoSS32Pipe();
                var pressMethod = (prefix == "mvs_" ? typeof(MvsChecks) : prefix == "cps_" ? typeof(CpsChecks) : typeof(Ss32Checks)).GetMethod("Press", statics);
                var active = false;
                try
                {
                    foreach (var file in Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), prefix + "*.xml"))
                    {
                        GameProfile profile;
                        using (var stream = File.OpenRead(file)) profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
                        profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                        var shifter = profile.ConfigValues.FirstOrDefault(field => field.FieldName == "Shifter Mode");
                        if (shifter != null) shifter.FieldValue = "Lever";
                        InputCode.GameProfile = profile; xiProfile.SetValue(null, profile);
                        Clear();
                        if (prefix == "ss32_" && active) { pipe.Stop(); active = false; }
                        if (!active) { pipe.Start(); active = true; }
                        ++profiles;
                        foreach (var binding in profile.JoystickButtons)
                        {
                            var mapping = binding.InputMapping.ToString();
                            if (mapping.StartsWith("Analog") || mapping.Contains("Trackball") || mapping.Contains("LightGun")) continue;
                            binding.XInputButton = new XInputButton { IsButton = true, ButtonCode = (short)GamepadButtonFlags.A, XInputIndex = 0 };
                            Clear(); var neutral = Page(pipe); pressMethod.Invoke(null, new object[] { mapping }); var expected = Page(pipe);
                            Require(!neutral.SequenceEqual(expected), "No reference contact for " + profile.ProfileName + " " + mapping);
                            Clear(); xiInput.Invoke(xi, new object[] { binding, press, release, 0 });
                            Require(Page(pipe).SequenceEqual(expected), "XInput contact mismatch " + profile.ProfileName + " " + mapping);
                            xiInput.Invoke(xi, new object[] { binding, release, press, 0 });
                            Require(Page(pipe).SequenceEqual(neutral), "XInput stuck contact " + profile.ProfileName + " " + mapping);
                            ++contacts;
                        }
                    }
                }
                finally { if (active) pipe.Stop(); }
            }
            Require(profiles == 995, "Incomplete profile input coverage");
            Console.WriteLine("All " + profiles + " profiles: " + contacts + " digital controls passed actual SDL3 gamepad mapper listener-to-emulator-bridge press/release/isolation checks.");
        }
        finally { xiProfile.SetValue(null, oldXi); }
    }
}
