using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoCPSLauncher
    {
        private static readonly SemaphoreSlim MediaPreparation = new SemaphoreSlim(1, 1);

        internal static async Task PrepareMediaAsync(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || !gameId.StartsWith("cps_", StringComparison.Ordinal) ||
                gameId.Substring(4).Length == 0 || gameId.Substring(4).Any(c => !char.IsLetterOrDigit(c)))
                throw new ArgumentException("Invalid CPS game profile");
            await MediaPreparation.WaitAsync().ConfigureAwait(false);
            try
            {
                var profilePath = Path.Combine("UserProfiles", gameId + ".xml");
                if (!File.Exists(profilePath)) profilePath = Path.Combine("GameProfiles", gameId + ".xml");
                GameProfile profile;
                using (var reader = XmlReader.Create(profilePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(reader);
                profile.ProfileName = gameId;
                if (!profile.HasTpoSupport || profile.EmulatorType != EmulatorType.TeknoCPS)
                    throw new InvalidOperationException("This profile has no CPS online multiplayer mode");
                await RunMediaPreparationAsync(Build(profile, profile.GamePath, null, prepareMedia: true), TimeSpan.FromMinutes(20)).ConfigureAwait(false);
            }
            finally { MediaPreparation.Release(); }
        }

        internal static async Task RunMediaPreparationAsync(ProcessStartInfo info, TimeSpan timeout)
        {
            using (var process = new Process { StartInfo = info, EnableRaisingEvents = true })
            {
                var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.Exited += (sender, args) => exited.TrySetResult(true);
                if (!process.Start()) throw new InvalidOperationException("Unable to start game media preparation");
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                using (var deadline = new CancellationTokenSource())
                {
                    var timedOut = await Task.WhenAny(exited.Task, Task.Delay(timeout, deadline.Token)).ConfigureAwait(false) != exited.Task;
                    deadline.Cancel();
                    if (timedOut)
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (InvalidOperationException) { /* Exited between the check and Kill. */ }
                        await exited.Task.ConfigureAwait(false);
                        await Task.WhenAll(output, errors).ConfigureAwait(false);
                        throw new TimeoutException("Game media preparation timed out after " + timeout.TotalMinutes.ToString("0") + " minutes");
                    }
                }
                var diagnostics = (await errors.ConfigureAwait(false)) + (await output.ConfigureAwait(false));
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Game media preparation failed. Check the ROM ZIPs and CD Image Folder.\n\n" +
                        (diagnostics.Length > 8000 ? diagnostics.Substring(diagnostics.Length - 8000) : diagnostics));
            }
        }

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

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log, bool isTest = false, bool prepareMedia = false)
        {
            var id = profile.ProfileName ?? "";
            if (!id.StartsWith("cps_", StringComparison.Ordinal)) throw new ArgumentException("Invalid TeknoCPS profile");
            var set = id.Substring(4);
            if (set.Length == 0 || set.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid CPS ROM set");
            var online = !prepareMedia && Environment.GetEnvironmentVariable("TP_TPONLINE2") != null;
            if (online && !profile.HasTpoSupport) throw new ArgumentException("This CPS profile has no supported online multiplayer mode");
            var game = Path.GetFullPath(gameLocation);
            if (!File.Exists(game)) throw new FileNotFoundException("Select the game's ROM ZIP.", game);
            var root = Path.Combine(Directory.GetCurrentDirectory(), "TeknoCPS");
            var executable = Path.Combine(root, "TeknoCPS.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Install the TeknoCPS emulator package.", executable);
            var scale = prepareMedia ? "2" : profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Window Scale")?.FieldValue ?? "2";
            if (!int.TryParse(scale, out var size) || size < 1 || size > 8) throw new ArgumentException("Window Scale must be 1..8");
            var args = new List<string> { "--game", set, "--rom-root", Quote(Path.GetDirectoryName(game)), "--scale", size.ToString() };
            var cdRoot = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "CD Image Folder")?.FieldValue;
            if (!string.IsNullOrWhiteSpace(cdRoot))
            {
                var media = Path.GetFullPath(cdRoot.Trim());
                if (!Directory.Exists(media)) throw new DirectoryNotFoundException("CD Image Folder does not exist: " + media);
                args.AddRange(new[] { "--rom-root", Quote(media) });
            }
            if (prepareMedia)
                args.AddRange(new[] { "--prepare-media", "--no-nvram" });
            else if (online)
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
            var info = new ProcessStartInfo(executable, string.Join(" ", args)) { WorkingDirectory = root, UseShellExecute = false };
            if (prepareMedia)
            {
                info.EnvironmentVariables.Remove("TP_TPONLINE2");
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = info.RedirectStandardError = true;
            }
            return info;
        }
    }
}
