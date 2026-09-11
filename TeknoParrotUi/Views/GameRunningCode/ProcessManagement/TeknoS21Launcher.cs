using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoS21Launcher
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
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoS21");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select this game's merged ROM ZIP.", game);
            var romRoot = Path.GetDirectoryName(game);
            var set = profile.ProfileName;
            if (string.IsNullOrWhiteSpace(set) || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid System 21 game profile");
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Resampling", "bicubic").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "area" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "state"));
            Directory.CreateDirectory(Path.Combine(root, "bezels"));
            var args = new List<string> { "--game", Quote(set), "--rom-dir", Quote(romRoot), "--save-dir", Quote(Path.Combine(root, "state")), "--scale", scale, "--present-filter", filter };
            var fullscreen = Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase);
            if (fullscreen) args.Add("--fullscreen");
            if (fullscreen && Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (fullscreen && Enabled("Use Bezel", true)) args.Add("--use-bezel");
            var crt = Setting("CRT Shader", "None");
            args.Add("--crt");
            args.Add(crt == "Lottes Downsample" ? "lottes-downsample" : crt == "Lottes" ? "lottes" : "off");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Enable VR"))
            {
                args.Add("--vr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            // Viper's inherited TP_TPONLINE2 selects lobby cabinet IDs automatically.
            if (Enabled("Enable LAN") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2")))
            {
                args.Add("--link-node"); args.Add(Quote(Setting("Cabinet ID", "1")));
                args.Add("--link-count"); args.Add(Quote(Setting("Cabinet Count", "2")));
            }
            var exe = Path.Combine(root, "TeknoS21.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoS21 executable is missing", exe);
            log?.Invoke($"TeknoS21: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
