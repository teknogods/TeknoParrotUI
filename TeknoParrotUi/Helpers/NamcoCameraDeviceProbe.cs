using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    internal static class NamcoCameraDeviceProbe
    {
        internal static string GetLaunchSelection(string value)
        {
            value = value?.Trim() ?? "off";
            return value == "auto" || Regex.IsMatch(value, @"\Awebcam:[0-9a-f]{16}\z")
                ? value : "off";
        }

        internal static List<DynamicDropdownOption> GetDevices()
        {
            var devices = new List<DynamicDropdownOption>
            {
                new DynamicDropdownOption { DisplayName = "Webcam off", Value = "off" },
                new DynamicDropdownOption { DisplayName = "Default webcam", Value = "auto" }
            };
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "TeknoS23");
            var helper = Path.Combine(directory, "S23cam.exe");
            if (!File.Exists(helper)) return devices;
            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo(helper, "--list")
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
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        return devices;
                    }
                    var text = output.GetAwaiter().GetResult();
                    error.GetAwaiter().GetResult();
                    if (process.ExitCode != 0) return devices;
                    var tokens = new HashSet<string>(StringComparer.Ordinal) { "off", "auto" };
                    var labels = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var columns = line.Split(new[] { '\t' }, 2);
                        if (columns.Length != 2 || !Regex.IsMatch(columns[0], @"\Awebcam:[0-9a-f]{16}\z") ||
                            string.IsNullOrWhiteSpace(columns[1]) || !tokens.Add(columns[0])) continue;
                        var label = columns[1].Trim();
                        labels.TryGetValue(label, out var duplicates);
                        labels[label] = duplicates + 1;
                        if (duplicates != 0) label += $" ({duplicates + 1})";
                        devices.Add(new DynamicDropdownOption { DisplayName = label, Value = columns[0] });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception || ex is NotSupportedException)
            {
                // An older/missing helper leaves webcam selection optional.
            }
            return devices;
        }
    }
}
