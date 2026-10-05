using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class Program
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Setting(GameProfile profile, string name, string value)
    {
        profile.ConfigValues.First(x => x.FieldName == name).FieldValue = value;
    }

    private static int Main(string[] args)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalOnline = Environment.GetEnvironmentVariable("TP_TPONLINE2");
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "tponline-launchers-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(temporary);
        try
        {
            var root = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(temporary);
            foreach (var system in new[] { "TeknoS22", "TeknoS23" })
            {
                Directory.CreateDirectory(system);
                File.WriteAllBytes(Path.Combine(system, system + ".exe"), new byte[0]);
            }
            var serializer = new XmlSerializer(typeof(GameProfile));
            var games = new[] { "acedrive", "adillor", "alpinr2b", "cybrcomm", "cybrcycc", "raverace", "ridgera2j", "tokyowar", "victlapj", "500gp", "downhillu", "finfurl", "finfurl2", "gunwars", "motoxgo", "raceonj", "timecrs2v5a" };
            foreach (var game in games)
            {
                GameProfile profile;
                using (var input = File.OpenRead(Path.Combine(root, "TeknoParrotUi.Common", "GameProfiles", game + ".xml")))
                    profile = (GameProfile)serializer.Deserialize(input);
                profile.ProfileName = game;
                Require(profile.HasTpoSupport && !profile.DevOnly && profile.Patreon, "Wrong publication flags: " + game);
                var zip = Path.Combine(temporary, game + ".zip");
                File.WriteAllBytes(zip, new byte[0]);
                var s22 = profile.EmulatorType == EmulatorType.TeknoS22;
                Setting(profile, "Enable LAN", "1");
                Setting(profile, "Cabinet ID", "8");
                Setting(profile, "Cabinet Count", "8");
                Setting(profile, "Local Port", "59999");
                Setting(profile, "Peer Address", "invalid-saved-peer");
                if (!s22) Setting(profile, "Session ID", "123456");
                Environment.SetEnvironmentVariable("TP_TPONLINE2", "test|0|player|2");
                var online = s22 ? TeknoS22Launcher.Build(profile, zip, null) : TeknoS23Launcher.Build(profile, zip, null);
                Require(!online.Arguments.Contains("--link-") && !online.Arguments.Contains("--cabinet-node") && !online.Arguments.Contains("--link-session"), "Saved LAN settings leaked into TPOnline: " + game);
                Require(!online.UseShellExecute, "TPOnline environment cannot be inherited: " + game);
                Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
                var manual = s22 ? TeknoS22Launcher.Build(profile, zip, null) : TeknoS23Launcher.Build(profile, zip, null);
                Require(manual.Arguments.Contains(s22 ? "--link-node" : "--link-nodes"), "Manual LAN arguments lost: " + game);

            }
            Console.WriteLine("All 17 real XML profiles passed TPOnline/manual LAN launch checks.");
            MvsChecks.Run(root, temporary);
            CpsChecks.Run(root, temporary);
            Ss32Checks.Run(root, temporary);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("TP_TPONLINE2", originalOnline);
            Directory.SetCurrentDirectory(originalDirectory);
            var prefix = Path.GetFullPath(Path.GetTempPath()) + "tponline-launchers-";
            if (temporary.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) Directory.Delete(temporary, true);
        }
    }
}
