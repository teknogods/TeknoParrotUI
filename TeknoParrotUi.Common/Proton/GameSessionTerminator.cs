using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    public enum SessionTerminationReason
    {
        ForceQuit,
        ConfirmedGameExitedWrapperLingering,
        WrapperExitedGameOrphaned,
        ApplicationShutdown,
        CleanupFailure
    }

    /// <summary>Honest outcome of one session termination - success is NEVER assumed, only verified by re-scanning.</summary>
    public sealed class SessionTerminationResult
    {
        public bool WrapperExited { get; init; }

        public IReadOnlyList<SessionProcessInfo> GracefullyExitedProcesses { get; init; } = Array.Empty<SessionProcessInfo>();

        public IReadOnlyList<SessionProcessInfo> ForceKilledProcesses { get; init; } = Array.Empty<SessionProcessInfo>();

        /// <summary>Session processes STILL running after the full sequence - non-empty means termination did not fully succeed.</summary>
        public IReadOnlyList<SessionProcessInfo> RemainingSessionProcesses { get; init; } = Array.Empty<SessionProcessInfo>();

        /// <summary>Processes deliberately NOT signaled (identity no longer valid, PID reused, protected pid, ...), with reasons logged.</summary>
        public IReadOnlyList<SessionProcessInfo> SkippedProcesses { get; init; } = Array.Empty<SessionProcessInfo>();

        public bool CompletedSuccessfully => WrapperExited && RemainingSessionProcesses.Count == 0;
    }

    /// <summary>Injectable signal seam (SIGTERM/SIGKILL) so termination ordering is testable without killing real processes.</summary>
    public interface IProcessSignaler
    {
        /// <summary>SIGTERM. Returns false when the signal could not be sent (process already gone, permission).</summary>
        bool SignalGraceful(int pid);

        /// <summary>SIGKILL. Returns false when the signal could not be sent.</summary>
        bool SignalForce(int pid);
    }

    /// <summary>Real libc kill(2) signaler - only ever invoked for identity-verified session PIDs (see <see cref="ProcessSafetyGuard"/>).</summary>
    public sealed class LinuxProcessSignaler : IProcessSignaler
    {
        private const int SIGTERM = 15;
        private const int SIGKILL = 9;

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        public bool SignalGraceful(int pid) => Send(pid, SIGTERM);

        public bool SignalForce(int pid) => Send(pid, SIGKILL);

        private static bool Send(int pid, int sig)
        {
            if (pid <= 1)
                return false; // absolute floor - guarded again upstream
            try { return kill(pid, sig) == 0; }
            catch { return false; }
        }
    }

    /// <summary>
    /// PURE signal-order planning: descendants deepest-first, wrapper last.
    /// Depth is the parent-chain distance from the wrapper computed within
    /// the observed set; detached/reparented token processes (no chain to
    /// the wrapper) are treated as depth 1 - signaled before the wrapper,
    /// after deeper descendants.
    /// </summary>
    public static class SessionTerminationPlanner
    {
        public static IReadOnlyList<SessionProcessInfo> PlanSignalOrder(IReadOnlyList<SessionProcessInfo> processes)
        {
            if (processes == null || processes.Count == 0)
                return Array.Empty<SessionProcessInfo>();

            var byPid = processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
            var depthCache = new Dictionary<int, int>();

            int DepthOf(SessionProcessInfo p)
            {
                if (p.IsWrapper)
                    return 0;
                if (depthCache.TryGetValue(p.Pid, out var cached))
                    return cached;

                // Walk the parent chain within the observed set; a chain that
                // reaches the wrapper yields the real depth, otherwise the
                // process is detached/reparented -> depth 1.
                var depth = 1;
                var seen = new HashSet<int> { p.Pid };
                var current = p;
                var steps = 0;
                while (byPid.TryGetValue(current.ParentPid, out var parent) && seen.Add(parent.Pid) && ++steps < 128)
                {
                    if (parent.IsWrapper)
                    {
                        depth = steps;
                        break;
                    }
                    current = parent;
                }
                depthCache[p.Pid] = Math.Max(depth, 1);
                return depthCache[p.Pid];
            }

            return processes
                .OrderBy(p => p.IsWrapper ? 1 : 0)       // wrapper strictly last
                .ThenByDescending(DepthOf)               // deepest first
                .ThenByDescending(p => p.Pid)            // deterministic tie-break
                .ToList();
        }
    }

    /// <summary>The ONE centralized termination path for wrapped Gamescope sessions - see interface docs on <see cref="IGameSessionTerminator"/>.</summary>
    public interface IGameSessionTerminator
    {
        Task<SessionTerminationResult> TerminateSessionAsync(
            GameLaunchSessionIdentity sessionIdentity,
            SessionProcessInfo wrapperProcess,
            IReadOnlyCollection<SessionProcessInfo> knownProcesses,
            SessionTerminationReason reason,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Production session terminator. Sequence (task: session-scoped
    /// termination): fresh token re-scan -&gt; identity-verify each member
    /// (<see cref="ProcessSafetyGuard"/>) immediately before each signal -&gt;
    /// SIGTERM deepest-first with the wrapper last -&gt; wait -&gt; re-scan -&gt;
    /// SIGKILL only still-running verified members -&gt; wait -&gt; final re-scan.
    /// The result reports what ACTUALLY remained - success is never assumed.
    /// Never uses any process-NAME-wide kill command or global Wine/Gamescope
    /// termination; only individually identity-verified PIDs are ever signaled.
    /// </summary>
    public sealed class GameSessionTerminator : IGameSessionTerminator
    {
        private readonly IGameSessionProcessLocator _locator;
        private readonly IProcessSignaler _signaler;
        private readonly WrappedGameLifecycleOptions _options;
        private readonly int _currentProcessPid;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Action<string> _log;

        public GameSessionTerminator(
            IGameSessionProcessLocator locator,
            IProcessSignaler signaler,
            WrappedGameLifecycleOptions options,
            int currentProcessPid,
            Func<TimeSpan, CancellationToken, Task> delay = null,
            Action<string> log = null)
        {
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _signaler = signaler ?? throw new ArgumentNullException(nameof(signaler));
            _options = options ?? new WrappedGameLifecycleOptions();
            _currentProcessPid = currentProcessPid;
            _delay = delay ?? Task.Delay;
            _log = log ?? (_ => { });
        }

        public async Task<SessionTerminationResult> TerminateSessionAsync(
            GameLaunchSessionIdentity sessionIdentity,
            SessionProcessInfo wrapperProcess,
            IReadOnlyCollection<SessionProcessInfo> knownProcesses,
            SessionTerminationReason reason,
            CancellationToken cancellationToken)
        {
            if (sessionIdentity == null) throw new ArgumentNullException(nameof(sessionIdentity));

            // 1-2. Fresh re-scan; include the verified wrapper and any known
            // processes the scan may have missed (they get individually
            // revalidated before signaling anyway).
            var members = Rescan(sessionIdentity, wrapperProcess, knownProcesses);

            // 3-5. Deepest-first order, wrapper last.
            var ordered = SessionTerminationPlanner.PlanSignalOrder(members);

            var skipped = new List<SessionProcessInfo>();
            var preserved = new List<SessionProcessInfo>();
            var gracefulSignaled = new List<SessionProcessInfo>();
            foreach (var member in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Shared Wine prefix daemons (wineserver, services.exe, ...)
                // inherit this session's token because our wine spawned them,
                // but they serve the WHOLE prefix - killing them would break
                // another TeknoParrot launch sharing the same prefix, and the
                // pre-refactor behavior never killed them either. Deliberately
                // preserved (recorded, logged, excluded from "remaining").
                // Gamescope's own processes (gamescope/gamescopereaper) are
                // session-scoped and ARE terminated.
                if (IsPreservedSharedInfrastructure(member))
                {
                    preserved.Add(member);
                    skipped.Add(member);
                    _log($"[GameSessionTermination] Preserving pid {member.Pid} ({member.ProcessName}): shared Wine prefix infrastructure (never terminated by a session)");
                    continue;
                }

                var fresh = _locator.TryDescribeProcess(member.Pid, sessionIdentity);
                var decision = ProcessSafetyGuard.Validate(member, fresh, _currentProcessPid);
                if (!decision.Allowed)
                {
                    skipped.Add(member);
                    _log($"[GameSessionTermination] Skipping pid {member.Pid} ({member.ProcessName}): {decision.Reason}");
                    continue;
                }
                if (decision.ReducedConfidence)
                    _log($"[GameSessionTermination] pid {member.Pid}: start time unavailable - PID-only identity (reduced confidence)");

                _signaler.SignalGraceful(member.Pid);
                gracefulSignaled.Add(member);
            }

            // 8. Graceful wait.
            await SafeDelay(_options.GracefulTerminationTimeout, cancellationToken).ConfigureAwait(false);

            // 9-10. Re-scan; force-kill only still-running verified members.
            var afterGraceful = Rescan(sessionIdentity, wrapperProcess, members);
            var stillRunning = afterGraceful
                .Where(p => members.Any(m => m.SameIdentityAs(p)) && !IsPreservedSharedInfrastructure(p))
                .ToList();
            var forceSignaled = new List<SessionProcessInfo>();
            foreach (var member in SessionTerminationPlanner.PlanSignalOrder(stillRunning))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fresh = _locator.TryDescribeProcess(member.Pid, sessionIdentity);
                var decision = ProcessSafetyGuard.Validate(member, fresh, _currentProcessPid);
                if (!decision.Allowed)
                {
                    _log($"[GameSessionTermination] Skipping force-kill of pid {member.Pid}: {decision.Reason}");
                    continue;
                }
                _signaler.SignalForce(member.Pid);
                forceSignaled.Add(member);
            }

            // 11-12. Force wait, then the final honest re-scan.
            if (forceSignaled.Count > 0)
                await SafeDelay(_options.ForceTerminationTimeout, cancellationToken).ConfigureAwait(false);

            var remaining = Rescan(sessionIdentity, wrapperProcess, members)
                .Where(p => members.Any(m => m.SameIdentityAs(p)) && !IsPreservedSharedInfrastructure(p))
                .ToList();

            bool wrapperExited = wrapperProcess == null ||
                                 !remaining.Any(p => p.SameIdentityAs(wrapperProcess));

            var gracefullyExited = gracefulSignaled
                .Where(m => !forceSignaled.Any(f => f.SameIdentityAs(m)) && !remaining.Any(r => r.SameIdentityAs(m)))
                .ToList();

            var result = new SessionTerminationResult
            {
                WrapperExited = wrapperExited,
                GracefullyExitedProcesses = gracefullyExited,
                ForceKilledProcesses = forceSignaled,
                RemainingSessionProcesses = remaining,
                SkippedProcesses = skipped
            };

            _log(BuildLogBlock(sessionIdentity, wrapperProcess, members, gracefulSignaled, forceSignaled, result, reason));
            return result;
        }

        /// <summary>
        /// Shared Wine prefix daemons (wineserver/services.exe/...) are never
        /// terminated by a session - they may serve OTHER launches in the
        /// same prefix. The wrapper and Gamescope's session-scoped processes
        /// are always fair game.
        /// </summary>
        internal static bool IsPreservedSharedInfrastructure(SessionProcessInfo process)
        {
            if (process == null || process.IsWrapper)
                return false;
            if (ProcSessionProcessLocator.IsGamescopeInfrastructureName(process.ProcessName))
                return false;
            return ProtonProcessDetector.IsInfrastructureProcessName(process.ProcessName);
        }

        private List<SessionProcessInfo> Rescan(
            GameLaunchSessionIdentity sessionIdentity,
            SessionProcessInfo wrapperProcess,
            IEnumerable<SessionProcessInfo> alsoConsider)
        {
            var found = new List<SessionProcessInfo>(_locator.FindSessionProcesses(sessionIdentity));

            // Make sure the wrapper and previously-known members are represented
            // if the scan missed them but they still exist with matching identity.
            var extras = new List<SessionProcessInfo>();
            if (wrapperProcess != null)
                extras.Add(wrapperProcess);
            if (alsoConsider != null)
                extras.AddRange(alsoConsider);

            foreach (var extra in extras)
            {
                if (extra == null || found.Any(f => f.Pid == extra.Pid))
                    continue;
                var fresh = _locator.TryDescribeProcess(extra.Pid, sessionIdentity);
                if (fresh != null && fresh.SameIdentityAs(extra) && (fresh.HasSessionToken || fresh.IsWrapper))
                    found.Add(fresh);
            }
            return found;
        }

        private async Task SafeDelay(TimeSpan duration, CancellationToken ct)
        {
            if (duration <= TimeSpan.Zero)
                return;
            try { await _delay(duration, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* proceed to final verification anyway */ }
        }

        private static string BuildLogBlock(
            GameLaunchSessionIdentity id,
            SessionProcessInfo wrapper,
            IReadOnlyList<SessionProcessInfo> members,
            IReadOnlyList<SessionProcessInfo> graceful,
            IReadOnlyList<SessionProcessInfo> forced,
            SessionTerminationResult result,
            SessionTerminationReason reason)
        {
            static string Pids(IEnumerable<SessionProcessInfo> list)
            {
                var joined = string.Join(",", list.Select(p => p.Pid));
                return joined.Length == 0 ? "none" : joined;
            }

            return "[GameSessionTermination]\n" +
                   $"Reason: {reason}\n" +
                   $"SessionId: {id.EnvironmentVariableValue}\n" +
                   $"WrapperPid: {(wrapper?.Pid.ToString() ?? "none")}\n" +
                   $"KnownSessionPids: {Pids(members)}\n" +
                   $"GracefulSignalsSent: {Pids(graceful)}\n" +
                   $"ForceSignalsSent: {Pids(forced)}\n" +
                   $"SkippedPids: {Pids(result.SkippedProcesses)}\n" +
                   $"WrapperExited: {(result.WrapperExited ? "true" : "false")}\n" +
                   $"RemainingSessionPids: {Pids(result.RemainingSessionProcesses)}\n" +
                   $"CompletedSuccessfully: {(result.CompletedSuccessfully ? "true" : "false")}";
        }
    }
}
