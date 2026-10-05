using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    // Force-feedback device discovery for TeknoS22 (S22haptic.exe) and
    // TeknoS23 (S23haptic.exe). Both helpers share model3haptic's --list format.
    internal static class NamcoFfbDeviceProbe
    {
        private const int ProbeTimeoutMilliseconds = 5000;

        public const string S22Directory = "TeknoS22";
        public const string S22Executable = "S22haptic.exe";
        public const string S23Directory = "TeknoS23";
        public const string S23Executable = "S23haptic.exe";

        // TeknoS22/TeknoS23 accept the helper's wheel:<n> / gamepad:<n> tokens
        // directly (zero-based discovery positions, as TeknoModel3 does), plus
        // the persistent hex name/path selectors.
        internal static string GetLaunchSelection(string value)
        {
            value = value?.Trim() ?? "off";

            if (Regex.IsMatch(value, @"\A(?:wheel|gamepad):(?:0|[1-9][0-9]*)\z") &&
                uint.TryParse(value.Substring(value.IndexOf(':') + 1), out _))
                return value;

            if (Regex.IsMatch(value,
                    @"\A(?:haptic-name|gamepad-path|gamepad-name):(?:[0-9a-f]{2})+\z"))
                return value;
            return "off";
        }

        public static List<DynamicDropdownOption> GetDevices(EmulatorType emulatorType) =>
            emulatorType == EmulatorType.TeknoS23
                ? GetDevices(S23Directory, S23Executable)
                : GetDevices(S22Directory, S22Executable);

        public static List<DynamicDropdownOption> GetDevices(string emulatorDirectory, string executableName)
        {
            var devices = new List<DynamicDropdownOption>
            {
                new DynamicDropdownOption
                {
                    DisplayName = "Force feedback off",
                    Value = "off"
                }
            };

            // The haptic helpers are small unprotected executables that never load
            // the emulator; there is deliberately no fallback to TeknoS22/S23.exe.
            var directory = Path.Combine(Directory.GetCurrentDirectory(), emulatorDirectory);
            var probePath = Path.Combine(directory, executableName);
            if (!File.Exists(probePath))
                return devices;

            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo(probePath, "--list")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = directory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                })
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(ProbeTimeoutMilliseconds))
                    {
                        process.Kill();
                        return devices;
                    }

                    var stdout = output.GetAwaiter().GetResult();
                    error.GetAwaiter().GetResult();
                    if (process.ExitCode != 0)
                        return devices;

                    var labels = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var line in stdout.Split(new[] { "\r\n", "\n" },
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        var fields = line.Split(new[] { '\t' }, 2);
                        if (fields.Length != 2 || fields[0] == "off" ||
                            string.IsNullOrWhiteSpace(fields[0]) ||
                            string.IsNullOrWhiteSpace(fields[1]))
                            continue;

                        var label = fields[1].Trim();
                        if (!labels.Add(label))
                            label += $" [{fields[0]}]";
                        devices.Add(new DynamicDropdownOption
                        {
                            DisplayName = label,
                            Value = fields[0].Trim()
                        });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                // Missing or older emulator packages leave feedback switched off.
            }

            return devices;
        }
    }
}
