using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    public sealed class MoonlightCommandResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }

        public string GetBestError(string fallback)
        {
            if (!string.IsNullOrWhiteSpace(StandardError))
                return StandardError.Trim();

            if (!string.IsNullOrWhiteSpace(StandardOutput))
                return StandardOutput.Trim();

            return fallback;
        }
    }

    /// <summary>
    /// Controls the separately-downloaded Moonlight portable located at:
    ///     &lt;TeknoParrot root&gt;\Moonlight\Moonlight.exe
    ///
    /// Moonlight is not bundled into the TeknoParrot portable itself.
    /// </summary>
    public static class MoonlightManager
    {
        public static string MoonlightDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Moonlight");

        public static string MoonlightExecutablePath =>
            Path.Combine(MoonlightDirectory, "Moonlight.exe");

        public static bool IsInstalled()
        {
            return File.Exists(MoonlightExecutablePath);
        }

        public static void Open()
        {
            EnsureInstalled();

            Process.Start(new ProcessStartInfo
            {
                FileName = MoonlightExecutablePath,
                WorkingDirectory = MoonlightDirectory,
                UseShellExecute = false
            });
        }

        public static async Task<MoonlightCommandResult> PairAsync(string host, string pin)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));

            if (string.IsNullOrWhiteSpace(pin))
                throw new ArgumentException("PIN is required.", nameof(pin));

            EnsureInstalled();

            // Moonlight's pair command can remain alive after Sunshine has already
            // accepted the PIN. Launch it separately, then verify pairing by polling
            // the host's app list. This prevents a successful pair from being reported
            // as a timeout simply because the CLI process did not exit.
            var startInfo = new ProcessStartInfo
            {
                FileName = MoonlightExecutablePath,
                Arguments = $"pair {Quote(host)} --pin {Quote(pin)}",
                WorkingDirectory = MoonlightDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // Custom Moonlight build: use the normal pair command/backend but skip
            // CliPair.qml so TeknoParrot owns the pairing UI.
            startInfo.EnvironmentVariables["TEKNOPARROT_HEADLESS_PAIR"] = "1";

            using (var pairProcess = Process.Start(startInfo))
            {
                if (pairProcess == null)
                    throw new InvalidOperationException("Moonlight pairing could not be started.");

                var deadline = DateTime.UtcNow.AddSeconds(60);
                MoonlightCommandResult lastCheck = null;

                while (DateTime.UtcNow < deadline)
                {
                    if (pairProcess.HasExited)
                    {
                        if (pairProcess.ExitCode == 0)
                        {
                            return new MoonlightCommandResult
                            {
                                ExitCode = 0,
                                StandardOutput = "Pairing completed.",
                                StandardError = string.Empty
                            };
                        }
                    }

                    try
                    {
                        lastCheck = await RunCommandAsync(
                            $"list {Quote(host)}",
                            TimeSpan.FromSeconds(5)
                        );

                        if (lastCheck.ExitCode == 0)
                        {
                            try
                            {
                                if (!pairProcess.HasExited)
                                    pairProcess.Kill();
                            }
                            catch
                            {
                                // Pairing is already verified; cleanup is best effort.
                            }

                            return new MoonlightCommandResult
                            {
                                ExitCode = 0,
                                StandardOutput = "Pairing completed.",
                                StandardError = string.Empty
                            };
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Host may still be waiting for the PIN to be entered.
                    }
                    catch
                    {
                        // Expected while the host is not paired yet.
                    }

                    await Task.Delay(500);
                }

                try
                {
                    if (!pairProcess.HasExited)
                        pairProcess.Kill();
                }
                catch
                {
                    // Best effort cleanup only.
                }

                return new MoonlightCommandResult
                {
                    ExitCode = 1,
                    StandardOutput = lastCheck?.StandardOutput ?? string.Empty,
                    StandardError = string.IsNullOrWhiteSpace(lastCheck?.StandardError)
                        ? "Pairing was not confirmed within 60 seconds."
                        : lastCheck.StandardError
                };
            }
        }

        public static async Task<IReadOnlyList<string>> ListAppsAsync(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));

            var result = await RunCommandAsync(
                $"list {Quote(host)}",
                TimeSpan.FromSeconds(45)
            );

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    result.GetBestError("Moonlight failed to retrieve the application list.")
                );
            }

            // Moonlight's normal runtime logging is written to stderr. The CLI list
            // command writes application names to stdout, one per line.
            var apps = (result.StandardOutput ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return apps;
        }

        public static Process StartStream(string host, string appName)
        {
            EnsureInstalled();

            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));

            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("Application name is required.", nameof(appName));

            var startInfo = new ProcessStartInfo
            {
                FileName = MoonlightExecutablePath,
                Arguments = $"stream {Quote(host)} {Quote(appName)}",
                WorkingDirectory = MoonlightDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo);

            if (process == null)
                throw new InvalidOperationException("Moonlight could not be started.");

            return process;
        }

        public static Task<MoonlightCommandResult> QuitStreamAsync(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));

            return RunCommandAsync(
                $"quit {Quote(host)}",
                TimeSpan.FromSeconds(30)
            );
        }

        public static void StopAll()
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName("Moonlight");
            }
            catch
            {
                return;
            }

            foreach (var process in processes)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    var processPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(processPath))
                        continue;

                    if (string.Equals(
                        Path.GetFullPath(processPath),
                        Path.GetFullPath(MoonlightExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill();
                    }
                }
                catch
                {
                    // Do not touch Moonlight processes we cannot safely identify
                    // as the TeknoParrot-root portable.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static async Task<MoonlightCommandResult> RunCommandAsync(
            string arguments,
            TimeSpan timeout)
        {
            EnsureInstalled();

            var startInfo = new ProcessStartInfo
            {
                FileName = MoonlightExecutablePath,
                Arguments = arguments,
                WorkingDirectory = MoonlightDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stdout.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stderr.AppendLine(e.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException("Moonlight could not be started.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var exited = await Task.Run(
                    () => process.WaitForExit((int)timeout.TotalMilliseconds)
                );

                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                    }

                    throw new TimeoutException(
                        $"Moonlight did not finish within {timeout.TotalSeconds:0} seconds."
                    );
                }

                // Drain asynchronous stdout/stderr handlers before returning.
                process.WaitForExit();

                return new MoonlightCommandResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString()
                };
            }
        }

        private static void EnsureInstalled()
        {
            if (!IsInstalled())
            {
                throw new FileNotFoundException(
                    "Moonlight could not be found. Download the Moonlight portable and copy its folder into the TeknoParrot root as Moonlight.",
                    MoonlightExecutablePath
                );
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
