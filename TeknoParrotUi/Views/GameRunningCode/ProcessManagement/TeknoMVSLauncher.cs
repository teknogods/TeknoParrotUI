using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoMVSLauncher
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

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log, bool isTest = false)
        {
            string Setting(string name, string fallback) => profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            var id = profile.ProfileName ?? "";
            if (!id.StartsWith("mvs_", StringComparison.Ordinal)) throw new ArgumentException("Invalid TeknoMVS profile");
            var linked = id == "mvs_lbowling_link" || id == "mvs_ridhero" || id == "mvs_ridheroh" || id == "mvs_trally";
            var set = id == "mvs_lbowling_link" ? "lbowling" : id.Substring(4);
            if (set.Length == 0 || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid MVS ROM set");
            var online = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (online && !profile.HasTpoSupport) throw new ArgumentException("This MVS profile has no online multiplayer mode");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select the game's merged ROM ZIP.", game);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoMVS");
            var executable = Path.Combine(root, online ? "TeknoMVSOnline.exe" : "TeknoMVSDiagnostic.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Install the TeknoMVS emulator package.", executable);
            var bios = set == "kizuna4p" ? "asia" : set == "irrmaze" ? "asia-sp1" : new[] { "ms5pcb", "svcpcb", "svcpcba", "kf2k3pcb" }.Contains(set) ? "default" : "euro";
            var args = new List<string> { "--game", set, "--bios", bios, "--rom-root", Quote(Path.GetDirectoryName(game)) };
            if (online)
            {
                // All peers use a canonical boot configuration. The inherited
                // lobby assigns seats; saved local cabinet settings never enter it.
                args.AddRange(new[] { "--cpu-clock", "12", "--net-mode", "rollback", "--net-input-delay", "2" });
                if (linked) args.Add("--net-link");
            }
            else
            {
                var clock = Setting("CPU Clock", "12");
                var mode = Setting("Rendering Mode", "original");
                if (clock != "12" && clock != "24") throw new ArgumentException("CPU Clock must be 12 or 24 MHz");
                if (!new[] { "original", "enhanced", "remastered" }.Contains(mode)) throw new ArgumentException("Invalid MVS rendering mode");
                var dipMask = set == "kizuna4p" ? 2 : new[] { "janshin", "minasan", "bakatono", "ms5pcb", "svcpcb", "svcpcba" }.Contains(set) ? 4 : 0;
                for (var dip = 0; dip < 8; ++dip)
                {
                    var name = "DIP Switch " + (dip + 1);
                    var value = Setting(name, (dipMask & (1 << dip)) != 0 ? "On" : "Off");
                    if (value != "On" && value != "Off") throw new ArgumentException(name + " must be On or Off");
                    if (value == "On") dipMask |= 1 << dip;
                    else dipMask &= ~(1 << dip);
                }
                args.AddRange(new[] { "--play", "--start", "--cpu-clock", clock, "--render-mode", mode,
                    "--dip-mask", dipMask.ToString(),
                    "--cabinet-state-dir", Quote(Path.Combine(root, "state")), "--rtc-offline-policy", "frozen" });
                if (isTest) args.Add("--test-menu");
            }
            if (Setting("Audio", "1") == "1") { if (!online) args.Add("--audio"); }
            else if (online) args.Add("--no-audio");
            log?.Invoke("TeknoMVS: " + set + (online ? linked ? ", online linked cabinets" : ", online shared cabinet" : ", local play"));
            return new ProcessStartInfo(executable, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
