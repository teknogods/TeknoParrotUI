using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoSS32Launcher
    {
        private static string Quote(string value)
        {
            var text = new StringBuilder("\""); var slashes = 0;
            foreach (var c in value ?? "")
            {
                if (c == '\\') { ++slashes; continue; }
                text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                text.Append(c); slashes = 0;
            }
            return text.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log, bool isTest = false)
        {
            profile = ArcadeGameRevisions.CreateLaunchProfile(profile);
            string Setting(string key, string fallback) => profile.ConfigValues?.FirstOrDefault(v => v.FieldName == key)?.FieldValue ?? fallback;
            var id = profile.ProfileName ?? "";
            if (!id.StartsWith("ss32_", StringComparison.Ordinal)) throw new ArgumentException("Invalid TeknoSS32 profile");
            var linked = id.EndsWith("_link", StringComparison.Ordinal) || new[] { "ss32_f1lap", "ss32_f1lapt", "ss32_f1lapj", "ss32_radr", "ss32_radru", "ss32_radrj" }.Contains(id);
            var set = id.Substring(5); if (id.EndsWith("_link", StringComparison.Ordinal)) set = set.Substring(0, set.Length - 5);
            if (set.Length == 0 || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid System 32 ROM set");
            var online = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_TPONLINE2"));
            if (online && !profile.HasTpoSupport) throw new ArgumentException("This SS32 profile has no available multiplayer mode");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select the game's ROM ZIP.", game);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoSS32");
            var executable = Path.Combine(root, "TeknoSS32.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Install the TeknoSS32 emulator package.", executable);
            var cdSetting = Setting("CD Image Folder", "");
            var cdRoot = string.IsNullOrWhiteSpace(cdSetting) ? Path.GetDirectoryName(game) : Path.GetFullPath(cdSetting.Trim());
            if (!Directory.Exists(cdRoot)) throw new DirectoryNotFoundException("CD Image Folder does not exist: " + cdRoot);
            var args = new List<string> { "--set", set, "--rom-root", Quote(Path.GetDirectoryName(game)), "--chd-root", Quote(cdRoot), "--renderer", "vulkan" };
            NativeArcadeLaunch.AddPresentation(profile, root, args);
            if (online)
            {
                args.AddRange(new[] { "--net-mode", "rollback", "--net-input-delay", "2" });
                if (linked) args.Add("--net-link");
            }
            else
            {
                if (!int.TryParse(Setting("Window Scale", "2"), out var scale) || scale < 1 || scale > 4) throw new ArgumentException("Window Scale must be 1..4");
                args.AddRange(new[] { "--scale", scale.ToString(), "--state-root", Quote(Path.Combine(root, "state")) });
                if (isTest) args.Add("--test-menu");
                if (set.StartsWith("orunners", StringComparison.Ordinal))
                {
                    var screens = Setting("Screens", "Both");
                    if (!new[] { "Both", "Monitor 1", "Monitor 2" }.Contains(screens)) throw new ArgumentException("Invalid OutRunners screen selection");
                    args.AddRange(new[] { "--screens", screens == "Monitor 1" ? "first" : screens == "Monitor 2" ? "second" : "both" });
                }
            }
            if (Setting("Audio", "1") != "1") args.Add("--no-audio");
            log?.Invoke("TeknoSS32: " + set + (online ? ", automatic rollback room" : ", local play"));
            return NativeArcadeLaunch.FromTeknoParrotUi(new ProcessStartInfo(executable, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false });
        }
    }
}
