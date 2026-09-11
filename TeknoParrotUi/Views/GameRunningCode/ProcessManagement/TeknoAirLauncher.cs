using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoAirLauncher
    {
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback) => profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) => new[] { "1", "true" }.Contains(Setting(name, fallback ? "1" : "0").ToLowerInvariant());
            string Quote(string value) => "\"" + value.TrimEnd('\\').Replace("\"", "\\\"") + "\"";
            var selectedRom = Path.GetFullPath(string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation);
            var workDir = Path.Combine(Directory.GetCurrentDirectory(), "TeknoAir");
            var args = new List<string> {
                "--game", Quote(profile.ProfileName), "--rom-root", Quote(Path.GetDirectoryName(selectedRom)),
                "--state-dir", Quote(Path.Combine(workDir, "state")), "--tpui",
                "--backend", Enabled("Enable VR") ? "vr" : "vulkan",
                "--scale", Setting("Internal Resolution", "4"),
                "--scale-filter", Setting("Scale Filter", "off"),
                "--presentation-filter", Setting("Presentation Resampling", "bicubic"),
                "--crt", Setting("CRT Shader", "none"),
                "--cabinet", Setting("Cabinet", "auto"),
                "--vr-view", Setting("VR View", "free")
            };
            if (Setting("DisplayMode", "Fullscreen") == "Fullscreen") args.Add("--fullscreen");
            if (Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (Enabled("Use Bezel", true)) args.Add("--bezel");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (Enabled("Persistent Cabinet State", true)) args.Add("--persistent-state");
            if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            var info = new ProcessStartInfo(Path.Combine(workDir, "TeknoAir.exe"), string.Join(" ", args)) {
                WorkingDirectory = workDir, UseShellExecute = false
            };
            log?.Invoke("Launching TeknoAir: " + info.Arguments);
            return info;
        }
    }
}
