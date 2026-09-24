using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Common.GameLaunch
{
    internal static class TeknoTPJCLauncher
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
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoTPJC");
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected)) throw new ArgumentException("Select the game's merged ROM ZIP.");
            var game = Path.GetFullPath(selected);
            if (!File.Exists(game) || !Path.GetExtension(game).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Select the game's merged ROM ZIP.", game);
            var set = profile.ProfileName;
            if (set != "optiger" && set != "optigersm") throw new ArgumentException("Invalid TeknoTPJC set name");
            var scale = Setting("Internal Resolution", "1");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Filter", "area").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "area", "bicubic" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            var crt = Setting("CRT Shader", "none").ToLowerInvariant();
            if (crt != "none" && crt != "lottes" && crt != "lottes-ssaa") throw new ArgumentException("Invalid CRT shader");
            var args = new List<string> { "--run", "--set", Quote(set), "--rom-root", Quote(Path.GetDirectoryName(game)),
                "--save-dir", Quote(Path.Combine(root, "state")), "--resolution-scale", scale, "--presentation-filter", filter,
                "--crt", crt, "--renderer", "vulkan", "--audio", Enabled("Mute Audio") ? "off" : "on" };
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase)) args.Add("--fullscreen");
            if (Enabled("Stretch To Fullscreen")) { args.Add("--presentation-layout"); args.Add("stretch"); }
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Enable VR")) {
                var strength=Setting("Stereo Strength", "100");
                if (!int.TryParse(strength,out var percentage) || percentage < 100 || percentage > 200) throw new ArgumentException("Stereo Strength must be 100 to 200");
                args.Add("--stereo"); args.Add("openxr");
                args.Add("--stereo-strength"); args.Add(strength);
                if (!Enabled("Use VR Controls",true)) args.Add("--no-vr-controls");
            }
            var exe=Path.Combine(root,"TeknoTPJC.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoTPJC executable is missing",exe);
            log?.Invoke($"TeknoTPJC: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe,string.Join(" ",args)) {
                WorkingDirectory=root, UseShellExecute=false, RedirectStandardError=true,
                StandardErrorEncoding=Encoding.UTF8
            };
        }
    }
}
