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
        public static void WriteConfigIni(GameProfile gameProfile, string gameLocation, string gameLocation2, bool twoExes)
        {
            var lameFile = "";
            var categories = gameProfile.ConfigValues.Select(x => x.CategoryName).Distinct().ToList();

            if (!string.IsNullOrEmpty(gameProfile.GameVersion))
            {
                lameFile += "[GameInfo]\n";
                lameFile += "GameVersion=" + gameProfile.GameVersion + "\n";
            }

            lameFile += "[GlobalHotkeys]\n";
            lameFile += "ExitKey=" + Lazydata.ParrotData.ExitGameKey + "\n";
            lameFile += "PauseKey=" + Lazydata.ParrotData.PauseGameKey + "\n";

            bool scoreEnabled = gameProfile.ConfigValues.Any(x => x.FieldName == "Enable Submission" && x.FieldValue == "1");
            if (scoreEnabled)
            {
                lameFile += "[GlobalScore]\n";
                lameFile += "Submission ID=" + Lazydata.ParrotData.ScoreSubmissionID + "\n";
                lameFile += "CollapseGUIKey=" + Lazydata.ParrotData.ScoreCollapseGUIKey + "\n";
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
