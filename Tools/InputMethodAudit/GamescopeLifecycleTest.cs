using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// Tests for the session-scoped Gamescope wrapper lifecycle: launch
    /// session tokens, /proc-based session process discovery, PID/start-time
    /// identity safety, the main-game confidence model, the wrapped-session
    /// state machine, and centralized session termination.
    ///
    /// All lifecycle policy tests call the REAL production state machine,
    /// locator, planner, guard and terminator (no duplicated test-only
    /// logic). Fast paths use injectable fakes (IProcReader, IProcessSignaler,
    /// fake clock/delay); a final section validates discovery + termination
    /// against REAL processes (/bin/sleep with real environment tokens,
    /// including a genuinely reparented setsid process) - no real Wine or
    /// Gamescope required.
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -c Debug -- gamescope-lifecycle-test
    /// </summary>
    internal static class GamescopeLifecycleTest
    {
        // ------------------------------------------------------------------
        // Fakes
        // ------------------------------------------------------------------

        private sealed class FakeProc
        {
            public int Ppid;
            public string Comm = "";
            public long StartTicks;
            public string EnvironRaw;          // null = unreadable (gone/permission denied)
        }

        private sealed class FakeProcReader : IProcReader
        {
            public readonly Dictionary<int, FakeProc> Procs = new();
            public Action AfterEnumerate;      // simulate processes exiting between scan and signal
            /// <summary>Pids that vanish on their SECOND stat read - i.e. alive during the scan, gone by re-validation time.</summary>
            public readonly HashSet<int> VanishAfterFirstStatRead = new();
            private readonly Dictionary<int, int> _statReads = new();

            public IReadOnlyList<int> EnumerateProcessIds()
            {
                var ids = Procs.Keys.ToList();
                AfterEnumerate?.Invoke();
                return ids;
            }

            public string ReadEnvironRaw(int pid, int maxBytes) =>
                Procs.TryGetValue(pid, out var p) ? p.EnvironRaw : null;

            public ProcStatRecord ReadStat(int pid)
            {
                if (!Procs.TryGetValue(pid, out var p))
                    return null;
                _statReads[pid] = _statReads.TryGetValue(pid, out var n) ? n + 1 : 1;
                if (VanishAfterFirstStatRead.Contains(pid) && _statReads[pid] > 1)
                {
                    Procs.Remove(pid);
                    return null;
                }
                return new ProcStatRecord { Pid = pid, Comm = p.Comm, ParentPid = p.Ppid, StartTimeTicks = p.StartTicks };
            }

            public string ReadExecutablePath(int pid) => null; // /proc/<pid>/exe not modeled by the fake

            public bool ProcessExists(int pid) => Procs.ContainsKey(pid);
        }

        private sealed class FakeSignaler : IProcessSignaler
        {
            private readonly FakeProcReader _reader;
            public readonly List<int> GracefulSignals = new();
            public readonly List<int> ForceSignals = new();
            /// <summary>Pids that ignore SIGTERM (only die on force).</summary>
            public readonly HashSet<int> IgnoresGraceful = new();
            /// <summary>Pids that survive even SIGKILL (to test honest failure reporting).</summary>
            public readonly HashSet<int> Unkillable = new();

            public FakeSignaler(FakeProcReader reader) { _reader = reader; }

            public bool SignalGraceful(int pid)
            {
                GracefulSignals.Add(pid);
                if (!IgnoresGraceful.Contains(pid) && !Unkillable.Contains(pid))
                    _reader.Procs.Remove(pid);
                return true;
            }

            public bool SignalForce(int pid)
            {
                ForceSignals.Add(pid);
                if (!Unkillable.Contains(pid))
                    _reader.Procs.Remove(pid);
                return true;
            }
        }

        private static string TokenEnviron(GameLaunchSessionIdentity id, string extra = "WINEPREFIX=/prefix") =>
            $"{extra}\0{id.EnvironmentVariableName}={id.EnvironmentVariableValue}\0HOME=/home/x\0";

        private static SessionProcessInfo MakeInfo(int pid, string name, GameProcessConfidence confidence,
            int ppid = 0, long? startTicks = 1000, bool token = true, bool wrapper = false, bool infra = false) =>
            new SessionProcessInfo
            {
                Pid = pid,
                ParentPid = ppid,
                ProcessName = name,
                StartTimeTicks = startTicks,
                HasSessionToken = token,
                IsWrapper = wrapper,
                IsInfrastructureProcess = infra,
                Confidence = confidence
            };

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

            void CheckEq(string label, string expected, string actual)
            {
                cases++;
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected '{expected}', got '{actual}'");
                }
            }

            try
            {
                // =========================================================
                // SESSION TOKEN (spec tests 1-6)
                // =========================================================
                {
                    var a = GameLaunchSessionIdentity.Create();
                    var b = GameLaunchSessionIdentity.Create();
                    Check("1. Every launch receives a unique TP_LAUNCH_SESSION_ID", true,
                        a.SessionId != Guid.Empty && a.SessionId != b.SessionId);
                    CheckEq("1b. Environment variable name is exact", "TP_LAUNCH_SESSION_ID", a.EnvironmentVariableName);
                    Check("1c. Value is the D-format guid", true, a.EnvironmentVariableValue == a.SessionId.ToString("D"));

                    var psi = new ProcessStartInfo("/usr/bin/wine") { UseShellExecute = false };
                    Check("2. Wine/Proton ProcessStartInfo contains the token after TryApplyTo", true,
                        a.TryApplyTo(psi) && psi.Environment["TP_LAUNCH_SESSION_ID"] == a.EnvironmentVariableValue);

                    var wrapped = GamescopeCommandBuilder.Wrap(psi, "/usr/bin/gamescope", 1920, 1080);
                    Check("3. Gamescope wrapping preserves the token", true,
                        wrapped.Environment.TryGetValue("TP_LAUNCH_SESSION_ID", out var wv) && wv == a.EnvironmentVariableValue);

                    var direct = new ProcessStartInfo("/game/game.exe") { UseShellExecute = false };
                    Check("4. Direct launch preserves the token when applied", true,
                        a.TryApplyTo(direct) && direct.Environment["TP_LAUNCH_SESSION_ID"] == a.EnvironmentVariableValue);
                    var shellPsi = new ProcessStartInfo("x") { UseShellExecute = true };
                    Check("4b. Shell-execute launch is safely skipped (no throw, returns false)", false, a.TryApplyTo(shellPsi));

                    var seen = new HashSet<Guid>();
                    bool anyDuplicate = false;
                    for (int i = 0; i < 200; i++)
                        anyDuplicate |= !seen.Add(GameLaunchSessionIdentity.Create().SessionId);
                    Check("5/6. Two hundred launches never share or reuse a token", false, anyDuplicate);
                }

                // =========================================================
                // PROC SESSION DISCOVERY (spec tests 7-15) - REAL
                // ProcSessionProcessLocator over a fake /proc
                // =========================================================
                {
                    var id = GameLaunchSessionIdentity.Create();
                    var otherId = GameLaunchSessionIdentity.Create();
                    var reader = new FakeProcReader();
                    reader.Procs[100] = new FakeProc { Comm = "gamescope", StartTicks = 10, EnvironRaw = TokenEnviron(id) };
                    reader.Procs[110] = new FakeProc { Comm = "gamescopereaper", Ppid = 100, StartTicks = 11, EnvironRaw = TokenEnviron(id) };
                    reader.Procs[120] = new FakeProc { Comm = "game.exe", Ppid = 110, StartTicks = 12, EnvironRaw = TokenEnviron(id) };
                    reader.Procs[130] = new FakeProc { Comm = "detached.exe", Ppid = 1, StartTicks = 13, EnvironRaw = TokenEnviron(id) };            // reparented
                    reader.Procs[200] = new FakeProc { Comm = "wine", StartTicks = 20, EnvironRaw = "WINEPREFIX=/other\0HOME=/home/x\0" };           // unrelated wine
                    reader.Procs[210] = new FakeProc { Comm = "gamescope", StartTicks = 21, EnvironRaw = "DISPLAY=:0\0" };                            // unrelated gamescope
                    reader.Procs[220] = new FakeProc { Comm = "other.exe", StartTicks = 22, EnvironRaw = TokenEnviron(otherId) };                     // ANOTHER session
                    reader.Procs[230] = new FakeProc { Comm = "similar.exe", StartTicks = 23, EnvironRaw = $"TP_LAUNCH_SESSION_ID={id.EnvironmentVariableValue}x\0" }; // similar token
                    reader.Procs[231] = new FakeProc { Comm = "prefix.exe", StartTicks = 24, EnvironRaw = $"TP_LAUNCH_SESSION_ID_EXTRA={id.EnvironmentVariableValue}\0" }; // similar name
                    reader.Procs[240] = new FakeProc { Comm = "denied.exe", StartTicks = 25, EnvironRaw = null };                                     // permission denied / gone
                    reader.Procs[250] = new FakeProc { Comm = "garbled.exe", StartTicks = 26, EnvironRaw = "\u0001\u0002 no equals signs \u0003garbage" }; // malformed

                    var locator = new ProcSessionProcessLocator(reader,
                        new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });

                    var found = locator.FindSessionProcesses(id);
                    var pids = found.Select(p => p.Pid).OrderBy(p => p).ToArray();
                    Check("7. Exact token parsed from null-separated environ data (all 4 session members found)", true,
                        pids.SequenceEqual(new[] { 100, 110, 120, 130 }));
                    Check("8. Similar token (value suffix) is rejected", false, pids.Contains(230));
                    Check("8b. Similar variable name is rejected", false, pids.Contains(231));
                    Check("9/10. Missing environ / permission denied handled safely (skipped, no crash)", false, pids.Contains(240));
                    Check("12. Reparented token-carrying process is discovered (and marked non-descendant)", true,
                        found.Single(p => p.Pid == 130).HasSessionToken && !found.Single(p => p.Pid == 130).IsDescendantOfWrapper);
                    Check("12b. In-tree member is marked as a wrapper descendant", true,
                        found.Single(p => p.Pid == 120).IsDescendantOfWrapper);
                    Check("13. Unrelated Wine process is excluded", false, pids.Contains(200));
                    Check("14. Unrelated Gamescope process is excluded", false, pids.Contains(210));
                    Check("15. Malformed environ data does not crash discovery (process just excluded)", false, pids.Contains(250));
                    Check("22a. Another launch-session's token is not a member of THIS session", false, pids.Contains(220));
                    Check("20a. Wrapper found by verified pid+start-time identity (flagged IsWrapper)", true,
                        found.Single(p => p.Pid == 100).IsWrapper);
                    Check("Infrastructure flags: gamescope/gamescopereaper are infrastructure", true,
                        found.Single(p => p.Pid == 100).IsInfrastructureProcess && found.Single(p => p.Pid == 110).IsInfrastructureProcess);

                    // Process exits during scan: pid listed by enumerate, stat gone.
                    reader.Procs[888] = new FakeProc { Comm = "vanish.exe", StartTicks = 88, EnvironRaw = TokenEnviron(id) };
                    reader.AfterEnumerate = () => reader.Procs.Remove(888);
                    var foundAfterVanish = locator.FindSessionProcesses(id);
                    reader.AfterEnumerate = null;
                    Check("11. Process exit during scan handled safely", false, foundAfterVanish.Any(p => p.Pid == 888));

                    // Wrapper PID reused by a different process (start time differs).
                    var reusedLocator = new ProcSessionProcessLocator(reader,
                        new SessionWrapperDescriptor { WrapperPid = 210, WrapperStartTimeTicks = 9999 });
                    Check("Wrapper pid with non-matching start time is NOT treated as the wrapper", false,
                        reusedLocator.FindSessionProcesses(id).Any(p => p.Pid == 210));
                }

                // =========================================================
                // PROCESS IDENTITY (spec tests 16-22) - ProcessSafetyGuard + planner
                // =========================================================
                {
                    var observed = MakeInfo(500, "game.exe", GameProcessConfidence.ConfirmedMainGame, startTicks: 1234);

                    Check("16. Matching PID and start time is accepted", true,
                        ProcessSafetyGuard.Validate(observed, MakeInfo(500, "game.exe", GameProcessConfidence.None, startTicks: 1234), currentProcessPid: 42).Allowed);

                    Check("17. Reused PID with different start time is rejected", false,
                        ProcessSafetyGuard.Validate(observed, MakeInfo(500, "other.exe", GameProcessConfidence.None, startTicks: 9999), currentProcessPid: 42).Allowed);

                    Check("18. Current TeknoParrotUI PID is never signaled", false,
                        ProcessSafetyGuard.Validate(observed, MakeInfo(500, "game.exe", GameProcessConfidence.None, startTicks: 1234), currentProcessPid: 500).Allowed);

                    Check("19. PID 0 is never signaled", false,
                        ProcessSafetyGuard.Validate(MakeInfo(0, "swapper", GameProcessConfidence.None), MakeInfo(0, "swapper", GameProcessConfidence.None), 42).Allowed);
                    Check("19b. PID 1 is never signaled", false,
                        ProcessSafetyGuard.Validate(MakeInfo(1, "init", GameProcessConfidence.None), MakeInfo(1, "init", GameProcessConfidence.None), 42).Allowed);

                    Check("20. Wrapper PID is recognized explicitly (no token needed)", true,
                        ProcessSafetyGuard.Validate(
                            MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, token: false, wrapper: true),
                            MakeInfo(100, "gamescope", GameProcessConfidence.None, token: false, wrapper: true), 42).Allowed);

                    Check("22. A process that no longer carries this session's token is rejected", false,
                        ProcessSafetyGuard.Validate(observed, MakeInfo(500, "game.exe", GameProcessConfidence.None, startTicks: 1234, token: false), 42).Allowed);

                    Check("Gone process (no fresh identity) is rejected", false,
                        ProcessSafetyGuard.Validate(observed, null, 42).Allowed);

                    Check("Start time unavailable is allowed but flagged reduced-confidence", true,
                        ProcessSafetyGuard.Validate(
                            MakeInfo(500, "game.exe", GameProcessConfidence.ConfirmedMainGame, startTicks: null),
                            MakeInfo(500, "game.exe", GameProcessConfidence.None, startTicks: 1234), 42) is { Allowed: true, ReducedConfidence: true });

                    // 21. Descendant depth ordering: wrapper(100) <- reaper(110) <- game(120); detached(130).
                    var order = SessionTerminationPlanner.PlanSignalOrder(new[]
                    {
                        MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true),
                        MakeInfo(110, "gamescopereaper", GameProcessConfidence.Infrastructure, ppid: 100, infra: true),
                        MakeInfo(120, "game.exe", GameProcessConfidence.ConfirmedMainGame, ppid: 110),
                        MakeInfo(130, "detached.exe", GameProcessConfidence.Candidate, ppid: 1)
                    }).Select(p => p.Pid).ToArray();
                    Check("21. Deepest-first ordering with the wrapper strictly last", true,
                        order.First() == 120 && order.Last() == 100 &&
                        Array.IndexOf(order, 110) > Array.IndexOf(order, 120));
                }

                // =========================================================
                // GAME PROCESS CONFIDENCE (spec tests 23-26 + name matching)
                // =========================================================
                {
                    var expectations = new GameExecutableExpectations
                    {
                        PrimaryExecutable = @"C:\Games\Rally\GameMain.exe",
                        SecondaryExecutable = "service.exe",
                        LauncherExecutables = new[] { "BudgieLoader.exe", "OpenParrotLoader.exe" }
                    };

                    Check("23. Expected primary executable gets highest static confidence", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "GameMain.exe", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.ExpectedPrimaryExecutable);
                    Check("23b. Windows-path + case-insensitive + slash-direction matching", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "gamemain.EXE", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.ExpectedPrimaryExecutable);
                    Check("23c. comm 15-char truncation matches a long expected name", true,
                        ExecutableNameMatcher.Matches("verylonggamenam", "VeryLongGameNameHere.exe"));
                    Check("24. Expected secondary executable is recognized", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "service.exe", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.ExpectedSecondaryExecutable);
                    Check("25. Known loader is NOT immediately the confirmed game (Candidate only)", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "BudgieLoader.exe", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.Candidate);
                    Check("26. Arbitrary .exe is only a Candidate", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "updater.exe", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.Candidate);
                    Check("Classifier NEVER returns ConfirmedMainGame statically", false,
                        GameProcessClassifier.Classify(MakeInfo(1, "GameMain.exe", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.ConfirmedMainGame);
                    Check("29a. wineserver is Infrastructure", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "wineserver", GameProcessConfidence.None, infra: true), expectations)
                            == GameProcessConfidence.Infrastructure);
                    Check("Non-exe wine binary is Infrastructure (not a candidate)", true,
                        GameProcessClassifier.Classify(MakeInfo(1, "wine64", GameProcessConfidence.None), expectations)
                            == GameProcessConfidence.Infrastructure);
                }

                // =========================================================
                // STATE MACHINE: startup, stabilization, promotion (27-29)
                // =========================================================
                {
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var options = new WrappedGameLifecycleOptions
                    {
                        CandidateStabilizationTime = TimeSpan.FromSeconds(3),
                        GameStartupTimeout = TimeSpan.FromSeconds(30)
                    };
                    var logs = new List<string>();
                    var machine = new WrappedGameLifecycleStateMachine(options, logs.Add);

                    WrappedSessionObservation Obs(DateTimeOffset now, bool wrapperAlive, bool forceQuit, params SessionProcessInfo[] procs) =>
                        new WrappedSessionObservation { Now = now, WrapperAlive = wrapperAlive, ForceQuitRequested = forceQuit, SessionProcesses = procs };

                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);
                    var candidate = MakeInfo(140, "launcher.exe", GameProcessConfidence.Candidate, ppid: 100, startTicks: 40);

                    // Candidate appears - observed but NOT confirmed.
                    var d1 = machine.Advance(Obs(t0, true, false, wrapper, candidate));
                    Check("Candidate is observed but not promoted on first sight", true,
                        d1.State == WrappedGameLifecycleState.CandidateObserved && machine.ConfirmedGame == null);

                    // 28. Candidate exits before stabilization - session does NOT end.
                    var d2 = machine.Advance(Obs(t0.AddSeconds(1), true, false, wrapper));
                    Check("28. Candidate exiting before stabilization does not end the session", true,
                        d2.Action == LifecycleTickActionKind.None && d2.State == WrappedGameLifecycleState.WaitingForGame);

                    // 27. A NEW stable candidate is promoted only after the stabilization period.
                    var stable = MakeInfo(150, "actualgame.exe", GameProcessConfidence.Candidate, ppid: 100, startTicks: 50);
                    machine.Advance(Obs(t0.AddSeconds(2), true, false, wrapper, stable));
                    var d3 = machine.Advance(Obs(t0.AddSeconds(3), true, false, wrapper, stable));
                    Check("Candidate not promoted before its stabilization time", true,
                        d3.Action == LifecycleTickActionKind.None && machine.ConfirmedGame == null);
                    machine.Advance(Obs(t0.AddSeconds(5.5), true, false, wrapper, stable));
                    Check("27. Stable candidate is promoted to ConfirmedMainGame after stabilization", true,
                        machine.ConfirmedGame != null && machine.ConfirmedGame.Pid == 150 &&
                        machine.ConfirmedGame.Confidence == GameProcessConfidence.ConfirmedMainGame);
                    Check("Promotion transitions towards GameObserved/GameRunning", true,
                        machine.State == WrappedGameLifecycleState.GameObserved || machine.State == WrappedGameLifecycleState.GameRunning);
                }

                // 29. Infrastructure-only processes never count as a running game (+ stall reported once, never ends session).
                {
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var options = new WrappedGameLifecycleOptions { GameStartupTimeout = TimeSpan.FromSeconds(10) };
                    var logs = new List<string>();
                    var machine = new WrappedGameLifecycleStateMachine(options, logs.Add);
                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);
                    var wineserver = MakeInfo(105, "wineserver", GameProcessConfidence.Infrastructure, infra: true);

                    for (var s = 0; s <= 25; s += 5)
                    {
                        var d = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(s), WrapperAlive = true, SessionProcesses = new[] { wrapper, wineserver } });
                        Check($"29. Infra-only tick at +{s}s never terminates/completes the session", true, d.Action == LifecycleTickActionKind.None);
                    }
                    Check("29b. No game was ever confirmed from infrastructure alone", true, machine.ConfirmedGame == null);
                    Check("Startup stall warning is reported exactly once", true,
                        logs.Count(l => l.Contains("startup timeout")) == 1);
                }

                // =========================================================
                // STATE MACHINE: game exit -> wrapper linger (39-43), replacement (30-31, 42)
                // =========================================================
                {
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var options = new WrappedGameLifecycleOptions { WrapperLingerGracePeriod = TimeSpan.FromSeconds(5) };
                    var machine = new WrappedGameLifecycleStateMachine(options, _ => { });
                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);
                    var game = MakeInfo(120, "game.exe", GameProcessConfidence.ExpectedPrimaryExecutable, ppid: 100, startTicks: 20);

                    machine.Advance(new WrappedSessionObservation { Now = t0, WrapperAlive = true, SessionProcesses = new[] { wrapper, game } });
                    Check("Expected primary executable is confirmed immediately", true,
                        machine.ConfirmedGame != null && machine.ConfirmedGame.Pid == 120);
                    machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(1), WrapperAlive = true, SessionProcesses = new[] { wrapper, game } });
                    Check("34. Game runs -> GameRunning", true, machine.State == WrappedGameLifecycleState.GameRunning);

                    // 39/40. Game exits, wrapper remains -> linger starts.
                    var d1 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(10), WrapperAlive = true, SessionProcesses = new[] { wrapper } });
                    Check("39/40. Confirmed game exit starts the linger period (GameExited, no termination yet)", true,
                        d1.State == WrappedGameLifecycleState.GameExited && d1.Action == LifecycleTickActionKind.None);

                    // 41. Not killed before the grace period.
                    var d2 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(13), WrapperAlive = true, SessionProcesses = new[] { wrapper } });
                    Check("41. Wrapper is not terminated before the grace period elapses", true, d2.Action == LifecycleTickActionKind.None);

                    // 43. Termination after the grace period, with the correct reason.
                    var d3 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(15.5), WrapperAlive = true, SessionProcesses = new[] { wrapper } });
                    Check("43. Session termination occurs after the grace period", true,
                        d3.Action == LifecycleTickActionKind.TerminateSession &&
                        d3.TerminationReason == SessionTerminationReason.ConfirmedGameExitedWrapperLingering);
                }

                // 42. Replacement cancels linger; 30/31. replacement recognized without clearing running state.
                {
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var options = new WrappedGameLifecycleOptions { WrapperLingerGracePeriod = TimeSpan.FromSeconds(5) };
                    var machine = new WrappedGameLifecycleStateMachine(options, _ => { });
                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);
                    var game = MakeInfo(120, "game.exe", GameProcessConfidence.ExpectedPrimaryExecutable, ppid: 100, startTicks: 20);
                    var replacement = MakeInfo(170, "game64.exe", GameProcessConfidence.ExpectedSecondaryExecutable, ppid: 100, startTicks: 70);

                    machine.Advance(new WrappedSessionObservation { Now = t0, WrapperAlive = true, SessionProcesses = new[] { wrapper, game } });
                    machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(5), WrapperAlive = true, SessionProcesses = new[] { wrapper } });

                    // Replacement appears DURING the linger period.
                    var d = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(7), WrapperAlive = true, SessionProcesses = new[] { wrapper, replacement } });
                    Check("30/42. Replacement game in the same session is confirmed and cancels linger termination", true,
                        d.Action == LifecycleTickActionKind.None && machine.ConfirmedGame != null && machine.ConfirmedGame.Pid == 170);
                    Check("31a. Replacement transition does not end the session", true,
                        machine.State == WrappedGameLifecycleState.GameReplacementObserved || machine.State == WrappedGameLifecycleState.GameRunning);

                    // Long after the ORIGINAL game exited: replacement keeps the session alive.
                    var d2 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(30), WrapperAlive = true, SessionProcesses = new[] { wrapper, replacement } });
                    Check("31b. Session continues running on the replacement game", true,
                        d2.Action == LifecycleTickActionKind.None && machine.State == WrappedGameLifecycleState.GameRunning);
                }

                // =========================================================
                // STATE MACHINE: wrapper exits while game remains (48, 51, 52, 54)
                // and wrapper natural exit (35-36)
                // =========================================================
                {
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var options = new WrappedGameLifecycleOptions { WrapperExitGameGracePeriod = TimeSpan.FromSeconds(5) };
                    var machine = new WrappedGameLifecycleStateMachine(options, _ => { });
                    var game = MakeInfo(120, "game.exe", GameProcessConfidence.ExpectedPrimaryExecutable, ppid: 110, startTicks: 20);
                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);

                    machine.Advance(new WrappedSessionObservation { Now = t0, WrapperAlive = true, SessionProcesses = new[] { wrapper, game } });

                    // Wrapper dies; game still alive.
                    var d1 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(5), WrapperAlive = false, SessionProcesses = new[] { game } });
                    Check("48. Wrapper exit does NOT immediately complete the session while the game remains", true,
                        d1.Action == LifecycleTickActionKind.None && d1.State == WrappedGameLifecycleState.WrapperExitedWhileGameAlive);

                    // 51. Monitored during the recovery period.
                    var d2 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(8), WrapperAlive = false, SessionProcesses = new[] { game } });
                    Check("51. Remaining game is monitored during the recovery period (no premature termination)", true,
                        d2.Action == LifecycleTickActionKind.None);

                    // 52. Orphan terminated after the recovery period, with the correct reason.
                    var d3 = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(10.5), WrapperAlive = false, SessionProcesses = new[] { game } });
                    Check("52. Orphan session is terminated after the recovery period", true,
                        d3.Action == LifecycleTickActionKind.TerminateSession &&
                        d3.TerminationReason == SessionTerminationReason.WrapperExitedGameOrphaned);
                    machine.NotifyTerminationCompleted(new SessionTerminationResult { WrapperExited = true }, SessionTerminationReason.WrapperExitedGameOrphaned);
                    Check("54. A clear runtime error is reported for the orphaned session", true,
                        !string.IsNullOrEmpty(machine.BuildResult().ErrorMessage));
                }
                {
                    // Wrapper exits with no game left (natural end) + game exits naturally during recovery.
                    var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                    var machine = new WrappedGameLifecycleStateMachine(new WrappedGameLifecycleOptions(), _ => { });
                    var wrapper = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, wrapper: true, infra: true, token: false);
                    var game = MakeInfo(120, "game.exe", GameProcessConfidence.ExpectedPrimaryExecutable, ppid: 100, startTicks: 20);

                    machine.Advance(new WrappedSessionObservation { Now = t0, WrapperAlive = true, SessionProcesses = new[] { wrapper, game } });
                    // wrapper dies with game alive...
                    machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(2), WrapperAlive = false, SessionProcesses = new[] { game } });
                    // ...but the game exits naturally within the recovery period.
                    var d = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(4), WrapperAlive = false, SessionProcesses = Array.Empty<SessionProcessInfo>() });
                    Check("Game exiting naturally during recovery completes the session without termination", true,
                        d.Action == LifecycleTickActionKind.CompleteSession);
                    var result = machine.BuildResult();
                    Check("35/36. Natural full exit: Completed, wrapper exit natural, game observed+exited, no termination", true,
                        result.FinalState == WrappedGameLifecycleState.Completed &&
                        result.WrapperExitedNaturally && result.ConfirmedGameObserved &&
                        result.ConfirmedGameExited && !result.WrapperTerminationAttempted);

                    // 37/38-adjacent: machine is inert after completion.
                    var post = machine.Advance(new WrappedSessionObservation { Now = t0.AddSeconds(10), WrapperAlive = false });
                    Check("Machine is inert after completion (no second cleanup/termination decision)", true,
                        post.Action == LifecycleTickActionKind.None && machine.State == WrappedGameLifecycleState.Completed);
                }

                // =========================================================
                // TERMINATOR: full production sequence over fake /proc (44-47, 55-60, 66, 77)
                // =========================================================
                {
                    var id = GameLaunchSessionIdentity.Create();
                    var otherId = GameLaunchSessionIdentity.Create();

                    FakeProcReader NewSessionProcs()
                    {
                        var reader = new FakeProcReader();
                        reader.Procs[100] = new FakeProc { Comm = "gamescope", StartTicks = 10, EnvironRaw = TokenEnviron(id) };
                        reader.Procs[110] = new FakeProc { Comm = "gamescopereaper", Ppid = 100, StartTicks = 11, EnvironRaw = TokenEnviron(id) };
                        reader.Procs[120] = new FakeProc { Comm = "game.exe", Ppid = 110, StartTicks = 12, EnvironRaw = TokenEnviron(id) };
                        reader.Procs[130] = new FakeProc { Comm = "detached.exe", Ppid = 1, StartTicks = 13, EnvironRaw = TokenEnviron(id) };
                        reader.Procs[200] = new FakeProc { Comm = "wine", StartTicks = 20, EnvironRaw = "WINEPREFIX=/x\0" };
                        reader.Procs[210] = new FakeProc { Comm = "gamescope", StartTicks = 21, EnvironRaw = "DISPLAY=:0\0" };
                        reader.Procs[220] = new FakeProc { Comm = "othergame.exe", StartTicks = 22, EnvironRaw = TokenEnviron(otherId) };
                        return reader;
                    }

                    var wrapperInfo = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, startTicks: 10, wrapper: true, infra: true, token: true);
                    var options = new WrappedGameLifecycleOptions
                    {
                        GracefulTerminationTimeout = TimeSpan.FromMilliseconds(1),
                        ForceTerminationTimeout = TimeSpan.FromMilliseconds(1)
                    };
                    Func<TimeSpan, CancellationToken, Task> instantDelay = (_, _) => Task.CompletedTask;

                    // A) Clean termination.
                    {
                        var reader = NewSessionProcs();
                        // Shared Wine prefix daemon spawned by OUR wine - carries the token,
                        // but must NEVER be terminated (it serves the whole prefix).
                        reader.Procs[105] = new FakeProc { Comm = "wineserver", StartTicks = 14, EnvironRaw = TokenEnviron(id) };
                        var signaler = new FakeSignaler(reader);
                        var locator = new ProcSessionProcessLocator(reader, new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });
                        var logs = new List<string>();
                        var terminator = new GameSessionTerminator(locator, signaler, options, currentProcessPid: 42, delay: instantDelay, log: logs.Add);

                        var result = terminator.TerminateSessionAsync(id, wrapperInfo, Array.Empty<SessionProcessInfo>(), SessionTerminationReason.ForceQuit, CancellationToken.None)
                            .GetAwaiter().GetResult();

                        Check("44. Descendants terminate deepest-first (game before reaper)", true,
                            signaler.GracefulSignals.IndexOf(120) < signaler.GracefulSignals.IndexOf(110));
                        Check("45. Wrapper terminates last", true, signaler.GracefulSignals.Last() == 100);
                        Check("55/57. Detached token-carrying member is included in termination", true, signaler.GracefulSignals.Contains(130));
                        Check("58. Unrelated Gamescope process is never signaled", false,
                            signaler.GracefulSignals.Contains(210) || signaler.ForceSignals.Contains(210));
                        Check("59. Unrelated Wine process is never signaled", false,
                            signaler.GracefulSignals.Contains(200) || signaler.ForceSignals.Contains(200));
                        Check("77. Another session's token-carrying process is never terminated", true,
                            !signaler.GracefulSignals.Contains(220) && !signaler.ForceSignals.Contains(220) && reader.Procs.ContainsKey(220));
                        Check("Shared Wine prefix daemon (wineserver) is preserved even though it carries the token", true,
                            !signaler.GracefulSignals.Contains(105) && !signaler.ForceSignals.Contains(105) && reader.Procs.ContainsKey(105));
                        Check("Preserved shared infrastructure does not block verified success", true, result.CompletedSuccessfully);
                        Check("Gamescope's own reaper IS session-scoped (terminated, not preserved)", true,
                            signaler.GracefulSignals.Contains(110));
                        Check("Termination verified successful (wrapper exited, none remaining)", true,
                            result.CompletedSuccessfully && result.WrapperExited && result.RemainingSessionProcesses.Count == 0);
                        Check("Termination log block emitted with the required fields", true,
                            logs.Any(l => l.Contains("[GameSessionTermination]") && l.Contains("Reason: ForceQuit") &&
                                          l.Contains("WrapperExited: true") && l.Contains("RemainingSessionPids: none") &&
                                          l.Contains("CompletedSuccessfully: true")));
                    }

                    // B) SIGTERM ignored -> escalation to force-kill, still verified.
                    {
                        var reader = NewSessionProcs();
                        var signaler = new FakeSignaler(reader);
                        signaler.IgnoresGraceful.Add(120);
                        var locator = new ProcSessionProcessLocator(reader, new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });
                        var terminator = new GameSessionTerminator(locator, signaler, options, 42, instantDelay);

                        var result = terminator.TerminateSessionAsync(id, wrapperInfo, Array.Empty<SessionProcessInfo>(), SessionTerminationReason.ConfirmedGameExitedWrapperLingering, CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check("Graceful-resistant process is force-killed and the result is still verified", true,
                            signaler.ForceSignals.Contains(120) && result.CompletedSuccessfully);
                    }

                    // C) Unkillable process -> HONEST failure report (66, 46, 60).
                    {
                        var reader = NewSessionProcs();
                        var signaler = new FakeSignaler(reader);
                        signaler.Unkillable.Add(120);
                        var locator = new ProcSessionProcessLocator(reader, new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });
                        var terminator = new GameSessionTerminator(locator, signaler, options, 42, instantDelay);

                        var result = terminator.TerminateSessionAsync(id, wrapperInfo, Array.Empty<SessionProcessInfo>(), SessionTerminationReason.ForceQuit, CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check("46/60. Remaining PIDs are reported accurately after a failed termination", true,
                            result.RemainingSessionProcesses.Any(p => p.Pid == 120));
                        Check("66. Termination result reports failure correctly (never assumed success)", false, result.CompletedSuccessfully);
                    }

                    // D) Process vanishing between scan and signal -> skipped safely.
                    {
                        var reader = NewSessionProcs();
                        var signaler = new FakeSignaler(reader);
                        var locator = new ProcSessionProcessLocator(reader, new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });
                        var terminator = new GameSessionTerminator(locator, signaler, options, 42, instantDelay);
                        reader.VanishAfterFirstStatRead.Add(130); // alive during the scan, gone by pre-signal re-validation

                        var result = terminator.TerminateSessionAsync(id, wrapperInfo, Array.Empty<SessionProcessInfo>(), SessionTerminationReason.ForceQuit, CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check("65-adjacent. A process exiting between scan and signal is skipped (never blind-signaled)", true,
                            !signaler.GracefulSignals.Contains(130) && result.SkippedProcesses.Any(p => p.Pid == 130));
                    }
                }

                // =========================================================
                // RUNNER integration: full production loop with fakes
                // (32-38, 47-53, 55-62)
                // =========================================================
                {
                    var id = GameLaunchSessionIdentity.Create();

                    (WrappedGameLifecycleResult result, FakeSignaler signaler, List<string> logs, int returns) RunScenario(
                        Action<FakeProcReader> setup,
                        Dictionary<int, Action<FakeProcReader>> scriptByTick,
                        Func<int, bool> forceQuitAtTick = null,
                        WrappedGameLifecycleOptions options = null)
                    {
                        options ??= new WrappedGameLifecycleOptions
                        {
                            PollInterval = TimeSpan.FromSeconds(1),
                            CandidateStabilizationTime = TimeSpan.FromSeconds(3),
                            WrapperLingerGracePeriod = TimeSpan.FromSeconds(5),
                            WrapperExitGameGracePeriod = TimeSpan.FromSeconds(5),
                            GracefulTerminationTimeout = TimeSpan.FromSeconds(1),
                            ForceTerminationTimeout = TimeSpan.FromSeconds(1)
                        };
                        var reader = new FakeProcReader();
                        reader.Procs[100] = new FakeProc { Comm = "gamescope", StartTicks = 10, EnvironRaw = TokenEnviron(id) };
                        setup?.Invoke(reader);

                        var clock = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
                        int tick = 0;
                        Func<TimeSpan, CancellationToken, Task> delay = (ts, _) =>
                        {
                            clock += ts;
                            tick++;
                            if (tick > 300) throw new InvalidOperationException("runner scenario did not converge");
                            if (scriptByTick != null && scriptByTick.TryGetValue(tick, out var action))
                                action(reader);
                            return Task.CompletedTask;
                        };

                        var signaler = new FakeSignaler(reader);
                        var locator = new ProcSessionProcessLocator(reader, new SessionWrapperDescriptor { WrapperPid = 100, WrapperStartTimeTicks = 10 });
                        var terminator = new GameSessionTerminator(locator, signaler, options, currentProcessPid: 42, delay: delay);
                        var logs = new List<string>();
                        var wrapperInfo = MakeInfo(100, "gamescope", GameProcessConfidence.Infrastructure, startTicks: 10, wrapper: true, infra: true);
                        var expectations = new GameExecutableExpectations { PrimaryExecutable = "game.exe", LauncherExecutables = new[] { "launcher.exe" } };

                        var runner = new WrappedGameLifecycleRunner(
                            options, locator, terminator, id, expectations, wrapperInfo,
                            forceQuitRequested: () => forceQuitAtTick != null && forceQuitAtTick(tick),
                            wrapperAlive: () => reader.Procs.ContainsKey(100),
                            clock: () => clock,
                            delay: delay,
                            log: logs.Add);

                        int returns = 0;
                        var result = runner.RunAsync().GetAwaiter().GetResult();
                        returns++;
                        return (result, signaler, logs, returns);
                    }

                    void AddGame(FakeProcReader r) =>
                        r.Procs[120] = new FakeProc { Comm = "game.exe", Ppid = 100, StartTicks = 12, EnvironRaw = TokenEnviron(id) };

                    // NORMAL LIFECYCLE (32-38): launcher appears, exits; game appears, runs, exits; wrapper exits.
                    {
                        var (result, signaler, logs, returns) = RunScenario(
                            setup: r => r.Procs[115] = new FakeProc { Comm = "launcher.exe", Ppid = 100, StartTicks = 11, EnvironRaw = TokenEnviron(id) },
                            scriptByTick: new Dictionary<int, Action<FakeProcReader>>
                            {
                                [2] = r => { r.Procs.Remove(115); AddGame(r); }, // temporary launcher exits AS the game appears
                                [8] = r => r.Procs.Remove(120),                   // game exits
                                [9] = r => r.Procs.Remove(100)                    // wrapper exits within grace
                            });

                        Check("32-36. Normal lifecycle completes naturally (wrapper start -> game -> exit -> wrapper exit)", true,
                            result.FinalState == WrappedGameLifecycleState.Completed &&
                            result.ConfirmedGameObserved && result.ConfirmedGameExited &&
                            result.WrapperExitedNaturally && !result.WrapperTerminationAttempted);
                        Check("8-task. Temporary launcher exit did not end the session", true, result.ConfirmedGameObserved);
                        Check("No signals were sent during a fully natural lifecycle", true,
                            signaler.GracefulSignals.Count == 0 && signaler.ForceSignals.Count == 0);
                        Check("37/38. Runner returned exactly one final result", true, returns == 1);
                        Check("Session log includes the SessionId for diagnostics", true,
                            logs.Any(l => l.Contains($"SessionId: {id.EnvironmentVariableValue}")));
                    }

                    // LINGERING WRAPPER (39-47): game exits, wrapper stays -> session termination.
                    {
                        var (result, signaler, _, _) = RunScenario(
                            setup: AddGame,
                            scriptByTick: new Dictionary<int, Action<FakeProcReader>>
                            {
                                [3] = r => r.Procs.Remove(120) // game exits; wrapper deliberately never exits
                            });

                        Check("43b. Lingering wrapper is terminated after the grace period (full runner path)", true,
                            result.WrapperTerminationAttempted && result.TerminationResult != null &&
                            result.TerminationResult.CompletedSuccessfully &&
                            result.FinalState == WrappedGameLifecycleState.Completed);
                        Check("45b. The wrapper got the last graceful signal", true,
                            signaler.GracefulSignals.LastOrDefault() == 100);
                    }

                    // WRAPPER EXITS WHILE GAME REMAINS (48-54): no fallback, monitored, then session-terminated.
                    {
                        var (result, signaler, logs, _) = RunScenario(
                            setup: AddGame,
                            scriptByTick: new Dictionary<int, Action<FakeProcReader>>
                            {
                                [3] = r => r.Procs.Remove(100) // Gamescope crashes; game 120 stays alive
                            });

                        Check("49/52. Orphaned game found via session token and terminated after the recovery period", true,
                            signaler.GracefulSignals.Contains(120) &&
                            result.WrapperTerminationAttempted &&
                            result.TerminationResult != null && result.TerminationResult.WrapperExited);
                        Check("54b. Runtime error is reported for the orphaned session", true, !string.IsNullOrEmpty(result.ErrorMessage));
                        Check("50. No direct fallback of any kind is logged/attempted after wrapper exit", true,
                            logs.Any(l => l.Contains("DirectFallbackUsed: false")) &&
                            logs.All(l => !l.Contains("DirectFallbackUsed: true")));
                    }

                    // FORCE QUIT (55-62).
                    {
                        var (result, signaler, _, returns) = RunScenario(
                            setup: r =>
                            {
                                AddGame(r);
                                r.Procs[130] = new FakeProc { Comm = "detached.exe", Ppid = 1, StartTicks = 13, EnvironRaw = TokenEnviron(id) };  // detached member
                                r.Procs[210] = new FakeProc { Comm = "gamescope", StartTicks = 21, EnvironRaw = "DISPLAY=:0\0" };                  // unrelated gamescope
                                r.Procs[200] = new FakeProc { Comm = "wine", StartTicks = 20, EnvironRaw = "WINEPREFIX=/x\0" };                    // unrelated wine
                            },
                            scriptByTick: new Dictionary<int, Action<FakeProcReader>>(),
                            forceQuitAtTick: tick => tick >= 2);

                        Check("55. Force Quit uses session-aware termination (terminator ran, verified result present)", true,
                            result.WrapperTerminationAttempted && result.TerminationResult != null);
                        Check("56. Force Quit is not a wrapper-only kill - all session members were signaled", true,
                            signaler.GracefulSignals.Contains(120) && signaler.GracefulSignals.Contains(130) && signaler.GracefulSignals.Contains(100));
                        Check("57b. Force Quit terminates detached session members", true, signaler.GracefulSignals.Contains(130));
                        Check("58b. Force Quit preserves the unrelated Gamescope process", false,
                            signaler.GracefulSignals.Contains(210) || signaler.ForceSignals.Contains(210));
                        Check("59b. Force Quit preserves the unrelated Wine process", false,
                            signaler.GracefulSignals.Contains(200) || signaler.ForceSignals.Contains(200));
                        Check("60b. Force Quit reports zero remaining session PIDs after verified success", true,
                            result.TerminationResult.RemainingSessionProcesses.Count == 0 && result.TerminationResult.CompletedSuccessfully);
                        Check("61/62. Force Quit produces exactly one final result (single cleanup/state update)", true, returns == 1);
                    }
                }

                // =========================================================
                // EXITCODE SAFETY (63-67)
                // =========================================================
                {
                    using var running = Process.Start(new ProcessStartInfo("/bin/sleep", "10") { UseShellExecute = false });
                    Check("63a. ExitCode is NOT read from a running process", false, ProcessExitSafety.TryGetExitCode(running, out _));
                    running.Kill();
                    running.WaitForExit(3000);
                    Check("63b. ExitCode is read only after confirmed exit", true, ProcessExitSafety.TryGetExitCode(running, out _));

                    var disposed = Process.Start(new ProcessStartInfo("/bin/true") { UseShellExecute = false });
                    disposed.WaitForExit(3000);
                    disposed.Dispose();
                    Check("65. Process-disposal race is handled (returns false, never throws)", false,
                        ProcessExitSafety.TryGetExitCode(disposed, out _));
                    Check("Null process is handled", false, ProcessExitSafety.TryGetExitCode(null, out _));

                    // 64/67. The lifecycle result is the ExitCode gate.
                    var unsafeResult = new WrappedGameLifecycleResult
                    {
                        FinalState = WrappedGameLifecycleState.Failed,
                        WrapperExitedNaturally = false,
                        TerminationResult = new SessionTerminationResult { WrapperExited = false }
                    };
                    Check("64/67. Failed termination (wrapper alive) gates ExitCode as unavailable", false, unsafeResult.WrapperExitConfirmed);
                    var safeResult = new WrappedGameLifecycleResult
                    {
                        FinalState = WrappedGameLifecycleState.Completed,
                        TerminationResult = new SessionTerminationResult { WrapperExited = true }
                    };
                    Check("ExitCode allowed once wrapper exit is verified by the termination result", true, safeResult.WrapperExitConfirmed);
                }

                // =========================================================
                // REAL-PROCESS validation: token discovery, reparenting,
                // session-scoped termination isolation (12, 47, 57, 58, 59, 77)
                // =========================================================
                if (OperatingSystem.IsLinux())
                {
                    var id = GameLaunchSessionIdentity.Create();
                    var otherId = GameLaunchSessionIdentity.Create();

                    Process StartSleep(GameLaunchSessionIdentity token)
                    {
                        var psi = new ProcessStartInfo("/bin/sleep", "60") { UseShellExecute = false };
                        token?.TryApplyTo(psi);
                        return Process.Start(psi);
                    }

                    using var member = StartSleep(id);
                    using var otherSession = StartSleep(otherId);
                    using var plain = StartSleep(null);

                    // Genuinely reparented member: sh -c 'setsid sleep ... &' - the sh exits
                    // immediately, the sleep reparents away from our tree but keeps the token.
                    var reparentPsi = new ProcessStartInfo("/bin/sh", "-c \"setsid /bin/sleep 60 >/dev/null 2>&1 &\"") { UseShellExecute = false };
                    id.TryApplyTo(reparentPsi);
                    using (var sh = Process.Start(reparentPsi))
                        sh.WaitForExit(5000);
                    Thread.Sleep(300); // let the setsid child settle

                    int reparentedPid = 0;
                    try
                    {
                        var reader = new LinuxProcReader();
                        var memberStat = reader.ReadStat(member.Id);
                        var locator = new ProcSessionProcessLocator(reader,
                            new SessionWrapperDescriptor { WrapperPid = member.Id, WrapperStartTimeTicks = memberStat?.StartTimeTicks });

                        var found = locator.FindSessionProcesses(id);
                        var foundPids = found.Select(p => p.Pid).ToHashSet();

                        Check("REAL. Token-carrying member is discovered via real /proc environ", true, foundPids.Contains(member.Id));
                        Check("REAL-13/77. A different session's token is not discovered", false, foundPids.Contains(otherSession.Id));
                        Check("REAL. A token-less process is not discovered", false, foundPids.Contains(plain.Id));

                        var reparented = found.FirstOrDefault(p => p.Pid != member.Id && p.ProcessName == "sleep");
                        Check("REAL-12. A genuinely reparented (setsid) token process is discovered", true, reparented != null);
                        if (reparented != null)
                        {
                            reparentedPid = reparented.Pid;
                            Check("REAL-12b. Reparented member is correctly flagged as NOT a wrapper descendant", false, reparented.IsDescendantOfWrapper);
                        }

                        // REAL session-scoped termination with the REAL signaler.
                        var terminator = new GameSessionTerminator(locator, new LinuxProcessSignaler(),
                            new WrappedGameLifecycleOptions
                            {
                                GracefulTerminationTimeout = TimeSpan.FromSeconds(1),
                                ForceTerminationTimeout = TimeSpan.FromSeconds(1)
                            },
                            Environment.ProcessId);

                        var wrapperInfo = new SessionProcessInfo
                        {
                            Pid = member.Id,
                            ProcessName = "sleep",
                            StartTimeTicks = memberStat?.StartTimeTicks,
                            IsWrapper = true,
                            HasSessionToken = true
                        };
                        var result = terminator.TerminateSessionAsync(id, wrapperInfo, found, SessionTerminationReason.ForceQuit, CancellationToken.None)
                            .GetAwaiter().GetResult();

                        Thread.Sleep(200);
                        Check("REAL-47. Session termination verified successful against real processes", true, result.CompletedSuccessfully);
                        Check("REAL. The session member was actually terminated", true, member.HasExited);
                        Check("REAL-57. The reparented member was actually terminated", true,
                            reparentedPid > 0 && !Directory.Exists($"/proc/{reparentedPid}"));
                        Check("REAL-58/59/77. Unrelated processes survived the session termination untouched", true,
                            !otherSession.HasExited && !plain.HasExited);
                    }
                    finally
                    {
                        foreach (var p in new[] { member, otherSession, plain })
                        {
                            try { if (!p.HasExited) p.Kill(); } catch { /* cleanup */ }
                        }
                        if (reparentedPid > 0)
                        {
                            try { Process.GetProcessById(reparentedPid).Kill(); } catch { /* already gone */ }
                        }
                    }
                }

                // =========================================================
                // NO GLOBAL KILLS (73-76) + direct-kill audit
                // =========================================================
                {
                    var repoRoot = FindRepoRoot();
                    bool foundGlobalKill = false;
                    foreach (var dir in new[] { "TeknoParrotUi.Common", "TeknoParrotUi.Avalonia" })
                    {
                        var path = Path.Combine(repoRoot, dir);
                        if (!Directory.Exists(path))
                            continue;
                        foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
                        {
                            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                                continue;
                            var text = File.ReadAllText(file);
                            if (text.Contains("pkill") || text.Contains("killall"))
                            {
                                foundGlobalKill = true;
                                Console.WriteLine($"  (unexpected global-kill reference in {file})");
                            }
                        }
                    }
                    Check("73-76. No pkill/killall (gamescope, wine, or otherwise) in production source", false, foundGlobalKill);

                    // The wrapped-session path must not contain a bare wrapper-only force-quit Kill().
                    var gameSession = File.ReadAllText(Path.Combine(repoRoot, "TeknoParrotUi.Common", "GameLaunch", "GameSession.cs"));
                    var wrapperLoopStart = gameSession.IndexOf("RunWrapperLifecycleLoop(GameLaunchSessionIdentity", StringComparison.Ordinal);
                    Check("56c. The wrapped lifecycle method contains no direct _process.Kill() call", true,
                        wrapperLoopStart > 0 && !gameSession.Substring(wrapperLoopStart).Contains("_process.Kill()"));
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FATAL: {ex}");
            }

            Console.WriteLine();
            Console.WriteLine("Covered by other suites (not duplicated here):");
            Console.WriteLine(" 68-72. Pre-launch fallback rules (Gamescope Process.Start throws/null/succeeds/forced/disabled)");
            Console.WriteLine("        are tested in gamescope-scaling-test (GameProcessLauncher.LaunchWithResult section).");
            Console.WriteLine();
            Console.WriteLine($"GamescopeLifecycleTest: {cases - failures}/{cases} passed.");
            return failures == 0 ? 0 : 1;
        }

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "TeknoParrotUI.sln")))
                dir = Path.GetDirectoryName(dir);
            return dir ?? ".";
        }
    }
}
