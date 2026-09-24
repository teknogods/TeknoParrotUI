using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Common.GameLaunch
{
    internal static class TeknoS11Launcher
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
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoS11");
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected)) throw new ArgumentException("Select the game's merged ROM ZIP.");
            var game = Path.GetFullPath(selected);
            if (!File.Exists(game) || !Path.GetExtension(game).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Select the game's merged ROM ZIP.", game);
            var set = profile.ProfileName;
            if (string.IsNullOrWhiteSpace(set) || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid TeknoS11 set name");
            var scale = Setting("Internal Resolution", "1");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Filter", "area").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "area", "bicubic", "lanczos" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            var rotation = Setting("Rotation", set.Equals("nflclsfb", StringComparison.OrdinalIgnoreCase) ? "270" : "auto").ToLowerInvariant();
            if (!new[] { "auto", "original", "0", "90", "180", "270" }.Contains(rotation)) throw new ArgumentException("Invalid rotation");
            var crt = Setting("CRT Shader", "none").ToLowerInvariant();
            if (crt != "none" && crt != "lottes") throw new ArgumentException("Invalid CRT shader");
            var args = new List<string> { "--set", Quote(set), "--rom-root", Quote(Path.GetDirectoryName(game)),
                "--state-root", Quote(Path.Combine(root, "state")), "--internal-resolution", scale, "--filter", filter,
                "--crt-shader", crt, "--rotation", rotation, "--renderer", Enabled("Enable VR") ? "openxr" : "vulkan" };
            if (profile.HasTwoExecutables)
            {
                if (string.IsNullOrWhiteSpace(profile.GamePath2)) throw new ArgumentException("Select the game's CHD file.");
                var disk = Path.GetFullPath(profile.GamePath2);
                if (!File.Exists(disk) || !Path.GetExtension(disk).Equals(".chd", StringComparison.OrdinalIgnoreCase))
                    throw new FileNotFoundException("Select the game's CHD file.", disk);
                // The selected set directory contains all discs; the loader also
                // searches parent set names in the surrounding CHD collection.
                var directory = Path.GetDirectoryName(disk);
                var parent = Directory.GetParent(directory)?.FullName;
                args.Add("--chd-root");
                args.Add(Quote(string.Equals(Path.GetFileName(directory), set, StringComparison.OrdinalIgnoreCase) ? parent ?? directory : directory));
            }
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase)) args.Add("--fullscreen");
            if (Enabled("Stretch To Fullscreen")) args.Add("--stretch");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Crosshair Auto Hide", true)) args.Add("--no-crosshair-autohide");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (Enabled("Enable VR")) {
                var strength=Setting("Stereo Strength", "150");
                if (!int.TryParse(strength,out var percentage) || percentage < 100 || percentage > 200) throw new ArgumentException("Stereo Strength must be 100 to 200");
                args.Add("--stereo-strength"); args.Add(strength);
                if (!Enabled("Use VR Controls",true)) args.Add("--no-vr-controls");
            }
            if (Enabled("Enable LAN") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"))) {
                var node=Setting("Cabinet ID","1");
                if(node!="1" && node!="2")throw new ArgumentException("Cabinet ID must be 1 or 2");
                args.Add("--link-node"); args.Add(node);
                args.Add("--cabinet-id");args.Add("cabinet-"+node);
                args.Add("--link-session");args.Add(Quote(Setting("Link Session","default")));
            }
            var exe=Path.Combine(root,"TeknoS11.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoS11 executable is missing",exe);
            log?.Invoke($"TeknoS11: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe,string.Join(" ",args)) {
                WorkingDirectory=root, UseShellExecute=false, RedirectStandardError=true,
                StandardErrorEncoding=Encoding.UTF8
            };
        }
    }
}
