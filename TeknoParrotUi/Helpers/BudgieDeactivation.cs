using System;
using System.Collections;
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
            var localActivation = ReadLocalActivation();
            var output = new List<string>();
            var outputLock = new object();
            int exitCode;
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

                exitCode = process.ExitCode;
            }

            lock (outputLock)
            {
                CompleteDeactivation(exitCode, output, localActivation, ReadLocalActivation, DeleteLocalActivation);
            }
        }

        // The result handling can be tested without starting a loader or touching the registry.
        internal static void CompleteDeactivation(int exitCode, IEnumerable<string> output, object localActivation,
            Func<object> readLocalActivation, Action deleteLocalActivation)
        {
            if (exitCode != 0)
                throw new InvalidOperationException(string.Format(Properties.Resources.DeactivationProcessFailed, exitCode));
            if (!TryParseResultCode(output, out var resultCode))
                throw new InvalidOperationException(Properties.Resources.DeactivationNoResult);
            if (resultCode != 0)
                throw new BudgieDeactivationException(resultCode, FailureMessage(resultCode), localActivation);

            RemoveLocalActivation(localActivation, readLocalActivation, deleteLocalActivation);
        }

        internal static object ReadLocalActivation()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                return key?.GetValue(RegistryValueName);
        }

        internal static void DeleteLocalActivation()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true))
                key?.DeleteValue(RegistryValueName, throwOnMissingValue: false);
        }

        internal static bool IsSameActivation(object expected, object current)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(expected, current);
        }

        internal static void RemoveLocalActivation(object expected, Func<object> read, Action delete)
        {
            var current = read();
            if (current == null)
                return;
            // A different key may have been saved while the loader or confirmation was open.
            if (expected == null || !IsSameActivation(expected, current))
                throw new InvalidOperationException(Properties.Resources.LocalActivationChanged);

            delete();
            if (read() != null)
                throw new InvalidOperationException(Properties.Resources.LocalActivationRemovalFailed);
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
                uint parsed;
                if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed) &&
                    parsed <= int.MaxValue && (resultCode == -1 || resultCode == (int)parsed))
                {
                    resultCode = (int)parsed;
                    continue;
                }
                // Ambiguous or malformed results must never clear the saved key.
                resultCode = -1;
                return false;
            }
            return resultCode != -1;
        }

        private static string FailureMessage(int resultCode)
        {
            switch (resultCode)
            {
                case 2:
                    return Properties.Resources.DeactivationNoConnection;
                case 3:
                    return Properties.Resources.DeactivationBadReply;
                case 8:
                    return Properties.Resources.DeactivationUnknownSerial;
                case 10:
                    return Properties.Resources.DeactivationCooldown;
                default:
                    return string.Format(Properties.Resources.DeactivationRejected, resultCode);
            }
        }
    }

    internal sealed class BudgieDeactivationException : InvalidOperationException
    {
        private readonly object _localActivation;
        internal int ResultCode { get; }

        // VMProtect: banned, bad activation code, unknown serial, expired activation code.
        // Connection errors, corrupt responses and the server cooldown are not recoverable here.
        internal bool CanRemoveLocalActivation => _localActivation != null &&
            (ResultCode == 4 || ResultCode == 6 || ResultCode == 8 || ResultCode == 9);

        internal BudgieDeactivationException(int resultCode, string message, object localActivation) : base(message)
        {
            ResultCode = resultCode;
            _localActivation = localActivation is Array array ? array.Clone() : localActivation;
        }

        internal bool TryRemoveLocalActivation(Func<bool> confirm)
        {
            return TryRemoveLocalActivation(confirm, BudgieDeactivation.ReadLocalActivation,
                BudgieDeactivation.DeleteLocalActivation);
        }

        internal bool TryRemoveLocalActivation(Func<bool> confirm, Func<object> read, Action delete)
        {
            if (!CanRemoveLocalActivation)
                return false;
            var current = read();
            if (current == null)
                return false;
            if (!BudgieDeactivation.IsSameActivation(_localActivation, current))
                throw new InvalidOperationException(Properties.Resources.LocalActivationChanged);
            if (!confirm())
                return false;

            // This only forgets the old local key. It does not release a server activation.
            BudgieDeactivation.RemoveLocalActivation(_localActivation, read, delete);
            return true;
        }
    }
}
