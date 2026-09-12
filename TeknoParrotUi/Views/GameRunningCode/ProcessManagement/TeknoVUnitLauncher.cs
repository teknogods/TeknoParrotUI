using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoVUnitLauncher
    {
        // Windows CommandLineToArgvW quoting, including trailing backslashes.
        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value ?? "")
            {
                if (c == '\\') { ++slashes; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") => profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) => Setting(name, fallback ? "1" : "0") == "1" || Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            string Choice(string name, string fallback, params string[] choices) => choices.Contains(Setting(name)) ? Setting(name) : fallback;
            string Number(string name, int min, int max, int fallback) => int.TryParse(Setting(name), out int value) && value >= min && value <= max ? value.ToString() : fallback.ToString();
            string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path));
            var workDir = Resolve("TeknoVUnit");
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected)) throw new InvalidOperationException("Select the game's ROM ZIP in Game Settings.");
            var rom = Resolve(selected);
            var state = Resolve(Setting("State Root", @"TeknoVUnit\state"));
            Directory.CreateDirectory(state);
            var args = new List<string> { "--set", Quote(profile.ProfileName), "--rom-dir", Quote(Path.GetDirectoryName(rom)),
                "--state-dir", Quote(state), "--renderer", Enabled("Enable VR") ? "openxr" : "vulkan",
                "--upscale", Number("Internal Resolution", 1, 8, 4), "--filter", Choice("Presentation Resampling", "bicubic", "nearest", "linear", "bicubic", "area") };
            if (profile.ProfileName == "wargods")
            {
                if (string.IsNullOrWhiteSpace(profile.GamePath2)) throw new InvalidOperationException("Select the War Gods 11/07/1996 CHD as the second game file.");
                args.Add("--disk"); args.Add(Quote(Resolve(profile.GamePath2)));
            }
            args.Add("--outputs");
            var ffbDevice = Setting("Force Feedback Device", "off").Trim();
            var token = ffbDevice.Split(':');
            if (profile.ProfileName == "wargods" ||
                (ffbDevice != "off" && (token.Length != 2 ||
                 (token[0] != "wheel" && token[0] != "gamepad") ||
                 !uint.TryParse(token[1], out _))))
                ffbDevice = "off";
            args.Add("--ffb-device"); args.Add(Quote(ffbDevice));
            args.Add("--ffb-gain"); args.Add(Number("Force Feedback Strength", 0, 100, 100));
            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch-to-fullscreen");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            if (Setting("CRT Shader") == "Lottes") args.Add("--crt-shader lottes");
            if (Setting("CRT Shader") == "Lottes Downsample") args.Add("--crt-shader lottes-ssaa");
            var port = Number("Network Port", 0, 65530, 0);
            if (port != "0" && profile.ProfileName != "wargods" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2")))
            {
                int max = profile.ProfileName == "crusnusa" ? 2 : 4;
                args.Add("--network-port " + port);
                args.Add("--link-node " + Number("Cabinet Id", 1, max, 1));
                args.Add("--link-count " + Number("Cabinet Count", 2, max, 2));
                args.Add("--link-session " + Number("Link Session", 1, int.MaxValue, 713));
                args.Add("--network-interface " + Quote(Setting("Network Interface", "auto")));
            }
            var executable = Path.Combine(workDir, "TeknoVUnit.exe");
            if (!File.Exists(executable)) log?.Invoke("TeknoVUnit executable missing: " + executable);
            return new ProcessStartInfo(executable, string.Join(" ", args)) { UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true };
        }
    }
}
