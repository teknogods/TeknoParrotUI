using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoGClubLauncher
    {
        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var c in value ?? "")
            {
                if (c == '\\') { ++slashes; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") => profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) => Setting(name, fallback ? "1" : "0") == "1" || Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoGClub");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select this game's merged ROM ZIP.", game);
            var romRoot = Path.GetDirectoryName(game);
            var set = profile.ProfileName;
            if (string.IsNullOrWhiteSpace(set) || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid GTI Club game profile");
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Resampling", "bicubic").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "area" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "state"));
            Directory.CreateDirectory(Path.Combine(root, "bezels"));
            // Cabinet lamps/meters are always published, independently of FFB.
            var args = new List<string> { "--rom-root", Quote(romRoot), "--state-root", Quote(Path.Combine(root, "state")), "--resolution-scale", scale, "--filter", filter, "--outputs" };
            var supportsFeedback = new[] { "gticlub", "gticlubu", "gticluba", "gticlubj" }.Contains(set);
            var ffbDevice = supportsFeedback
                ? GClubFfbDeviceProbe.GetLaunchSelection(Setting("Force Feedback Device", "off"))
                : "off";
            if (!int.TryParse(Setting("Force Feedback Strength", "35"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var ffbGain) || ffbGain < 0 || ffbGain > 100)
                ffbGain = 35;
            args.Add("--ffb-device"); args.Add(ffbDevice);
            args.Add("--ffb-gain"); args.Add(ffbGain.ToString(CultureInfo.InvariantCulture));
            if (supportsFeedback && Enabled("Invert Force Feedback")) args.Add("--ffb-invert-x");
            var fullscreen = Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase);
            if (fullscreen) args.Add("--fullscreen");
            if (fullscreen && Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (fullscreen && Enabled("Use Bezel", true)) args.Add("--bezels");
            var crt = Setting("CRT Shader", "None");
            args.Add("--crt");
            args.Add(crt == "Lottes Downsample" ? "lottes-downsample" : crt == "Lottes" ? "lottes" : "off");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Enable VR"))
            {
                args.Add("--renderer"); args.Add("openxr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            else if (Enabled("Widescreen")) args.Add("--widescreen");
            // Match Viper: LAN peers share a multicast port; TPOnline takes precedence.
            var tpOnline = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (!tpOnline)
            {
                if (!int.TryParse(Setting("Network Port", "0"), out var port) || port < 0 || port > 65535)
                    throw new ArgumentException("Network Port must be 0 (disabled) or 1 to 65535.");
                if (port != 0)
                {
                    if (!int.TryParse(Setting("Cabinet Count", "2"), out var count) || count < 2 || count > 4)
                        throw new ArgumentException("Cabinet Count must be 2 to 4.");
                    if (!int.TryParse(Setting("Cabinet ID", "1"), out var node) || node < 1 || node > count)
                        throw new ArgumentException("Cabinet ID must be unique and between 1 and Cabinet Count.");
                    var networkInterface = Setting("Network Interface", "auto").Trim();
                    if (string.IsNullOrEmpty(networkInterface)) networkInterface = "auto";
                    if (!networkInterface.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                        (!System.Net.IPAddress.TryParse(networkInterface, out var address) ||
                         address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                        throw new ArgumentException("Network Interface must be auto or a local IPv4 address.");
                    if (networkInterface.Equals("auto", StringComparison.OrdinalIgnoreCase)) networkInterface = "auto";
                    args.Add("--network-port"); args.Add(port.ToString());
                    args.Add("--network-node"); args.Add(node.ToString());
                    args.Add("--network-count"); args.Add(count.ToString());
                    args.Add("--network-interface"); args.Add(Quote(networkInterface));
                }
            }
            args.Add("--set"); args.Add(Quote(set));
            var exe = Path.Combine(root, "TeknoGClub.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoGClub executable is missing", exe);
            log?.Invoke($"TeknoGClub: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
