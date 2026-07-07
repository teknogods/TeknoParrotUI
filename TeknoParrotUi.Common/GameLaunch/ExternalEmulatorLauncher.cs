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
                ConfigureDolphinIni(true);
            }
            else
            {
                ConfigureDolphinIni(false);

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

            return new ProcessStartInfo(@".\CrediarDolphin\Dolphin.exe", string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CrediarDolphin")
            };
        }

        private static void ConfigureDolphinIni(bool isTatsuvscap)
        {
            string configPath = Path.Combine(".", "CrediarDolphin", "User", "Config", "Dolphin.ini");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                var lines = new List<string>();
                bool coreSection = false;
                bool foundCore = false;

                var coreSettings = new Dictionary<string, string>
                {
                    {"SIDevice0", "11"},
                    {"SIDevice1", "6"},
                    {"SIDevice2", "0"},
                    {"SIDevice3", "0"},
                    {"SerialPort1", isTatsuvscap ? "255" : "6"},
                    {"SlotA", "14"},
                    {"SlotB", "14"},
                    {"MEM1Size", "0x04000000"},
                    {"MEM2Size", "0x08000000"},
                    {"RAMOverrideEnable", isTatsuvscap ? "True" : "False"}
                };

                var processedSettings = new HashSet<string>();
                bool foundAnalytics = false;
                bool inAnalyticsSection = false;
                bool analyticsEnabledSet = false;
                bool permissionAskedSet = false;

                if (File.Exists(configPath))
                {
                    var existingLines = File.ReadAllLines(configPath);

                    for (int i = 0; i < existingLines.Length; i++)
                    {
                        string line = existingLines[i];
                        string trimmedLine = line.Trim();

                        if (trimmedLine == "[Core]")
                        {
                            coreSection = true;
                            foundCore = true;
                            lines.Add(line);
                            continue;
                        }

                        if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                        {
                            if (coreSection)
                            {
                                foreach (var setting in coreSettings)
                                {
                                    if (!processedSettings.Contains(setting.Key))
                                        lines.Add($"{setting.Key} = {setting.Value}");
                                }
                            }

                            coreSection = false;

                            inAnalyticsSection = trimmedLine == "[Analytics]";
                            if (inAnalyticsSection)
                                foundAnalytics = true;

                            lines.Add(line);
                            continue;
                        }

                        if (coreSection && trimmedLine.Contains("="))
                        {
                            string key = trimmedLine.Split('=')[0].Trim();
                            if (coreSettings.ContainsKey(key))
                            {
                                lines.Add($"{key} = {coreSettings[key]}");
                                processedSettings.Add(key);
                                continue;
                            }
                        }

                        if (inAnalyticsSection)
                        {
                            if (trimmedLine.StartsWith("ID"))
                                continue; // Skip the ID line

                            if (trimmedLine.StartsWith("Enabled"))
                            {
                                lines.Add("Enabled = False");
                                analyticsEnabledSet = true;
                                continue;
                            }

                            if (trimmedLine.StartsWith("PermissionAsked"))
                            {
                                lines.Add("PermissionAsked = True");
                                permissionAskedSet = true;
                                continue;
                            }
                        }

                        lines.Add(line);
                    }

                    if (coreSection)
                    {
                        foreach (var setting in coreSettings)
                        {
                            if (!processedSettings.Contains(setting.Key))
                                lines.Add($"{setting.Key} = {setting.Value}");
                        }
                    }
                }

                if (!foundCore)
                {
                    lines.Add("[Core]");
                    foreach (var setting in coreSettings)
                        lines.Add($"{setting.Key} = {setting.Value}");
                }

                if (!foundAnalytics)
                {
                    lines.Add("");
                    lines.Add("[Analytics]");
                    lines.Add("Enabled = False");
                    lines.Add("PermissionAsked = True");
                }
                else
                {
                    // Add missing keys to existing [Analytics] section
                    int insertIndex = lines.FindIndex(l => l.Trim() == "[Analytics]") + 1;

                    if (!analyticsEnabledSet)
                        lines.Insert(insertIndex++, "Enabled = False");

                    if (!permissionAskedSet)
                        lines.Insert(insertIndex, "PermissionAsked = True");
                }

                File.WriteAllLines(configPath, lines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating Dolphin config: {ex.Message}");
            }
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
