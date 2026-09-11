using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoViperVegasLauncher
    {
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;
        private const int ErrorNotSupported = 50;
        private const int ErrorInvalidName = 123;

        private static string VrDepthArgument(string value)
        {
            if (!int.TryParse(value, out var percentage) ||
                percentage < 100 || percentage > 200)
                percentage = 150;
            return $"{percentage / 100}.{percentage % 100:D2}";
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string fileName);

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            switch (profile.EmulatorType)
            {
                case EmulatorType.TeknoAir:
                    return TeknoAirLauncher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoVegas:
                    return BuildTeknoVegas(profile, gameLocation, log);
                case EmulatorType.TeknoGClub:
                    return TeknoGClubLauncher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoS23:
                    return TeknoS23Launcher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoS21:
                    return TeknoS21Launcher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoS22:
                    return TeknoS22Launcher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoZeus:
                    return TeknoZeusLauncher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoHNG64:
                    return TeknoHNG64Launcher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoCobra:
                    return TeknoCobraLauncher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoHornet:
                    return BuildTeknoHornet(profile, gameLocation, log);
                case EmulatorType.TeknoAGX:
                    return BuildTeknoAGX(profile, gameLocation, log);
                case EmulatorType.TeknoVUnit:
                    return TeknoVUnitLauncher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoM2:
                    return TeknoM2Launcher.Build(profile, gameLocation, log);
                case EmulatorType.TeknoViper:
                    return BuildTeknoViper(profile, gameLocation, log);
                case EmulatorType.TeknoModel2:
                    return BuildTeknoModel2(profile, gameLocation, log);
                case EmulatorType.TeknoModel1:
                    return BuildTeknoModel1(profile, gameLocation, log);
                default:
                    throw new InvalidOperationException($"{profile.EmulatorType} is not a supported standalone emulator");
            }
        }

        private static ProcessStartInfo BuildTeknoAGX(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) =>
                Setting(name, fallback ? "1" : "0") == "1" ||
                Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            // Windows argv quoting, including trailing backslashes in directory paths.
            string Quote(string value)
            {
                var result = new System.Text.StringBuilder("\"");
                var slashes = 0;
                foreach (var c in value ?? "")
                {
                    if (c == '\\') { ++slashes; continue; }
                    result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                    result.Append(c);
                    slashes = 0;
                }
                return result.Append('\\', slashes * 2).Append('"').ToString();
            }
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoAGX");
            string Resolve(string value) => Path.GetFullPath(
                Path.IsPathRooted(value) ? value : Path.Combine(uiRoot, value));
            var rom = Resolve(string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation);
            if (!File.Exists(rom)) throw new FileNotFoundException("Select a51site4.zip as the game executable.", rom);
            if (string.IsNullOrWhiteSpace(profile.GamePath2))
                throw new InvalidOperationException("Select a51site4-2_01.chd as Game CHD.");
            var disk = Resolve(profile.GamePath2);
            if (!File.Exists(disk)) throw new FileNotFoundException("The selected Site 4 CHD does not exist.", disk);
            var state = Path.Combine(workDir, "state");
            Directory.CreateDirectory(state);
            var filter = Setting("Presentation Resampling", "bicubic");
            if (filter != "nearest" && filter != "linear" && filter != "bicubic" && filter != "lanczos") filter = "bicubic";
            var args = new List<string> { "--set", "a51site4", "--rom-root", Quote(Path.GetDirectoryName(rom)),
                "--disk", Quote(disk), "--state-root", Quote(state), "--vulkan", "--filter", filter };
            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch-to-fullscreen");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            if (Enabled("Use Bezel", true)) args.Add("--bezels");
            if (Setting("CRT Shader", "None") == "Lottes") args.Add("--crt-shader lottes");
            if (Enabled("VSync")) args.Add("--vsync");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (Enabled("Enable VR")) {
                args.Add("--openxr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            UnblockFile(rom, log); UnblockFile(disk, log);
            return new ProcessStartInfo(Path.Combine(workDir, "TeknoAGX.exe"), string.Join(" ", args))
                { UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true };
        }

        private static ProcessStartInfo BuildTeknoModel2(
            GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            // Windows argv quoting, including trailing backslashes in directory paths.
            string Quote(string value)
            {
                var result = new System.Text.StringBuilder("\"");
                var slashes = 0;
                foreach (var c in value ?? "")
                {
                    if (c == '\\') { ++slashes; continue; }
                    result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                    result.Append(c);
                    slashes = 0;
                }
                return result.Append('\\', slashes * 2).Append('"').ToString();
            }
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoModel2");
            string Resolve(string value) => Path.GetFullPath(
                Path.IsPathRooted(value) ? value : Path.Combine(uiRoot, value));
            var selectedRom = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            var romRoot = Setting("ROM Root");
            if (string.IsNullOrWhiteSpace(romRoot))
                romRoot = string.IsNullOrWhiteSpace(selectedRom) ? workDir :
                    selectedRom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(Resolve(selectedRom)) : Resolve(selectedRom);
            else romRoot = Resolve(romRoot);
            var gameId = profile.ProfileName;
            var configuredSaveRoot = Setting("Save Root");
            var saveRoot = Resolve(string.IsNullOrWhiteSpace(configuredSaveRoot)
                ? Path.Combine(workDir, "nvram") : configuredSaveRoot);
            Directory.CreateDirectory(saveRoot);
            var scale = Setting("Internal Resolution", "4");
            if (scale != "1" && scale != "2" && scale != "4" && scale != "8") scale = "4";
            var filter = Setting("Presentation Filter", "linear").Trim().ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "ssaa" }.Contains(filter))
                filter = "linear";
            var parameters = new List<string>
            {
                "--game", Quote(gameId), "--rom-root", Quote(romRoot),
                "--nvram", Quote(Path.Combine(saveRoot, gameId + ".nvram")),
                "--renderer", Setting("Renderer", "vulkan") == "software" ? "software" : "vulkan",
                "--upscale", scale,
                "--filter", filter,
                "--outputs"
            };
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) parameters.Add("--stretch");
            if (Enabled("Use Bezel")) parameters.Add("--bezels");
            var crt = Setting("CRT Shader", "None").Trim();
            if (crt.Equals("Lottes", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--crt lottes");
            else if (crt.Equals("Lottes Downsample", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--crt lottes-downsample");
            // The lobby handoff supplies the cabinet ID/count and uses direct TPO peers.
            var tpOnline = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (!tpOnline && Enabled("Enable LAN"))
            {
                if (!int.TryParse(Setting("Network Port", "42002"), out var port) || port < 1 || port > 65535)
                    throw new ArgumentException("Network Port must be between 1 and 65535.");
                if (!int.TryParse(Setting("Cabinet Count", "2"), out var count) || count < 2 || count > 8)
                    throw new ArgumentException("Cabinet Count must be between 2 and 8.");
                if (!int.TryParse(Setting("Cabinet Number", "1"), out var node) || node < 1 || node > count)
                    throw new ArgumentException("Cabinet Number must be unique and between 1 and Cabinet Count.");
                var networkInterface = Setting("Network Interface", "auto").Trim();
                if (string.IsNullOrWhiteSpace(networkInterface)) networkInterface = "auto";
                if (!networkInterface.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                    (!System.Net.IPAddress.TryParse(networkInterface, out var address) ||
                     address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                    throw new ArgumentException("Network Interface must be auto or a local IPv4 address.");
                if (networkInterface.Equals("auto", StringComparison.OrdinalIgnoreCase)) networkInterface = "auto";
                parameters.Add("--network-port " + port.ToString(CultureInfo.InvariantCulture));
                parameters.Add("--network-node " + node.ToString(CultureInfo.InvariantCulture));
                parameters.Add("--network-nodes " + count.ToString(CultureInfo.InvariantCulture));
                parameters.Add("--network-interface " + Quote(networkInterface));
            }
            if ((tpOnline || Enabled("Enable LAN")) && Enabled("Network Diagnostics", true))
                parameters.Add("--network-diagnostics");
            if (Enabled("Mute Audio")) parameters.Add("--no-audio");
            if (Enabled("Enable VR"))
            {
                parameters.Add("--vr");
                if (!Enabled("Use VR Controls", true)) parameters.Add("--no-vr-controls");
            }
            var ffbDevice = TeknoParrotUi.Helpers.Model2FfbDeviceProbe.GetLaunchSelection(
                Setting("Force Feedback Device", "off"));
            parameters.Add("--ffb-device");
            parameters.Add(ffbDevice);
            if (!int.TryParse(Setting("Force Feedback Strength", "35"), out var ffbGain) ||
                ffbGain < 0 || ffbGain > 100)
                ffbGain = 35;
            parameters.Add("--ffb-gain");
            parameters.Add(ffbGain.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (Enabled("Invert Force Feedback")) parameters.Add("--ffb-invert-x");
            var executable = Path.Combine(workDir, "TeknoModel2.exe");
            if (!File.Exists(executable)) log?.Invoke($"TeknoModel2 executable was not found at {executable}");
            if (!Directory.Exists(romRoot)) log?.Invoke($"TeknoModel2 ROM root was not found at {romRoot}");
            return new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir,
                RedirectStandardError = true
            };
        }

        private static ProcessStartInfo BuildTeknoModel1(
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
            var workDir = Path.Combine(uiRoot, "TeknoModel1");
            var executable = Path.Combine(workDir, "TeknoModel1.exe");

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

            var gameId = profile.ProfileName;
            var saveRoot = ResolveUiPath(
                Setting("Save Root"), Path.Combine(workDir, "saves"));
            Directory.CreateDirectory(saveRoot);

            var scale = Setting("Internal Resolution", "4");
            if (!int.TryParse(scale, out var scaleValue) || scaleValue < 1 || scaleValue > 8)
                scale = "4";
            var vrEnabled = Enabled("Enable VR");
            var filter = Setting("Presentation Filter", "ssaa").Trim().ToLowerInvariant();
            if (filter != "ssaa" && filter != "nearest" && filter != "linear" && filter != "bicubic")
                filter = "ssaa";
            
            if (vrEnabled && filter == "bicubic")
                filter = "nearest";

            string DisplayValue(string name, double fallback, double minimum, double maximum)
            {
                var value = Setting(name);
                if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                     !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) ||
                    double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed < minimum || parsed > maximum)
                    parsed = fallback;
                return parsed.ToString(CultureInfo.InvariantCulture);
            }

            var parameters = new List<string>
            {
                "--game", gameId,
                "--rom-dir", Quote(romRoot),
                "--save-dir", Quote(saveRoot),
                "--internal-scale", scale,
                "--presentation-filter", filter,
                "--outputs"
            };

            if (Enabled("Smooth Geometry"))
                parameters.Add("--smooth-geometry");
            if ((gameId == "vr" || gameId == "vformula") && Enabled("Extended Draw Distance"))
                parameters.Add("--vr-extended-draw-distance");
            if (gameId == "wingwar" && Enabled("Extended Draw Distance"))
                parameters.Add("--wingwar-extended-draw-distance");
            if (gameId == "vformula" && !vrEnabled && Enabled("Widescreen hack"))
                parameters.Add("--vformula-widescreen");
            if (gameId == "vf" && !vrEnabled && Enabled("Widescreen hack"))
                parameters.Add("--vf-widescreen");
            if (gameId == "wingwar" && !vrEnabled && Enabled("Widescreen hack"))
                parameters.Add("--wingwar-widescreen");
            if (!vrEnabled && !Enabled("Uncapped") &&
                !Enabled("Legacy Low-Latency Pacing") && Enabled("Low Latency Mode", true))
                parameters.Add("--adaptive-late-start");
            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen")
                parameters.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen"))
                parameters.Add("--stretch-to-fullscreen");
            if (vrEnabled)
            {
                parameters.Add("--vr");
                parameters.Add("--vr-depth");
                parameters.Add(VrDepthArgument(Setting("VR Depth", "150")));
            }
            else
            {
                var crtShader = Setting("CRT Shader", "None").Trim().ToLowerInvariant();
                switch (crtShader)
                {
                    case "lottes":
                        break;
                    case "lottes downsample":
                    case "lottes-ssaa":
                        crtShader = "lottes-ssaa";
                        break;
                    default:
                        crtShader = "none";
                        break;
                }
                parameters.AddRange(new[]
                {
                    "--crt-shader", crtShader,
                    "--sharpen", DisplayValue("Presentation Sharpening", 0.0, 0.0, 1.0),
                    "--gamma", DisplayValue("Display Gamma", 1.0, 0.5, 2.0),
                    "--saturation", DisplayValue("Display Saturation", 1.0, 0.5, 2.0),
                    "--contrast", DisplayValue("Display Contrast", 1.0, 0.5, 2.0)
                });
                if (Enabled("Use Bezel"))
                    parameters.Add("--bezels");
            }
            if (Enabled("Mute Audio"))
                parameters.Add("--no-audio");
            if (Enabled("Cabinet Outputs"))
                parameters.Add("--outputs");
            if (Enabled("Uncapped"))
                parameters.Add("--uncapped");
            if (Enabled("Legacy Low-Latency Pacing"))
                parameters.Add("--pace-after-present");

            var ffbDevice = Setting("Force Feedback Device", "off").Trim();
            var ffbToken = ffbDevice.Split(':');
            if (ffbDevice != "off" &&
                (ffbToken.Length != 2 ||
                 (ffbToken[0] != "wheel" && ffbToken[0] != "gamepad") ||
                 !uint.TryParse(ffbToken[1], out _)))
                ffbDevice = "off";
            parameters.Add("--ffb-device");
            parameters.Add(ffbDevice);

            if (gameId == "netmerc")
            {
                var donor = Setting("NetMerc Audio Donor", "vf");
                if (donor != "vf" && donor != "vr" && donor != "swa" &&
                    donor != "wingwar" && donor != "off")
                    donor = "vf";
                parameters.Add("--netmerc-donor");
                parameters.Add(donor);
            }

            var linkRole = Setting("Link Role", "Off").ToLowerInvariant();
            var linkServer = Setting("Link Server").Trim();
            if ((linkRole == "master" || linkRole == "slave") &&
                !string.IsNullOrWhiteSpace(linkServer))
            {
                parameters.Add("--link-server");
                parameters.Add(Quote(linkServer));
                parameters.Add("--link-role");
                parameters.Add(linkRole);
            }

            if (!File.Exists(executable))
                log?.Invoke($"TeknoModel1 executable was not found at {executable}");
            if (!Directory.Exists(romRoot))
                log?.Invoke($"TeknoModel1 ROM root was not found at {romRoot}");

            if (!string.IsNullOrWhiteSpace(selectedRom) &&
                selectedRom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                UnblockFile(ResolveUiPath(selectedRom, workDir), log);

            return new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir,
                RedirectStandardError = true
            };
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
                "--outputs",
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

            var ffbDevice = Setting("Force Feedback Device", "off").Trim();
            var ffbToken = ffbDevice.Split(':');
            if (ffbDevice != "off" &&
                (ffbToken.Length != 2 ||
                 (ffbToken[0] != "wheel" && ffbToken[0] != "gamepad") ||
                 !uint.TryParse(ffbToken[1], NumberStyles.None,
                     CultureInfo.InvariantCulture, out _)))
                ffbDevice = "off";
            parameters.Add("--ffb-device");
            parameters.Add(ffbDevice);

            if (!int.TryParse(Setting("Force Feedback Strength", "200"), out var ffbStrength) ||
                ffbStrength < 25 || ffbStrength > 400)
                ffbStrength = 200;
            parameters.Add("--ffb-gain");
            parameters.Add((ffbStrength / 100.0).ToString("0.00", CultureInfo.InvariantCulture));

            var widescreen = Setting("True Widescreen", "off");
            if (widescreen != "16:9")
                widescreen = "off";
            parameters.Add("--widescreen");
            parameters.Add(widescreen);

            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen")
                parameters.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen"))
                parameters.Add("--stretch-to-fullscreen");
            if (Enabled("Enable VR"))
            {
                parameters.Add("--vr");
                parameters.Add("--vr-depth");
                parameters.Add(VrDepthArgument(Setting("VR Depth", "150")));
                if (!Enabled("Use VR Controls", true))
                    parameters.Add("--no-vr-controls");
            }
            if (Enabled("Mute Audio"))
                parameters.Add("--mute");
            if (Enabled("Use Bezel"))
                parameters.Add("--bezels");
            if (Setting("CRT Shader", "None") == "Lottes")
                parameters.Add("--crt-shader lottes");
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

        private static ProcessStartInfo BuildTeknoHornet(GameProfile profile,
            string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name) => Setting(name) == "1" ||
                Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            string Quote(string value)
            {
                var result = new System.Text.StringBuilder("\"");
                var slashes = 0;
                foreach (var c in value ?? "")
                {
                    if (c == '\\') { ++slashes; continue; }
                    result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                    result.Append(c);
                    slashes = 0;
                }
                return result.Append('\\', slashes * 2).Append('"').ToString();
            }
            var workDir = Path.Combine(Directory.GetCurrentDirectory(), "TeknoHornet");
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            var romRoot = Setting("ROM Root");
            if (string.IsNullOrWhiteSpace(romRoot)) romRoot = selected;
            if (string.IsNullOrWhiteSpace(romRoot)) romRoot = workDir;
            romRoot = Path.GetFullPath(romRoot);
            if (romRoot.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                romRoot = Path.GetDirectoryName(romRoot);
            var game = profile.ProfileName;
            var stateRoot = Setting("State Root", Path.Combine(workDir, "state"));
            if (string.IsNullOrWhiteSpace(stateRoot)) stateRoot = Path.Combine(workDir, "state");
            stateRoot = Path.GetFullPath(stateRoot);
            Directory.CreateDirectory(stateRoot);
            var nvram = Path.Combine(stateRoot, game + ".m48t58");
            // TeknoHornet.exe owns first-run setup after its license gate.
            var parameters = new List<string> { Quote(game), "--rom-root", Quote(romRoot),
                "--nvram", Quote(nvram), "--renderer", "vulkan", "--upscale", Setting("Internal Resolution", "4"),
                "--filter", Setting("Presentation Resampling", "area"), "--parallel-graphics" };
            parameters.Add(Setting("DisplayMode", "Fullscreen") == "Fullscreen" ? "--fullscreen" : "--windowed");
            if (Enabled("Stretch to Fullscreen")) parameters.Add("--stretch-to-fullscreen");
            if (Enabled("Use Bezel")) parameters.Add("--bezels");
            if (Enabled("Mute Audio")) parameters.Add("--mute");
            var crt = Setting("CRT Shader", "None");
            if (crt == "Lottes") parameters.Add("--crt-shader lottes");
            if (crt == "Lottes Downsample") parameters.Add("--crt-shader lottes-ssaa");
            if (Enabled("Enable LAN") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2")))
            {
                int Number(string key, int min, int max, string fallback) {
                    if (!int.TryParse(Setting(key, fallback), out var value) || value < min || value > max)
                        throw new ArgumentException(key + " must be between " + min + " and " + max + ".");
                    return value;
                }
                parameters.Add("--link-id " + (Number("Cabinet Id", 1, 4, "1") - 1));
                parameters.Add("--link-local-port " + Number("Local Port", 1, 65535, "47100"));
                parameters.Add("--link-peer-host " + Quote(Setting("Peer Address", "127.0.0.1")));
                parameters.Add("--link-peer-port " + Number("Peer Port", 1, 65535, "47101"));
                parameters.Add("--link-session " + Number("Link Session", 1, int.MaxValue, "713"));
            }
            return new ProcessStartInfo(Path.Combine(workDir, "TeknoHornet.exe"), string.Join(" ", parameters))
            { UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true };
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
                "--outputs",
                "--internal-scale", Setting("Internal Resolution", "4").TrimEnd('x', 'X'),
                "--texture-filter", Setting("Texture Filtering", "trilinear"),
                "--presentation-filter", Setting("Presentation Resampling", "bicubic"),
                "--sharpen", Setting("Presentation Sharpening", "0.15"),
                "--gamma", Setting("Display Gamma", "1.0"),
                "--saturation", Setting("Display Saturation", "1.0"),
                "--contrast", Setting("Display Contrast", "1.0")
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
            if (Enabled("Stretch to Fullscreen"))
                parameters.Add("--stretch-to-fullscreen");
            if (Enabled("Enable VR"))
             {
                parameters.Add("--vr");
                parameters.Add("--vr-depth");
                parameters.Add(VrDepthArgument(Setting("VR Depth", "150")));
                if (!Enabled("Use VR Controls", true))
                    parameters.Add("--no-vr-controls");
            }
            if (profile.GunGame && !Enabled("Crosshairs", true))
                parameters.Add("--no-crosshairs");
            if (Enabled("Mute Audio"))
                parameters.Add("--mute");

            string FfbDevice(string settingName)
            {
                var device = Setting(settingName, "off").Trim();
                var token = device.Split(':');
                if (device != "off" &&
                    (token.Length != 2 ||
                     (token[0] != "wheel" && token[0] != "gamepad") ||
                     !uint.TryParse(token[1], out _)))
                    return "off";
                return device;
            }

            parameters.Add("--ffb-device");
            parameters.Add(FfbDevice("Force Feedback Device"));
            parameters.Add("--ffb-device2");
            parameters.Add(FfbDevice("Player 2 Force Feedback Device"));

            if (Enabled("Prefer High Performance", true))
            {
                parameters.Add("--high-priority");
                parameters.Add("--gpu-high-performance");
            }
            if (Enabled("Use Bezel"))
                parameters.Add("--bezels");
            if (Setting("CRT Shader", "None") == "Lottes")
                parameters.Add("--crt-shader lottes");
            if (Setting("CRT Shader", "None") == "Lottes Downsample")
                parameters.Add("--crt-shader lottes-ssaa");

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
