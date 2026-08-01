using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace InputMethodAudit
{
    internal static class DolphinPresetTest
    {
        public static int Run()
        {
            var originalDirectory = Directory.GetCurrentDirectory();
            var testDirectory = Path.Combine(
                Path.GetTempPath(),
                "tp-dolphin-preset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);

            try
            {
                Directory.SetCurrentDirectory(testDirectory);
                Apply(EmulationProfile.Tatsunoko);

                var dolphinPath = Path.Combine(
                    testDirectory,
                    "CrediarDolphin",
                    "User",
                    "Config",
                    "Dolphin.ini");
                var gfxPath = Path.Combine(
                    testDirectory,
                    "CrediarDolphin",
                    "User",
                    "Config",
                    "GFX.ini");

                AssertSetting(dolphinPath, "SerialPort1", "255");
                AssertSetting(dolphinPath, "SlotA", "15");
                AssertSetting(dolphinPath, "SlotB", "15");
                AssertSetting(dolphinPath, "RAMOverrideEnable", "True");
                AssertSetting(dolphinPath, "SkipIPL", "True");
                AssertSetting(gfxPath, "InternalResolution", "4");

                File.AppendAllLines(dolphinPath, new[]
                {
                    "",
                    "[Custom]",
                    "KeepMe = Yes",
                    "",
                    "[Analytics]",
                    "ID = should-be-removed"
                });

                Apply(EmulationProfile.MarioKartGP2);
                AssertSetting(dolphinPath, "SerialPort1", "6");
                AssertSetting(dolphinPath, "RAMOverrideEnable", "False");
                AssertSetting(dolphinPath, "KeepMe", "Yes");

                var lines = File.ReadAllLines(dolphinPath);
                if (lines.Any(line => line.TrimStart().StartsWith("ID =", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Dolphin analytics ID was not removed.");
                if (lines.Count(line => line.TrimStart().StartsWith(
                        "SerialPort1 =", StringComparison.OrdinalIgnoreCase)) != 1)
                    throw new InvalidDataException("SerialPort1 was duplicated while switching profiles.");

                Console.WriteLine("PASS: Dolphin presets switch cleanly between RVA and Triforce profiles.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error);
                return 1;
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                Directory.Delete(testDirectory, recursive: true);
            }
        }

        private static void Apply(EmulationProfile profile)
        {
            var method = typeof(ExternalEmulatorLauncher).GetMethod(
                "ConfigureDolphinIni",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException("ConfigureDolphinIni was not found.");

            method.Invoke(null, new object[] { profile });
        }

        private static void AssertSetting(string path, string key, string expected)
        {
            var prefix = key + " =";
            var line = File.ReadAllLines(path).LastOrDefault(candidate =>
                candidate.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (line == null)
                throw new InvalidDataException($"{key} is missing from {path}.");

            var actual = line.Substring(line.IndexOf('=') + 1).Trim();
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{key} expected {expected}, got {actual}.");
        }
    }
}
