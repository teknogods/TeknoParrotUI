using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoViperVegasLauncher
    {
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;
        private const int ErrorNotSupported = 50;
        private const int ErrorInvalidName = 123;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string fileName);

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            switch (profile.EmulatorType)
            {
                case EmulatorType.TeknoVegas:
                    return BuildTeknoVegas(profile, gameLocation, log);
                case EmulatorType.TeknoViper:
                    return BuildTeknoViper(profile, gameLocation, log);
                default:
                    throw new InvalidOperationException($"{profile.EmulatorType} is not a Viper/Vegas emulator");
            }
        }

        private static ProcessStartInfo BuildTeknoVegas(
            GameProfile profile,
            string gameLocation,
            Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue
                ?? fallback;

            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            string Quote(string value) =>
                "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoVegas");
            var executable = Path.Combine(workDir, "TeknoVegas.exe");
            var romPath = string.IsNullOrWhiteSpace(gameLocation)
                ? profile.GamePath
                : gameLocation;
            var gameId = profile.ProfileName;
            var diskId = gameId switch
            {
                "nbagold" => "nbanfl3",
                "roadburn" => "road burners v1.04",
                _ => gameId
            };

            string ResolveUiPath(string path, string fallback)
            {
                var value = string.IsNullOrWhiteSpace(path) ? fallback : path;
                return Path.IsPathRooted(value)
                    ? value
                    : Path.GetFullPath(Path.Combine(uiRoot, value));
            }

            var selectedChd = profile.GamePath2;
            var chdRoot = Setting("CHD Root");
            string diskPath;
            if (!string.IsNullOrWhiteSpace(selectedChd))
            {
                diskPath = ResolveUiPath(selectedChd, workDir);
            }
            else if (!string.IsNullOrWhiteSpace(chdRoot))
            {
                chdRoot = ResolveUiPath(chdRoot, workDir);
                diskPath = chdRoot.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)
                    ? chdRoot
                    : Path.Combine(chdRoot, gameId, diskId + ".chd");
            }
            else
            {
                var romDirectory = Path.GetDirectoryName(romPath) ?? workDir;
                diskPath = Path.Combine(romDirectory, gameId, diskId + ".chd");
            }

            var cabinet = Setting("Cabinet Id", "1");
            if (!int.TryParse(cabinet, out var cabinetId) || cabinetId < 1 || cabinetId > 8)
                cabinetId = 1;

            var stateRoot = ResolveUiPath(
                Setting("State Root"), Path.Combine(workDir, "nvram"));
            Directory.CreateDirectory(stateRoot);
            var stateName = $"{gameId}-cab{cabinetId}";

            var parameters = new List<string>
            {
                Quote(romPath),
                "--disk", Quote(diskPath),
                "--game", gameId,
                "--vulkan",
                "--internal-scale", Setting("Internal Resolution", "4").TrimEnd('x', 'X'),
                "--texture-filter", Setting("Texture Filtering", "trilinear"),
                "--presentation-filter", Setting("Presentation Resampling", "bicubic"),
                "--sharpen", Setting("Presentation Sharpening", "0.15"),
                "--gamma", Setting("Display Gamma", "1.0"),
                "--saturation", Setting("Display Saturation", "1.0"),
                "--contrast", Setting("Display Contrast", "1.0"),
                "--jit",
                "--gpu-high-performance",
                "--sync-dcs",
                "--scale", Setting("Window Scale", "1"),
                "--cabinet", cabinetId.ToString(),
                "--nvram", Quote(Path.Combine(stateRoot, stateName + ".nvram")),
                "--overlay", Quote(Path.Combine(stateRoot, stateName + ".vgdif"))
            };

            var widescreen = Setting("True Widescreen", "off");
            if (widescreen != "16:9")
                widescreen = "off";
            parameters.Add("--widescreen");
            parameters.Add(widescreen);

            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen")
                parameters.Add("--fullscreen");
            if (Enabled("Mute Audio"))
                parameters.Add("--mute");
            if (Enabled("Use Bezel"))
                parameters.Add("--bezels");
            if (Enabled("Show Performance Overlay"))
                parameters.Add("--statistics");
            if (Enabled("Crosshairs"))
            {
                parameters.Add("--crosshairs");
                var crosshairScale = Setting("Crosshair Scale", "1.0");
                if (crosshairScale != "0.25" && crosshairScale != "0.50" &&
                    crosshairScale != "0.75" && crosshairScale != "1.0" &&
                    crosshairScale != "1.25" && crosshairScale != "1.50" &&
                    crosshairScale != "2.0" && crosshairScale != "3.0" &&
                    crosshairScale != "4.0")
                    crosshairScale = "1.0";
                parameters.Add("--crosshair-scale");
                parameters.Add(crosshairScale);
            }

            if (int.TryParse(Setting("Network Port", "0"), out var networkPort) &&
                networkPort > 0 && networkPort <= 65535)
            {
                parameters.Add("--network-port");
                parameters.Add(networkPort.ToString());
                var networkInterface = Setting("Network Interface", "auto").Trim();
                if (!string.IsNullOrWhiteSpace(networkInterface))
                {
                    parameters.Add("--network-interface");
                    parameters.Add(Quote(networkInterface));
                }
                if (Enabled("Network Diagnostics", true))
                    parameters.Add("--network-diagnostics");
            }

            var texturePackRoot = ResolveUiPath(
                Setting("Texture Pack Root"), Path.Combine(workDir, "texture-packs"));
            if (Enabled("Load Texture Packs", true))
            {
                parameters.Add("--texture-pack");
                parameters.Add(Quote(texturePackRoot));
                if (!int.TryParse(Setting("Texture VRAM Budget MB", "1024"), out var textureBudget) ||
                    textureBudget < 0 || textureBudget > 16384)
                    textureBudget = 1024;
                parameters.Add("--texture-budget-mb");
                parameters.Add(textureBudget.ToString());
                var anisotropy = Setting("HD Texture Anisotropy", "4");
                if (anisotropy != "1" && anisotropy != "2" &&
                    anisotropy != "4" && anisotropy != "8")
                    anisotropy = "4";
                parameters.Add("--texture-anisotropy");
                parameters.Add(anisotropy);
                if (Enabled("Texture Hot Reload"))
                    parameters.Add("--texture-hot-reload");
            }
            if (Enabled("Dump Textures"))
            {
                var textureDumpRoot = ResolveUiPath(
                    Setting("Texture Dump Root"), Path.Combine(workDir, "texture-dumps"));
                Directory.CreateDirectory(textureDumpRoot);
                parameters.Add("--texture-dump");
                parameters.Add(Quote(textureDumpRoot));
            }

            if (!File.Exists(executable))
                log?.Invoke($"TeknoVegas executable was not found at {executable}");
            if (!File.Exists(diskPath))
                log?.Invoke($"TeknoVegas CHD was not found at {diskPath}. Select it as the second game file in Game Settings.");

            var romFilePath = Path.IsPathRooted(romPath)
                ? romPath
                : Path.GetFullPath(Path.Combine(workDir, romPath));
            UnblockFile(romFilePath, log);
            UnblockFile(diskPath, log);

            var startInfo = new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir,
                RedirectStandardError = true
            };
            return startInfo;
        }

        private static ProcessStartInfo BuildTeknoViper(
            GameProfile profile,
            string gameLocation,
            Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue
                ?? fallback;

            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            string Quote(string value) =>
                "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoViper");
            var preferredExecutable = Path.Combine(workDir, "TeknoViper.exe");
            var legacyExecutable = Path.Combine(workDir, "viperwin.exe");
            var executable = File.Exists(preferredExecutable)
                ? preferredExecutable
                : legacyExecutable;

            string ResolveUiPath(string path, string fallback)
            {
                var value = string.IsNullOrWhiteSpace(path) ? fallback : path;
                return Path.IsPathRooted(value)
                    ? value
                    : Path.GetFullPath(Path.Combine(uiRoot, value));
            }

            var selectedRom = string.IsNullOrWhiteSpace(gameLocation)
                ? profile.GamePath
                : gameLocation;
            var configuredRomRoot = Setting("ROM Root");
            string romRoot;
            if (!string.IsNullOrWhiteSpace(configuredRomRoot))
                romRoot = ResolveUiPath(configuredRomRoot, workDir);
            else if (!string.IsNullOrWhiteSpace(selectedRom) &&
                     selectedRom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                romRoot = Path.GetDirectoryName(ResolveUiPath(selectedRom, workDir)) ?? workDir;
            else
                romRoot = ResolveUiPath(selectedRom, workDir);

            var selectedChd = profile.GamePath2;
            var configuredChdRoot = Setting("CHD Root");
            string diskPath = null;
            string chdRoot = null;
            if (!string.IsNullOrWhiteSpace(selectedChd) &&
                selectedChd.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            {
                diskPath = ResolveUiPath(selectedChd, workDir);
            }
            else if (!string.IsNullOrWhiteSpace(configuredChdRoot))
            {
                chdRoot = ResolveUiPath(configuredChdRoot, workDir);
            }
            else
            {
                chdRoot = ResolveUiPath(selectedChd, workDir);
            }

            var gameId = profile.ProfileName;
            var stateRoot = ResolveUiPath(
                Setting("State Root"), Path.Combine(workDir, "state"));
            var shaderRoot = Path.Combine(stateRoot, "shader-cache", gameId);
            Directory.CreateDirectory(stateRoot);
            Directory.CreateDirectory(shaderRoot);

            var parameters = new List<string>
            {
                "--game", gameId,
                "--rom-root", Quote(romRoot),
                "--state-dir", Quote(stateRoot),
                "--shader-cache-dir", Quote(shaderRoot),
                "--vulkan",
                "--internal-scale", Setting("Internal Resolution", "4").TrimEnd('x', 'X'),
                "--texture-filter", Setting("Texture Filtering", "trilinear"),
                "--presentation-filter", Setting("Presentation Resampling", "bicubic"),
                "--sharpen", Setting("Presentation Sharpening", "0.15"),
                "--gamma", Setting("Display Gamma", "1.0"),
                "--saturation", Setting("Display Saturation", "1.0"),
                "--contrast", Setting("Display Contrast", "1.0"),
            };

            if (diskPath != null)
            {
                parameters.Add("--disk");
                parameters.Add(Quote(diskPath));
            }
            else
            {
                parameters.Add("--chd-root");
                parameters.Add(Quote(chdRoot));
            }

            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen")
                parameters.Add("--fullscreen");
            if (profile.GunGame && !Enabled("Crosshairs", true))
                parameters.Add("--no-crosshairs");
            if (Enabled("Mute Audio"))
                parameters.Add("--mute");
            if (Enabled("Prefer High Performance", true))
            {
                parameters.Add("--high-priority");
                parameters.Add("--gpu-high-performance");
            }
            if (Enabled("Use Bezel"))
                parameters.Add("--bezels");
            if (!Enabled("Hide Crosshairs after Inactivity"))
                parameters.Add("--no-crosshair-autohide");

            var texturePackRoot = ResolveUiPath(
               Setting("Texture Pack Root"), Path.Combine(workDir, "texture-packs"));
            if (Enabled("Load Texture Packs", true))
            {
                parameters.Add("--texture-pack");
                parameters.Add(Quote(texturePackRoot));
                if (!int.TryParse(Setting("Texture VRAM Budget MB", "1024"), out var textureBudget) ||
                    textureBudget < 0 || textureBudget > 16384)
                    textureBudget = 1024;
                parameters.Add("--texture-budget-mb");
                parameters.Add(textureBudget.ToString());
                var anisotropy = Setting("HD Texture Anisotropy", "4");
                if (anisotropy != "1" && anisotropy != "2" &&
                    anisotropy != "4" && anisotropy != "8")
                    anisotropy = "4";
                parameters.Add("--texture-anisotropy");
                parameters.Add(anisotropy);
                if (Enabled("Texture Hot Reload"))
                    parameters.Add("--texture-hot-reload");
            }
            if (Enabled("Dump Textures"))
            {
                var textureDumpRoot = ResolveUiPath(
                    Setting("Texture Dump Root"), Path.Combine(workDir, "texture-dumps"));
                Directory.CreateDirectory(textureDumpRoot);
                parameters.Add("--texture-dump");
                parameters.Add(Quote(textureDumpRoot));
            }

            // TeknoViper consumes TPOnline's inherited environment handoff
            // directly. Its peer transport is mutually exclusive with the
            // local UDP multicast transport configured by --network-port.
            var tpOnline = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (!tpOnline &&
                int.TryParse(Setting("Network Port", "0"), out var networkPort) &&
                networkPort > 0 && networkPort <= 65535)
            {
                var cabinet = Setting("Cabinet Id", "1");
                if (!int.TryParse(cabinet, out var cabinetId) || cabinetId < 1 || cabinetId > 8)
                    cabinetId = 1;

                parameters.Add("--network-port");
                parameters.Add(networkPort.ToString());
                parameters.Add("--network-node");
                parameters.Add(cabinetId.ToString());

                var networkInterface = Setting("Network Interface", "auto").Trim();
                if (!string.IsNullOrWhiteSpace(networkInterface))
                {
                    parameters.Add("--network-interface");
                    parameters.Add(Quote(networkInterface));
                }
                if (Enabled("Network Diagnostics", true))
                    parameters.Add("--network-diagnostics");
            }
            else if (tpOnline && Enabled("Network Diagnostics", true))
            {
                parameters.Add("--network-diagnostics");
            }

            if (!File.Exists(executable))
                log?.Invoke($"TeknoViper executable was not found at {preferredExecutable} or {legacyExecutable}");
            if (!Directory.Exists(romRoot))
                log?.Invoke($"TeknoViper ROM root was not found at {romRoot}");
            if (diskPath != null && !File.Exists(diskPath))
                log?.Invoke($"TeknoViper CHD was not found at {diskPath}");
            else if (diskPath == null && !Directory.Exists(chdRoot))
                log?.Invoke($"TeknoViper CHD root was not found at {chdRoot}");

            // Viper loads the selected set and its shared system archive. Avoid
            // scanning a complete MAME collection on every launch.
            if (!string.IsNullOrWhiteSpace(selectedRom) &&
                selectedRom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                UnblockFile(ResolveUiPath(selectedRom, workDir), log);
            }
            else
            {
                UnblockFile(Path.Combine(romRoot, gameId + ".zip"), log);
            }
            UnblockFile(Path.Combine(romRoot, "kviper.zip"), log);
            if (diskPath != null)
            {
                UnblockFile(diskPath, log);
            }
            else
            {
                UnblockFilesInDirectory(
                    Path.Combine(chdRoot, gameId),
                    "*.chd",
                    SearchOption.AllDirectories,
                    log);
            }

            return new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir,
                // Fatal startup detail is emitted on stderr. GameProcessManager
                // drains it asynchronously and includes it in TPUI's exit-code
                // dialog instead of letting a release build disappear silently.
                RedirectStandardError = true
            };
        }

        private static void UnblockFilesInDirectory(
            string directory,
            string searchPattern,
            SearchOption searchOption,
            Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, searchPattern, searchOption))
                    UnblockFile(path, log);
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is ArgumentException)
            {
                log?.Invoke($"Could not scan launch media in {directory} for Internet zone metadata: {ex.Message}");
            }
        }

        private static void UnblockFile(string path, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                // .NET Framework's File.Delete rejects alternate-data-stream
                // paths, so call Win32 directly to remove the Mark of the Web.
                if (DeleteFile(path + ":Zone.Identifier"))
                    return;

                var error = Marshal.GetLastWin32Error();
                // A missing stream means the file is already unblocked. File
                // systems without alternate streams cannot contain this mark.
                if (error == ErrorFileNotFound ||
                    error == ErrorPathNotFound ||
                    error == ErrorNotSupported ||
                    error == ErrorInvalidName)
                    return;

                throw new Win32Exception(error);
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException ||
                                       ex is Win32Exception ||
                                       ex is ArgumentException)
            {
                // Unblocking is best-effort; a read-only/network ROM should not
                // prevent the emulator from attempting to launch it.
                log?.Invoke($"Could not remove Internet zone metadata from {path}: {ex.Message}");
            }
        }
    }
}
