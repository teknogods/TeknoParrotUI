using System;
using System.Linq;

namespace TeknoParrotUi.Common
{
    public static class TypeZeroControls
    {
        public static bool IsPowerShovel(GameProfile profile) =>
            profile?.EmulatorType == EmulatorType.TeknoTZero &&
            (profile.ProfileName == "pwrshovl_tz" || profile.ExecutableName == "pwrshovl.zip");

        public static string PreviousControlName(GameProfile profile, string name)
        {
            if (!IsPowerShovel(profile)) return name;
            var levers = new[] { "Player 1 Left", "Player 1 Right", "Player 2 Left", "Player 2 Right" };
            for (var lever = 0; lever < levers.Length; ++lever)
            {
                var prefix = levers[lever] + " ";
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var axis = name.Substring(prefix.Length);
                switch (axis)
                {
                    case "X": case "Y": break;
                    case "X (Left)": axis = "Left"; break;
                    case "X (Right)": axis = "Right"; break;
                    case "Y (Up)": axis = "Up"; break;
                    case "Y (Down)": axis = "Down"; break;
                    default: return name;
                }
                return "Player " + (lever + 1) + " Joystick " + axis;
            }
            return name;
        }

        // Online room launches can load saved profiles before the library migrates them.
        // Upgrade the transient copy so the old X polarity cannot survive an online launch.
        public static void UpgradeSavedPowerShovelControls(GameProfile stock, GameProfile saved)
        {
            if (!IsPowerShovel(stock) || saved.GameProfileRevision >= stock.GameProfileRevision) return;
            for (var index = 0; index < saved.JoystickButtons.Count; ++index)
            {
                var old = saved.JoystickButtons[index];
                var current = stock.JoystickButtons.FirstOrDefault(button =>
                    button.ButtonName == old.ButtonName || PreviousControlName(stock, button.ButtonName) == old.ButtonName);
                if (current == null) continue;
                var replacement = current.Clone();
                replacement.DirectInputButton = old.DirectInputButton;
                replacement.XInputButton = old.XInputButton;
                replacement.RawInputButton = old.RawInputButton;
                replacement.BindNameDi = old.BindNameDi;
                replacement.BindNameXi = old.BindNameXi;
                replacement.BindNameRi = old.BindNameRi;
                replacement.BindName = old.BindName;
                saved.JoystickButtons[index] = replacement;
            }
            foreach (var field in stock.ConfigValues)
                if (!saved.ConfigValues.Any(value => value.FieldName == field.FieldName))
                    saved.ConfigValues.Add(field.Clone());
            saved.GameProfileRevision = stock.GameProfileRevision;
        }
    }
}
