using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

internal static class PresentationChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static GameProfile Load(string root, string id)
    {
        using (var stream = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles", id + ".xml")))
        {
            var profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
            profile.ProfileName = id;
            return profile;
        }
    }
    private static void Set(GameProfile profile, string name, string value) => profile.ConfigValues.Single(field => field.FieldName == name).FieldValue = value;
    private static ProcessStartInfo Build(GameProfile profile, string zip)
    {
        if (profile.EmulatorType == EmulatorType.TeknoMVS) return TeknoMVSLauncher.Build(profile, zip, null);
        if (profile.EmulatorType == EmulatorType.TeknoCPS) return TeknoCPSLauncher.Build(profile, zip, null);
        return TeknoSS32Launcher.Build(profile, zip, null);
    }

    internal static void Run(string root, string temporary)
    {
        var files = Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), "*.xml")
            .Where(file => new[] { "mvs_", "cps_", "ss32_" }.Any(prefix => Path.GetFileName(file).StartsWith(prefix, StringComparison.Ordinal))).ToArray();
        Require(files.Length == 995, "Expected all native arcade revisions");
        foreach (var file in files)
        {
            var profile = Load(root, Path.GetFileNameWithoutExtension(file));
            foreach (var name in new[] { "Window Mode", "Use Bezel", "CRT Shader" })
                Require(profile.ConfigValues.Count(field => field.FieldName == name && field.CategoryName == "Video") == 1, profile.ProfileName + " missing " + name);
            Require(profile.GameProfileRevision >= (profile.EmulatorType == EmulatorType.TeknoCPS ? 5 : 4), "Missing profile migration revision");
            Require(profile.ConfigValues.Single(field => field.FieldName == "Window Mode").FieldValue == "Windowed", "Window default changed");
        }
        var cases = 0;
        foreach (var id in new[] { "mvs_nam1975", "cps_sf2", "ss32_orunners" })
        {
            var profile = Load(root, id);
            var zip = Path.Combine(temporary, profile.ExecutableName); File.WriteAllBytes(zip, new byte[0]);
            foreach (var online in new[] { false, true })
            foreach (var mode in new[] { "Windowed", "Fullscreen" })
            foreach (var bezel in new[] { "0", "1" })
            foreach (var crt in new[] { "off", "lottes", "lottes-downsample" })
            {
                Environment.SetEnvironmentVariable("TP_TPONLINE2", online ? "presentation|1|Player|2" : null);
                Set(profile, "Window Mode", mode); Set(profile, "Use Bezel", bezel); Set(profile, "CRT Shader", crt);
                var launch = Build(profile, zip); Program.NativeLaunch(launch);
                var args = Program.Arguments(launch.Arguments);
                Require(args.Count(value => value == "--fullscreen") == (mode == "Fullscreen" ? 1 : 0), "Fullscreen setting lost " + id);
                Require(args.Count(value => value == "--use-bezel") == (bezel == "1" ? 1 : 0), "Bezel setting lost " + id);
                Require(args.Count(value => value == "--crt") == 1 && args[Array.IndexOf(args, "--crt") + 1] == crt, "CRT setting lost " + id);
                if (bezel == "1") Require(Directory.Exists(Path.Combine(launch.WorkingDirectory, "bezels")), "Artwork folder missing");
                ++cases;
            }
            Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
            foreach (var name in new[] { "Window Mode", "Use Bezel", "CRT Shader" })
            {
                var field = profile.ConfigValues.Single(value => value.FieldName == name); var saved = field.FieldValue;
                field.FieldValue = "invalid";
                var rejected = false; try { Build(profile, zip); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "Malformed presentation option accepted " + name); field.FieldValue = saved;
            }
            var revision = ArcadeGameRevisions.GetOptions(profile).Last().Value;
            Set(profile, "Game Revision", revision);
            var selected = ArcadeGameRevisions.CreateLaunchProfile(profile);
            Require(selected.ConfigValues.Single(field => field.FieldName == "Use Bezel").FieldValue == "1" &&
                selected.ConfigValues.Single(field => field.FieldName == "CRT Shader").FieldValue == "lottes-downsample" &&
                selected.ConfigValues.Single(field => field.FieldName == "Window Mode").FieldValue == "Fullscreen", "Presentation preferences lost when selecting a revision");
        }
        foreach (var file in files.Where(file => Path.GetFileName(file).StartsWith("ss32_orunners", StringComparison.Ordinal)))
        {
            var profile = Load(root, Path.GetFileNameWithoutExtension(file));
            var zip = Path.Combine(temporary, profile.ExecutableName); File.WriteAllBytes(zip, new byte[0]);
            foreach (var screens in new[] { "Both", "Monitor 1", "Monitor 2" })
            {
                Environment.SetEnvironmentVariable("TP_TPONLINE2", null); Set(profile, "Screens", screens);
                var args = Program.Arguments(Build(profile, zip).Arguments);
                Require(args[Array.IndexOf(args, "--screens") + 1] == (screens == "Both" ? "both" : screens == "Monitor 1" ? "first" : "second"), "OutRunners offline monitor choice lost");
                for (var seat = 0; seat < (profile.ProfileName.EndsWith("_link") ? 4 : 2); ++seat)
                {
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", "screens|" + seat + "|Player|" + (profile.ProfileName.EndsWith("_link") ? 4 : 2));
                    Require(!Program.Arguments(Build(profile, zip).Arguments).Contains("--screens"), "Local monitor choice overrides TPO seat");
                }
            }
        }
        Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
        Console.WriteLine("All 995 profiles have video settings; " + cases + " offline/TPO presentation combinations and all 6 OutRunners screen profiles passed.");
    }
}
