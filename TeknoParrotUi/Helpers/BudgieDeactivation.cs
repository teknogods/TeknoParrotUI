using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;

namespace TeknoParrotUi.Helpers
{
    internal static class BudgieDeactivation
    {
        private const string RegistryKeyPath = @"SOFTWARE\TeknoGods\TeknoParrot";
        private const string RegistryValueName = "PatreonSerialKey";
        private const string ResultPrefix = "Deactivation exited with code:";

        public static void Deactivate(string loaderPath, Action<string> outputCallback)
        {
            var output = new List<string>();
            var outputLock = new object();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = loaderPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = "-deactivate"
                };
                DataReceivedEventHandler capture = (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                        return;
                    lock (outputLock)
                        output.Add(args.Data);
                    outputCallback?.Invoke(args.Data);
                };
                process.OutputDataReceived += capture;
                process.ErrorDataReceived += capture;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"BudgieLoader failed with exit code {process.ExitCode}. The local activation was retained.");
            }

            int resultCode;
            lock (outputLock)
            {
                if (!TryParseResultCode(output, out resultCode))
                    throw new InvalidOperationException(
                        "BudgieLoader did not return a deactivation result. The local activation was retained.");
            }
            if (resultCode != 0)
                throw new InvalidOperationException(FailureMessage(resultCode));

            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
                key?.DeleteValue(RegistryValueName, throwOnMissingValue: false);

            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key?.GetValue(RegistryValueName) != null)
                    throw new InvalidOperationException(
                        "The activation server accepted deactivation, but the local activation could not be removed.");
            }
        }

        internal static bool TryParseResultCode(IEnumerable<string> output, out int resultCode)
        {
            resultCode = -1;
            foreach (var line in output)
            {
                var start = line.IndexOf(ResultPrefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    continue;
                var value = line.Substring(start + ResultPrefix.Length).Trim();
                var separator = value.IndexOfAny(new[] { ' ', '\t' });
                if (separator >= 0)
                    value = value.Substring(0, separator);
                uint parsed;
                if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed) &&
                    parsed <= int.MaxValue)
                {
                    resultCode = (int)parsed;
                    return true;
                }
            }
            return false;
        }

        private static string FailureMessage(int resultCode)
        {
            switch (resultCode)
            {
                case 2:
                    return "The activation server could not be reached. The local activation was retained.";
                case 3:
                    return "The activation server returned an invalid response. The local activation was retained.";
                case 8:
                    return "The activation server did not recognize this activation. The local activation was retained.";
                case 10:
                    return "The activation server rejected deactivation. This serial may still be in its 30-day cooldown.";
                default:
                    return $"The activation server rejected deactivation with code {resultCode}. The local activation was retained.";
            }
        }
    }
}
