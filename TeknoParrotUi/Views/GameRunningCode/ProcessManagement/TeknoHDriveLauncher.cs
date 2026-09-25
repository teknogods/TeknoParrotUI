using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoHDriveLauncher
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
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoHDrive");
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected)) throw new ArgumentException("Select the game's merged ROM ZIP.");
            var game = Path.GetFullPath(selected);
            if (!File.Exists(game) || !Path.GetExtension(game).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Select the game's merged ROM ZIP.", game);
            var set = profile.ProfileName;
            if (!new[] { "harddriv", "harddrivc", "racedriv", "racedrivc", "racedrivpan", "stunrunj", "steeltal", "strtdriv", "hdrivair" }.Contains(set))
                throw new ArgumentException("Invalid TeknoHDrive set name");
            var scale = Setting("Internal Resolution", "1");
            if (!int.TryParse(scale, out var resolution) || !new[] { 1, 2, 4, 8 }.Contains(resolution)) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Filter", "area").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "area", "bicubic" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            var vr = Enabled("Enable VR");
            var args = new List<string> { "--set", Quote(set), "--rom-root", Quote(Path.GetDirectoryName(game)),
                "--state-root", Quote(Path.Combine(root, "state")), "--internal-scale", scale, "--filter", vr ? "linear" : filter,
                "--renderer", vr ? "openxr" : "vulkan", "--input", "keyboard", "--outputs", "--unattended" };
            if (!vr || !Enabled("Use VR Controls", true)) args.Add("--tp-input");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase)) args.Add("--fullscreen");
            if (Enabled("Stretch To Fullscreen")) args.Add("--stretch");
            if (Enabled("CRT Shader") && !vr) args.Add("--crt");
            if (Enabled("Use Bezel")) {
                var bezel = Path.Combine(root, "bezels", set + ".png");
                if (!File.Exists(bezel)) bezel = Path.Combine(root, "bezels", "default.png");
                if (File.Exists(bezel)) { args.Add("--bezel"); args.Add(Quote(bezel)); }
            }
            if (!Enabled("VSync", true)) { args.Add("--vsync"); args.Add("off"); }
            // HDrive exposes one combined steering-force channel on driving cabinets.
            if (set != "steeltal" && set != "stunrunj")
            {
                var device = Setting("Force Feedback Device", "off").Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(device, @"\A(?:off|(?:wheel|gamepad):[0-9]+)\z"))
                    device = "off";
                int Strength(string name) => int.TryParse(Setting(name, "100"), out var value) &&
                    value >= 0 && value <= 100 ? value : 100;
                args.AddRange(new[] { "--ffb-device", device, "--ffb-gain",
                    Strength("Force Feedback Strength").ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--ffb-constant-gain", (Enabled("Enable Constant Effect", true)
                        ? Strength("Constant Effect Strength") : 0).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            var exe=Path.Combine(root,"TeknoHDrive.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoHDrive executable is missing",exe);
            log?.Invoke($"TeknoHDrive: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe,string.Join(" ",args)) {
                WorkingDirectory=root, UseShellExecute=false, RedirectStandardError=true,
                StandardErrorEncoding=Encoding.UTF8
            };
        }
    }
}
