using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoS23Launcher
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
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoS23");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select this game's merged ROM ZIP.", game);
            var romRoot = Path.GetDirectoryName(game);
            var set = profile.ProfileName;
            if (string.IsNullOrWhiteSpace(set) || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid System 23 game profile");
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Resampling", "bicubic").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "area" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "state"));
            Directory.CreateDirectory(Path.Combine(root, "bezels"));
            var args = new List<string> { "--rom-root", Quote(romRoot), "--state-root", Quote(Path.Combine(root, "state")), "--scale", scale, "--present-filter", filter };
            var fullscreen = Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase);
            if (fullscreen) args.Add("--fullscreen");
            if (fullscreen && Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (fullscreen && Enabled("Use Bezel", true)) args.Add("--use-bezel");
            var crt = Setting("CRT Shader", "None");
            args.Add("--crt");
            args.Add(crt == "Lottes Downsample" ? "lottes-downsample" : crt == "Lottes" ? "lottes" : "off");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Enable VR"))
            {
                args.Add("--vr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            else if (Enabled("Widescreen")) args.Add("--experimental-widescreen");
            // TPOnline supplies TP_TPONLINE2 in the inherited environment, as for Viper.
            // Manual LAN peers use the native C422/GMEN transport in the same executable.
            if (Enabled("Enable LAN"))
            {
                foreach (var pair in new[] { new[] { "Cabinet ID", "--cabinet-node" }, new[] { "Cabinet Count", "--link-nodes" }, new[] { "Local Port", "--link-bind" }, new[] { "Session ID", "--link-session" } })
                { args.Add(pair[1]); args.Add(Quote(Setting(pair[0]))); }
                foreach (var peer in Setting("Peer Address").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                { args.Add("--link-peer"); args.Add(Quote(peer.Trim())); }
            }
            args.Add(Quote(set));
            var exe = Path.Combine(root, "TeknoS23.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoS23 executable is missing", exe);
            log?.Invoke($"TeknoS23: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
