using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoCobraLauncher
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
            var workDir = Path.Combine(uiRoot, "TeknoCobra");
            string Resolve(string value, string fallback) => Path.GetFullPath(
                Path.Combine(uiRoot, string.IsNullOrWhiteSpace(value) ? fallback : value));
            var executable = Path.Combine(workDir, "TeknoCobra.exe");
            var game = profile.ProfileName;
            if (game != "bujutsu" && game != "racjamdx")
                throw new InvalidOperationException("Unsupported TeknoCobra set: " + game);
            var selectedRom = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            var romRoot = workDir;
            if (!string.IsNullOrWhiteSpace(selectedRom))
            {
                var path = Resolve(selectedRom, workDir);
                romRoot = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(path) : path;
            }
            var selectedChd = profile.GamePath2;
            var disk = !string.IsNullOrWhiteSpace(selectedChd) &&
                selectedChd.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)
                ? Resolve(selectedChd, workDir) : null;
            var chdRoot = Resolve(selectedChd, workDir);
            var saves = Resolve(Setting("State Root"), Path.Combine(workDir, "state", "saves"));
            var vr = Enabled("Enable VR");
            var geometric = vr && Enabled("Geometric VR");
            var scale = Choice("Internal Resolution", "1", "1", "2", "4", "8");
            if (vr && !geometric) scale = "1";
            if (vr && scale == "8") scale = "4";
            var resampling = Choice("Presentation Resampling", "linear", "linear", "nearest", "ssaa");
            if (vr && resampling == "ssaa") resampling = "linear";
            var args = new List<string> { "--game", game, "--tp-input", "--rom-dir", Quote(romRoot),
                "--internal-scale", scale, "--hud-filter", Choice("HUD Filter", "smooth", "smooth", "crisp"),
                "--presentation-filter", resampling };
            if (disk != null) args.AddRange(new[] { "--disk", Quote(disk) });
            else args.AddRange(new[] { "--chd-dir", Quote(chdRoot) });
            args.AddRange(new[] { "--save-dir", Quote(saves) });
            if (Choice("DisplayMode", "fullscreen", "fullscreen", "windowed") == "fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch-to-fullscreen");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("Fast Boot", true)) args.Add("--no-fast-boot");
            if (vr) args.Add("--vr");
            if (geometric) args.Add("--geometric-stereo");
            if (!vr)
            {
                var crt = Choice("CRT Shader", "none", "none", "lottes", "lottes-ssaa", "lottes downsample");
                args.AddRange(new[] { "--crt-shader", crt == "lottes downsample" ? "lottes-ssaa" : crt });
            }
            if (game == "racjamdx")
            {
                var role = Choice("Link Role", "off", "off", "master", "slave");
                if (role != "off")
                {
                    if (!int.TryParse(Setting("Network Port", "17602"), out var port) || port < 1 || port > 65535)
                        throw new InvalidOperationException("Cobra Network Port must be in 1..65535.");
                    var networkInterface = Setting("Network Interface", "auto").Trim();
                    if (networkInterface.Length == 0) networkInterface = "auto";
                    args.AddRange(new[] { "--network-node", role == "master" ? "1" : "2",
                        "--network-port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "--network-interface", Quote(networkInterface) });
                    log?.Invoke("Cobra uses direct LAN play. Use the same port, different cabinet roles, and separate State Root folders.");
                }
            }
            if (!File.Exists(executable)) log?.Invoke("TeknoCobra executable not found: " + executable);
            if (!Directory.Exists(romRoot)) log?.Invoke("TeknoCobra ROM root not found: " + romRoot);
            if (disk != null && !File.Exists(disk)) log?.Invoke("TeknoCobra CHD not found: " + disk);
            var info = new ProcessStartInfo(executable, string.Join(" ", args)) {
                UseShellExecute = false, WorkingDirectory = workDir, RedirectStandardError = true
            };
            return info;
        }
    }
}
