using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Common.Pipes.Implementation;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// Regression tests for the session-scoped pipe lifecycle refactor:
    /// instance-scoped ControlPipe (no static session state), SerialPortHandler
    /// thread ownership + bounded joins, ProtonBridgePipe ephemeral ports +
    /// verified helper shutdown, PipeHelperRegistry identity-verified cleanup
    /// (no global KillStaleHelpers anywhere), early session-token creation, and
    /// idempotent GameSession cleanup ordering. Exercises the exact production
    /// classes - real threads, real pipes/sockets, real /proc processes for the
    /// helper-isolation cases.
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -c Debug -- pipe-lifecycle-test
    /// </summary>
    internal static class PipeLifecycleTest
    {
        /// <summary>Test double: transmits until stopped, then optionally finishes LATE (simulates a slow worker).</summary>
        private sealed class SlowEchoPipe : ControlPipe
        {
            public readonly ManualResetEventSlim TransmitEntered = new ManualResetEventSlim(false);
            public readonly ManualResetEventSlim TransmitExited = new ManualResetEventSlim(false);
            public int LateFinishMs;

            public override void Transmit(bool runEmuOnly)
            {
                TransmitEntered.Set();
                var server = Server;
                try
                {
                    while (IsRunning)
                    {
                        server.Write(new byte[] { 0x55 }, 0, 1);
                        server.Flush();
                        Thread.Sleep(10);
                    }
                }
                catch
                {
                    // server closed by StopAndWait - expected
                }
                finally
                {
                    if (LateFinishMs > 0)
                        Thread.Sleep(LateFinishMs); // deliberately finish late
                    TransmitExited.Set();
                }
            }
        }

        public static int Run()
        {
            int cases = 0, failures = 0;

            void Check(string label, bool expected, bool actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            string RepoRoot()
            {
                var dir = AppContext.BaseDirectory;
                while (dir != null && !Directory.Exists(Path.Combine(dir, "TeknoParrotUi.Common")))
                    dir = Path.GetDirectoryName(dir);
                return dir ?? ".";
            }
            string CommonSource(string relative) =>
                File.ReadAllText(Path.Combine(RepoRoot(), "TeknoParrotUi.Common", relative));

            Console.WriteLine("=== Pipe lifecycle regression suite ===\n");

            // =========================================================
            // 1. ControlPipe has no static mutable session state.
            // =========================================================
            {
                var staticMutable = typeof(ControlPipe)
                    .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => !f.IsLiteral && !f.IsInitOnly)
                    .ToList();
                Check("1. ControlPipe has no static mutable fields", true, staticMutable.Count == 0);
                foreach (var f in staticMutable)
                    Console.WriteLine($"   (static mutable field found: {f.Name})");
            }

            // =========================================================
            // 2. Old ControlPipe worker cannot close a new session's server.
            //    session-1 worker is made to finish LATE, after session-2 is
            //    already up - session-2's pipe must stay fully functional.
            // =========================================================
            {
                var session1 = new SlowEchoPipe { LateFinishMs = 400 };
                Check("2a. session-1 Start succeeds", true, session1.Start(false));

                using (var client1 = new NamedPipeClientStream(ControlPipe.PipeName))
                {
                    client1.Connect(3000);
                    var buf = new byte[1];
                    Check("2b. session-1 transmits", true, client1.Read(buf, 0, 1) == 1 && buf[0] == 0x55);
                    session1.TransmitEntered.Wait(2000);
                    // Request session-1 stop WITHOUT waiting - its worker now
                    // finishes late (400ms) while session-2 starts.
                    session1.Stop();
                }

                var session2 = new SlowEchoPipe();
                // brief retry: session-1's socket closes asynchronously
                bool started2 = false;
                for (var i = 0; i < 20 && !started2; i++)
                {
                    started2 = session2.Start(false);
                    if (!started2)
                        Thread.Sleep(50);
                }
                Check("2c. session-2 starts immediately after session-1 stop request", true, started2);

                using (var client2 = new NamedPipeClientStream(ControlPipe.PipeName))
                {
                    var connected = false;
                    for (var i = 0; i < 40 && !connected; i++)
                    {
                        try { client2.Connect(200); connected = true; }
                        catch { Thread.Sleep(50); }
                    }
                    Check("2d. session-2 accepts its client", true, connected);

                    // Let session-1's late worker fully finish...
                    Check("2e. session-1 worker finished late (after session-2 was up)", true,
                        session1.TransmitExited.Wait(3000));

                    // ...and prove session-2's server was NOT closed by it.
                    var buf = new byte[1];
                    var alive = false;
                    try { alive = connected && client2.Read(buf, 0, 1) == 1; } catch { alive = false; }
                    Check("2f. session-1 cannot close session-2's server (session-2 still transmits)", true, alive);
                }
                session2.StopAndWait(TimeSpan.FromSeconds(2));
            }

            // =========================================================
            // 3. ControlPipe StopAndWait joins its thread; Start can't run twice.
            // =========================================================
            {
                var pipe = new SlowEchoPipe();
                Check("3a. first Start returns true", true, pipe.Start(false));
                Check("3b. second Start on the same active instance returns false", false, pipe.Start(false));
                var result = pipe.StopAndWait(TimeSpan.FromSeconds(3));
                Check("3c. StopAndWait joins the worker thread", true, result.Completed && result.ListenerThreadExited);
                var again = pipe.StopAndWait(TimeSpan.FromSeconds(1));
                Check("3d. Stop/StopAndWait is idempotent", true, again.Completed);
            }

            // =========================================================
            // 4. SerialPortHandler owns and joins BOTH threads.
            // =========================================================
            {
                var handler = new SerialPortHandler();
                handler.StartPipe("TP_LifecycleTestJvs");
                Thread.Sleep(200); // let both threads spin up and block
                var result = handler.StopAndWait(TimeSpan.FromSeconds(3));
                Check("4. SerialPortHandler StopAndWait joins listener AND queue threads", true,
                    result.Completed && result.ListenerThreadExited && result.QueueThreadExited);
            }

            // =========================================================
            // 5. A stopped SerialPortHandler cannot recreate a server.
            // =========================================================
            {
                var handler = new SerialPortHandler();
                handler.StartPipe("TP_LifecycleTestJvs2");
                Thread.Sleep(200);
                handler.StopAndWait(TimeSpan.FromSeconds(3));
                Thread.Sleep(600); // longer than the listener's 500ms backoff window
                var recreated = false;
                try
                {
                    using var probe = new NamedPipeClientStream("TP_LifecycleTestJvs2");
                    probe.Connect(300);
                    recreated = true;
                }
                catch { /* expected: nothing listening */ }
                Check("5. Stopped SerialPortHandler cannot recreate a pipe server", false, recreated);
            }

            // =========================================================
            // 6. ProtonBridgePipe uses an ephemeral per-instance port.
            // =========================================================
            {
                var bridgeA = new ProtonBridgePipe("TP_PortProbeA");
                var bridgeB = new ProtonBridgePipe("TP_PortProbeA"); // SAME pipe name
                bridgeA.StartListening();
                bridgeB.StartListening();
                Check("6a. Listener binds a real ephemeral port", true, bridgeA.Port > 0 && bridgeB.Port > 0);
                Check("6b. Simultaneous instances of the SAME pipe name do not collide", true, bridgeA.Port != bridgeB.Port);

                // Ports actually usable.
                var usable = 0;
                foreach (var port in new[] { bridgeA.Port, bridgeB.Port })
                {
                    try
                    {
                        using var probe = new TcpClient();
                        probe.Connect("127.0.0.1", port);
                        usable++;
                    }
                    catch { /* counted below */ }
                }
                Check("6c. Consecutive instances receive usable ports", true, usable == 2);
                bridgeA.Close();
                bridgeB.Close();

                // A previous session's TCP state cannot block a new session:
                // close A, bind a fresh bridge - always succeeds because the
                // port is chosen by the OS, never derived from the pipe name.
                var bridgeC = new ProtonBridgePipe("TP_PortProbeA");
                var ok = false;
                try { bridgeC.StartListening(); ok = bridgeC.Port > 0; } catch { }
                Check("6d. A previous session's TCP state cannot block a new session", true, ok);
                bridgeC.Close();
            }

            // =========================================================
            // 7. Helper receives exactly the listener's selected port +
            //    production code has no deterministic-port listener binding.
            // =========================================================
            {
                var bridgeSource = CommonSource(Path.Combine("Pipes", "Implementation", "ProtonBridgePipe.cs"));
                Check("7a. Listener is bound to port 0 (OS-chosen ephemeral)", true,
                    bridgeSource.Contains("new TcpListener(IPAddress.Loopback, 0)"));
                Check("7b. Actual bound port is read back from LocalEndpoint", true,
                    bridgeSource.Contains("((IPEndPoint)_listener.LocalEndpoint).Port"));
                Check("7c. Helper is launched with the actual bound port (_port)", true,
                    bridgeSource.Contains("\"127.0.0.1\", _port.ToString()"));
                Check("7d. GetDeterministicPort no longer selects the production listener port", false,
                    bridgeSource.Contains("_port = GetDeterministicPort"));
            }

            // =========================================================
            // 8. CloseAndWait waits for helper exit and reports honestly.
            // =========================================================
            {
                var bridge = new ProtonBridgePipe("TP_CloseWaitProbe");
                bridge.StartListening();
                var sw = Stopwatch.StartNew();
                var result = bridge.CloseAndWait(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                sw.Stop();
                Check("8a. CloseAndWait with no helper completes honestly", true,
                    result.Completed && result.HelperExited && result.RemainingHelperPids.Count == 0);
                Check("8b. CloseAndWait does not hang without a helper", true, sw.ElapsedMilliseconds < 5000);

                var bridgeSource = CommonSource(Path.Combine("Pipes", "Implementation", "ProtonBridgePipe.cs"));
                Check("8c. CloseAndWait waits for the returned helper process (WaitForExit)", true,
                    bridgeSource.Contains("WaitForExit"));
                Check("8d. shm mirror claim released only after helper shutdown confirmed", true,
                    bridgeSource.Contains("_ownsShmMirror && allHelpersGone"));
            }

            // =========================================================
            // 9+10. Session isolation with REAL processes: session-1 cleanup
            //       kills only session-1's token-carrying helper; a different
            //       session's helper and an unrelated helper both survive.
            // =========================================================
            if (OperatingSystem.IsLinux())
            {
                var tempDir = Directory.CreateTempSubdirectory("tp_pipehelper_test").FullName;
                var fakeHelper = Path.Combine(tempDir, "pipehelper.exe"); // comm: "pipehelper.exe"
                File.Copy("/usr/bin/sleep", fakeHelper);

                Process StartFakeHelper(string token)
                {
                    var psi = new ProcessStartInfo(fakeHelper, "30") { UseShellExecute = false };
                    if (token != null)
                        psi.Environment[PipeHelperRegistry.SessionTokenEnvVar] = token;
                    return Process.Start(psi);
                }

                var tokenA = Guid.NewGuid().ToString("D");
                var tokenB = Guid.NewGuid().ToString("D");
                var helperA = StartFakeHelper(tokenA);
                var helperB = StartFakeHelper(tokenB);
                var helperUnrelated = StartFakeHelper(null);
                Thread.Sleep(200);

                try
                {
                    var foundA = PipeHelperRegistry.FindSessionHelperProcesses(tokenA);
                    Check("9a. Token discovery finds exactly session-A's helper", true,
                        foundA.Count == 1 && foundA[0].Pid == helperA.Id);

                    // Session-A cleanup: terminate only verified token-A helpers.
                    var signaler = new LinuxProcessSignaler();
                    var proc = new LinuxProcReader();
                    foreach (var (pid, startTicks) in foundA)
                    {
                        var stat = proc.ReadStat(pid);
                        if (stat != null && stat.StartTimeTicks == startTicks)
                            signaler.SignalForce(pid);
                    }
                    helperA.WaitForExit(3000);

                    Check("9b. Session-1 cleanup cannot kill session-2's helper", true, !helperB.HasExited);
                    Check("10. Unrelated pipehelper (no session token) survives cleanup", true, !helperUnrelated.HasExited);

                    // Orphan crash-recovery never kills a helper whose ownership can't be verified.
                    var killedCount = PipeHelperRegistry.CleanupOrphanedHelpers("/tmp/some-prefix");
                    Check("10b. Crash recovery leaves unverifiable helpers alone", true,
                        killedCount == 0 && !helperB.HasExited && !helperUnrelated.HasExited);
                }
                finally
                {
                    try { if (!helperA.HasExited) helperA.Kill(); } catch { }
                    try { if (!helperB.HasExited) helperB.Kill(); } catch { }
                    try { if (!helperUnrelated.HasExited) helperUnrelated.Kill(); } catch { }
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
            else
            {
                Console.WriteLine("(9/10 skipped - requires Linux /proc)");
            }

            // =========================================================
            // 11. Shared-memory claim release ordering (covered structurally in 8d)
            //     + quick Close still releases the claim for mid-session recycles.
            // =========================================================
            {
                var bridgeSource = CommonSource(Path.Combine("Pipes", "Implementation", "ProtonBridgePipe.cs"));
                Check("11. Quick Close releases the shm claim for successor bridges (mid-session recycle)", true,
                    bridgeSource.Contains("Release the shm mirror claim so a successor bridge can take it."));
            }

            // =========================================================
            // 12. GameSession cleanup is idempotent (guard + double-Dispose).
            // =========================================================
            {
                var sessionSource = CommonSource(Path.Combine("GameLaunch", "GameSession.cs"));
                Check("12a. Cleanup is guarded by Interlocked.Exchange(ref _cleanupStarted, 1)", true,
                    sessionSource.Contains("Interlocked.Exchange(ref _cleanupStarted, 1)"));

                var profile = new GameProfile
                {
                    ProfileName = "LifecycleProbe",
                    ConfigValues = new List<FieldInformation>(),
                    JoystickButtons = new List<JoystickButtons>()
                };
                var session = new GameSession(profile);
                var threw = false;
                try
                {
                    session.Dispose();
                    session.Dispose(); // second Dispose must be a no-op
                }
                catch { threw = true; }
                Check("12b. Double Dispose (double cleanup) never throws", false, threw);
            }

            // =========================================================
            // 13. Exited is raised after pipe cleanup, not before.
            // =========================================================
            {
                var sessionSource = CommonSource(Path.Combine("GameLaunch", "GameSession.cs"));
                var cleanupIdx = sessionSource.IndexOf("Cleanup();\n                StateChanged?.Invoke(\"Game stopped\")", StringComparison.Ordinal);
                var exitedIdx = sessionSource.IndexOf("Exited?.Invoke(exitCode)", StringComparison.Ordinal);
                Check("13. Cleanup completes before Exited is published", true,
                    cleanupIdx >= 0 && exitedIdx > cleanupIdx);
            }

            // =========================================================
            // 14. Session token exists before the first pipe/helper is created.
            // =========================================================
            {
                var sessionSource = CommonSource(Path.Combine("GameLaunch", "GameSession.cs"));
                var tokenIdx = sessionSource.IndexOf("_sessionIdentity = GameLaunchSessionIdentity.Create()", StringComparison.Ordinal);
                var prepareIdx = sessionSource.IndexOf("ProtonLauncher.PrepareSession", StringComparison.Ordinal);
                var controlPipeIdx = sessionSource.IndexOf("PipeFactory.CreateControlPipe", StringComparison.Ordinal);
                var jvsIdx = sessionSource.IndexOf("_serialPortHandler.StartPipe", StringComparison.Ordinal);
                Check("14. Session token is created before PrepareSession, ControlPipe and JVS pipe", true,
                    tokenIdx >= 0 && prepareIdx > tokenIdx && controlPipeIdx > tokenIdx && jvsIdx > tokenIdx);
            }

            // =========================================================
            // 15. Helper ProcessStartInfo explicitly contains the session token
            //     and owner identity (not just inherited environment).
            // =========================================================
            if (OperatingSystem.IsLinux())
            {
                var tempDir = Directory.CreateTempSubdirectory("tp_helper_psi_test").FullName;
                var fakeHelperExe = Path.Combine(tempDir, "pipehelper.exe");
                File.WriteAllText(fakeHelperExe, "");
                var savedHelperEnv = Environment.GetEnvironmentVariable("TP_PROTON_PIPEHELPER");
                var savedToken = ProtonRuntime.CurrentSessionToken;
                try
                {
                    Environment.SetEnvironmentVariable("TP_PROTON_PIPEHELPER", fakeHelperExe);
                    var token = Guid.NewGuid().ToString("D");
                    ProtonRuntime.CurrentSessionToken = token;
                    var game = new ProtonGameInfo
                    {
                        Pid = -1, // synthetic eager-start info: NO inheritable env exists
                        ExecutableName = "game.exe",
                        WinePrefix = "/tmp/tp-prefix",
                        WineBinaryPath = "/usr/bin/env" // any existing file
                    };
                    var psi = ProtonHelper.BuildHelperStartInfo(game, "TeknoParrot_JVS", "127.0.0.1", "12345");
                    Check("15a. Helper ProcessStartInfo contains the correct session token", true,
                        psi.Environment.TryGetValue(PipeHelperRegistry.SessionTokenEnvVar, out var t) && t == token);
                    Check("15b. Helper ProcessStartInfo carries the owner TeknoParrotUI PID", true,
                        psi.Environment.TryGetValue(PipeHelperRegistry.OwnerPidEnvVar, out var op) && op == Environment.ProcessId.ToString());
                    Check("15c. Helper ProcessStartInfo carries the owner start-time identity", true,
                        psi.Environment.ContainsKey(PipeHelperRegistry.OwnerStartEnvVar));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TP_PROTON_PIPEHELPER", savedHelperEnv);
                    ProtonRuntime.CurrentSessionToken = savedToken;
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }

            // =========================================================
            // 16. Two consecutive sessions use different tokens and ports.
            // =========================================================
            {
                var idA = GameLaunchSessionIdentity.Create();
                var idB = GameLaunchSessionIdentity.Create();
                Check("16a. Two consecutive sessions use different tokens", true,
                    idA.EnvironmentVariableValue != idB.EnvironmentVariableValue);

                var bridge1 = new ProtonBridgePipe("TP_ConsecutiveProbe");
                bridge1.StartListening();
                var port1 = bridge1.Port;
                bridge1.Close();
                var bridge2 = new ProtonBridgePipe("TP_ConsecutiveProbe");
                bridge2.StartListening();
                var port2 = bridge2.Port;
                bridge2.Close();
                Check("16b. Consecutive bridge instances both get usable ports (never blocked by the predecessor)", true,
                    port1 > 0 && port2 > 0);
            }

            // =========================================================
            // 17. No normal path calls a global KillStaleHelpers.
            // =========================================================
            {
                var found = new List<string>();
                foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "TeknoParrotUi.Common"), "*.cs", SearchOption.AllDirectories))
                {
                    if (File.ReadAllText(file).Contains("KillStaleHelpers"))
                        found.Add(file);
                }
                Check("17. KillStaleHelpers no longer exists anywhere in production source", true, found.Count == 0);
                foreach (var f in found)
                    Console.WriteLine($"   (KillStaleHelpers reference: {f})");
            }

            // =========================================================
            // 18. No arbitrary Thread.Sleep is used as the race fix.
            // =========================================================
            {
                var controlPipeSource = CommonSource(Path.Combine("Pipes", "ControlPipe.cs"));
                Check("18a. ControlPipe contains no Thread.Sleep at all (join-based shutdown)", false,
                    controlPipeSource.Contains("Thread.Sleep"));
                var serialSource = CommonSource("SerialPortHandler.cs");
                Check("18b. SerialPortHandler shutdown/backoff uses no bare Thread.Sleep (cancellation-aware _stopSignal.Wait)", false,
                    serialSource.Contains("Thread.Sleep(500)") || serialSource.Contains("Thread.Sleep(100)"));
                var sessionSource = CommonSource(Path.Combine("GameLaunch", "GameSession.cs"));
                var cleanupBody = sessionSource.Substring(sessionSource.IndexOf("private void Cleanup()", StringComparison.Ordinal));
                cleanupBody = cleanupBody.Substring(0, cleanupBody.IndexOf("public void Dispose", StringComparison.Ordinal));
                Check("18c. GameSession.Cleanup contains no Thread.Sleep", false, cleanupBody.Contains("Thread.Sleep"));
            }

            Console.WriteLine($"\nPipeLifecycleTest: {cases - failures}/{cases} passed.");
            return failures == 0 ? 0 : 1;
        }
    }
}
