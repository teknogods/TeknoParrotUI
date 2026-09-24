using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace TeknoParrotUi.Common.Activation
{
    internal static class WindowsBudgieDeactivation
    {
        private const string ResultPrefix = "Deactivation exited with code:";

        internal static object ReadLocalActivation()
        {
            using var key = Registry.CurrentUser.OpenSubKey(TeknoParrotActivation.RegistryKeyPath);
            var value = key?.GetValue(TeknoParrotActivation.RegistryValueName);
            return value is Array array ? array.Clone() : value;
        }

        internal static void DeleteLocalActivation()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                TeknoParrotActivation.RegistryKeyPath, writable: true);
            key?.DeleteValue(TeknoParrotActivation.RegistryValueName, throwOnMissingValue: false);
        }

        internal static bool IsSameActivation(object expected, object current) =>
            StructuralComparisons.StructuralEqualityComparer.Equals(expected, current);

        internal static void RemoveLocalActivation(object expected, Func<object> read, Action delete)
        {
            var current = read();
            if (current == null)
                return;
            if (expected == null || !IsSameActivation(expected, current))
                throw new InvalidOperationException("The local activation changed during deactivation.");
            delete();
            if (read() != null)
                throw new InvalidOperationException("The local activation could not be removed.");
        }

        internal static bool TryParseResultCode(IEnumerable<string> output, out int resultCode)
        {
            resultCode = -1;
            if (output == null)
                return false;
            foreach (var line in output)
            {
                if (line == null)
                    continue;
                var start = line.IndexOf(ResultPrefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    continue;
                var value = line.Substring(start + ResultPrefix.Length).Trim();
                var separator = value.IndexOfAny(new[] { ' ', '\t' });
                if (separator >= 0)
                    value = value.Substring(0, separator);
                if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed <= int.MaxValue && (resultCode == -1 || resultCode == (int)parsed))
                {
                    resultCode = (int)parsed;
                    continue;
                }
                resultCode = -1;
                return false;
            }
            return resultCode != -1;
        }

        internal static bool CanForgetInvalidActivation(int resultCode) =>
            resultCode is 4 or 6 or 8 or 9;

        internal static string FailureMessage(int resultCode) => resultCode switch
        {
            2 => "The deactivation server could not be reached.",
            3 => "The deactivation server returned an invalid reply.",
            8 => "The activation serial is unknown to the deactivation server.",
            10 => "The deactivation cooldown is active.",
            _ => $"Deactivation was rejected (code {resultCode})."
        };
    }
}
