using System;
using System.IO;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Reads the small, validated profile INI forwarded with an Android game
    /// session. This keeps Android-only control behavior aligned with the same
    /// profile settings consumed by the desktop input listeners.
    /// </summary>
    public static class AndroidProfileConfig
    {
        public static bool IsBooleanEnabled(
            string profileConfigIni,
            string section,
            string key)
        {
            if (string.IsNullOrWhiteSpace(profileConfigIni))
                return false;

            var currentSection = string.Empty;
            using var reader = new StringReader(profileConfigIni);
            while (reader.ReadLine() is { } line)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] is ';' or '#')
                    continue;
                if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
                {
                    currentSection = trimmed[1..^1].Trim();
                    continue;
                }
                if (!string.Equals(
                        currentSection,
                        section,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var separator = trimmed.IndexOf('=');
                if (separator <= 0 ||
                    !string.Equals(
                        trimmed[..separator].Trim(),
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = trimmed[(separator + 1)..].Trim();
                return value == "1" ||
                       value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("on", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
