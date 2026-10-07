using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
namespace TeknoParrotUi.Common.GameLaunch
{
    internal static class TeknoS22Launcher
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
        // Shared by TeknoS22 and TeknoS23: SDL3 force feedback through the S22haptic/S23haptic
        // device tokens. Only profiles declaring a Force Feedback Device get these options, and
        // only the effects a profile declares are sent (ForceFeedbackArguments).
        internal static void AddForceFeedback(GameProfile profile, List<string> args)
        {
            string Setting(string name, string fallback = null) =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            if (Setting("Force Feedback Device") == null) return;
            args.Add("--ffb-device");
            args.Add(NamcoFfbDeviceProbe.GetLaunchSelection(Setting("Force Feedback Device", "off")));
            if (!int.TryParse(Setting("Force Feedback Strength", "100"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var gain) || gain < 0 || gain > 100)
                gain = 100;
            args.Add("--ffb-gain");
            args.Add(gain.ToString(CultureInfo.InvariantCulture));
            ForceFeedbackArguments.Add(profile, args, "Spring", "Constant", "Friction", "Sine");
            var sinePeriod = Setting("Sine Effect Period");
            if (sinePeriod == null) return;
            if (!int.TryParse(sinePeriod, NumberStyles.Integer, CultureInfo.InvariantCulture, out var period) ||
                period < 10 || period > 200)
                period = 40;
            args.Add("--ffb-sine-period");
            args.Add(period.ToString(CultureInfo.InvariantCulture));
        }
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            string Setting(string name, string fallback = "") => profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false) => Setting(name, fallback ? "1" : "0") == "1" || Setting(name).Equals("true", StringComparison.OrdinalIgnoreCase);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoS22");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select this game's merged ROM ZIP.", game);
            var romRoot = Path.GetDirectoryName(game);
            var set = NamcoGameRevisions.ResolveSet(profile);
            if (string.IsNullOrWhiteSpace(set) || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid System 22 game profile");
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var resolution) || resolution < 1 || resolution > 8) throw new ArgumentException("Internal Resolution must be 1 to 8");
            var filter = Setting("Presentation Resampling", "bicubic").ToLowerInvariant();
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "area" }.Contains(filter)) throw new ArgumentException("Invalid presentation filter");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "state"));
            Directory.CreateDirectory(Path.Combine(root, "bezels"));
            var args = new List<string> { "--game", Quote(set), "--rom-root", Quote(romRoot), "--state-dir", Quote(Path.Combine(root, "state")), "--render-scale", scale, "--present-filter", filter, "--outputs" };
            var fullscreen = Setting("DisplayMode", "Fullscreen").Equals("Fullscreen", StringComparison.OrdinalIgnoreCase);
            if (fullscreen) args.Add("--fullscreen");
            if (fullscreen && Enabled("Stretch to Fullscreen")) args.Add("--stretch");
            if (fullscreen && Enabled("Use Bezel", true)) args.Add("--use-bezel");
            if (profile.GunGame) {
                if (!Enabled("Crosshairs", true)) args.Add("--no-crosshairs");
                if (!Enabled("Hide Crosshairs after Inactivity", true)) args.Add("--no-crosshair-autohide");
            }
            var crt = Setting("CRT Shader", "None");
            args.Add("--crt");
            args.Add(crt == "Lottes Downsample" ? "lottes-downsample" : crt == "Lottes" ? "lottes" : "off");
            if (Enabled("Mute Audio")) args.Add("--no-audio");
            if (!Enabled("VSync", true)) args.Add("--no-vsync");
            if (Enabled("Enable VR"))
            {
                args.Add("--vr");
                if (!Enabled("Use VR Controls", true)) args.Add("--no-vr-controls");
            }
            else if (Enabled("Widescreen")) args.Add("--widescreen");
            AddForceFeedback(profile, args);
            // TPOnline supplies TP_TPONLINE2 in the inherited environment, as for Viper.
            // Manual LAN peers use the native C139 transport in the same executable.
            var tpOnline = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (Enabled("Enable LAN") && !tpOnline)
            {
                foreach (var pair in new[] { new[] { "Cabinet ID", "--link-node" }, new[] { "Cabinet Count", "--link-count" }, new[] { "Local Port", "--link-port" } })
                { args.Add(pair[1]); args.Add(Quote(Setting(pair[0]))); }
                foreach (var peer in Setting("Peer Address").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                { args.Add("--link-peer"); args.Add(Quote(peer.Trim())); }
            }
            var exe = Path.Combine(root, "TeknoS22.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("TeknoS22 executable is missing", exe);
            log?.Invoke($"TeknoS22: {set}, {scale}x, {filter}");
            return new ProcessStartInfo(exe, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
