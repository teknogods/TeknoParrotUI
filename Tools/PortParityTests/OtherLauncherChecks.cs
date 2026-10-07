using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

internal static class OtherLauncherChecks
{
    internal static void Run(string root, string temporary)
    {
        var types = new[] { EmulatorType.TeknoModel1, EmulatorType.TeknoModel2, EmulatorType.TeknoModel3,
            EmulatorType.TeknoHDrive, EmulatorType.TeknoMagic, EmulatorType.TeknoVegas };
        var count = 0;
        var serializer = new XmlSerializer(typeof(GameProfile));
        foreach (var file in Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common/GameProfiles"), "*.xml"))
        {
            GameProfile profile;
            using (var stream = File.OpenRead(file)) profile = (GameProfile)serializer.Deserialize(stream);
            if (!types.Contains(profile.EmulatorType) || profile.DevOnly) continue;
            profile.ProfileName = Path.GetFileNameWithoutExtension(file);
            var package = profile.EmulatorType.ToString();
            Directory.CreateDirectory(package);
            File.WriteAllBytes(Path.Combine(package, package == "TeknoMagic" ? "teknomagic.exe" : package + ".exe"), new byte[0]);
            var game = Path.Combine(temporary, profile.ProfileName + ".zip");
            profile.GamePath = game;
            profile.GamePath2 = Path.Combine(temporary, profile.ProfileName + ".chd");
            File.WriteAllBytes(game, new byte[64]); File.WriteAllBytes(profile.GamePath2, new byte[64]);
            void Set(string name, string value)
            {
                var field = profile.ConfigValues.FirstOrDefault(item => item.FieldName == name);
                if (field != null) field.FieldValue = value;
            }
            Set("Enable VR", "0");
            Set("Enable Submission", "1");
            Set("Integer Scaling", "1");
            Set("Set Game Link Settings", "0");
            var info = ExternalEmulatorLauncher.Build(profile, game, null);
            if (info.UseShellExecute || info.Arguments.Length == 0 || !info.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Invalid standalone launch: " + profile.ProfileName);
            if (profile.EmulatorType == EmulatorType.TeknoModel1 &&
                profile.ConfigValues.Any(field => field.FieldName == "Integer Scaling") &&
                !info.Arguments.Contains("--integer-scaling"))
                throw new Exception("Missing integer scaling: " + profile.ProfileName);
            if (profile.EmulatorType is EmulatorType.TeknoModel2 or EmulatorType.TeknoModel3 &&
                profile.ConfigValues.Any(field => field.FieldName == "Enable Submission") &&
                (!info.Arguments.Contains("--score-submission") || !info.Environment.ContainsKey("TP_SCORE_SUBMISSION_ID")))
                throw new Exception("Missing score handoff: " + profile.ProfileName);
            ++count;
        }
        Console.WriteLine(count + " Model 1/2/3, HDrive, Magic and Vegas native launcher configurations passed.");
    }
}
