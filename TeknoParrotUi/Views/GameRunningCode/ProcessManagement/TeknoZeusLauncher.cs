using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoZeusLauncher
    {
        // Windows CommandLineToArgvW quoting, including trailing directory slashes.
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
                Setting(name, fallback ? "1" : "0") == "1" ||
                Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            string Choice(string name, string fallback, params string[] choices)
            {
                var value = Setting(name, fallback).Trim().ToLowerInvariant();
                return choices.Contains(value) ? value : fallback;
            }
            string Number(string name, int fallback, int minimum, int maximum)
            {
                return int.TryParse(Setting(name), out var value) && value >= minimum && value <= maximum
                    ? value.ToString(CultureInfo.InvariantCulture) : fallback.ToString(CultureInfo.InvariantCulture);
            }
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoZeus");
            string Resolve(string value, string fallback) => Path.GetFullPath(
                Path.Combine(uiRoot, string.IsNullOrWhiteSpace(value) ? fallback : value));
            var selectedRom = Resolve(string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation, workDir);
            var romRoot = Resolve(Setting("ROM Root"),
                selectedRom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(selectedRom) : selectedRom);
            var stateRoot = Resolve(Setting("Save Root"), Path.Combine(workDir, "state"));
            Directory.CreateDirectory(stateRoot);
            var parameters = new List<string>
            {
                Quote(profile.ProfileName), "--rom-dir", Quote(romRoot), "--state-dir", Quote(stateRoot),
                "--internal-scale", Number("Internal Resolution", 4, 1, 8),
                "--window-scale", "2", "--aspect", Choice("Aspect Ratio", "4:3", "native", "4:3", "16:9", "stretch"),
                "--presentation-filter", Choice("Presentation Filter", "linear", "point", "linear", "bicubic", "ssaa"),
                Enabled("VSync", true) ? "--vsync" : "--no-vsync"
            };
            if (profile.HasTwoExecutables)
            {
                // Zeus resolves the selected revision's disk name in a flat or merged CHD root.
                var selectedChd = Resolve(profile.GamePath2, romRoot);
                var chdRoot = Resolve(Setting("CHD Root"),
                    selectedChd.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(selectedChd) : selectedChd);
                parameters.AddRange(new[] { "--chd-dir", Quote(chdRoot) });
                if (!Directory.Exists(chdRoot)) log?.Invoke($"TeknoZeus CHD root was not found at {chdRoot}");
            }
            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen") parameters.Add("--fullscreen");
            if (Enabled("Enable VR")) parameters.Add("--vr");
            else
            {
                if (profile.ProfileName == "crusnexo" && Enabled("Widescreen Hack"))
                    parameters.Add("--widescreen");
                var crt = Choice("CRT Shader", "none", "none", "lottes", "lottes downsample");
                parameters.AddRange(new[] { "--crt-shader", crt == "lottes downsample" ? "lottes-ssaa" : crt });
                if (Enabled("Use Bezel") && Setting("DisplayMode", "Fullscreen") == "Fullscreen")
                    parameters.Add("--bezels");
            }
            if (Enabled("Mute Audio")) parameters.Add("--no-audio");
            if (Enabled("Prefer High Performance", true)) parameters.Add("--gpu-high-performance");
            if (Enabled("Load Texture Packs"))
                parameters.AddRange(new[] { "--texture-pack", Quote(Resolve(Setting("Texture Pack Root"), Path.Combine(workDir, "texture-packs"))) });
            if (Enabled("Dump Textures"))
                parameters.AddRange(new[] { "--texture-dump", Quote(Resolve(Setting("Texture Dump Root"), Path.Combine(workDir, "texture-dumps"))) });
            // TPOnline provides the node identity; do not also enable the LAN bridge.
            var tpOnline = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (tpOnline && Enabled("Network Diagnostics", true)) parameters.Add("--network-diagnostics");
            if (!tpOnline && Enabled("Cabinet Link") && (profile.ProfileName == "crusnexo" || profile.ProfileName == "thegrid"))
            {
                parameters.AddRange(new[] { "--network-port", Number("Network Port", 46000, 1, 65535),
                    "--network-node", Number("Cabinet ID", 0, 0, 62),
                    "--network-session", Quote(Setting("Network Session", "default")),
                    "--network-interface", Quote(Setting("Network Interface", "auto")) });
            }
            var executable = Path.Combine(workDir, "TeknoZeus.exe");
            if (!File.Exists(executable)) log?.Invoke($"TeknoZeus executable was not found at {executable}");
            if (!Directory.Exists(romRoot)) log?.Invoke($"TeknoZeus ROM root was not found at {romRoot}");
            return new ProcessStartInfo(executable, string.Join(" ", parameters))
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardError = true
            };
        }
    }
}
