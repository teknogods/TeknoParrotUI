using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// IN-PROCESS immediate-relaunch reproduction: runs the REAL GameSession
    /// engine N times inside ONE process - exactly what happens when a user
    /// launches a game from the TeknoParrotUI menu, closes it, and launches it
    /// again without restarting the app. The old CLI --profile= harness always
    /// exited the whole process between runs, so it could never catch static
    /// state leaking between sessions of the SAME process.
    ///
    /// Must be run from the app output directory (bin/x86/Debug) so loaders,
    /// GameProfiles/UserProfiles and ParrotData.xml resolve exactly like the
    /// real app.
    ///
    /// Usage: cd bin/x86/Debug &&
    ///   dotnet ../../Tools/InputMethodAudit/bin/Debug/net8.0/InputMethodAudit.dll \
    ///     session-relaunch-test [profileXml] [rounds]
    /// </summary>
    internal static class SessionRelaunchTest
    {
        private sealed class TimestampedConsoleListener : TraceListener
        {
            public override void Write(string message) => Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            public override void WriteLine(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static int Run(string profileXml = "3DCosplayMahjong.xml", int rounds = 3, string closeMode = "game", int playSeconds = 3)
        {
            if (!Directory.Exists("GameProfiles") || !Directory.Exists("UserProfiles"))
            {
                Console.Error.WriteLine("Run from the app output directory (bin/x86/Debug).");
                return 1;
            }

            // Same startup order as the real app.
            JoystickHelper.DeSerialize();

            // Surface JvsPackageEmulator's Debug.WriteLine JVS conversation
            // (Package:/Reply: lines) with timestamps so good and bad rounds
            // can be correlated against the helper's shm-mirror tick logs.
            Trace.Listeners.Add(new TimestampedConsoleListener());

            // pipehelper.exe resolution uses AppContext.BaseDirectory, which
            // for this harness is the audit tool's own bin dir - point it at
            // the app output dir we are running from (same file the app uses).
            var localHelper = Path.GetFullPath("pipehelper.exe");
            if (File.Exists(localHelper) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TP_PROTON_PIPEHELPER")))
                Environment.SetEnvironmentVariable("TP_PROTON_PIPEHELPER", localHelper);

            var pass = 0;
            var fail = 0;

            // The MENU flow reuses ONE GameProfile object from the
            // GameProfileLoader static list across launches - reproduce that
            // exactly instead of re-loading a fresh object per round.
            var sharedProfile = LoadProfileLikeCli(profileXml);
            if (sharedProfile == null)
            {
                Console.Error.WriteLine($"Profile {profileXml} not found/loadable.");
                return 1;
            }

            for (var round = 1; round <= rounds; round++)
            {
                Console.WriteLine($"\n================ ROUND {round}/{rounds} ================");
                var profile = sharedProfile;

                using var session = new GameSession(profile);
                var connected = new ManualResetEventSlim(false);
                var exited = new ManualResetEventSlim(false);
                var ioError = false;
                var exitCode = int.MinValue;
                long maxTraffic = 0;

                session.OutputReceived += line =>
                {
                    Console.WriteLine($"  [out] {line}");
                    if (line.Contains("GAME CONNECTED"))
                        connected.Set();
                    if (line.Contains("I/O error", StringComparison.OrdinalIgnoreCase))
                        ioError = true;
                    // Track JVS traffic totals: a healthy session moves KBs;
                    // the I/O-error stall dies at ~28 bytes after the phantom
                    // second-node handshake.
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"closing \(game->TPUI (\d+) B");
                    if (m.Success && long.TryParse(m.Groups[1].Value, out var b) && b > maxTraffic)
                        maxTraffic = b;
                };
                session.StateChanged += s => Console.WriteLine($"  [state] {s}");
                session.Exited += code => { exitCode = code; exited.Set(); };

                if (!session.Start())
                {
                    Console.Error.WriteLine("Start() returned false");
                    fail++;
                    continue;
                }

                // Wait for the game to open the JVS pipe (controls working).
                if (!connected.Wait(TimeSpan.FromSeconds(90)))
                {
                    Console.Error.WriteLine($"ROUND {round}: FAIL - game never connected to the JVS pipe (the I/O-error symptom)");
                    fail++;
                    session.ForceQuit();
                    exited.Wait(TimeSpan.FromSeconds(30));
                    continue;
                }
                Console.WriteLine($"ROUND {round}: game connected - letting it run {playSeconds}s, then closing it (mode={closeMode})");
                Thread.Sleep(playSeconds * 1000);

                // Close the game like the user would. "game" terminates the
                // actual game process (in-game exit); "wrapper" terminates
                // GAMESCOPE first (clicking the window's close button - the
                // wrapper dies while the game is still alive).
                if (closeMode == "wrapper")
                    KillNewestByComm("gamescope");
                else
                    KillNewestByComm("game.exe");

                if (!exited.Wait(TimeSpan.FromSeconds(60)))
                {
                    Console.Error.WriteLine($"ROUND {round}: FAIL - session did not end after the game was closed");
                    fail++;
                    session.ForceQuit();
                    exited.Wait(TimeSpan.FromSeconds(30));
                    continue;
                }

                var leftovers = CountAliveByComm("pipehelper");
                var stalled = maxTraffic < 100; // healthy rounds exchange KBs; the I/O-error stall dies at ~28 B
                Console.WriteLine($"ROUND {round}: session ended (exitCode {exitCode}); ioError={ioError}; jvsTraffic={maxTraffic}B; leftover pipehelpers={leftovers}");
                if (ioError || stalled)
                {
                    fail++;
                    Console.Error.WriteLine($"ROUND {round}: FAIL - {(ioError ? "I/O error reported" : $"JVS traffic stalled at {maxTraffic} B (the I/O-error signature)")}");
                }
                else
                {
                    pass++;
                }
                // IMMEDIATE relaunch: no delay on purpose - this is the repro.
            }

            Console.WriteLine($"\n=== SessionRelaunchTest: {pass}/{rounds} passed, {fail} failed ===");
            return fail == 0 ? 0 : 1;
        }

        /// <summary>Loads the profile exactly like App.FetchProfile (CLI path).</summary>
        private static GameProfile LoadProfileLikeCli(string xmlName)
        {
            var stockPath = Path.Combine("GameProfiles", xmlName);
            if (!File.Exists(stockPath))
                return null;

            GameProfile profile;
            if (File.Exists(Path.Combine("UserProfiles", xmlName)))
            {
                profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", xmlName), true);
                var stock = JoystickHelper.DeSerializeGameProfile(stockPath, false);
                StockProfileMetadata.Apply(profile, stock);
            }
            else
            {
                profile = JoystickHelper.DeSerializeGameProfile(stockPath, false);
            }
            if (profile == null)
                return null;
            profile.FileName = stockPath;
            profile.ProfileName = Path.GetFileNameWithoutExtension(xmlName);
            profile.GameNameInternal = profile.ProfileName;
            TeknoParrotUi.Common.InputListening.ProfileStorage.BindingsStore.Apply(profile);
            return profile;
        }

        private static void KillNewestByComm(string comm)
        {
            var proc = new LinuxProcReader();
            var newest = proc.EnumerateProcessIds()
                .Select(pid => proc.ReadStat(pid))
                .Where(s => s != null && s.Comm.Equals(comm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.StartTimeTicks)
                .FirstOrDefault();
            if (newest == null)
            {
                Console.Error.WriteLine($"  (no {comm} process found to close!)");
                return;
            }
            Console.WriteLine($"  closing {comm} pid {newest.Pid}");
            new LinuxProcessSignaler().SignalGraceful(newest.Pid);
        }

        private static int CountAliveByComm(string prefix)
        {
            var proc = new LinuxProcReader();
            return proc.EnumerateProcessIds()
                .Select(pid => proc.ReadStat(pid))
                .Count(s => s != null && s.Comm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
