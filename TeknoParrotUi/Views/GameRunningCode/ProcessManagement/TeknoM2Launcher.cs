using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoM2Launcher
    {
        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value ?? string.Empty)
            {
                if (c == '\\') { ++slashes; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) =>
                Setting(name, fallback ? "1" : "0") == "1" || Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            string Choice(string name, string fallback, params string[] choices)
            {
                var value = Setting(name, fallback).Trim().ToLowerInvariant();
                return choices.Contains(value) ? value : fallback;
            }
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoM2");
            var game = profile.ProfileName;
            var games = new[] { "polystar", "totlvice", "totlvicj", "totlvica", "totlvicu", "btltryst", "heatof11", "evilngt", "hellngt" };
            if (!games.Contains(game)) throw new InvalidOperationException("Unsupported TeknoM2 game: " + game);
            string Resolve(string path) => Path.GetFullPath(Path.Combine(uiRoot, path));
            var rom = Resolve(string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation);
            var disc = Resolve(profile.GamePath2);
            if (!File.Exists(rom) || !File.Exists(disc))
                throw new FileNotFoundException("Select this game's ROM ZIP and CHD in Game Settings.");
            var executable = Path.Combine(workDir, "TeknoM2.exe");
            var args = new List<string> {
                "--game", game, "--rom-root", Quote(rom), "--chd-root", Quote(disc),
                "--state-dir", Quote(Path.Combine(workDir, "state")),
                "--internal-scale", Choice("Internal Resolution", "4", "1", "2", "4", "8"),
                "--presentation-filter", Choice("Presentation Resampling", "area", "point", "linear", "bicubic", "area"),
                "--texture-filter", Choice("Texture Filtering", "original", "original", "trilinear", "max"),
                "--crt-shader", Choice("CRT Shader", "none", "none", "lottes")
            };
            if (Choice("DisplayMode", "fullscreen", "fullscreen", "windowed") == "fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch-to-fullscreen");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
            if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            if (Enabled("Enable VR")) {
                args.Add("--vr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            if (!File.Exists(executable)) log?.Invoke("TeknoM2 executable not found: " + executable);
            return new ProcessStartInfo(executable, string.Join(" ", args)) {
                UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true
            };
        }
    }
}
