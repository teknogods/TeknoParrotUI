using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.Updater;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Builds the Troubleshooting report: PC hardware/OS info, TeknoParrot module
/// versions, Linux launch environment (wine/Proton/Gamescope), the last played
/// game's configuration (privacy-filtered), AND the full log of the last run
/// (everything the launch window showed - see
/// <see cref="GameSessionLogArchive"/>) so users can send one complete report
/// when a game does not boot. Ported from the classic WPF Troubleshooting view
/// and made cross-platform (the original used WMI/COM, Windows-only).
/// </summary>
public static class TroubleshootingReport
{
    /// <summary>Same privacy filter as the classic view - these config values are censored.</summary>
    private static readonly string[] FilteredGameConfigValues =
        { "APM3ID", "OnlineId", "PlayerId", "Pass", "PCB ID", "Card ID", "Card ID P1", "Card ID P2" };

    /// <summary>Module files whose versions identify the emulator payload.</summary>
    private static readonly string[] ModuleFiles =
    {
        "TeknoParrot/TeknoParrot.dll",
        "TeknoParrot/TeknoParrot64.dll",
        "TeknoParrot/BudgieLoader.exe",
        "OpenParrotWin32/OpenParrot.dll",
        "OpenParrotWin32/OpenParrotLoader.exe",
        "OpenParrotWin32/OpenParrotKonamiLoader.exe",
        "OpenParrotx64/OpenParrot64.dll",
        "OpenParrotx64/OpenParrotLoader64.exe",
        "ElfLdr2/BudgieLoader.exe",
        "pipehelper.exe",
        "pipehelper32.exe"
    };

    /// <param name="monitorLines">Monitor descriptions (needs the UI TopLevel, so the view supplies them).</param>
    public static string Build(IReadOnlyList<string> monitorLines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TeknoParrot System Information");
        sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        sb.AppendLine();
        sb.AppendLine("=== TeknoParrot Version Info ===");
        sb.AppendLine(GetTpVersions());

        sb.AppendLine("=== System Information ===");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        if (OperatingSystem.IsLinux())
            sb.AppendLine($"Distro: {GetLinuxDistro()}");
        sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture} (process: {RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"CPU: {GetCpuName()} ({Environment.ProcessorCount} logical cores)");
        sb.AppendLine($"RAM: {GetTotalRam()}");
        sb.AppendLine($"GPU: {GetGpuName()}");
        sb.AppendLine("Monitor Resolution(s):");
        if (monitorLines is { Count: > 0 })
            foreach (var line in monitorLines)
                sb.AppendLine($"  {line}");
        else
            sb.AppendLine("  Unknown");
        if (OperatingSystem.IsLinux())
        {
            sb.AppendLine($"Session: {Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown"}" +
                          $" (WAYLAND_DISPLAY={Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "unset"}," +
                          $" DISPLAY={Environment.GetEnvironmentVariable("DISPLAY") ?? "unset"})");
        }
        sb.AppendLine("Network Adapters:");
        sb.AppendLine(GetNetworkAdapters());
        sb.AppendLine("Serial Ports:");
        sb.AppendLine(GetSerialPorts());
        sb.AppendLine("Audio Devices:");
        sb.AppendLine(GetAudioDevices());

        if (OperatingSystem.IsLinux())
        {
            sb.AppendLine();
            sb.AppendLine("=== Linux Launch Environment ===");
            sb.AppendLine(GetLinuxLaunchEnvironment());
        }

        sb.AppendLine();
        sb.AppendLine("=== Last Played Game ===");
        sb.AppendLine(GetLastPlayedGameInfo());

        sb.AppendLine();
        sb.AppendLine("=== Last Run Log (console + launch window) ===");
        sb.AppendLine(GameSessionLogArchive.GetLastRunLog());

        return sb.ToString();
    }

    private static string GetTpVersions()
    {
        var sb = new StringBuilder();
        var uiVersion = typeof(TroubleshootingReport).Assembly.GetName().Version?.ToString() ?? "unknown";
        sb.AppendLine($"- TeknoParrotUI (Avalonia): {uiVersion}");
        try
        {
            if (File.Exists("../version.txt"))
                sb.AppendLine($"- version.txt: {File.ReadAllText("../version.txt").Trim()}");
            else if (File.Exists("version.txt"))
                sb.AppendLine($"- version.txt: {File.ReadAllText("version.txt").Trim()}");
        }
        catch { /* informational only */ }

        sb.AppendLine("- Modules:");
        foreach (var relative in ModuleFiles)
        {
            try
            {
                if (!File.Exists(relative))
                    continue;
                string version;
                if (IsPipehelperBuild(relative))
                {
                    // pipehelper.exe/pipehelper32.exe are built and shipped by us
                    // alongside the UI (Tools/ProtonPipeHelper), and are always
                    // rebuilt in lockstep with it - they carry no PE version
                    // resource of their own (plain mingw-w64 build, no .rc), so
                    // just report the UI's own version instead of "unreadable"/
                    // "no version resource".
                    version = uiVersion;
                }
                else
                {
                    // FileVersionInfo's cross-platform PE parser doesn't reliably
                    // read version resources from real Windows binaries on Linux
                    // (same issue UpdaterComponent.localVersion works around) -
                    // use the managed PeVersionReader on Linux, FileVersionInfo
                    // (which works fine for real PE files) on Windows.
                    var full = Path.GetFullPath(relative);
                    version = OperatingSystem.IsWindows()
                        ? FileVersionInfo.GetVersionInfo(full).FileVersion
                        : PeVersionReader.ReadProductVersion(full);
                    if (string.IsNullOrWhiteSpace(version))
                        version = "no version resource";
                }
                var stamp = File.GetLastWriteTime(relative).ToString("yyyy-MM-dd HH:mm");
                sb.AppendLine($"    {relative}: {version} (modified {stamp})");
            }
            catch
            {
                sb.AppendLine($"    {relative}: unreadable");
            }
        }
        return sb.ToString();
    }

    private static bool IsPipehelperBuild(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        return name.Equals("pipehelper.exe", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("pipehelper32.exe", StringComparison.OrdinalIgnoreCase);
    }


    private static string GetLinuxDistro()
    {
        try
        {
            foreach (var line in File.ReadAllLines("/etc/os-release"))
            {
                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                    return line.Substring("PRETTY_NAME=".Length).Trim('"');
            }
        }
        catch { /* fall through */ }
        return "Unknown";
    }

    private static string GetCpuName()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name", StringComparison.Ordinal))
                        return line.Split(':', 2)[1].Trim();
                }
            }
            catch { /* fall through */ }
        }
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";
    }

    private static string GetTotalRam()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        var kb = long.Parse(line.Split(':', 2)[1].Trim().Split(' ')[0]);
                        return $"{Math.Round(kb / (1024.0 * 1024.0), 2)} GB";
                    }
                }
            }
            catch { /* fall through */ }
        }
        try
        {
            var bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return $"{Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2)} GB (available to process)";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetGpuName()
    {
        if (!OperatingSystem.IsLinux())
            return "Unknown (collected on Linux only in this build)";
        var lspci = RunBounded("lspci", "", TimeSpan.FromSeconds(5));
        if (lspci == null)
            return "Unknown (lspci unavailable)";
        var gpus = lspci.Split('\n')
            .Where(l => l.Contains("VGA compatible controller") || l.Contains("3D controller") || l.Contains("Display controller"))
            .Select(l => l.Split(": ", 2).Length > 1 ? l.Split(": ", 2)[1].Trim() : l.Trim())
            .ToList();
        return gpus.Count > 0 ? string.Join(", ", gpus) : "Unknown";
    }

    private static string GetNetworkAdapters()
    {
        try
        {
            var result = new StringBuilder();
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var ipv4 = adapter.GetIPProperties().UnicastAddresses
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.Address.ToString())
                    .ToList();
                if (ipv4.Count > 0)
                    result.AppendLine($"  {adapter.Name}: {string.Join(", ", ipv4)}");
            }
            return result.Length > 0 ? result.ToString().TrimEnd('\r', '\n') : "  None";
        }
        catch
        {
            return "  Unknown";
        }
    }

    private static string GetSerialPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            return ports.Length > 0 ? "  " + string.Join(", ", ports.OrderBy(p => p)) : "  None";
        }
        catch
        {
            return "  Unknown";
        }
    }

    private static string GetAudioDevices()
    {
        if (!OperatingSystem.IsLinux())
            return "  Not collected on this build";
        // PipeWire/PulseAudio sinks and sources - honest fallback when pactl
        // is unavailable (the classic COM enumeration is Windows-only).
        var sinks = RunBounded("pactl", "list short sinks", TimeSpan.FromSeconds(5));
        var sources = RunBounded("pactl", "list short sources", TimeSpan.FromSeconds(5));
        if (sinks == null && sources == null)
            return "  Unknown (pactl unavailable)";
        var sb = new StringBuilder();
        sb.AppendLine("  Playback (sinks):");
        AppendPactl(sb, sinks);
        sb.AppendLine("  Recording (sources):");
        AppendPactl(sb, sources);
        return sb.ToString().TrimEnd('\r', '\n');

        static void AppendPactl(StringBuilder sb, string output)
        {
            var lines = (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                sb.AppendLine("    None");
                return;
            }
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                sb.AppendLine(parts.Length >= 2 ? $"    {parts[1]}" : $"    {line.Trim()}");
            }
        }
    }

    private static string GetLinuxLaunchEnvironment()
    {
        var sb = new StringBuilder();
        try
        {
            var wine = Common.Proton.ProtonLauncher.ResolveWineBinary();
            sb.AppendLine($"Wine binary: {wine ?? "not found"}");
            if (wine != null)
            {
                var version = RunBounded(wine, "--version", TimeSpan.FromSeconds(10));
                sb.AppendLine($"Wine version: {version?.Trim() ?? "unknown"}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Wine: error resolving ({ex.Message})");
        }

        try
        {
            var gamescope = Common.Proton.GamescopeLocator.Locate();
            sb.AppendLine(gamescope.IsAvailable
                ? $"Gamescope: {gamescope.ExecutablePath} (version {gamescope.Version})"
                : $"Gamescope: unavailable ({gamescope.Reason})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Gamescope: error probing ({ex.Message})");
        }

        sb.AppendLine($"Fullscreen scaling (global): {Lazydata.ParrotData?.FullscreenScalingMode.ToString() ?? "unknown"}");

        try
        {
            var prefixRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknoParrotUI", "prefixes");
            if (Directory.Exists(prefixRoot))
            {
                sb.AppendLine("Wine prefixes:");
                foreach (var dir in Directory.EnumerateDirectories(prefixRoot, "*", SearchOption.AllDirectories)
                             .Where(d => Directory.Exists(Path.Combine(d, "drive_c"))))
                    sb.AppendLine($"  {dir}");
            }
        }
        catch { /* informational only */ }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static string GetLastPlayedGameInfo()
    {
        try
        {
            // Prefer the recorded run (always accurate); fall back to the
            // persisted LastPlayed setting.
            var name = GameSessionLogArchive.HasRun
                ? GameSessionLogArchive.LastRunProfileName
                : Lazydata.ParrotData?.LastPlayed;
            if (string.IsNullOrWhiteSpace(name))
                return "  None";

            var result = new StringBuilder();
            result.AppendLine($"  Last Played: {name}");

            var profile =
                GameProfileLoader.UserProfiles?.FirstOrDefault(p => p.GameNameInternal == name) ??
                GameProfileLoader.GameProfiles?.FirstOrDefault(p => p.GameNameInternal == name) ??
                GameProfileLoader.GameProfiles?.FirstOrDefault(p => p.ProfileName == name);

            if (profile == null)
                return result.AppendLine("  Profile not found in loaded games").ToString().TrimEnd('\r', '\n');

            result.AppendLine($"  Profile: {profile.ProfileName}");
            result.AppendLine($"  Game Path: {profile.GamePath ?? "Not set"}");
            if (profile.HasTwoExecutables)
                result.AppendLine($"  Second Game Path: {profile.GamePath2 ?? "Not set"}");
            result.AppendLine($"  Emulator: {profile.EmulatorType}");
            if (OperatingSystem.IsLinux())
            {
                result.AppendLine($"  Fullscreen scaling (game): {profile.FullscreenScalingMode}");
                result.AppendLine($"  Gamescope window compatibility: {profile.GamescopeGameWindowCompatibility}");
                if (!string.IsNullOrEmpty(profile.ProtonVersion))
                    result.AppendLine($"  Proton/Wine pin: {profile.ProtonVersion}");
            }
            if (profile.ConfigValues is { Count: > 0 })
            {
                result.AppendLine("  Config Values:");
                foreach (var config in profile.ConfigValues)
                {
                    var filtered = FilteredGameConfigValues.Any(f =>
                        string.Equals(config.FieldName, f, StringComparison.OrdinalIgnoreCase));
                    var value = config.FieldValue;
                    if (config.FieldType == FieldType.Bool)
                        value = value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "True" : "False";
                    if (filtered)
                        value = "CENSORED FOR PRIVACY";
                    result.AppendLine($"    - [{config.CategoryName}] {config.FieldName}: {value}");
                }
            }
            return result.ToString().TrimEnd('\r', '\n');
        }
        catch (Exception ex)
        {
            return $"  Error retrieving last played game info: {ex.Message}";
        }
    }

    /// <summary>Runs a command with a bounded timeout and captured stdout; null on any failure.</summary>
    private static string RunBounded(string fileName, string arguments, TimeSpan timeout)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (proc == null)
                return null;
            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { proc.Kill(); } catch { /* ignored */ }
                return null;
            }
            return output;
        }
        catch
        {
            return null;
        }
    }
}
