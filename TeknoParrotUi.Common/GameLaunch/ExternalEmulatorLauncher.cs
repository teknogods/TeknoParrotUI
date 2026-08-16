using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Builds launch configuration for games that run inside external emulators
    /// (Dolphin/Triforce, Play!, RPCS3, PCSX2, Cxbx-Reloaded). Ported verbatim
    /// from the classic UI's GameProcessManager so behaviour is identical.
    /// </summary>
    public static class ExternalEmulatorLauncher
    {
        public static bool IsExternalEmulator(GameProfile profile)
        {
            switch (profile.EmulatorType)
            {
                case EmulatorType.Dolphin:
                case EmulatorType.Play:
                case EmulatorType.RPCS3:
                case EmulatorType.cxbxr:
                case EmulatorType.pcsx2x6:
                case EmulatorType.TeknoVegas:
                case EmulatorType.TeknoViper:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsWindowed(GameProfile profile)
        {
            return profile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") ||
                   profile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
        }

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            bool windowed = IsWindowed(profile);

            switch (profile.EmulatorType)
            {
                case EmulatorType.Dolphin: return BuildDolphin(profile, windowed);
                case EmulatorType.Play: return BuildPlay(profile, gameLocation, windowed, log);
                case EmulatorType.pcsx2x6: return BuildPcsx2x6(profile, windowed, log);
                case EmulatorType.TeknoVegas: return BuildTeknoVegas(profile, gameLocation, log);
                case EmulatorType.TeknoViper: return BuildTeknoViper(profile, gameLocation, log);
                case EmulatorType.RPCS3: return BuildRpcs3(profile, windowed, log);
                case EmulatorType.cxbxr: return BuildCxbxr(profile, windowed, log);
                default: throw new InvalidOperationException($"{profile.EmulatorType} is not an external emulator");
            }
        }

        // ---------- Dolphin (Triforce) ----------

        private static ProcessStartInfo BuildDolphin(GameProfile profile, bool windowed)
        {
            var parameters = new List<string>();

            if (profile.ProfileName == "tatsuvscap")
            {
                // Dolphin.exe -b -n 0000000100000002
                parameters.Add("-b");
                parameters.Add("-n 0000000100000002");
                ConfigureDolphinIni(profile.EmulationProfile);
            }
            else
            {
                ConfigureDolphinIni(profile.EmulationProfile);

                if (Lazydata.ParrotData.HideDolphinGUI)
                {
                    // -b (batch) to hide ui, which in turn requires -e to specify the game
                    parameters.Add("-b");
                    parameters.Add("-e");
                }

                // Important, game path needs to be after -e (executable)
                parameters.Add($"\"{profile.GamePath}\"");
            }

            if (!windowed)
            {
                parameters.Add("--config");
                parameters.Add("\"Dolphin.Display.Fullscreen=True\"");
            }

            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Dolphin.exe"
                : "dolphin-emu";
            return new ProcessStartInfo(
                Path.Combine(".", "CrediarDolphin", executableName),
                string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CrediarDolphin")
            };
        }

        private static void ConfigureDolphinIni(EmulationProfile emulationProfile)
        {
            var isRva = emulationProfile == EmulationProfile.Tatsunoko;
            var configDirectory = Path.Combine(".", "CrediarDolphin", "User", "Config");

            try
            {
                Directory.CreateDirectory(configDirectory);

                var backend = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "D3D"
                    : "Vulkan";
                var dolphinSettings = new Dictionary<string, Dictionary<string, string>>
                {
                    ["Core"] = new Dictionary<string, string>
                    {
                        ["SIDevice0"] = "11",
                        ["SIDevice1"] = "6",
                        ["SIDevice2"] = "0",
                        ["SIDevice3"] = "0",
                        ["SelectedLanguage"] = "0",
                        ["SerialPort1"] = isRva ? "255" : "6",
                        // Wii Arcade (RVA) was value 14 in 1.0.0.6. Current Dolphin
                        // assigns 14 to Ethernet IPC and Wii Arcade to 15.
                        ["SlotA"] = "15",
                        ["SlotB"] = "15",
                        ["MEM1Size"] = "0x04000000",
                        ["MEM2Size"] = "0x08000000",
                        ["RAMOverrideEnable"] = isRva ? "True" : "False",
                        ["SkipIPL"] = "True",
                        ["GFXBackend"] = backend,
                        ["CPUThread"] = "True"
                    },
                    ["Display"] = new Dictionary<string, string>
                    {
                        ["DisableScreenSaver"] = "True"
                    },
                    ["DSP"] = new Dictionary<string, string>
                    {
                        ["DSPThread"] = "True"
                    },
                    ["General"] = new Dictionary<string, string>
                    {
                        ["HotkeysRequireFocus"] = "True"
                    },
                    ["Interface"] = new Dictionary<string, string>
                    {
                        ["ConfirmStop"] = "False",
                        ["OnScreenDisplayMessages"] = "False",
                        ["ShowActiveTitle"] = "True",
                        ["UseBuiltinTitleDatabase"] = "True",
                        ["UsePanicHandlers"] = "False"
                    },
                    ["NetPlay"] = new Dictionary<string, string>
                    {
                        ["TraversalChoice"] = "direct"
                    },
                    ["Analytics"] = new Dictionary<string, string>
                    {
                        ["Enabled"] = "False",
                        ["PermissionAsked"] = "True"
                    }
                };

                var gfxSettings = new Dictionary<string, Dictionary<string, string>>
                {
                    ["Enhancements"] = new Dictionary<string, string>
                    {
                        ["DisableCopyFilter"] = "True",
                        ["ForceTrueColor"] = "True",
                        ["HDROutput"] = "False"
                    },
                    ["Hacks"] = new Dictionary<string, string>
                    {
                        ["BBoxEnable"] = "False",
                        ["DeferEFBCopies"] = "True",
                        ["EFBEmulateFormatChanges"] = "False",
                        ["EFBScaledCopy"] = "True",
                        ["EFBToTextureEnable"] = "True",
                        ["SkipDuplicateXFBs"] = "True",
                        ["XFBToTextureEnable"] = "True"
                    },
                    ["Hardware"] = new Dictionary<string, string>
                    {
                        ["VSync"] = "True"
                    },
                    ["Settings"] = new Dictionary<string, string>
                    {
                        ["AspectRatio"] = "2",
                        ["BackendMultithreading"] = "True",
                        ["DumpBaseTextures"] = "True",
                        ["DumpMipTextures"] = "True",
                        ["FastDepthCalc"] = "True",
                        ["FrameDumpsResolutionType"] = "1",
                        ["InternalResolution"] = "4",
                        ["SaveTextureCacheToState"] = "True",
                        ["ShowSpeedColors"] = "True",
                        ["WaitForShadersBeforeStarting"] = "True"
                    }
                };

                UpdateDolphinIni(
                    Path.Combine(configDirectory, "Dolphin.ini"),
                    dolphinSettings,
                    removeAnalyticsId: true);
                UpdateDolphinIni(
                    Path.Combine(configDirectory, "GFX.ini"),
                    gfxSettings,
                    removeAnalyticsId: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating Dolphin config: {ex.Message}");
            }
        }

        private static void UpdateDolphinIni(
            string path,
            Dictionary<string, Dictionary<string, string>> settings,
            bool removeAnalyticsId)
        {
            settings = settings.ToDictionary(
                section => section.Key,
                section => new Dictionary<string, string>(
                    section.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
            var lines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();
            var output = new List<string>();
            var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var writtenKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = null;

            foreach (var section in settings)
                writtenKeys[section.Key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action appendMissingForCurrentSection = () =>
            {
                if (currentSection == null || !settings.TryGetValue(currentSection, out var sectionSettings))
                    return;

                foreach (var setting in sectionSettings)
                {
                    if (!writtenKeys[currentSection].Contains(setting.Key))
                        output.Add($"{setting.Key} = {setting.Value}");
                }
            };

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    appendMissingForCurrentSection();
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    seenSections.Add(currentSection);
                    output.Add(line);
                    continue;
                }

                var equals = trimmed.IndexOf('=');
                if (currentSection != null && equals > 0)
                {
                    var key = trimmed.Substring(0, equals).Trim();
                    if (removeAnalyticsId &&
                        currentSection.Equals("Analytics", StringComparison.OrdinalIgnoreCase) &&
                        key.Equals("ID", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (settings.TryGetValue(currentSection, out var sectionSettings) &&
                        sectionSettings.TryGetValue(key, out var value))
                    {
                        output.Add($"{key} = {value}");
                        writtenKeys[currentSection].Add(key);
                        continue;
                    }
                }

                output.Add(line);
            }

            appendMissingForCurrentSection();

            foreach (var section in settings)
            {
                if (seenSections.Contains(section.Key))
                    continue;

                if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[output.Count - 1]))
                    output.Add(string.Empty);
                output.Add($"[{section.Key}]");
                foreach (var setting in section.Value)
                    output.Add($"{setting.Key} = {setting.Value}");
            }

            File.WriteAllLines(path, output);
        }

        // ---------- Play! ----------

        private static ProcessStartInfo BuildPlay(GameProfile profile, string gameLocation, bool windowed, Action<string> log)
        {
            string gamePath = Path.GetDirectoryName(gameLocation);
            string configPath = Path.Combine(".", "Play", "TeknoParrot", "Documents", "Play Data Files", "config.xml");
            var configDirectory = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDirectory))
                Directory.CreateDirectory(configDirectory);

            string sys256CabId = "1";
            if (profile.ConfigValues.Any(x => x.FieldName == "Cabinet Id" && x.FieldValue == "2"))
                sys256CabId = "2";

            try
            {
                var configValues = new Dictionary<string, (string type, string value)>
                {
                    ["ps2.arcaderoms.directory"] = ("path", gamePath),
                    ["video.gshandler"] = ("integer", GetPlayGraphicsBackendValue(profile)),
                    ["renderer.opengl.resfactor"] = ("integer", GetPlayResolutionFactorValue(profile)),
                    ["sys256.cabinet.linkid"] = ("integer", sys256CabId)
                };

                CreateOrUpdatePlayConfig(configPath, configValues);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error updating Play config: {ex.Message}");
            }

            var parameters = new List<string> { $"--arcade {profile.ProfileName}" };
            if (!windowed)
                parameters.Add("--fullscreen");

            return new ProcessStartInfo(@".\Play\Play.exe", string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Play")
            };
        }

        private static string GetPlayGraphicsBackendValue(GameProfile profile)
        {
            if (profile.ConfigValues.Any(x => x.FieldName == "Graphics Backend" && x.FieldValue == "Vulkan"))
                return "1";
            return "0";
        }

        private static string GetPlayResolutionFactorValue(GameProfile profile)
        {
            var resolutionConfig = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Resolution");

            if (resolutionConfig?.FieldValue == "960p") return "2";
            if (resolutionConfig?.FieldValue == "1920p") return "4";
            if (resolutionConfig?.FieldValue == "4320p") return "8";
            if (resolutionConfig?.FieldValue == "7680p") return "16";

            return "1";
        }

        private static void CreateOrUpdatePlayConfig(string configPath, Dictionary<string, (string type, string value)> configValues)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            var xmlDoc = new XmlDocument();

            if (File.Exists(configPath))
                xmlDoc.Load(configPath);
            else
                xmlDoc.LoadXml("<Config></Config>");

            var rootNode = xmlDoc.DocumentElement;

            foreach (var config in configValues)
            {
                var existingNode = xmlDoc.SelectSingleNode($"//Preference[@Name='{config.Key}']");

                if (existingNode != null)
                {
                    existingNode.Attributes["Value"].Value = config.Value.value;
                }
                else
                {
                    var newNode = xmlDoc.CreateElement("Preference");
                    newNode.SetAttribute("Name", config.Key);
                    newNode.SetAttribute("Type", config.Value.type);
                    newNode.SetAttribute("Value", config.Value.value);
                    rootNode.AppendChild(newNode);
                }
            }

            xmlDoc.Save(configPath);
        }

        // ---------- TeknoVegas ----------

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

            static string Quote(string value) =>
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

            var startInfo = new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir
            };

            // Somehow RTSS's Vulkan layer crashes immediately when Vegas starts so
            // disabling it for TeknoVegas seems to work around it on Windows.
            if (OperatingSystem.IsWindows())
            {

                startInfo.EnvironmentVariables["DISABLE_RTSS_LAYER"] = "1";
                startInfo.EnvironmentVariables["DISABLE_VULKAN_OBS_CAPTURE"] = "1";
                startInfo.EnvironmentVariables["VK_LOADER_LAYERS_DISABLE"] =
                    "VK_LAYER_RTSS,VK_LAYER_OBS_HOOK";
            }
            return startInfo;
        }

        // ---------- TeknoViper ----------

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

            static string Quote(string value) =>
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
            string chdRoot;
            if (!string.IsNullOrWhiteSpace(configuredChdRoot))
            {
                chdRoot = ResolveUiPath(configuredChdRoot, workDir);
            }
            else if (!string.IsNullOrWhiteSpace(selectedChd) &&
                     selectedChd.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            {
                var chdPath = ResolveUiPath(selectedChd, workDir);
                var setDirectory = Path.GetDirectoryName(chdPath);
                chdRoot = setDirectory == null
                    ? workDir
                    : (Directory.GetParent(setDirectory)?.FullName ?? setDirectory);
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
                "--chd-root", Quote(chdRoot),
                "--state-dir", Quote(stateRoot),
                "--shader-cache-dir", Quote(shaderRoot),
                "--vulkan",
                "--internal-scale", Setting("Internal Resolution", "4").TrimEnd('x', 'X'),
                "--texture-filter", Setting("Texture Filtering", "trilinear"),
                "--presentation-filter", Setting("Presentation Resampling", "bicubic"),
                "--sharpen", Setting("Presentation Sharpening", "0.15"),
                "--gamma", Setting("Display Gamma", "1.0"),
                "--saturation", Setting("Display Saturation", "1.0"),
                "--contrast", Setting("Display Contrast", "1.0")
            };

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

            if (!File.Exists(executable))
                log?.Invoke($"TeknoViper executable was not found at {preferredExecutable} or {legacyExecutable}");
            if (!Directory.Exists(romRoot))
                log?.Invoke($"TeknoViper ROM root was not found at {romRoot}");
            if (!Directory.Exists(chdRoot))
                log?.Invoke($"TeknoViper CHD root was not found at {chdRoot}");

            return new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir
            };
        }

        // ---------- PCSX2 ----------

        private static ProcessStartInfo BuildPcsx2x6(GameProfile profile, bool windowed, Action<string> log)
        {
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "pcsx2x6", "TeknoParrot", "inis", "PCSX2.ini");

            try
            {
                var hideCursor = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "HideCursor")?.FieldValue == "1";
                var configValues = new Dictionary<string, string>
                {
                    ["Renderer"] = GetPcsx2GraphicsBackendValue(profile),
                    ["upscale_multiplier"] = GetPcsx2ResolutionFactorValue(profile),
                    ["HideMouseCursor"] = hideCursor ? "true" : "false",
                    ["StartFullscreen"] = windowed ? "false" : "true",
                };

                CreateOrUpdatePcsx2x6Config(configPath, configValues);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error updating pcsx2x6 config: {ex.Message}");
            }

            var parameters = new List<string> { $"{profile.GamePath}" };
            if (!windowed)
                parameters.Add("-fullscreen");
            parameters.Add("-batch");
            parameters.Add("-nogui");

            var exe = profile.ConfigValues.Any(x => x.FieldName == "UseAVX2" && x.FieldValue == "1")
                ? @".\pcsx2x6\pcsx2-qtx64-avx2.exe"
                : @".\pcsx2x6\pcsx2-qtx64.exe";

            return new ProcessStartInfo(exe, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "pcsx2x6")
            };
        }

        private static string GetPcsx2GraphicsBackendValue(GameProfile profile)
        {
            var backend = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Graphics Backend")?.FieldValue;
            switch (backend)
            {
                case "Direct3D 11 (Legacy)": return "3";
                case "OpenGL": return "12";
                case "Software Renderer": return "13";
                case "Vulkan": return "14";
                case "Direct3D 12": return "15";
                default: return "-1"; // Automatic
            }
        }

        private static string GetPcsx2ResolutionFactorValue(GameProfile profile)
        {
            var resolutionConfig = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Resolution");
            switch (resolutionConfig?.FieldValue)
            {
                case "Native": return "1";
                case "720p": return "2";
                case "1080p": return "3";
                case "1440p": return "4";
                case "1800p": return "5";
                case "2160p": return "6";
                case "2520p": return "7";
                case "2880p": return "8";
                case "3240p": return "9";
                case "3600p": return "10";
                case "3960p": return "11";
                case "4320p": return "12";
                default: return "1";
            }
        }

        private static void CreateOrUpdatePcsx2x6Config(string configPath, Dictionary<string, string> configValues)
        {
            if (!File.Exists(configPath))
            {
                Debug.WriteLine($"PCSX2.ini not found at {configPath}, skipping config update");
                return;
            }

            var lines = File.ReadAllLines(configPath).ToList();
            string currentSection = null;
            var updated = new HashSet<string>();
            var sectionIndices = new Dictionary<string, int>();
            int bigPictureLine = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    sectionIndices[currentSection] = i;
                    continue;
                }

                if (currentSection == null) continue;

                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;

                string key = trimmed.Substring(0, eq).Trim();
                if (key == "StartBigPictureMode")
                {
                    bigPictureLine = i; // remove and re-insert at end of [UI]
                }
                else if (configValues.ContainsKey(key))
                {
                    lines[i] = $"{key} = {configValues[key]}";
                    updated.Add(key);
                }
            }

            // Remove existing StartBigPictureMode so we can re-insert at end of [UI]
            if (bigPictureLine >= 0)
            {
                lines.RemoveAt(bigPictureLine);
                // Rebuild section indices after removal
                sectionIndices.Clear();
                string sec = null;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        sec = t.Substring(1, t.Length - 2);
                        sectionIndices[sec] = i;
                    }
                }
            }

            // Find the last line index of a section (index of next section header, or end of list)
            int FindSectionEnd(int headerIdx)
            {
                for (int i = headerIdx + 1; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                        return i;
                }
                return lines.Count;
            }

            // Always insert StartBigPictureMode = false at the very end of [UI]
            if (sectionIndices.TryGetValue("UI", out int uiIdx))
            {
                lines.Insert(FindSectionEnd(uiIdx), "StartBigPictureMode = false");
            }
            else
            {
                lines.Add("[UI]");
                lines.Add("StartBigPictureMode = false");
            }

            File.WriteAllLines(configPath, lines);
        }

        // ---------- RPCS3 ----------

        private static ProcessStartInfo BuildRpcs3(GameProfile profile, bool windowed, Action<string> log)
        {
            ConfigureRPCS3(profile, windowed, log);

            var parameters = new List<string> { "--no-gui", "--allow-any-location" };
            if (!windowed)
                parameters.Add("--fullscreen");
            var workDir = Path.Combine(Directory.GetCurrentDirectory(), "RPCS3");
            parameters.Add($"\"{profile.GamePath}\"");

            return new ProcessStartInfo(@".\RPCS3\rpcs3.exe", string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir
            };
        }

        private static void ConfigureRPCS3(GameProfile profile, bool windowed, Action<string> log)
        {
            string configPath = Path.Combine(".", "RPCS3", "Config", "config.yml");

            try
            {
                if (!File.Exists(configPath))
                {
                    Debug.WriteLine("RPCS3 config.yml not found, skipping configuration");
                    return;
                }

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithIndentedSequences()
                    .Build();

                var yamlContent = File.ReadAllText(configPath);
                var config = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                ApplyManualRPCS3Settings(profile, config, windowed);

                // Values taken from GameProfile XML, per game stuff basically
                ApplyProfileRPCS3Settings(profile, config);

                // Fix hdd serial related errors
                ApplyHddFixRPCS3Settings(profile);

                var updatedYaml = serializer.Serialize(config);
                File.WriteAllText(configPath, updatedYaml);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error updating RPCS3 config: {ex.Message}");
            }
        }

        private static void ApplyHddFixRPCS3Settings(GameProfile profile)
        {
            string hddFixPath;
            switch (profile.ProfileName)
            {
                case "DSPS":
                case "RazingStorm":
                    hddFixPath = Path.Combine(Path.GetDirectoryName(profile.GamePath), "s357secr.bin");
                    break;
                default:
                    hddFixPath = Path.Combine(Path.GetDirectoryName(profile.GamePath), "s357security.bin");
                    break;
            }
            if (File.Exists(hddFixPath))
            {
                File.Delete(hddFixPath);
                File.WriteAllText(hddFixPath, "");
            }
        }

        private static void ApplyManualRPCS3Settings(GameProfile profile, Dictionary<string, object> config, bool windowed)
        {
            if (!config.ContainsKey("Video"))
                config["Video"] = new Dictionary<object, object>();

            var videoSection = (Dictionary<object, object>)config["Video"];

            videoSection["Fullscreen"] = !windowed;
            videoSection["Frame limit"] = GetRPCS3FrameLimit(profile);
            videoSection["Renderer"] = GetRPCS3Renderer(profile);
            videoSection["Resolution Scale"] = GetRPCS3ResolutionScale(profile);

            var useVsync = profile.ConfigValues.Any(x => x.FieldName == "Enable VSync" && x.FieldValue == "1");
            videoSection["VSync"] = useVsync;

            if (!config.ContainsKey("Miscellaneous"))
                config["Miscellaneous"] = new Dictionary<object, object>();

            var miscSection = (Dictionary<object, object>)config["Miscellaneous"];
            miscSection["Show mouse and keyboard toggle hint"] = false;
            miscSection["Show capture hints"] = false;

            if (!config.ContainsKey("Core"))
                config["Core"] = new Dictionary<object, object>();

            var coreSection = (Dictionary<object, object>)config["Core"];
            // Apparently might help with stability?
            coreSection["Enable TSX"] = "Disabled";

            ConfigureRPCS3GuiSettings(profile);
        }

        private static void ConfigureRPCS3GuiSettings(GameProfile profile)
        {
            string guiConfigPath = Path.Combine(".", "RPCS3", "GuiConfigs", "CurrentSettings.ini");
            var hideCursor = profile.ConfigValues.Any(x => x.FieldName == "Hide Cursor" && x.FieldValue == "1");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(guiConfigPath));
                // Disable double clicking for fullscreen/windowed mode toggle, kinda interferes with lightguns
                WriteIniValue(guiConfigPath, "GSFrame", "disableMouse", "true");
                WriteIniValue(guiConfigPath, "GSFrame", "hideMouseGlobal", hideCursor ? "true" : "false");
                WriteIniValue(guiConfigPath, "GSFrame", "lockMouseInFullscreen", "false");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating RPCS3 GUI config: {ex.Message}");
            }
        }

        private static void ApplyProfileRPCS3Settings(GameProfile profile, Dictionary<string, object> config)
        {
            var rpcs3Config = GetRPCS3ConfigFromGameProfile(profile);

            if (rpcs3Config == null || !rpcs3Config.Any())
                return;

            foreach (var section in rpcs3Config)
            {
                try
                {
                    string sectionName = section.Key;

                    if (!config.ContainsKey(sectionName))
                        config[sectionName] = new Dictionary<object, object>();

                    var yamlSection = (Dictionary<object, object>)config[sectionName];

                    foreach (var setting in section.Value)
                        yamlSection[setting.Key] = ConvertRPCS3Value(setting.Value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying RPCS3 section {section.Key}: {ex.Message}");
                }
            }
        }

        private static Dictionary<string, Dictionary<string, string>> GetRPCS3ConfigFromGameProfile(GameProfile profile)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            if (profile?.RPCS3Config?.ConfigItems == null || !profile.RPCS3Config.ConfigItems.Any())
                return result;

            var groupedItems = profile.RPCS3Config.ConfigItems
                .Where(item => !string.IsNullOrEmpty(item.Category) && !string.IsNullOrEmpty(item.Name) && !string.IsNullOrEmpty(item.Value))
                .GroupBy(item => item.Category);

            foreach (var categoryGroup in groupedItems)
            {
                var categorySettings = new Dictionary<string, string>();

                foreach (var configItem in categoryGroup)
                    categorySettings[configItem.Name] = configItem.Value;

                if (categorySettings.Any())
                    result[categoryGroup.Key] = categorySettings;
            }

            return result;
        }

        private static object ConvertRPCS3Value(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.ToLower() == "true") return true;
            if (value.ToLower() == "false") return false;

            if (int.TryParse(value, out int intValue)) return intValue;
            if (double.TryParse(value, out double doubleValue)) return doubleValue;

            return value;
        }

        private static string GetRPCS3FrameLimit(GameProfile profile)
        {
            string frameLimit;

            switch (profile.ProfileName)
            {
                case "AKB48":
                    frameLimit = "30";
                    break;
                default: // for most games
                    frameLimit = "Auto";
                    break;
            }

            // Check if there's a custom frame limit in config values
            var customFrameLimit = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Frame Limit");
            if (customFrameLimit != null && !string.IsNullOrEmpty(customFrameLimit.FieldValue))
                frameLimit = customFrameLimit.FieldValue;

            return frameLimit;
        }

        private static string GetRPCS3Renderer(GameProfile profile)
        {
            if (profile.ConfigValues.Any(x => x.FieldName == "Graphics Backend" && x.FieldValue == "Vulkan"))
                return "Vulkan";
            return "OpenGL";
        }

        private static string GetRPCS3ResolutionScale(GameProfile profile)
        {
            string resolutionScale = "100";
            var customResScale = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Resolution Scale");
            if (customResScale != null && !string.IsNullOrEmpty(customResScale.FieldValue))
                resolutionScale = customResScale.FieldValue;

            return resolutionScale;
        }

        // ---------- Cxbx-Reloaded (Chihiro) ----------

        private static ProcessStartInfo BuildCxbxr(GameProfile profile, bool windowed, Action<string> log)
        {
            ConfigureCxbxr(profile, log);

            var parameters = new List<string> { windowed ? "/win" : "/fs" };
            var workDir = Path.Combine(Directory.GetCurrentDirectory(), "cxbxr");
            parameters.Add($"/load \"{profile.GamePath}\" /chihiro");

            return new ProcessStartInfo(Path.Combine(workDir, "cxbxr-ldr.exe"), string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir
            };
        }

        private static void ConfigureCxbxr(GameProfile profile, Action<string> log)
        {
            try
            {
                string cxbxrDir = Path.Combine(Directory.GetCurrentDirectory(), "cxbxr");

                // Ensure required directories exist
                string emuMediaBoardDir = Path.Combine(cxbxrDir, "TeknoParrot", "EmuMediaBoard");
                string chihiroDir = Path.Combine(emuMediaBoardDir, "Chihiro");

                Directory.CreateDirectory(emuMediaBoardDir);
                Directory.CreateDirectory(chihiroDir);

                // Create empty settings.ini if it doesn't exist
                string settingsPath = Path.Combine(cxbxrDir, "TeknoParrot", "settings.ini");
                if (!File.Exists(settingsPath))
                    File.Create(settingsPath).Dispose();

                // Check for required Chihiro EEPROM files
                string[] chihiroFiles =
                {
                    "ic10_g24lc64.bin",
                    "pc20_g24lc64.bin",
                    "ic11_24lc024.bin"
                };

                // Check for required EmuMediaBoard flash file
                string[] mediaBoardFiles =
                {
                    "fpr21042_m29w160et.bin"
                };

                var missingFiles = new List<string>();
                foreach (var file in chihiroFiles)
                {
                    if (!File.Exists(Path.Combine(chihiroDir, file)))
                        missingFiles.Add(Path.Combine(chihiroDir, file));
                }
                foreach (var file in mediaBoardFiles)
                {
                    if (!File.Exists(Path.Combine(emuMediaBoardDir, file)))
                        missingFiles.Add(Path.Combine(emuMediaBoardDir, file));
                }

                if (missingFiles.Count > 0)
                {
                    string missingList = string.Join("\n", missingFiles);
                    log?.Invoke($"The following bios files are missing:\n\n{missingList}\n\nPlease acquire these files yourself and place them in the correct directories.");
                    return;
                }

                // Patch region byte in ic10_g24lc64.bin at offset 0x1F00
                var regionConfig = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Region");
                if (regionConfig != null)
                {
                    byte regionByte = 0x01; // default to Japan
                    switch (regionConfig.FieldValue)
                    {
                        case "JAPAN":
                            regionByte = 0x01;
                            break;
                        case "USA":
                            regionByte = 0x02;
                            break;
                        case "EXPORT":
                            regionByte = 0x03;
                            break;
                    }

                    string biosPath = Path.Combine(chihiroDir, "ic10_g24lc64.bin");
                    if (File.Exists(biosPath))
                    {
                        using (var fs = new FileStream(biosPath, FileMode.Open, FileAccess.Write))
                        {
                            fs.Seek(0x1F00, SeekOrigin.Begin);
                            fs.WriteByte(regionByte);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error configuring cxbxr: {ex.Message}");
            }
        }

        /// <summary>
        /// cxbxr-ldr re-launches itself — after the initial process exits, wait
        /// until no cxbxr-ldr processes remain (or kill them on force quit).
        /// </summary>
        public static void WaitForCxbxrChildren(Func<bool> forceQuit)
        {
            int notFoundCount = 0;
            while (notFoundCount < 3)
            {
                System.Threading.Thread.Sleep(500);
                bool found = false;
                try
                {
                    foreach (var p in Process.GetProcessesByName("cxbxr-ldr"))
                    {
                        found = true;
                        p.Dispose();
                        break;
                    }
                }
                catch
                {
                    // ignore access errors
                }

                if (found)
                {
                    notFoundCount = 0;

                    if (forceQuit())
                    {
                        try
                        {
                            foreach (var p in Process.GetProcessesByName("cxbxr-ldr"))
                            {
                                p.Kill();
                                p.Dispose();
                            }
                        }
                        catch { }
                        break;
                    }
                }
                else
                {
                    notFoundCount++;
                }
            }
        }

        // ---------- helpers ----------

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        private static void WriteIniValue(string path, string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, Path.GetFullPath(path));
        }
    }
}
