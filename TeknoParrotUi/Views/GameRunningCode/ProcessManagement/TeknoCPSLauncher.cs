using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoCPSLauncher
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
            var id = profile.ProfileName ?? "";
            if (!id.StartsWith("cps_", StringComparison.Ordinal)) throw new ArgumentException("Invalid TeknoCPS profile");
            var set = id.Substring(4);
            if (set.Length == 0 || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid CPS ROM set");
            var online = Environment.GetEnvironmentVariable("TP_TPONLINE2") != null;
            if (online && !profile.HasTpoSupport) throw new ArgumentException("This CPS profile has no supported online multiplayer mode");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select the game's ROM ZIP.", game);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoCPS");
            var executable = Path.Combine(root, "TeknoCPS.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Install the TeknoCPS emulator package.", executable);
            var scale = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Window Scale")?.FieldValue ?? "2";
            if (!int.TryParse(scale, out var size) || size < 1 || size > 8) throw new ArgumentException("Window Scale must be 1..8");
            var args = new List<string> { "--game", set, "--rom-root", Quote(Path.GetDirectoryName(game)), "--scale", size.ToString() };
            var cdRoot = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "CD Image Folder")?.FieldValue;
            if (!string.IsNullOrWhiteSpace(cdRoot))
            {
                var media = Path.GetFullPath(cdRoot.Trim());
                if (!Directory.Exists(media)) throw new DirectoryNotFoundException("CD Image Folder does not exist: " + media);
                args.AddRange(new[] { "--rom-root", Quote(media) });
            }
            if (online)
            {
                // The inherited lobby owns every seat and Tournament cabinet position.
                // The emulator supplies canonical timing and ignores offline EEPROM.
                args.Add("--no-nvram");
            }
            else
            {
                args.AddRange(new[] { "--nvram-dir", Quote(Path.Combine(root, "nvram")) });
                if (isTest && !string.IsNullOrEmpty(profile.TestMenuParameter)) args.Add("--test-menu");
            }
            log?.Invoke("TeknoCPS: " + set + (online ? ", automatic online room" : ", local play"));
            return new ProcessStartInfo(executable, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
        }
    }
}
