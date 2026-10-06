using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class Ss32Checks
{
    private static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Clear() { for (var p = 0; p < 4; ++p) InputCode.PlayerDigitalButtons[p] = new PlayerButtons(); }
    private static void Press(string mapping)
    {
        var player = 0;
        if (mapping.StartsWith("JvsTwo", StringComparison.Ordinal)) { player = 2; mapping = mapping.Substring(6); }
        string property;
        if (mapping.StartsWith("Extension", StringComparison.Ordinal))
        {
            var second = mapping.StartsWith("ExtensionTwo", StringComparison.Ordinal);
            player += second ? 1 : 0;
            var number = mapping.Substring(second ? 12 : 12);
            property = "ExtensionButton" + (number.Length == 1 ? number : number[0] + "_" + number[1]);
        }
        else if (mapping.StartsWith("P", StringComparison.Ordinal))
        {
            player += mapping[1] - '1'; property = mapping.Substring(8);
            if (char.IsDigit(property[0])) property = "Button" + property;
        }
        else
        {
            if (mapping.EndsWith("2", StringComparison.Ordinal)) ++player;
            property = mapping.StartsWith("Coin", StringComparison.Ordinal) ? "Coin" : mapping.StartsWith("Service", StringComparison.Ordinal) ? "Service" : "Test";
        }
        typeof(PlayerButtons).GetProperty(property).SetValue(InputCode.PlayerDigitalButtons[player], (bool?)true);
    }
    private static int Capacity(string id)
    {
        if (id.StartsWith("ss32_f1lap", StringComparison.Ordinal)) return 8;
        if (id.StartsWith("ss32_harddunk", StringComparison.Ordinal)) return 6;
        if (id.EndsWith("_link", StringComparison.Ordinal) || id.StartsWith("ss32_radr", StringComparison.Ordinal) ||
            id.StartsWith("ss32_arabfgt", StringComparison.Ordinal) || id.StartsWith("ss32_ga2", StringComparison.Ordinal) ||
            id.StartsWith("ss32_spidman", StringComparison.Ordinal) || id.StartsWith("ss32_titlef", StringComparison.Ordinal) || id == "ss32_kokoroj2") return 4;
        return id.StartsWith("ss32_sonic", StringComparison.Ordinal) ? 3 : 2;
    }
    public static void Run(string root, string temporary)
    {
        Directory.CreateDirectory("TeknoSS32");
        File.WriteAllBytes(Path.Combine("TeknoSS32", "TeknoSS32.exe"), new byte[0]);
        var files = Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), "ss32_*.xml");
        Require(files.Length == 67, "SS32 registration/link profile coverage");
        var serializer = new XmlSerializer(typeof(GameProfile));
        var modes = 0; var seats = 0; var contacts = 0; var axes = 0;
        foreach (var file in files)
        {
            GameProfile profile;
            using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
            profile.ProfileName = Path.GetFileNameWithoutExtension(file);
            Require(profile.EmulatorType == EmulatorType.TeknoSS32 && profile.EmulationProfile == EmulationProfile.TeknoSS32, "SS32 dispatch");
            Require(File.Exists(Path.Combine(root, "TeknoParrotUi.Common/Metadata", profile.ProfileName + ".json")), "SS32 metadata");
            var zip = Path.Combine(temporary, profile.ExecutableName); File.WriteAllBytes(zip, new byte[0]);
            Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
            var offline = TeknoSS32Launcher.Build(profile, zip, null, true);
            foreach (var blank in new[] { "", " ", "\t" })
            {
                Environment.SetEnvironmentVariable("TP_TPONLINE2", blank);
                var local = TeknoSS32Launcher.Build(profile, zip, null, true);
                Require(local.FileName == offline.FileName && local.Arguments == offline.Arguments,
                    "SS32 blank online environment changed offline launch");
            }
            Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
            Require(offline.FileName.EndsWith("TeknoSS32.exe") && offline.Arguments.Contains("--test-menu") && offline.Arguments.Contains("--state-root") && !offline.UseShellExecute, "SS32 operator launch");
            Require(!TeknoSS32Launcher.Build(profile, zip, null).Arguments.Contains("--test-menu"), "SS32 Test switch persisted");
            if (profile.HasTpoSupport)
            {
                ++modes; profile.ConfigValues.Single(v => v.FieldName == "Window Scale").FieldValue = "invalid-saved-option";
                for (var count = 2; count <= Capacity(profile.ProfileName); ++count) for (var seat = 0; seat < count; ++seat)
                {
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", $"test|{seat}|Player|{count}");
                    var online = TeknoSS32Launcher.Build(profile, zip, null, true);
                    Require(online.FileName.EndsWith("TeknoSS32.exe") && online.Arguments.Contains("--net-mode rollback --net-input-delay 2") && !online.UseShellExecute, "SS32 rollback dispatch");
                    Require(!online.Arguments.Contains("--net-player") && !online.Arguments.Contains("--state-root") && !online.Arguments.Contains("--test-menu"), "SS32 manual settings leaked online");
                    ++seats;
                }
            }
            else
            {
                Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|0|Player|2");
                var rejected = false; try { TeknoSS32Launcher.Build(profile, zip, null); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "Unavailable/single-player SS32 room advertised");
            }
            Clear(); InputCode.GameProfile = profile;
            var pipe = new TeknoSS32Pipe(); pipe.Start();
            try
            {
                var digital = 0; var absolute = 0;
                foreach (var button in profile.JoystickButtons)
                {
                    var mapping = button.InputMapping.ToString();
                    if (mapping.Contains("Trackball") || mapping.Contains("LightGun")) continue;
                    if (mapping.StartsWith("Analog", StringComparison.Ordinal))
                    {
                        if (profile.ProfileName.StartsWith("ss32_sonic", StringComparison.Ordinal)) continue;
                        var source = int.Parse(mapping.Substring(6));
                        var previous = InputCode.AnalogBytes[source]; InputCode.AnalogBytes[source] = 73;
                        pipe.Transmit(); Require(JvsHelper.StateView.ReadByte(24 + absolute) == 73, "SS32 analog routing " + profile.ProfileName + " " + mapping);
                        InputCode.AnalogBytes[source] = previous; ++absolute; ++axes; continue;
                    }
                    Clear(); Press(mapping); pipe.Transmit();
                    for (var n = 0; n < 16; ++n)
                        Require(JvsHelper.StateView.ReadByte(8 + n) == (n == digital / 8 ? 1 << (digital % 8) : 0), "SS32 contact isolation " + profile.ProfileName + " " + mapping);
                    Clear(); pipe.Transmit();
                    for (var n = 0; n < 16; ++n) Require(JvsHelper.StateView.ReadByte(8 + n) == 0, "SS32 stuck contact");
                    ++digital; ++contacts;
                }
                Require(JvsHelper.StateView.ReadByte(0) == 'S' && JvsHelper.StateView.ReadByte(1) == 'S' && JvsHelper.StateView.ReadByte(4) == 2 && (JvsHelper.StateView.ReadUInt16(6) & 1) == 0, "SSIN publication contract");
                for (var n = 60; n < 64; ++n) Require(JvsHelper.StateView.ReadByte(n) == 0, "SSIN reserved data");
            }
            finally { pipe.Stop(); Require(JvsHelper.StateView.ReadByte(5) == 0, "SSIN stop ownership"); }
        }
        // The CD folder is independent of ROM ZIPs in both launch paths.
        GameProfile cd;
        using (var stream = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles/ss32_kokoroj2.xml"))) cd = (GameProfile)serializer.Deserialize(stream);
        cd.ProfileName = "ss32_kokoroj2";
        var cdSetting = cd.ConfigValues.Single(v => v.FieldName == "CD Image Folder");
        var media = Directory.CreateDirectory(Path.Combine(temporary, "separate SS32 CD images")).FullName + Path.DirectorySeparatorChar;
        var cdZip = Path.Combine(temporary, "kokoroj2.zip"); File.WriteAllBytes(cdZip, new byte[0]);
        foreach (var online in new[] { false, true })
        {
            Environment.SetEnvironmentVariable("TP_TPONLINE2", online ? "test|0|Player|4" : null);
            cdSetting.FieldValue = media;
            var args = Program.Arguments(TeknoSS32Launcher.Build(cd, cdZip, null).Arguments);
            Require(args[Array.IndexOf(args, "--chd-root") + 1] == media && args[Array.IndexOf(args, "--rom-root") + 1] == Path.GetDirectoryName(cdZip), "SS32 split CD root or Windows quoting");
            cdSetting.FieldValue = " "; args = Program.Arguments(TeknoSS32Launcher.Build(cd, cdZip, null).Arguments);
            Require(args[Array.IndexOf(args, "--chd-root") + 1] == Path.GetDirectoryName(cdZip), "Empty SS32 CD root changed defaults");
            cdSetting.FieldValue = Path.Combine(temporary, "missing CD folder");
            var rejected = false; try { TeknoSS32Launcher.Build(cd, cdZip, null); } catch (DirectoryNotFoundException) { rejected = true; }
            Require(rejected, "Missing SS32 CD root was not diagnosed");
        }
        // Three independent trackballs, both axes, signed wrap, large motion and idle.
        GameProfile sonic;
        using (var stream = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles/ss32_sonic.xml"))) sonic = (GameProfile)serializer.Deserialize(stream);
        sonic.ProfileName = "ss32_sonic"; InputCode.GameProfile = sonic;
        sonic.ConfigValues.Single(v => v.FieldName == "Input API").FieldValue = "RawInputTrackball";
        var relative = new TeknoSS32Pipe(); relative.Start(); relative.Running = false; System.Threading.Thread.Sleep(40);
        var maps = new MemoryMappedFile[3]; var views = new MemoryMappedViewAccessor[3];
        try
        {
            for (var p = 0; p < 3; ++p)
            {
                maps[p] = MemoryMappedFile.CreateOrOpen("RawInputTrackballSharedMemory" + (p == 0 ? "" : (p + 1).ToString()), 12);
                views[p] = maps[p].CreateViewAccessor();
                views[p].Write(0, (short)(7 + p)); views[p].Write(4, (short)(-9 - p)); views[p].Write(8, 0);
            }
            relative.Transmit();
            for (var p = 0; p < 3; ++p)
            {
                Require(JvsHelper.StateView.ReadUInt32(24 + p * 8) == 7 + p && JvsHelper.StateView.ReadUInt32(28 + p * 8) == unchecked((uint)(-9 - p)), "SS32 independent trackball XY routing");
                Require(views[p].ReadInt32(8) == 1, "SS32 trackball acknowledgment");
            }
            relative.Transmit();
            for (var p = 0; p < 3; ++p) Require(JvsHelper.StateView.ReadUInt32(24 + p * 8) == 7 + p && JvsHelper.StateView.ReadUInt32(28 + p * 8) == unchecked((uint)(-9 - p)), "SS32 idle repeated motion");
            views[2].Write(0, (short)300); views[2].Write(4, (short)-300); views[2].Write(8, 0);
            relative.Transmit();
            Require(JvsHelper.StateView.ReadUInt32(40) == 309 && JvsHelper.StateView.ReadUInt32(44) == unchecked((uint)-311), "SS32 full host motion was truncated");
            views[2].Write(8, 0); relative.Transmit(); relative.Transmit();
            Require(JvsHelper.StateView.ReadUInt32(40) == 609 && JvsHelper.StateView.ReadUInt32(44) == unchecked((uint)-611), "SS32 skipped publication lost or repeated motion");
            sonic.ConfigValues.Single(v => v.FieldName == "Input API").FieldValue = "XInput";
            InputCode.AnalogBytes[0] = 144; InputCode.AnalogBytes[1] = 112; relative.Transmit();
            Require(JvsHelper.StateView.ReadUInt32(24) == 9 && JvsHelper.StateView.ReadUInt32(28) == unchecked((uint)-11), "SS32 controller relative axes");
        }
        finally
        {
            relative.Stop(); foreach (var view in views) view?.Dispose(); foreach (var map in maps) map?.Dispose();
        }
        Require(modes == 57, "SS32 room admission count");
        Console.WriteLine($"67 SS32 profiles, {modes} online modes, {seats} room seats, {contacts} contacts and {axes} axes passed.");
    }
}
