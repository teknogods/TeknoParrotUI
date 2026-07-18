using System;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Writes teknoparrot.ini next to the game executable(s).
    /// Verbatim port of the classic ConfigurationWriter.
    /// </summary>
    public static class TeknoParrotIniWriter
    {
        /// <summary>
        /// Builds the complete configuration consumed by OpenParrot/TeknoParrot.
        /// Android forwards this same payload to its Winlator companion so game
        /// profile options cannot diverge from the Windows/Linux launch path.
        /// </summary>
        public static string BuildConfigIni(GameProfile gameProfile)
        {
            ArgumentNullException.ThrowIfNull(gameProfile);
            var lameFile = "";
            var categories = gameProfile.ConfigValues.Select(x => x.CategoryName).Distinct().ToList();
            var parrotData = Lazydata.ParrotData ?? new ParrotData();

            if (!string.IsNullOrEmpty(gameProfile.GameVersion))
            {
                lameFile += "[GameInfo]\n";
                lameFile += "GameVersion=" + gameProfile.GameVersion + "\n";
            }

            lameFile += "[GlobalHotkeys]\n";
            lameFile += "ExitKey=" + parrotData.ExitGameKey + "\n";
            lameFile += "PauseKey=" + parrotData.PauseGameKey + "\n";

            bool scoreEnabled = gameProfile.ConfigValues.Any(x => x.FieldName == "Enable Submission" && x.FieldValue == "1");
            if (scoreEnabled)
            {
                lameFile += "[GlobalScore]\n";
                lameFile += "Submission ID=" + parrotData.ScoreSubmissionID + "\n";
                lameFile += "CollapseGUIKey=" + parrotData.ScoreCollapseGUIKey + "\n";
            }

            for (var i = 0; i < categories.Count; i++)
            {
                lameFile += $"[{categories[i]}]{Environment.NewLine}";
                var variables = gameProfile.ConfigValues.Where(x => x.CategoryName == categories[i]);
                lameFile = variables.Aggregate(lameFile,
                    (current, fieldInformation) =>
                    {
                        var fieldValue = fieldInformation.FieldType == FieldType.DropdownIndex
                            ? fieldInformation.FieldOptions.IndexOf(fieldInformation.FieldValue).ToString()
                            : fieldInformation.FieldValue;
                        return current + $"{fieldInformation.FieldName}={fieldValue}{Environment.NewLine}";
                    });
            }

            return lameFile;
        }

        public static void WriteConfigIni(GameProfile gameProfile, string gameLocation, string gameLocation2, bool twoExes)
        {
            var lameFile = BuildConfigIni(gameProfile);

            var gameDir = Path.GetDirectoryName(gameLocation) ?? throw new InvalidOperationException();
            File.WriteAllText(Path.Combine(gameDir, "teknoparrot.ini"), lameFile);

            if (gameProfile.EmulatorType == EmulatorType.TeknoMacaw && Path.GetFileName(gameDir) == "modules")
            {
                var iniPath = Path.GetFullPath(Path.Combine(gameDir, "..", "teknoparrot.ini"));
                File.WriteAllText(iniPath, lameFile);
            }

            if (twoExes && !string.IsNullOrEmpty(gameLocation2))
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(gameLocation2) ?? throw new InvalidOperationException(), "teknoparrot.ini"), lameFile);
            }
        }
    }
}
