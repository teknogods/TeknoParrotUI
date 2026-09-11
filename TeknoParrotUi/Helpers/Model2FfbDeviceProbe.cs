using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class Model2FfbDeviceProbe
    {
        private const int ProbeTimeoutMilliseconds = 5000;

        internal static bool IsPersistentSelection(string value) =>
            value != null && Regex.IsMatch(value,
                @"\A(?:haptic-name|gamepad-path|gamepad-name):(?:[0-9a-f]{2})+\z");

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

            var model2Directory = Path.Combine(Directory.GetCurrentDirectory(), "TeknoModel2");
            var probePath = Path.Combine(model2Directory, "TeknoModel2.exe");
            if (!File.Exists(probePath))
                return devices;

            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo(probePath, "--list-ffb-devices-ui")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = model2Directory,
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
                    var selections = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var line in stdout.Split(new[] { "\r\n", "\n" },
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        var fields = line.Split(new[] { '\t' }, 3);
                        // Only the third column survives a new emulator process.
                        // Empty selections identify devices the emulator cannot distinguish.
                        if (fields.Length != 3 || !IsPersistentSelection(fields[2]) ||
                            string.IsNullOrWhiteSpace(fields[1]) || !selections.Add(fields[2]))
                            continue;

                        var label = (fields[2].StartsWith("haptic-name:", StringComparison.Ordinal)
                            ? "Wheel FFB - " : "Controller vibration - ") + fields[1].Trim();
                        if (!labels.Add(label))
                            label += $" [{fields[0]}]";
                        devices.Add(new DynamicDropdownOption
                        {
                            DisplayName = label,
                            Value = fields[2]
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
