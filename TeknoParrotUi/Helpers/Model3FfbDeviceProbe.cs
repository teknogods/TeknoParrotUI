using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class Model3FfbDeviceProbe
    {
        private const int ProbeTimeoutMilliseconds = 5000;

        // TeknoModel3 accepts model3haptic's wheel:<n> / gamepad:<n> tokens
        // directly (zero-based discovery positions, unlike TeknoModel2 which
        // needs them translated), plus its persistent hex name/path selectors.
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

            // model3haptic.exe is a small unprotected helper that never loads the
            // emulator; there is deliberately no fallback to TeknoModel3.exe.
            var model3Directory = Path.Combine(Directory.GetCurrentDirectory(), "TeknoModel3");
            var probePath = Path.Combine(model3Directory, "model3haptic.exe");
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
                        WorkingDirectory = model3Directory,
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
