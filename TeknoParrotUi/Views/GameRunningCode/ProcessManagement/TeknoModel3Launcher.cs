using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoModel3Launcher
    {
        private static readonly Dictionary<string, int> MaxLinkNodes =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "daytona2", 8 }, { "dayto2pe", 8 }, { "scudplus", 8 },
                { "lemans24", 6 },
                { "harley", 4 }, { "skichamp", 4 }, { "srally2", 4 },
                { "dirtdvls", 4 }, { "spikeout", 4 }, { "spikeofe", 4 },
                { "von2", 2 }
            };

        private static readonly HashSet<string> NativeReticleGuns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lamachin", "oceanhun" };

        private static readonly string[] Filters = { "nearest", "linear", "bicubic", "lanczos", "ssaa" };
        private static readonly Dictionary<string, string> VSyncModes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "G-Sync / FreeSync monitor", "fifo" },
                { "Off (may tear)", "immediate" }
            };
        private static readonly string[] Regions = { "Japan", "USA", "Export", "Australia", "Korea", "Asia" };

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var c in value ?? string.Empty)
            {
                if (c == '\\') { ++slashes; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        internal static string SetId(GameProfile profile)
        {
            var name = profile.ProfileName ?? string.Empty;
            return name.EndsWith("_m3", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 3)
                : name;
        }

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            bool HasSetting(string name) =>
                profile.ConfigValues?.Any(x => x.FieldName == name) == true;
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            int Percent(string name, int minimum = 0, int maximum = 100, int fallback = 100)
            {
                if (!int.TryParse(Setting(name, fallback.ToString(CultureInfo.InvariantCulture)),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                    value < minimum || value > maximum)
                    value = fallback;
                return value;
            }
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            string VrDepth(int percent) => (percent / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            var vr = Enabled("Enable VR");

            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoModel3");
            string Resolve(string value) => Path.GetFullPath(
                Path.IsPathRooted(value) ? value : Path.Combine(uiRoot, value));

            var selectedRom = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selectedRom))
                throw new InvalidOperationException($"Select {profile.ExecutableName} as the game executable.");
            var romPath = Resolve(selectedRom);
            var romRoot = Directory.Exists(romPath) ? romPath : Path.GetDirectoryName(romPath);

            var set = SetId(profile);
            var stateDir = Path.Combine(workDir, "nvram", set);
            Directory.CreateDirectory(stateDir);

            var scale = Setting("Internal Resolution", "4").Trim();
            if (scale != "1" && scale != "2" && scale != "4" && scale != "8") scale = "4";
            var filter = Setting("Presentation Filter", "linear").Trim().ToLowerInvariant();
            if (!Filters.Contains(filter)) filter = "linear";

            var parameters = new List<string>
            {
                "--run", Quote(romRoot), set, "unlimited",
                vr ? "--vr" : "--vulkan", "--live-audio", "--native-ppc",
                "--state-dir=" + Quote(stateDir),
                "--internal-scale=" + scale,
                "--presentation-filter=" + filter
            };

            var crt = Setting("CRT Shader", "None").Trim();
            if (crt.Equals("Lottes", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--crt=lottes");
            else if (crt.Equals("Lottes Downsample", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--crt=lottes-downsample");
            if (Enabled("Use Bezel")) parameters.Add("--bezels");
            if (Enabled("Stretch to Fullscreen")) parameters.Add("--stretch");
            if (!vr && Enabled("Widescreen")) parameters.Add("--widescreen");
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase))
                parameters.Add("--fullscreen");
            if (!vr && VSyncModes.TryGetValue(Setting("VSync").Trim(), out var sync))
                parameters.Add("--presentation-sync=" + sync);

            var region = Setting("Region", "Default").Trim();
            if (Regions.Contains(region, StringComparer.OrdinalIgnoreCase))
                parameters.Add("--region=" + region.ToLowerInvariant());

            if (profile.GunGame)
            {
                var nativeReticles = NativeReticleGuns.Contains(set);
                if (nativeReticles || !Enabled("Crosshairs", true)) parameters.Add("--no-crosshairs");
                if (!nativeReticles && !Enabled("Hide Crosshairs after Inactivity", true))
                    parameters.Add("--no-crosshair-autohide");
            }

            if (Enabled("Mute Audio")) parameters.Add("--mute");
            if (vr)
            {
                parameters.Add("--vr-depth=" + VrDepth(Percent("VR Depth", 100, 200, 150)));
                if (!Enabled("Use VR Controls", true)) parameters.Add("--no-vr-controls");
            }
            parameters.Add("--outputs");

            var ffbDevice = HasSetting("Force Feedback Device")
                ? TeknoParrotUi.Helpers.Model3FfbDeviceProbe.GetLaunchSelection(Setting("Force Feedback Device", "off"))
                : "off";
            var ffbDevice2 = HasSetting("Player 2 Force Feedback Device")
                ? TeknoParrotUi.Helpers.Model3FfbDeviceProbe.GetLaunchSelection(Setting("Player 2 Force Feedback Device", "off"))
                : "off";
            bool Selected(string device) => !string.IsNullOrWhiteSpace(device) &&
                !device.Equals("off", StringComparison.OrdinalIgnoreCase);
            if (Selected(ffbDevice) || Selected(ffbDevice2))
            {
                if (Selected(ffbDevice)) parameters.Add("--ffb-device=" + ffbDevice);
                if (Selected(ffbDevice2)) parameters.Add("--ffb-device2=" + ffbDevice2);
                parameters.Add("--ffb-gain=" + Number(Percent("Force Feedback Strength")));
                if (HasSetting("Enable Spring Effect"))
                {
                    var springMode = Setting("Force Feedback Spring Effect", "Spring");
                    parameters.Add("--ffb-spring-mode=" +
                        (springMode == "Spring using Constant Force" || springMode == "Constant Spring"
                            ? "constant" : "spring"));
                    parameters.Add("--ffb-spring-gain=" + Number(
                        Enabled("Enable Spring Effect", true) ? Percent("Spring Effect Strength") : 0));
                }
                if (HasSetting("Enable Constant Effect"))
                    parameters.Add("--ffb-constant-gain=" + Number(
                        Enabled("Enable Constant Effect", true) ? Percent("Constant Effect Strength") : 0));
                if (HasSetting("Enable Friction Effect"))
                    parameters.Add("--ffb-friction-gain=" + Number(
                        Enabled("Enable Friction Effect", true) ? Percent("Friction Effect Strength", 0, 100, 30) : 0));
                if (HasSetting("Enable Recoil Effect"))
                    parameters.Add("--ffb-recoil-gain=" + Number(
                        Enabled("Enable Recoil Effect", true) ? Percent("Recoil Effect Strength") : 0));
                if (HasSetting("Player 2 Enable Recoil Effect"))
                    parameters.Add("--ffb-recoil-gain-p2=" + Number(
                        Enabled("Player 2 Enable Recoil Effect", true) ? Percent("Player 2 Recoil Effect Strength") : 0));
                if (HasSetting("Enable Sine Effect"))
                {
                    parameters.Add("--ffb-vibration-gain=" + Number(
                        Enabled("Enable Sine Effect", true) ? Percent("Sine Effect Strength") : 0));
                    parameters.Add("--ffb-sine-period=" + Number(Percent("Sine Effect Period", 10, 200, 40)));
                }
            }

            var linkCapable = MaxLinkNodes.TryGetValue(set, out var maxNodes);
            var tpOnline = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            var lan = linkCapable && Enabled("Enable LAN");
            if (!tpOnline && lan)
            {
                if (!int.TryParse(Setting("Network Port", "42003"), out var port) || port < 1 || port > 65535)
                    throw new ArgumentException("Network Port must be between 1 and 65535.");
                if (!int.TryParse(Setting("Cabinet Count", "2"), out var count) || count < 2 || count > maxNodes)
                    throw new ArgumentException($"Cabinet Count must be between 2 and {maxNodes} for this game.");
                if (!int.TryParse(Setting("Cabinet Number", "1"), out var node) || node < 1 || node > count)
                    throw new ArgumentException("Cabinet Number must be unique and between 1 and Cabinet Count.");
                var networkInterface = Setting("Network Interface", "auto").Trim();
                if (string.IsNullOrWhiteSpace(networkInterface) ||
                    networkInterface.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    networkInterface = "auto";
                else if (!System.Net.IPAddress.TryParse(networkInterface, out var address) ||
                         address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    throw new ArgumentException("Network Interface must be auto or a local IPv4 address.");
                parameters.Add("--network-port=" + Number(port));
                parameters.Add("--network-node=" + Number(node));
                parameters.Add("--network-nodes=" + Number(count));
                parameters.Add("--network-interface=" + networkInterface);
            }
            if (linkCapable && (tpOnline || lan) && Enabled("Network Diagnostics", true))
                parameters.Add("--network-diagnostics");
            if (HasSetting("Set Game Link Settings") && !Enabled("Set Game Link Settings", true))
                parameters.Add("--no-link-settings");
            // TeknoModel3 loads Score Submission itself (there is no loader DLL).
            var scoreSubmission = Enabled("Enable Submission");
            if (scoreSubmission) parameters.Add("--score-submission");

            var executable = Path.Combine(workDir, "TeknoModel3.exe");
            log?.Invoke($"TeknoModel3: {set}, rom root={romRoot}, renderer={(vr ? "openxr" : "vulkan")}");
            if (!File.Exists(executable)) log?.Invoke($"TeknoModel3 executable was not found at {executable}");
            if (!File.Exists(romPath) && !Directory.Exists(romPath))
                log?.Invoke($"TeknoModel3 ROM was not found at {romPath}");
            var info = new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                UseShellExecute = false,
                WorkingDirectory = workDir,
                RedirectStandardError = true
            };
            if (scoreSubmission) TeknoViperVegasLauncher.AddScoreSubmissionEnvironment(profile, info);
            return info;
        }
    }
}
