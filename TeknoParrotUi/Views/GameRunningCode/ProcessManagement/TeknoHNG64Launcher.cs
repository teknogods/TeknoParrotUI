using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoHNG64Launcher
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
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            string Choice(string name, string fallback, params string[] choices)
            {
                var value = Setting(name, fallback).Trim().ToLowerInvariant();
                return choices.Contains(value) ? value : fallback;
            }
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoHNG64");
            var executable = Path.Combine(workDir, "TeknoHNG64.exe");
            var game = profile.ProfileName;
            if (!new[] { "roadedge", "sams64", "xrally", "bbust2", "sams64_2", "fatfurwa", "buriki" }.Contains(game))
                throw new InvalidOperationException("Unsupported TeknoHNG64 set: " + game);
            string Resolve(string value, string fallback) => Path.GetFullPath(Path.Combine(uiRoot,
                string.IsNullOrWhiteSpace(value) ? fallback : value));
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            var media = Resolve(selected, workDir);
            if (media.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) media = Path.GetDirectoryName(media);
            var args = new List<string> { "--game", game, "--tp-input", "--rom-root", Quote(media),
                "--save-root", Quote(Resolve(Setting("State Root"), Path.Combine(workDir, "state", "saves"))),
                "--internal-scale", Choice("Internal Resolution", "1", "1", "2", "4", "8"),
                "--presentation-filter", Choice("Presentation Resampling", "area", "auto", "nearest", "linear", "bicubic", "area") };
            if (Choice("DisplayMode", "fullscreen", "fullscreen", "windowed") == "fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (Enabled("Enable VR")) {
                args.AddRange(new[] { "--vr", "--vr-filter", "linear", "--vr-calibration-root", Quote(Path.Combine(workDir, "state", "vr")) });
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            } else {
                var crt = Choice("CRT Shader", "none", "none", "lottes", "lottes downsample");
                args.AddRange(new[] { "--crt", crt.Replace(" ", "-") });
            }
            if (!File.Exists(executable)) log?.Invoke("TeknoHNG64 executable not found: " + executable);
            return new ProcessStartInfo(executable, string.Join(" ", args)) {
                UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true
            };
        }
    }
}
