using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class VUnitFfbDeviceProbe
    {
        private const int ProbeTimeoutMilliseconds = 5000;

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

            var vunitDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TeknoVUnit");
            var probePath = Path.Combine(vunitDirectory, "VUnitHaptic.exe");
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
                        WorkingDirectory = vunitDirectory,
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
                // Older TeknoVUnit packages may not include the optional haptic probe.
            }

            return devices;
        }
    }
}
