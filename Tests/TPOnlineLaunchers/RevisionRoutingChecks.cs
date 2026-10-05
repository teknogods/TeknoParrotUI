using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class RevisionRoutingChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(string temporary)
    {
        var serializer = new XmlSerializer(typeof(GameProfile));
        var games = new[] { "acedrive", "adillor", "alpinr2b", "cybrcomm", "cybrcycc", "raverace", "ridgera2j", "tokyowar", "victlapj", "500gp", "downhillu", "finfurl", "finfurl2", "gunwars", "motoxgo", "raceonj", "timecrs2v5a" };
        Directory.CreateDirectory("UserProfiles");
        var checkedRevisions = 0;
        foreach (var game in games)
        {
            GameProfile profile;
            using (var input = File.OpenRead(Path.Combine("GameProfiles", game + ".xml")))
                profile = (GameProfile)serializer.Deserialize(input);
            profile.ProfileName = game;
            profile.GamePath = Path.Combine(temporary, game + ".zip");
            File.WriteAllBytes(profile.GamePath, new byte[0]);
            var choices = NamcoGameRevisions.GetOptions(profile);
            var selected = profile.ConfigValues.FirstOrDefault(field => field.FieldName == NamcoGameRevisions.SettingName);
            if (selected != null) selected.FieldValue = choices.Last().Value;
            var userPath = Path.Combine("UserProfiles", game + ".xml");
            using (var output = File.Create(userPath)) serializer.Serialize(output, profile);
            var original = File.ReadAllBytes(userPath);
            foreach (var choice in choices)
            {
                var online = OnlineGameRevisionProfiles.Load(choice.Value);
                Require(online != null && online.ProfileName == game, "Online parent lookup failed: " + choice.Value);
                Require(online.GamePath == profile.GamePath, "Parent media path lost: " + choice.Value);
                Require(NamcoGameRevisions.ResolveSet(online) == choice.Value, "Room revision did not override offline revision");
                Environment.SetEnvironmentVariable("TP_TPONLINE2", "revision-test|0|Player|2");
                var launch = online.EmulatorType == EmulatorType.TeknoS22
                    ? TeknoS22Launcher.Build(online, online.GamePath, null)
                    : TeknoS23Launcher.Build(online, online.GamePath, null);
                var arguments = Program.Arguments(launch.Arguments);
                var nativeSet = online.EmulatorType == EmulatorType.TeknoS22
                    ? arguments[Array.IndexOf(arguments, "--game") + 1] : arguments.Last();
                Require(nativeSet == choice.Value, "Native command used wrong revision: " + choice.Value);
                Require(File.ReadAllBytes(userPath).SequenceEqual(original), "Online launch modified offline settings");
                ++checkedRevisions;
            }
        }
        Environment.SetEnvironmentVariable("TP_TPONLINE2", null);
        Require(checkedRevisions == 38, "Expected 38 supported S22/S23 room revisions");
        Require(OnlineGameRevisionProfiles.Load("ordinary-game") == null, "Ordinary TPO profile routing changed");
        var rejected = false;
        try { OnlineGameRevisionProfiles.Load("crszone"); } catch (ArgumentException) { rejected = true; }
        Require(rejected, "Offline Namco game was accepted as online");
        Console.WriteLine("All 38 S22/S23 room revisions resolve to configured parents without changing offline selections.");
    }
}
