using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class CpsChecks
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Clear() { for (var p = 0; p < 4; ++p) InputCode.PlayerDigitalButtons[p] = new PlayerButtons(); }
    private static int Press(string mapping)
    {
        var player = mapping.StartsWith("JvsTwo", StringComparison.Ordinal) ? 2 : 0;
        if (player == 2) mapping = mapping.Substring(6);
        if (mapping.StartsWith("P2", StringComparison.Ordinal) || mapping == "Coin2") ++player;
        var input = InputCode.PlayerDigitalButtons[player];
        if (mapping == "Test") { input.Test = true; return 13; }
        if (mapping == "Service1") { input.Service = true; return 12; }
        if (mapping.StartsWith("Coin", StringComparison.Ordinal)) { input.Coin = true; return player * 16 + 11; }
        var button = mapping.Substring(8);
        if (button == "Start") { input.Start = true; return player * 16 + 10; }
        var directions = new[] { "Right", "Left", "Down", "Up" };
        var index = Array.IndexOf(directions, button);
        typeof(PlayerButtons).GetProperty(index >= 0 ? button : "Button" + button).SetValue(input, (bool?)true);
        return player * 16 + (index >= 0 ? index : 3 + int.Parse(button));
    }
    public static void Run(string root, string temporary)
    {
        Directory.CreateDirectory("TeknoCPS"); File.WriteAllBytes(Path.Combine("TeknoCPS", "TeknoCPS.exe"), new byte[0]);
        var serializer = new XmlSerializer(typeof(GameProfile));
        var files = Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common", "GameProfiles"), "cps_*.xml");
        Require(files.Length == 641, "Expected every CPS catalog set");
        var pipe = new TeknoCPSPipe(); Clear(); pipe.Start();
        var onlineCount = 0; var seats = 0; var contacts = 0;
        try
        {
            foreach (var file in files)
            {
                GameProfile profile;
                using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                Require(profile.EmulationProfile == EmulationProfile.TeknoCPS && profile.EmulatorType == EmulatorType.TeknoCPS, "CPS dispatch");
                Require(File.Exists(Path.Combine(root, "TeknoParrotUi.Common", "Metadata", profile.ProfileName + ".json")), "CPS metadata");
                var zip = Path.Combine(temporary, profile.ExecutableName); File.WriteAllBytes(zip, new byte[0]);
                Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
                var offline = TeknoCPSLauncher.Build(profile, zip, null, true);
                Require(offline.Arguments.Contains("--test-menu") == !string.IsNullOrEmpty(profile.TestMenuParameter) && offline.Arguments.Contains("--nvram-dir") && !offline.UseShellExecute, "Offline test launch");
                Require(!TeknoCPSLauncher.Build(profile, zip, null).Arguments.Contains("--test-menu"), "Test switch leaked into normal launch");
                if (profile.HasTpoSupport)
                {
                    ++onlineCount;
                    var mappings = profile.JoystickButtons.Select(b => b.InputMapping.ToString()).ToArray();
                    var capacity = profile.ProfileName.StartsWith("cps_ssf2tb", StringComparison.Ordinal) || mappings.Contains("JvsTwoP2ButtonStart") ? 4 : mappings.Contains("JvsTwoP1ButtonStart") ? 3 : 2;
                    for (var count = 2; count <= capacity; ++count) for (var seat = 0; seat < count; ++seat)
                    {
                        Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|" + seat + "|Player|" + count);
                        var online = TeknoCPSLauncher.Build(profile, zip, null, true);
                        Require(online.Arguments.Contains("--no-nvram") && !online.Arguments.Contains("--test-menu") && !online.Arguments.Contains("--player") && !online.Arguments.Contains("--link-") && !online.Arguments.Contains("--nvram-dir"), "Automatic TPO assignment");
                        ++seats;
                    }
                }
                else
                {
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|0|Player|2");
                    var rejected = false; try { TeknoCPSLauncher.Build(profile, zip, null); } catch (ArgumentException) { rejected = true; }
                    Require(rejected, "Pending/single-player CPS game advertised online");
                }
                InputCode.GameProfile = profile;
                foreach (var button in profile.JoystickButtons)
                {
                    Clear(); var bit = Press(button.InputMapping.ToString()); pipe.Transmit();
                    for (var offset = 0; offset < 8; ++offset)
                        Require(JvsHelper.StateView.ReadByte(8 + offset) == (offset == bit / 8 ? 1 << (bit % 8) : 0), profile.ProfileName + ": wrong contact/isolation " + button.InputMapping);
                    Clear(); pipe.Transmit();
                    for (var offset = 0; offset < 8; ++offset) Require(JvsHelper.StateView.ReadByte(8 + offset) == 0, "Stuck CPS contact");
                    ++contacts;
                }
            }
            Require(onlineCount == 488, "CPS online count");
            Require(JvsHelper.StateView.ReadByte(0) == 'C' && JvsHelper.StateView.ReadByte(1) == 'P' && JvsHelper.StateView.ReadByte(4) == 1 && (JvsHelper.StateView.ReadUInt16(6) & 1) == 0, "CPIN publication contract");
            Console.WriteLine($"641 CPS profiles, 488 online modes, {seats} room seats and {contacts} input press/release/isolation checks passed.");
        }
        finally { pipe.Stop(); Require(JvsHelper.StateView.ReadByte(5) == 0, "CPS bridge stop did not release ownership"); }
    }
}
