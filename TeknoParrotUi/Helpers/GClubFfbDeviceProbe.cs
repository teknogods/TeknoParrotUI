using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class GClubFfbDeviceProbe
    {
        private const int ProbeTimeoutMilliseconds = 5000;

        internal static string GetLaunchSelection(string value)
        {
            value = value?.Trim() ?? "off";
            if (value.Length > 8192) return "off";

            // The helper reports ordinals; the emulator's bare gamepad:N token
            // denotes an SDL session ID, so pass explicit index selectors.
            if (Regex.IsMatch(value, @"\A(?:wheel|gamepad):(?:0|[1-9][0-9]*)\z") &&
                uint.TryParse(value.Substring(value.IndexOf(':') + 1), out _))
            {
                return (value.StartsWith("wheel:", StringComparison.Ordinal)
                    ? "haptic-index:" : "gamepad-index:") +
                    value.Substring(value.IndexOf(':') + 1);
            }

            if (Regex.IsMatch(value,
                    @"\A(?:haptic-name|gamepad-path|gamepad-name):(?:[0-9a-f]{2})+\z") ||
                (Regex.IsMatch(value, @"\A(?:haptic|gamepad)-index:(?:0|[1-9][0-9]*)\z") &&
                 uint.TryParse(value.Substring(value.IndexOf(':') + 1), out _)))
                return value;
            return "off";
        }

        public static List<DynamicDropdownOption> GetDevices()
        {
            var devices = new List<DynamicDropdownOption>
            {
                new DynamicDropdownOption
                {
                    DisplayName = "Force feedback off",
                    Value = "off"
                }
            };

            var gclubDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TeknoGClub");
            var probePath = Path.Combine(gclubDirectory, "GClubHaptic.exe");
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
                        WorkingDirectory = gclubDirectory,
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
                    var tokens = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var line in stdout.Split(new[] { "\r\n", "\n" },
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        var fields = line.Split(new[] { '\t' }, 2);
                        if (fields.Length != 2 || fields[0] == "off" ||
                            string.IsNullOrWhiteSpace(fields[0]) ||
                            string.IsNullOrWhiteSpace(fields[1]))
                            continue;

                        var token = fields[0].Trim();
                        if (GetLaunchSelection(token) == "off" || !tokens.Add(token))
                            continue;

                        var label = fields[1].Trim();
                        if (!labels.Add(label))
                            label += $" [{fields[0]}]";
                        devices.Add(new DynamicDropdownOption
                        {
                            DisplayName = label,
                            Value = token
                        });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                // Keep the off option available when the helper cannot be run.
            }

            return devices;
        }
    }
}
