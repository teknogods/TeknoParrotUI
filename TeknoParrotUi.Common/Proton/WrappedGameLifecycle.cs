using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Explicit states of one Gamescope-wrapped launch session.</summary>
    public enum WrappedGameLifecycleState
    {
        WrapperStarting,
        WaitingForGame,
        CandidateObserved,
        GameObserved,
        GameRunning,
        GameReplacementObserved,
        GameExited,
        WrapperLingering,
        WrapperExitedWhileGameAlive,
        Terminating,
        Completed,
        Failed
    }

    /// <summary>All lifecycle timing in ONE place - no timing constants scattered through GameSession.</summary>
    public sealed class WrappedGameLifecycleOptions
    {
        /// <summary>How long the wrapper may run without ANY game/candidate before a startup stall is reported (never terminates by itself).</summary>
        public TimeSpan GameStartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>How long an arbitrary candidate .exe must stay alive before it may be promoted to the confirmed game.</summary>
        public TimeSpan CandidateStabilizationTime { get; init; } = TimeSpan.FromSeconds(3);

        /// <summary>How long the wrapper may linger after the confirmed game exited (with no replacement) before session termination.</summary>
        public TimeSpan WrapperLingerGracePeriod { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>How long remaining game processes are monitored for natural exit after the wrapper died under them.</summary>
        public TimeSpan WrapperExitGameGracePeriod { get; init; } = TimeSpan.FromSeconds(5);

        public TimeSpan GracefulTerminationTimeout { get; init; } = TimeSpan.FromSeconds(3);

        public TimeSpan ForceTerminationTimeout { get; init; } = TimeSpan.FromSeconds(3);

        public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    }

    /// <summary>Everything the state machine is told about one poll tick - it never touches processes/clocks itself.</summary>
    public sealed class WrappedSessionObservation
    {
        public DateTimeOffset Now { get; init; }
        public bool ForceQuitRequested { get; init; }
        public bool WrapperAlive { get; init; }
        /// <summary>Current session members (token carriers + wrapper), Confidence already assigned by <see cref="GameProcessClassifier"/>.</summary>
        public IReadOnlyList<SessionProcessInfo> SessionProcesses { get; init; } = Array.Empty<SessionProcessInfo>();
    }

    public enum LifecycleTickActionKind
    {
        /// <summary>Keep polling.</summary>
        None,
        /// <summary>Run the session terminator with <see cref="LifecycleTickDecision.TerminationReason"/>, then report back via NotifyTerminationCompleted.</summary>
        TerminateSession,
        /// <summary>Session over, nothing to terminate.</summary>
        CompleteSession
    }

    public sealed class LifecycleTickDecision
    {
        public WrappedGameLifecycleState State { get; init; }
        public LifecycleTickActionKind Action { get; init; }
        public SessionTerminationReason TerminationReason { get; init; }
        public string Detail { get; init; }
    }

    /// <summary>Structured outcome of one wrapped session - consumed by GameSession for cleanup, ExitCode safety, error reporting and logging.</summary>
    public sealed class WrappedGameLifecycleResult
    {
        public WrappedGameLifecycleState FinalState { get; init; }
        public bool WrapperStarted { get; init; }
        public bool WrapperExitedNaturally { get; init; }
        public bool ConfirmedGameObserved { get; init; }
        public bool ConfirmedGameExited { get; init; }
        public bool WrapperTerminationAttempted { get; init; }
        public SessionTerminationResult TerminationResult { get; init; }
        public IReadOnlyList<SessionProcessInfo> RemainingProcesses { get; init; } = Array.Empty<SessionProcessInfo>();
        public string ErrorMessage { get; init; }

        /// <summary>True only when the wrapper's exit has actually been confirmed - the gate for reading Process.ExitCode.</summary>
        public bool WrapperExitConfirmed =>
            WrapperExitedNaturally || (TerminationResult != null && TerminationResult.WrapperExited);
    }

    /// <summary>
    /// The wrapped-session state machine. Deterministic and I/O-free: every
    /// input arrives explicitly via <see cref="Advance"/> (observation +
    /// time), so every scenario - temporary launcher exits, candidate
    /// stabilization, replacement executables, wrapper linger, wrapper crash
    /// with the game still alive, force quit - is directly unit-testable
    /// with fake observations and a fake clock. The runner
    /// (<see cref="WrappedGameLifecycleRunner"/>) does the real polling,
    /// process discovery and termination around it.
    ///
    /// Confirmation policy (task: main-game confidence model):
    ///  1. ExpectedPrimaryExecutable  -&gt; confirmed immediately.
    ///  2. ExpectedSecondaryExecutable -&gt; confirmed immediately.
    ///  3. Candidate (any other .exe, including known loaders) -&gt; confirmed
    ///     only after remaining alive for CandidateStabilizationTime.
    ///  A candidate that exits before stabilizing NEVER ends the session.
    /// </summary>
    public sealed class WrappedGameLifecycleStateMachine
    {
        private readonly WrappedGameLifecycleOptions _options;
        private readonly Action<string> _log;

        private WrappedGameLifecycleState _state = WrappedGameLifecycleState.WrapperStarting;
        private DateTimeOffset? _launchTime;
        private SessionProcessInfo _confirmedGame;
        private bool _everConfirmed;
        private DateTimeOffset? _confirmedGameExitedAt;
        private DateTimeOffset? _wrapperExitObservedAt;
        private bool _startupStallReported;
        private bool _terminationRequested;
        private SessionTerminationResult _terminationResult;
        private string _errorMessage;
        private IReadOnlyList<SessionProcessInfo> _lastObservedProcesses = Array.Empty<SessionProcessInfo>();
        // Key: "pid:startTicks" so a reused PID is a NEW candidate.
        private readonly Dictionary<string, DateTimeOffset> _candidateFirstSeen = new Dictionary<string, DateTimeOffset>();

        public WrappedGameLifecycleState State => _state;
        public SessionProcessInfo ConfirmedGame => _confirmedGame;

        public WrappedGameLifecycleStateMachine(WrappedGameLifecycleOptions options, Action<string> log = null)
        {
            _options = options ?? new WrappedGameLifecycleOptions();
            _log = log ?? (_ => { });
        }

        public LifecycleTickDecision Advance(WrappedSessionObservation obs)
        {
            if (obs == null) throw new ArgumentNullException(nameof(obs));
            if (_state == WrappedGameLifecycleState.Completed || _state == WrappedGameLifecycleState.Failed)
                return Decide(LifecycleTickActionKind.None, "session already finished");
            if (_state == WrappedGameLifecycleState.Terminating)
                return Decide(LifecycleTickActionKind.None, "termination already in progress");

            _launchTime ??= obs.Now;
            _lastObservedProcesses = obs.SessionProcesses ?? Array.Empty<SessionProcessInfo>();

            // Priority 1: user force quit - session-aware termination, never a bare wrapper Kill().
            if (obs.ForceQuitRequested)
            {
                _terminationRequested = true;
                Transition(WrappedGameLifecycleState.Terminating, obs, "force quit requested");
                return Decide(LifecycleTickActionKind.TerminateSession, "force quit", SessionTerminationReason.ForceQuit);
            }

            var processes = _lastObservedProcesses;
            UpdateConfirmedGame(processes, obs);

            bool confirmedAlive = _confirmedGame != null && processes.Any(p => p.SameIdentityAs(_confirmedGame));
            var aliveCandidates = processes.Where(p => p.Confidence == GameProcessConfidence.Candidate).ToList();
            bool anyNonInfrastructureAlive = confirmedAlive || aliveCandidates.Count > 0 ||
                processes.Any(p => p.Confidence == GameProcessConfidence.ExpectedPrimaryExecutable ||
                                   p.Confidence == GameProcessConfidence.ExpectedSecondaryExecutable ||
                                   p.Confidence == GameProcessConfidence.ConfirmedMainGame);

            // Priority 2: the wrapper is gone.
            if (!obs.WrapperAlive)
            {
                if (_wrapperExitObservedAt == null)
                {
                    _wrapperExitObservedAt = obs.Now;
                    _log($"[GameSessionLifecycle] WrapperExited: true. ConfirmedGameAlive: {Bool(confirmedAlive)}. " +
                         $"RemainingNonInfrastructurePids: {PidsOf(processes.Where(p => !p.IsWrapper && !p.IsInfrastructureProcess))}");
                }

                if (!anyNonInfrastructureAlive)
                {
                    // Nothing game-like left (leftover Wine infrastructure such
                    // as wineserver is deliberately NOT terminated here - it is
                    // shared prefix plumbing, exactly as before this refactor).
                    Transition(WrappedGameLifecycleState.Completed, obs, "wrapper exited, no session game remains");
                    return Decide(LifecycleTickActionKind.CompleteSession, "wrapper exited naturally");
                }

                // Wrapper died under a still-alive game: NEVER direct-fallback
                // or relaunch; monitor for natural exit, then terminate the
                // orphaned session if it doesn't recover.
                if (_state != WrappedGameLifecycleState.WrapperExitedWhileGameAlive)
                {
                    Transition(WrappedGameLifecycleState.WrapperExitedWhileGameAlive, obs,
                        "Gamescope exited unexpectedly while session game processes remain - monitoring, no direct fallback, no relaunch");
                    _log("[GameSessionLifecycle]\n" +
                         "WrapperExited: true\n" +
                         $"ConfirmedGameAlive: {Bool(confirmedAlive)}\n" +
                         "DirectFallbackUsed: false\n" +
                         $"RemainingSessionPids: {PidsOf(processes.Where(p => !p.IsWrapper))}\n" +
                         "Action: MonitorThenTerminateSession");
                }

                if (obs.Now - _wrapperExitObservedAt.Value >= _options.WrapperExitGameGracePeriod)
                {
                    _terminationRequested = true;
                    _errorMessage = "Gamescope exited while the game was still running; the orphaned session did not exit " +
                                    "within the recovery period and was terminated.";
                    Transition(WrappedGameLifecycleState.Terminating, obs, "orphaned session recovery period elapsed");
                    return Decide(LifecycleTickActionKind.TerminateSession, "orphaned session", SessionTerminationReason.WrapperExitedGameOrphaned);
                }

                return Decide(LifecycleTickActionKind.None, "monitoring orphaned session during recovery period");
            }

            // Priority 3 (wrapper alive): confirmed game exited - bounded linger, replacement-aware.
            if (_everConfirmed && !confirmedAlive)
            {
                if (_confirmedGameExitedAt == null)
                {
                    _confirmedGameExitedAt = obs.Now;
                    Transition(WrappedGameLifecycleState.GameExited, obs,
                        $"confirmed game (pid {_confirmedGame?.Pid}) no longer running - starting wrapper linger grace period");
                }

                if (aliveCandidates.Count > 0)
                {
                    // A possible replacement is stabilizing - do NOT count linger
                    // time against the wrapper while it exists.
                    _confirmedGameExitedAt = obs.Now;
                    return Decide(LifecycleTickActionKind.None, "candidate replacement stabilizing - linger timer held");
                }

                if (obs.Now - _confirmedGameExitedAt.Value >= _options.WrapperLingerGracePeriod)
                {
                    if (_state != WrappedGameLifecycleState.WrapperLingering)
                        Transition(WrappedGameLifecycleState.WrapperLingering, obs, "only Gamescope/infrastructure processes remain");
                    _terminationRequested = true;
                    Transition(WrappedGameLifecycleState.Terminating, obs, "wrapper linger grace period elapsed");
                    return Decide(LifecycleTickActionKind.TerminateSession, "lingering wrapper", SessionTerminationReason.ConfirmedGameExitedWrapperLingering);
                }

                return Decide(LifecycleTickActionKind.None, "within wrapper linger grace period");
            }

            // Priority 4: startup progress / stall reporting.
            if (!_everConfirmed)
            {
                if (aliveCandidates.Count > 0 && _state != WrappedGameLifecycleState.CandidateObserved)
                    Transition(WrappedGameLifecycleState.CandidateObserved, obs, $"candidate executable(s) observed: {PidsOf(aliveCandidates)}");
                else if (aliveCandidates.Count == 0 && (_state == WrappedGameLifecycleState.WrapperStarting || _state == WrappedGameLifecycleState.CandidateObserved))
                    Transition(WrappedGameLifecycleState.WaitingForGame, obs, "wrapper running, waiting for a game process");

                if (!_startupStallReported && obs.Now - _launchTime.Value >= _options.GameStartupTimeout)
                {
                    _startupStallReported = true;
                    _log("[GameSessionLifecycle] WARNING: no game process confirmed within the startup timeout - " +
                         "the game may still be loading; the session is NOT being ended for this.");
                }
            }
            else if (confirmedAlive && _state != WrappedGameLifecycleState.GameRunning)
            {
                Transition(WrappedGameLifecycleState.GameRunning, obs, $"confirmed game running (pid {_confirmedGame.Pid})");
            }

            return Decide(LifecycleTickActionKind.None, "polling");
        }

        /// <summary>The runner reports the terminator's actual outcome here; the machine turns it into the final state.</summary>
        public void NotifyTerminationCompleted(SessionTerminationResult result, SessionTerminationReason reason)
        {
            _terminationResult = result;
            if (result != null && result.CompletedSuccessfully)
            {
                _state = WrappedGameLifecycleState.Completed;
            }
            else
            {
                _state = WrappedGameLifecycleState.Failed;
                _errorMessage ??= "Session termination did not fully complete - see RemainingSessionPids in the termination log.";
            }
        }

        public WrappedGameLifecycleResult BuildResult()
        {
            return new WrappedGameLifecycleResult
            {
                FinalState = _state,
                WrapperStarted = true,
                // Only ever set while observing the wrapper ALREADY exited (before/without
                // us terminating it) - a wrapper we terminated never counts as natural.
                WrapperExitedNaturally = _wrapperExitObservedAt.HasValue,
                ConfirmedGameObserved = _everConfirmed,
                ConfirmedGameExited = _everConfirmed && (_confirmedGameExitedAt.HasValue ||
                    !_lastObservedProcesses.Any(p => _confirmedGame != null && p.SameIdentityAs(_confirmedGame))),
                WrapperTerminationAttempted = _terminationRequested,
                TerminationResult = _terminationResult,
                RemainingProcesses = _terminationResult?.RemainingSessionProcesses ?? _lastObservedProcesses,
                ErrorMessage = _errorMessage
            };
        }

        private void UpdateConfirmedGame(IReadOnlyList<SessionProcessInfo> processes, WrappedSessionObservation obs)
        {
            bool confirmedStillAlive = _confirmedGame != null && processes.Any(p => p.SameIdentityAs(_confirmedGame));
            if (confirmedStillAlive)
            {
                _confirmedGameExitedAt = null;
                PruneCandidates(processes);
                return;
            }

            // No live confirmed game: look for an immediate (expected-executable)
            // confirmation first, then a stabilized candidate.
            var immediate = processes.FirstOrDefault(p => p.Confidence == GameProcessConfidence.ExpectedPrimaryExecutable)
                            ?? processes.FirstOrDefault(p => p.Confidence == GameProcessConfidence.ExpectedSecondaryExecutable);

            SessionProcessInfo promoted = null;
            if (immediate == null)
            {
                foreach (var candidate in processes.Where(p => p.Confidence == GameProcessConfidence.Candidate))
                {
                    var key = CandidateKey(candidate);
                    if (!_candidateFirstSeen.TryGetValue(key, out var firstSeen))
                    {
                        _candidateFirstSeen[key] = obs.Now;
                        _log("[GameSessionLifecycle]\n" +
                             $"CandidatePid: {candidate.Pid}\n" +
                             $"CandidateExecutable: {candidate.ProcessName}\n" +
                             "CandidateAge: 0.0s\n" +
                             "Confidence: Candidate\n" +
                             "PromotedToMainGame: false\n" +
                             "Reason: stabilization period not reached");
                        continue;
                    }
                    if (obs.Now - firstSeen >= _options.CandidateStabilizationTime)
                    {
                        promoted = candidate;
                        break;
                    }
                }
            }

            var newlyConfirmed = immediate ?? promoted;
            if (newlyConfirmed == null)
            {
                PruneCandidates(processes);
                return;
            }

            var previous = _confirmedGame;
            _confirmedGame = newlyConfirmed.Confidence == GameProcessConfidence.Candidate
                ? newlyConfirmed.WithConfidence(GameProcessConfidence.ConfirmedMainGame)
                : newlyConfirmed;
            _confirmedGameExitedAt = null;

            if (_everConfirmed && previous != null)
            {
                Transition(WrappedGameLifecycleState.GameReplacementObserved, obs,
                    "replacement game executable confirmed in the same session");
                _log("[GameSessionLifecycle]\n" +
                     $"PreviousGamePid: {previous.Pid}\n" +
                     $"ReplacementGamePid: {_confirmedGame.Pid}\n" +
                     $"ReplacementExecutable: {_confirmedGame.ProcessName}\n" +
                     $"SessionTokenMatched: {Bool(_confirmedGame.HasSessionToken)}\n" +
                     "Action: ConfirmReplacement");
            }
            else
            {
                Transition(WrappedGameLifecycleState.GameObserved, obs,
                    immediate != null
                        ? $"pid {_confirmedGame.Pid} ({_confirmedGame.ProcessName}) matches the configured " +
                          (immediate.Confidence == GameProcessConfidence.ExpectedPrimaryExecutable ? "primary" : "secondary") +
                          " executable - confirmed"
                        : $"pid {_confirmedGame.Pid} ({_confirmedGame.ProcessName}) promoted after remaining stable for " +
                          $"{_options.CandidateStabilizationTime.TotalSeconds:0.#}s (was Candidate)");
            }

            _everConfirmed = true;
            PruneCandidates(processes);
        }

        private void PruneCandidates(IReadOnlyList<SessionProcessInfo> processes)
        {
            // A candidate that exited before stabilizing simply disappears -
            // it never ends the session.
            var aliveKeys = new HashSet<string>(processes
                .Where(p => p.Confidence == GameProcessConfidence.Candidate)
                .Select(CandidateKey));
            foreach (var key in _candidateFirstSeen.Keys.Where(k => !aliveKeys.Contains(k)).ToList())
                _candidateFirstSeen.Remove(key);
        }

        private static string CandidateKey(SessionProcessInfo p) => $"{p.Pid}:{p.StartTimeTicks?.ToString() ?? "?"}";

        private void Transition(WrappedGameLifecycleState next, WrappedSessionObservation obs, string reason)
        {
            if (_state == next)
                return;
            _state = next;
            _log($"[GameSessionLifecycle] State: {next}. Reason: {reason}. " +
                 $"ConfirmedGamePid: {(_confirmedGame?.Pid.ToString() ?? "none")}. " +
                 $"SessionPids: {PidsOf(obs.SessionProcesses)}.");
        }

        private LifecycleTickDecision Decide(LifecycleTickActionKind action, string detail,
            SessionTerminationReason reason = SessionTerminationReason.ForceQuit)
        {
            return new LifecycleTickDecision { State = _state, Action = action, Detail = detail, TerminationReason = reason };
        }

        private static string PidsOf(IEnumerable<SessionProcessInfo> processes)
        {
            var joined = string.Join(",", (processes ?? Array.Empty<SessionProcessInfo>()).Select(p => p.Pid));
            return joined.Length == 0 ? "none" : joined;
        }

        private static string Bool(bool b) => b ? "true" : "false";
    }
}
