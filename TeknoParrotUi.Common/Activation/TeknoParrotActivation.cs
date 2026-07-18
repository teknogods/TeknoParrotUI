using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.Activation
{
    public sealed record TeknoParrotActivationStatus(bool IsActivated, string Message);

    public sealed record TeknoParrotActivationResult(
        bool Success,
        bool IsActivated,
        string Message,
        IReadOnlyList<string> Output);

    /// <summary>
    /// Cross-platform owner of the TeknoParrot subscription activation. The
    /// generated value consumed by the game runtime is not the serial entered
    /// by the user. BudgieLoader remains the only component allowed to turn a
    /// serial into that activation data.
    /// </summary>
    public static class TeknoParrotActivation
    {
        internal const string RegistryKeyPath = @"SOFTWARE\TeknoGods\TeknoParrot";
        internal const string RegistryValueName = "PatreonSerialKey";
        private const string WindowsRegistryKey = @"HKCU\SOFTWARE\TeknoGods\TeknoParrot";
        private const string ExportedRegistryKey =
            @"[HKEY_CURRENT_USER\SOFTWARE\TeknoGods\TeknoParrot]";
        private const string BudgieLoaderRelativePath = "TeknoParrot/BudgieLoader.exe";
        private const string ActivationProfileName = "_teknoparrot-activation";
        private const string SeedFileName = "teknoparrot-license.reg";
        private const string RevocationFileName = "teknoparrot-license.revoked";
        private const string SeedMarkerFileName = ".tpui-license-seed";
        private static readonly object BackendSync = new();
        private static Func<CancellationToken, Task<TeknoParrotActivationStatus>> _platformStatus;
        private static Func<string, CancellationToken, Task<TeknoParrotActivationResult>> _platformActivate;
        private static Func<CancellationToken, Task<TeknoParrotActivationResult>> _platformDeactivate;
        private static Func<bool> _platformCachedStatus;

        public static string SeedPath => Path.Combine(
            WinePrefixManager.DefaultDataRoot, "activation", SeedFileName);

        private static string RevocationPath => Path.Combine(
            WinePrefixManager.DefaultDataRoot, "activation", RevocationFileName);

        /// <summary>
        /// Android registers its signed Winlator-backed implementation here so
        /// the shared UI never references Android framework assemblies.
        /// </summary>
        public static void RegisterPlatformBackend(
            Func<CancellationToken, Task<TeknoParrotActivationStatus>> status,
            Func<string, CancellationToken, Task<TeknoParrotActivationResult>> activate,
            Func<CancellationToken, Task<TeknoParrotActivationResult>> deactivate,
            Func<bool> cachedStatus = null)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(activate);
            ArgumentNullException.ThrowIfNull(deactivate);
            lock (BackendSync)
            {
                _platformStatus = status;
                _platformActivate = activate;
                _platformDeactivate = deactivate;
                _platformCachedStatus = cachedStatus;
            }
        }

        public static bool IsActivatedLocally()
        {
            if (OperatingSystem.IsWindows())
                return WindowsRegistryValueExists();
            if (OperatingSystem.IsLinux())
                return IsValidSeedFile(SeedPath);
            if (OperatingSystem.IsAndroid())
            {
                lock (BackendSync)
                    return _platformCachedStatus?.Invoke() == true;
            }
            return false;
        }

        public static Task<TeknoParrotActivationStatus> GetStatusAsync(
            CancellationToken cancellationToken = default)
        {
            if (OperatingSystem.IsAndroid())
            {
                lock (BackendSync)
                    return (_platformStatus ?? MissingAndroidStatus)(cancellationToken);
            }

            if (OperatingSystem.IsWindows())
            {
                var active = WindowsRegistryValueExists();
                return Task.FromResult(new TeknoParrotActivationStatus(
                    active,
                    active ? "Subscription is activated on this machine." :
                        "No subscription activation is installed on this machine."));
            }

            if (OperatingSystem.IsLinux())
            {
                var active = IsValidSeedFile(SeedPath);
                return Task.FromResult(new TeknoParrotActivationStatus(
                    active,
                    active ? "Subscription activation is ready for Wine game prefixes." :
                        "No Wine subscription activation has been registered yet."));
            }

            return Task.FromResult(new TeknoParrotActivationStatus(
                false, "Subscription activation is not supported on this platform."));
        }

        public static Task<TeknoParrotActivationResult> ActivateAsync(
            string serial,
            CancellationToken cancellationToken = default)
        {
            serial = serial?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serial) || serial.Length > 512 ||
                serial.Any(character => !char.IsAsciiLetterOrDigit(character) &&
                    character is not '.' and not '_' and not '-'))
                return Task.FromResult(Failure(false, "The subscription serial is invalid."));

            if (OperatingSystem.IsAndroid())
            {
                lock (BackendSync)
                    return (_platformActivate ?? MissingAndroidActivate)(serial, cancellationToken);
            }

            if (OperatingSystem.IsWindows())
                return RunWindowsBudgieAsync("-register", serial, true, cancellationToken);
            if (OperatingSystem.IsLinux())
                return RunLinuxBudgieAsync("-register", serial, true, cancellationToken);
            return Task.FromResult(Failure(false,
                "Subscription activation is not supported on this platform."));
        }

        public static Task<TeknoParrotActivationResult> DeactivateAsync(
            CancellationToken cancellationToken = default)
        {
            if (OperatingSystem.IsAndroid())
            {
                lock (BackendSync)
                    return (_platformDeactivate ?? MissingAndroidDeactivate)(cancellationToken);
            }

            if (OperatingSystem.IsWindows())
                return RunWindowsBudgieAsync("-deactivate", null, false, cancellationToken);
            if (OperatingSystem.IsLinux())
                return RunLinuxBudgieAsync("-deactivate", null, false, cancellationToken);
            return Task.FromResult(Failure(false,
                "Subscription deactivation is not supported on this platform."));
        }

        /// <summary>
        /// Imports the private central activation export into the exact prefix
        /// about to host a game. This preserves TeknoParrot.dll unchanged and
        /// works for shared and isolated Wine/Proton environments.
        /// </summary>
        public static void SynchronizeToGamePrefix(
            string wine,
            ResolvedWineEnvironment environment,
            Action<string> log = null)
        {
            if (!OperatingSystem.IsLinux())
                return;

            var marker = Path.Combine(environment.ActualPrefixPath, SeedMarkerFileName);
            if (File.Exists(RevocationPath))
            {
                const string revokedMarker = "revoked";
                if (File.Exists(marker) &&
                    string.Equals(File.ReadAllText(marker).Trim(), revokedMarker,
                        StringComparison.Ordinal) &&
                    !WineRegistryValueExists(wine, environment))
                    return;

                _ = RunWineCommandAsync(
                        wine,
                        environment,
                        "reg",
                        new[] { "delete", WindowsRegistryKey, "/v", RegistryValueName, "/f" },
                        null,
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (WineRegistryValueExists(wine, environment))
                    throw new InvalidOperationException(
                        "The deactivated TeknoParrot subscription could not be removed from this Wine prefix.");
                Directory.CreateDirectory(environment.ActualPrefixPath);
                File.WriteAllText(marker, revokedMarker + Environment.NewLine);
                log?.Invoke("[Activation] Removed deactivated subscription data from the game Wine prefix.");
                return;
            }

            if (!IsValidSeedFile(SeedPath))
                return;

            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SeedPath)));
            if (File.Exists(marker) && string.Equals(File.ReadAllText(marker).Trim(), hash,
                    StringComparison.Ordinal) &&
                WineRegistryValueExists(wine, environment))
                return;

            var result = RunWineCommandAsync(
                    wine,
                    environment,
                    "reg",
                    new[] { "import", ProtonHelper.ToWinePath(SeedPath) },
                    null,
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            if (result.ExitCode != 0 || !WineRegistryValueExists(wine, environment))
                throw new InvalidOperationException(
                    "The TeknoParrot subscription activation could not be installed in this Wine prefix.");

            Directory.CreateDirectory(environment.ActualPrefixPath);
            File.WriteAllText(marker, hash + Environment.NewLine);
            log?.Invoke("[Activation] Subscription activation synchronized to the game Wine prefix.");
        }

        private static async Task<TeknoParrotActivationResult> RunWindowsBudgieAsync(
            string operation,
            string secret,
            bool expectedActive,
            CancellationToken cancellationToken)
        {
            var loader = Path.GetFullPath(BudgieLoaderRelativePath);
            if (!File.Exists(loader))
                return Failure(IsActivatedLocally(),
                    "TeknoParrot core (BudgieLoader.exe) is not installed. Run Updates first.");

            var info = CreateDirectProcess(loader, Path.GetDirectoryName(loader));
            info.ArgumentList.Add(operation);
            if (secret != null)
                info.ArgumentList.Add(secret);
            var process = await RunProcessAsync(info, secret, cancellationToken).ConfigureAwait(false);
            var active = WindowsRegistryValueExists();
            return BuildOperationResult(process, active, expectedActive);
        }

        private static async Task<TeknoParrotActivationResult> RunLinuxBudgieAsync(
            string operation,
            string secret,
            bool expectedActive,
            CancellationToken cancellationToken)
        {
            var loader = Path.GetFullPath(BudgieLoaderRelativePath);
            if (!File.Exists(loader))
                return Failure(IsActivatedLocally(),
                    "TeknoParrot core (BudgieLoader.exe) is not installed. Run Updates first.");

            var wine = ProtonLauncher.ResolveWineBinary();
            if (wine == null)
                return Failure(IsActivatedLocally(),
                    "No Wine or Proton runtime is installed for BudgieLoader.exe.");

            var runnerKind = ProtonLauncher.FindProtonScript(wine) == null
                ? WineRunnerKind.PlainWine
                : WineRunnerKind.Proton;
            var environment = WinePrefixManager.Resolve(
                ActivationProfileName,
                WinePrefixMode.Shared,
                WinePrefixCompatibilityGroup.Standard,
                runnerKind);
            WinePrefixManager.EnsureDirectories(environment);
            if (runnerKind == WineRunnerKind.PlainWine)
                ProtonLauncher.EnsurePlainWinePrefixReady(wine, environment);

            string executable;
            List<string> arguments;
            if (secret != null)
            {
                // Keep the user-entered serial out of the Linux process
                // command line. Wine's cmd.exe expands it from the private
                // child environment immediately before launching Budgie.
                executable = "cmd";
                arguments = new List<string>
                {
                    "/c",
                    ProtonHelper.ToWinePath(loader),
                    operation,
                    "%TPUI_ACTIVATION_SERIAL%"
                };
            }
            else
            {
                executable = loader;
                arguments = new List<string> { operation };
            }
            var process = await RunWineCommandAsync(
                wine, environment, executable, arguments, secret, cancellationToken).ConfigureAwait(false);
            var active = WineRegistryValueExists(wine, environment);
            if (process.ExitCode != 0 || active != expectedActive)
                return BuildOperationResult(process, active, expectedActive);

            if (expectedActive)
            {
                var export = await ExportWineActivationAsync(
                    wine, environment, cancellationToken).ConfigureAwait(false);
                if (!export.Success)
                    return export;
            }
            else
            {
                // This runs only after the user explicitly presses Deactivate.
                if (File.Exists(SeedPath))
                    File.Delete(SeedPath);
                Directory.CreateDirectory(Path.GetDirectoryName(RevocationPath)!);
                File.WriteAllText(RevocationPath, DateTime.UtcNow.ToString("O") + Environment.NewLine);
            }

            return BuildOperationResult(process, active, expectedActive);
        }

        private static async Task<TeknoParrotActivationResult> ExportWineActivationAsync(
            string wine,
            ResolvedWineEnvironment environment,
            CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(SeedPath)!;
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, SeedFileName + ".new");
            var process = await RunWineCommandAsync(
                wine,
                environment,
                "reg",
                new[] { "export", WindowsRegistryKey, ProtonHelper.ToWinePath(temporary), "/y" },
                null,
                cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || !IsValidSeedFile(temporary))
                return Failure(true,
                    "BudgieLoader activated successfully, but the Wine registry activation could not be saved for game prefixes.",
                    process.Output);

            File.Move(temporary, SeedPath, true);
            if (File.Exists(RevocationPath))
                File.Delete(RevocationPath);
            try
            {
                File.SetUnixFileMode(SeedPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (PlatformNotSupportedException)
            {
                // Linux is the only caller, but keep the storage operation
                // tolerant of unusual filesystems without Unix mode support.
            }
            return new TeknoParrotActivationResult(
                true, true, "Subscription activation saved for Wine game prefixes.", process.Output);
        }

        private static bool WineRegistryValueExists(string wine, ResolvedWineEnvironment environment)
        {
            try
            {
                var result = RunWineCommandAsync(
                        wine,
                        environment,
                        "reg",
                        new[] { "query", WindowsRegistryKey, "/v", RegistryValueName },
                        null,
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<ProcessResult> RunWineCommandAsync(
            string wine,
            ResolvedWineEnvironment environment,
            string executable,
            IEnumerable<string> arguments,
            string secret,
            CancellationToken cancellationToken)
        {
            var protonScript = ProtonLauncher.FindProtonScript(wine);
            ProcessStartInfo info;
            if (protonScript != null)
            {
                info = CreateDirectProcess("python3", Environment.CurrentDirectory);
                info.ArgumentList.Add(protonScript);
                info.ArgumentList.Add("run");
                info.ArgumentList.Add(executable);
                info.Environment["STEAM_COMPAT_DATA_PATH"] = environment.SteamCompatDataPath;
                info.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] =
                    ProtonLauncher.ResolveSteamClientPath();
            }
            else
            {
                info = CreateDirectProcess(wine, Environment.CurrentDirectory);
                info.ArgumentList.Add(executable);
                info.Environment["WINEPREFIX"] = environment.WinePrefixPath;
                info.Environment["WINEDEBUG"] = "-all";
            }

            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            if (secret != null)
                info.Environment["TPUI_ACTIVATION_SERIAL"] = secret;
            return await RunProcessAsync(info, secret, cancellationToken).ConfigureAwait(false);
        }

        private static ProcessStartInfo CreateDirectProcess(string executable, string workingDirectory) => new()
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        private static async Task<ProcessResult> RunProcessAsync(
            ProcessStartInfo info,
            string secret,
            CancellationToken cancellationToken)
        {
            using var process = new Process { StartInfo = info };
            if (!process.Start())
                throw new InvalidOperationException("The activation process could not be started.");

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var combined = (await stdout.ConfigureAwait(false)) + Environment.NewLine +
                           (await stderr.ConfigureAwait(false));
            var output = combined.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => Redact(line.Trim(), secret))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(100)
                .ToArray();
            return new ProcessResult(process.ExitCode, output);
        }

        private static string Redact(string value, string secret) =>
            string.IsNullOrEmpty(secret)
                ? value
                : value.Replace(secret, "[serial redacted]", StringComparison.OrdinalIgnoreCase);

        private static TeknoParrotActivationResult BuildOperationResult(
            ProcessResult process,
            bool active,
            bool expectedActive)
        {
            var success = process.ExitCode == 0 && active == expectedActive;
            var message = success
                ? active
                    ? "Subscription activation completed successfully."
                    : "Subscription activation was deactivated successfully."
                : process.ExitCode != 0
                    ? $"BudgieLoader failed with exit code {process.ExitCode}."
                    : expectedActive
                        ? "BudgieLoader exited without installing a subscription activation."
                        : "BudgieLoader exited without removing the subscription activation.";
            return new TeknoParrotActivationResult(success, active, message, process.Output);
        }

        private static bool WindowsRegistryValueExists()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                return key?.GetValueNames().Contains(RegistryValueName, StringComparer.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsValidSeedFile(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length is <= 0 or > 64 * 1024)
                    return false;
                return IsValidSeedExportText(File.ReadAllText(path));
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsValidSeedExportText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64 * 1024 ||
                !text.Contains("Windows Registry Editor", StringComparison.Ordinal) ||
                !text.Contains(ExportedRegistryKey, StringComparison.OrdinalIgnoreCase))
                return false;
            var valuePrefix = '"' + RegistryValueName + "\"=";
            var start = text.IndexOf(valuePrefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return false;
            start += valuePrefix.Length;
            return text.AsSpan(start).StartsWith("hex:", StringComparison.OrdinalIgnoreCase) ||
                   (start < text.Length && text[start] == '"');
        }

        private static TeknoParrotActivationResult Failure(
            bool active,
            string message,
            IReadOnlyList<string> output = null) =>
            new(false, active, message, output ?? Array.Empty<string>());

        private static Task<TeknoParrotActivationStatus> MissingAndroidStatus(
            CancellationToken cancellationToken) => Task.FromResult(new TeknoParrotActivationStatus(
            false, "The Android Winlator activation backend is not registered."));

        private static Task<TeknoParrotActivationResult> MissingAndroidActivate(
            string serial,
            CancellationToken cancellationToken) => Task.FromResult(Failure(
            false, "The Android Winlator activation backend is not registered."));

        private static Task<TeknoParrotActivationResult> MissingAndroidDeactivate(
            CancellationToken cancellationToken) => Task.FromResult(Failure(
            false, "The Android Winlator activation backend is not registered."));

        private sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output);
    }
}
