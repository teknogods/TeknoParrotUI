using System;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.Utilities
{
    internal class ConfigurationWriter
    {
        private readonly GameProfile _gameProfile;
        private readonly string _gameLocation;
        private readonly string _gameLocation2;
        private readonly bool _twoExes;

        public ConfigurationWriter(GameProfile gameProfile, string gameLocation, string gameLocation2, bool twoExes)
        {
            _gameProfile = gameProfile;
            _gameLocation = gameLocation;
            _gameLocation2 = gameLocation2;
            _twoExes = twoExes;
        }

        public void WriteConfigIni()
        {
            var lameFile = "";
            var categories = _gameProfile.ConfigValues.Select(x => x.CategoryName).Distinct().ToList();
            lameFile += "[GlobalHotkeys]\n";
            lameFile += "ExitKey=" + Lazydata.ParrotData.ExitGameKey + "\n";
            lameFile += "PauseKey=" + Lazydata.ParrotData.PauseGameKey + "\n";

            bool ScoreEnabled = _gameProfile.ConfigValues.Any(x => x.FieldName == "Enable Submission" && x.FieldValue == "1");
            if (ScoreEnabled)
            {
                lameFile += "[GlobalScore]\n";
                lameFile += "Submission ID=" + Lazydata.ParrotData.ScoreSubmissionID + "\n";
                lameFile += "CollapseGUIKey=" + Lazydata.ParrotData.ScoreCollapseGUIKey + "\n";
            }

            for (var i = 0; i < categories.Count(); i++)
            {
                lameFile += $"[{categories[i]}]{Environment.NewLine}";
                var variables = _gameProfile.ConfigValues.Where(x => x.CategoryName == categories[i]);
                lameFile = variables.Aggregate(lameFile,
                    (current, fieldInformation) =>
                    {
                        var fieldValue = fieldInformation.FieldType == FieldType.DropdownIndex
                            ? fieldInformation.FieldOptions.IndexOf(fieldInformation.FieldValue).ToString()
                            : fieldInformation.FieldValue;
                        return current + $"{fieldInformation.FieldName}={fieldValue}{Environment.NewLine}";
                    });
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException(), "teknoparrot.ini"), lameFile);

            if (_twoExes && !string.IsNullOrEmpty(_gameLocation2))
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation2) ?? throw new InvalidOperationException(), "teknoparrot.ini"), lameFile);
            }
        }
    }
}