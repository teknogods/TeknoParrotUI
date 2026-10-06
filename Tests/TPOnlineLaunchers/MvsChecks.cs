using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class MvsChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void ClearInputs() { for (var n = 0; n < 4; ++n) InputCode.PlayerDigitalButtons[n] = new PlayerButtons(); }
    private static int Press(string mapping)
    {
        var player = mapping.StartsWith("JvsTwo", StringComparison.Ordinal) ? 2 : 0;
        if (player == 2) mapping = mapping.Substring(6);
        if (mapping.StartsWith("P2", StringComparison.Ordinal) || mapping == "Coin2") ++player;
        var input = InputCode.PlayerDigitalButtons[player];
        if (mapping == "Test") { input.Test = true; return 21; }
        if (mapping == "Service1") { input.Service = true; return 20; }
        if (mapping.StartsWith("Coin", StringComparison.Ordinal)) { input.Coin = true; return 18 + player % 2; }
        if (mapping.StartsWith("ExtensionOne", StringComparison.Ordinal))
        {
            var key = int.Parse(mapping.Substring(12));
            var property = key < 10 ? "ExtensionButton" + key : key < 20 ? "ExtensionButton1_" + (key - 10) : "ExtensionButton2_" + (key - 20);
            typeof(PlayerButtons).GetProperty(property).SetValue(input, (bool?)true);
            return 30 + (key < 10 ? key - 1 : key < 20 ? key - 7 : key - 9);
        }
        var button = mapping.Substring(8);
        if (button == "Start") { input.Start = true; return player < 2 ? 16 + player : 46 + player - 2; }
        var names = new[] { "Up", "Down", "Left", "Right", "1", "2", "3", "4" };
        var index = Array.IndexOf(names, button);
        Require(index >= 0, "Unexpected mapping " + mapping);
        typeof(PlayerButtons).GetProperty(index < 4 ? button : "Button" + button).SetValue(input, (bool?)true);
        return (player < 2 ? player * 8 : 30 + (player - 2) * 8) + index;
    }

    public static void Run(string root, string temporary)
    {
        Directory.CreateDirectory("TeknoMVS");
        File.WriteAllBytes(Path.Combine("TeknoMVS", "TeknoMVS.exe"), new byte[0]);
        var serializer = new XmlSerializer(typeof(GameProfile));
        var files = Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common", "GameProfiles"), "mvs_*.xml");
        Require(files.Length == 287, "Expected 286 MVS games plus linked Bowling");
        var modes = 0; var contacts = 0; var seats = 0;
        var pipe = new TeknoMVSPipe();
        ClearInputs(); pipe.Start();
        try
        {
            foreach (var file in files)
            {
                GameProfile profile;
                using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                Require(profile.EmulatorType == EmulatorType.TeknoMVS && profile.EmulationProfile == EmulationProfile.TeknoMVS, "MVS dispatch mismatch");
                Require(File.Exists(Path.Combine(root, "TeknoParrotUi.Common", "Metadata", profile.ProfileName + ".json")), "MVS metadata missing");
                var zip = Path.Combine(temporary, profile.ExecutableName); File.WriteAllBytes(zip, new byte[0]);
                Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
                profile.ConfigValues.Single(v => v.FieldName == "CPU Clock").FieldValue = "24";
                var offline = TeknoMVSLauncher.Build(profile, zip, null, true);
                foreach (var blank in new[] { "", " ", "\t" })
                {
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", blank);
                    var local = TeknoMVSLauncher.Build(profile, zip, null, true);
                    Require(local.FileName == offline.FileName && local.Arguments == offline.Arguments,
                        "MVS blank online environment changed offline launch");
                }
                Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
                var switches = profile.ConfigValues.Where(v => v.CategoryName == "DIP Switches").ToArray();
                Require(switches.Length == 8 && profile.GameProfileRevision >= 2, "MVS DIP settings missing");
                var defaultMask = profile.ProfileName == "mvs_kizuna4p" ? 2 : new[] { "mvs_janshin", "mvs_minasan", "mvs_bakatono", "mvs_ms5pcb", "mvs_svcpcb", "mvs_svcpcba" }.Contains(profile.ProfileName) ? 4 : 0;
                var nativeArgs = Program.Arguments(offline.Arguments);
                Require(nativeArgs[Array.IndexOf(nativeArgs, "--dip-mask") + 1] == defaultMask.ToString(), "MVS physical DIP defaults changed");
                if (profile.ProfileName == "mvs_nam1975")
                {
                    for (var mask = 0; mask < 256; ++mask)
                    {
                        for (var dip = 0; dip < 8; ++dip)
                            profile.ConfigValues.Single(v => v.FieldName == "DIP Switch " + (dip + 1)).FieldValue = (mask & (1 << dip)) != 0 ? "On" : "Off";
                        var configured = Program.Arguments(TeknoMVSLauncher.Build(profile, zip, null).Arguments);
                        Require(configured[Array.IndexOf(configured, "--dip-mask") + 1] == mask.ToString(), "Physical DIP bit order/combination");
                    }
                    switches[0].FieldValue = "invalid";
                    var invalid = false;
                    try { TeknoMVSLauncher.Build(profile, zip, null); } catch (ArgumentException) { invalid = true; }
                    Require(invalid, "Malformed offline DIP accepted");
                    foreach (var value in switches) value.FieldValue = "Off";
                }
                Require(offline.FileName.EndsWith("TeknoMVS.exe") && offline.Arguments.Contains("--test-menu") && offline.Arguments.Contains("--start") && offline.Arguments.Contains("--cpu-clock 24"), "MVS offline/test launch mismatch");
                var linked = new[] { "mvs_ridhero", "mvs_ridheroh", "mvs_trally", "mvs_lbowling_link" }.Contains(profile.ProfileName);
                if (profile.HasTpoSupport)
                {
                    ++modes;
                    // Invalid saved display/clock settings cannot contaminate TPO.
                    profile.ConfigValues.Single(v => v.FieldName == "CPU Clock").FieldValue = "invalid-saved-clock";
                    profile.ConfigValues.Single(v => v.FieldName == "Rendering Mode").FieldValue = "invalid-saved-mode";
                    foreach (var value in switches) value.FieldValue = "invalid-saved-dip";
                    var capacity = linked || profile.ProfileName == "mvs_kizuna4p" || profile.ProfileName == "mvs_lbowling" ? 4 : 2;
                    for (var count = 2; count <= capacity; ++count) for (var seat = 0; seat < count; ++seat)
                    {
                        Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|" + seat + "|Player|" + count);
                        var online = TeknoMVSLauncher.Build(profile, zip, null);
                        Require(online.FileName.EndsWith("TeknoMVS.exe") && !online.UseShellExecute, "MVS online dispatch");
                        Require(online.Arguments.Contains("--net-link") == linked, "MVS linked mode mismatch");
                        Require(online.Arguments.Contains("--net-mode rollback --net-input-delay 2"), "MVS TPO must use rollback");
                        Require(online.Arguments.Contains("--cpu-clock 12") && !online.Arguments.Contains("--cabinet-state-dir") && !online.Arguments.Contains("--net-player ") && !online.Arguments.Contains("--net-players"), "MVS lobby assignment not automatic");
                        Require(!online.Arguments.Contains("--dip-mask"), "Offline DIP switches leaked into TPO");
                        ++seats;
                    }
                }
                else
                {
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|0|Player|2");
                    var rejected = false;
                    try { TeknoMVSLauncher.Build(profile, zip, null); } catch (ArgumentException) { rejected = true; }
                    Require(rejected, "Offline-only MVS profile accepted TPO");
                }
                InputCode.GameProfile = profile;
                foreach (var button in profile.JoystickButtons)
                {
                    var mapping = button.InputMapping.ToString();
                    if (mapping.StartsWith("Analog", StringComparison.Ordinal) || mapping.Contains("Trackball")) continue;
                    ClearInputs(); var expected = Press(mapping); pipe.Transmit();
                    var value = JvsHelper.StateView.ReadByte(8 + expected / 8);
                    Require((value & (1 << (expected % 8))) != 0, profile.ProfileName + ": missing contact for " + mapping);
                    for (var dip = 22; dip < 30; ++dip) Require((JvsHelper.StateView.ReadByte(8 + dip / 8) & (1 << (dip % 8))) == 0, "TPUI wrote a cabinet DIP");
                    ClearInputs(); pipe.Transmit();
                    Require((JvsHelper.StateView.ReadByte(8 + expected / 8) & (1 << (expected % 8))) == 0, "Contact stuck on release");
                    ++contacts;
                }
            }
            pipe.Running = false; Thread.Sleep(40);
            InputCode.GameProfile.ProfileName = "mvs_irrmaze";
            InputCode.GameProfile.ConfigValues.Single(v => v.FieldName == "Input API").FieldValue = "RawInputTrackball";
            using (var first = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory", 12))
            using (var second = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory2", 12))
            using (var a = first.CreateViewAccessor())
            using (var b = second.CreateViewAccessor())
            {
                a.Write(0, (short)32); a.Write(4, (short)-16); a.Write(8, 0);
                pipe.Transmit();
                Require(JvsHelper.StateView.ReadUInt32(16) == 8 && JvsHelper.StateView.ReadUInt32(20) == unchecked((uint)-4), "Raw trackball axes/signs lost");
                pipe.Transmit();
                Require(JvsHelper.StateView.ReadUInt32(16) == 8 && JvsHelper.StateView.ReadUInt32(20) == unchecked((uint)-4), "Raw movement was consumed twice");
                InputCode.GameProfile.ProfileName = "mvs_popbounc";
                a.Write(0, (short)12); a.Write(4, (short)0); a.Write(8, 0);
                b.Write(0, (short)20); b.Write(4, (short)0); b.Write(8, 0);
                pipe.Transmit();
                Require(JvsHelper.StateView.ReadUInt32(16) == 11 && JvsHelper.StateView.ReadUInt32(20) == 1, "Two independent dial devices lost");
                a.Write(0, (short)1200); a.Write(8, 0); b.Write(0, (short)-1200); b.Write(8, 0);
                pipe.Transmit(); a.Write(8, 0); b.Write(8, 0); pipe.Transmit(); pipe.Transmit();
                Require(JvsHelper.StateView.ReadUInt32(16) == 611 && JvsHelper.StateView.ReadUInt32(20) == unchecked((uint)-599), "MVS skipped-publication burst was truncated or repeated");
                Require(JvsHelper.StateView.ReadByte(4) == 2 && (JvsHelper.StateView.ReadUInt16(6) & 1) == 0, "MVIN v2 publication contract");
            }
        }
        finally { pipe.Stop(); ClearInputs(); Environment.SetEnvironmentVariable("TP_TPONLINE2", null); }
        Thread.Sleep(30);
        Require(JvsHelper.StateView.ReadByte(5) == 0, "Stopped MVS publisher remained active");
        Require(modes == 273, "Wrong TPO MVS profile count");
        Console.WriteLine($"287 MVS XML/metadata/offline/test profiles, {modes} online modes, {seats} lobby seats, {contacts} contact press/release checks passed.");
    }
}
