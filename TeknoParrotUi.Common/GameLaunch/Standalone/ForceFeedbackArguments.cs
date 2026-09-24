using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Common.GameLaunch
{
    // Only emit options supported by the emulator and declared by this game.
    internal static class ForceFeedbackArguments
    {
        public static void Add(GameProfile profile, IList<string> arguments, params string[] effects)
        {
            if (profile.ConfigValues == null) return;
            string Setting(string name, string fallback = null) =>
                profile.ConfigValues.FirstOrDefault(f => f.FieldName == name)?.FieldValue ?? fallback;
            if (effects.Contains("Spring") && Setting("Force Feedback Spring Effect") != null)
            {
                var mode = Setting("Force Feedback Spring Effect");
                arguments.Add("--ffb-spring-mode");
                arguments.Add(mode == "Spring using Constant Force" || mode == "Constant Spring" ? "constant" : "spring");
            }
            if (effects.Contains("Uncentering") && Setting("Uncentering Effect Mode") != null)
            {
                arguments.Add("--ffb-uncentering-mode");
                var mode = Setting("Uncentering Effect Mode");
                arguments.Add(mode == "Both" ? "both" :
                    mode == "Sine vibration" || mode == "Sine vibration (experimental)" ? "sine" : "uncentering");
                if (!int.TryParse(Setting("Uncentering Sine Period", "40"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var period) || period < 10 || period > 200)
                    period = 40;
                arguments.Add("--ffb-uncentering-period");
                arguments.Add(period.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var effect in effects)
            {
                for (var player = 1; player <= (effect == "Recoil" ? 3 : 1); ++player)
                {
                    var prefix = player == 1 ? "" : "Player " + player + " ";
                    var value = Setting(prefix + effect + " Effect Strength");
                    if (value == null) continue;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gain) || gain < 0 || gain > 100)
                        gain = effect == "Uncentering" ? 30 : 100;
                    var enabled = Setting(prefix + "Enable " + effect + " Effect", "1");
                    if (enabled != "1" && !enabled.Equals("true", StringComparison.OrdinalIgnoreCase)) gain = 0;
                    arguments.Add("--ffb-" + effect.ToLowerInvariant() + "-gain" + (player == 1 ? "" : "-p" + player));
                    arguments.Add(gain.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
