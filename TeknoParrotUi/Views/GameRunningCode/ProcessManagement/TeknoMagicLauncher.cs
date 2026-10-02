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
    // Launches the TeknoMagic emulator (Acclaim Magic: The Gathering Armageddon).
    // The game ships as a single merged MAME ZIP: there is no BIOS or CHD, so the
    // ROM path comes straight from GamePath and there is no ROM/CHD root setting.
    internal static class TeknoMagicLauncher
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

        // 100..200 (percent) -> "1.00".."2.00", matching the emulator's --vr-depth.
        private static string VrDepthArgument(string percentText)
        {
            if (!int.TryParse(percentText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var percentage))
                percentage = 150;
            percentage = Math.Max(100, Math.Min(200, percentage));
            return $"{percentage / 100}.{percentage % 100:D2}";
        }

        // RawInputTrackball scale, percent, clamped to the emulator's 1..400.
        private static string SensitivityArgument(string percentText)
        {
            if (!int.TryParse(percentText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var percentage))
                percentage = 100;
            return Math.Max(1, Math.Min(400, percentage)).ToString(CultureInfo.InvariantCulture);
        }

        // serviceMode is TPUI's Test Menu launch: the game only opens its
        // operator menu when Service is held while it boots.
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log, bool serviceMode = false)
        {
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) =>
                Setting(name, fallback ? "1" : "0") == "1" ||
                Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);

            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoMagic");

            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected))
                throw new ArgumentException("Select the game's merged ROM ZIP (magictg.zip).");
            var game = Path.GetFullPath(selected);
            if (!File.Exists(game) || !Path.GetExtension(game).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Select the game's merged ROM ZIP (magictg.zip).", game);

            var set = string.IsNullOrWhiteSpace(profile.ProfileName) ? "magictg" : profile.ProfileName;
            if (!new[] { "magictg", "magictga" }.Contains(set))
                throw new ArgumentException("Invalid TeknoMagic set name: " + set);

            var scale = Setting("Internal Resolution", "1");
            if (!int.TryParse(scale, out var resolution) || !new[] { 1, 2, 4, 8 }.Contains(resolution))
                throw new ArgumentException("Internal Resolution must be 1, 2, 4 or 8");

            var vr = Enabled("Enable VR");

            var filter = Setting("Presentation Resampling", "bicubic").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic" }.Contains(filter))
                filter = "bicubic";

            var stateRoot = Path.Combine(root, "state");
            var shaderRoot = Path.Combine(stateRoot, "shader-cache", set);
            Directory.CreateDirectory(stateRoot);
            Directory.CreateDirectory(shaderRoot);

            var args = new List<string>
            {
                "--game", set,
                "--outputs",
                "--rom", Quote(game),
                "--state-dir", Quote(stateRoot),
                "--shader-cache-dir", Quote(shaderRoot),
                "--video", vr ? "vulkan" : "vulkan",
                "--internal-scale", scale.ToString(CultureInfo.InvariantCulture),
                "--presentation-filter", vr ? "linear" : filter,
                "--sharpen", Setting("Presentation Sharpening", "0.15"),
                "--gamma", Setting("Display Gamma", "1.0"),
                "--saturation", Setting("Display Saturation", "1.0"),
                "--contrast", Setting("Display Contrast", "1.0"),
            };

            if (vr)
            {
                args.Add("--vr");
                args.Add("--vr-depth");
                args.Add(VrDepthArgument(Setting("VR Depth", "150")));
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            if (Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase))
                args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch-to-fullscreen");
            if (!vr && Setting("CRT Shader", "None") != "None") args.Add("--crt-shader lottes");
            if (Enabled("Use Bezel")) args.Add("--bezels");
            if (serviceMode) args.Add("--service-mode");
            if (Setting("Input API") == "RawInputTrackball")
            {
                args.Add("--trackball-sensitivity-x");
                args.Add(SensitivityArgument(Setting("Trackball Sensitivity X", "100")));
                args.Add("--trackball-sensitivity-y");
                args.Add(SensitivityArgument(Setting("Trackball Sensitivity Y", "100")));
            }
            if (Enabled("Mute Audio")) args.Add("--mute");
            if (Enabled("Prefer High Performance", true))
            {
                args.Add("--high-priority");
                args.Add("--gpu-high-performance");
            }

            var exe = Path.Combine(root, "teknomagic.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException("TeknoMagic executable is missing", exe);

            log?.Invoke($"TeknoMagic: {set}, {scale}x, {(vr ? "VR" : filter)}{(serviceMode ? ", service menu" : "")}");
            return new ProcessStartInfo(exe, string.Join(" ", args))
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
    }
}
