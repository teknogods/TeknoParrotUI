using System;
using System.Collections.Generic;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Identity of the original Gamescope wrapper process this session launched (PID + start-time guard).</summary>
    public sealed class SessionWrapperDescriptor
    {
        public int WrapperPid { get; init; }

        /// <summary>Start-time ticks captured right after launch; null when unreadable (PID-only identity, reduced confidence).</summary>
        public long? WrapperStartTimeTicks { get; init; }
    }

    /// <summary>
    /// Finds every process belonging to a specific launch session. The
    /// primary signal is the inherited TP_LAUNCH_SESSION_ID environment
    /// token (survives forking, detaching and reparenting - see
    /// <see cref="GameLaunchSessionIdentity"/>); wrapper-descendant links
    /// are recorded as supplementary information only.
    /// </summary>
    public interface IGameSessionProcessLocator
    {
        /// <summary>All currently-running processes that belong to <paramref name="sessionIdentity"/> (token carriers plus the verified original wrapper).</summary>
        IReadOnlyList<SessionProcessInfo> FindSessionProcesses(GameLaunchSessionIdentity sessionIdentity);

        /// <summary>
        /// Re-describes ONE process right now - used to revalidate identity
        /// immediately before signaling (PID reuse guard). Null when the
        /// process no longer exists or is unreadable.
        /// </summary>
        SessionProcessInfo TryDescribeProcess(int pid, GameLaunchSessionIdentity sessionIdentity);
    }

    /// <summary>
    /// Production /proc-backed locator. One FindSessionProcesses call does a
    /// single /proc pass: stat (pid/ppid/comm/starttime) for every process,
    /// then a bounded environ read ONLY to test for the exact session token.
    /// Unreadable/vanishing/malformed processes are skipped (with diagnostic
    /// logging), never crash discovery.
    /// </summary>
    public sealed class ProcSessionProcessLocator : IGameSessionProcessLocator
    {
        /// <summary>Names that are session plumbing, never the game: the Wine infrastructure set plus Gamescope's own processes.</summary>
        private static readonly string[] GamescopeInfrastructureNames = { "gamescope", "gamescopereaper" };

        private readonly IProcReader _proc;
        private readonly SessionWrapperDescriptor _wrapper;
        private readonly Action<string> _log;
        private readonly int _maxEnvironBytes;

        public ProcSessionProcessLocator(IProcReader proc, SessionWrapperDescriptor wrapper, Action<string> log = null, int maxEnvironBytes = LinuxProcReader.DefaultMaxEnvironBytes)
        {
            _proc = proc ?? throw new ArgumentNullException(nameof(proc));
            _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            _log = log ?? (_ => { });
            _maxEnvironBytes = maxEnvironBytes;
        }

        public IReadOnlyList<SessionProcessInfo> FindSessionProcesses(GameLaunchSessionIdentity sessionIdentity)
        {
            if (sessionIdentity == null) throw new ArgumentNullException(nameof(sessionIdentity));

            // Pass 1: stat for everything (cheap) - needed both for identity
            // and for the descendant map.
            var stats = new Dictionary<int, ProcStatRecord>();
            foreach (var pid in _proc.EnumerateProcessIds())
            {
                var stat = _proc.ReadStat(pid);
                if (stat != null)
                    stats[pid] = stat; // vanished/unreadable processes simply don't appear
            }

            var descendants = ComputeWrapperDescendants(stats);

            // Pass 2: token test. Only exact "NAME=value" entries count - a
            // similar-but-different token must never match.
            var expectedEntry = sessionIdentity.EnvironmentVariableName + "=" + sessionIdentity.EnvironmentVariableValue;
            var result = new List<SessionProcessInfo>();
            foreach (var stat in stats.Values)
            {
                bool hasToken = HasExactTokenEntry(stat.Pid, expectedEntry);
                bool isWrapper = IsVerifiedWrapper(stat);
                if (!hasToken && !isWrapper)
                    continue;

                result.Add(Build(stat, hasToken, isWrapper, descendants.Contains(stat.Pid)));
            }
            return result;
        }

        public SessionProcessInfo TryDescribeProcess(int pid, GameLaunchSessionIdentity sessionIdentity)
        {
            if (sessionIdentity == null) throw new ArgumentNullException(nameof(sessionIdentity));
            var stat = _proc.ReadStat(pid);
            if (stat == null)
                return null;

            var expectedEntry = sessionIdentity.EnvironmentVariableName + "=" + sessionIdentity.EnvironmentVariableValue;
            bool hasToken = HasExactTokenEntry(pid, expectedEntry);
            bool isWrapper = IsVerifiedWrapper(stat);
            // Descendant status deliberately not recomputed for a single-pid
            // lookup - membership/identity never depends on it.
            return Build(stat, hasToken, isWrapper, isDescendant: false);
        }

        private bool IsVerifiedWrapper(ProcStatRecord stat)
        {
            if (stat.Pid != _wrapper.WrapperPid)
                return false;
            if (_wrapper.WrapperStartTimeTicks.HasValue && _wrapper.WrapperStartTimeTicks.Value != stat.StartTimeTicks)
                return false; // PID reused by a different process - NOT our wrapper
            return true;
        }

        private bool HasExactTokenEntry(int pid, string expectedEntry)
        {
            var raw = _proc.ReadEnvironRaw(pid, _maxEnvironBytes);
            if (raw == null)
                return false; // gone or unreadable - skipped (not a member)

            foreach (var entry in raw.Split('\0'))
            {
                if (string.Equals(entry, expectedEntry, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private SessionProcessInfo Build(ProcStatRecord stat, bool hasToken, bool isWrapper, bool isDescendant)
        {
            return new SessionProcessInfo
            {
                Pid = stat.Pid,
                ParentPid = stat.ParentPid,
                ProcessName = stat.Comm,
                ExecutablePath = _proc.ReadExecutablePath(stat.Pid),
                StartTimeTicks = stat.StartTimeTicks,
                StartTime = null, // absolute wall-clock start not derived; StartTimeTicks is the identity signal
                HasSessionToken = hasToken,
                IsWrapper = isWrapper,
                IsDescendantOfWrapper = isDescendant,
                IsInfrastructureProcess = IsInfrastructureName(stat.Comm)
            };
        }

        private HashSet<int> ComputeWrapperDescendants(Dictionary<int, ProcStatRecord> stats)
        {
            var childrenByParent = new Dictionary<int, List<int>>();
            foreach (var stat in stats.Values)
            {
                if (!childrenByParent.TryGetValue(stat.ParentPid, out var list))
                    childrenByParent[stat.ParentPid] = list = new List<int>();
                list.Add(stat.Pid);
            }

            var visited = new HashSet<int> { _wrapper.WrapperPid };
            var descendants = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(_wrapper.WrapperPid);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!childrenByParent.TryGetValue(current, out var children))
                    continue;
                foreach (var child in children)
                {
                    if (visited.Add(child))
                    {
                        descendants.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }
            return descendants;
        }

        /// <summary>Wine infrastructure names (shared with ProtonProcessDetector) plus Gamescope's own processes.</summary>
        public static bool IsInfrastructureName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
            return IsGamescopeInfrastructureName(processName) || ProtonProcessDetector.IsInfrastructureProcessName(processName);
        }

        /// <summary>
        /// Gamescope's OWN processes - session-scoped infrastructure that
        /// belongs to exactly one launch and IS terminated with it (unlike
        /// the shared Wine prefix daemons, which are preserved - see
        /// <see cref="GameSessionTerminator"/>).
        /// </summary>
        public static bool IsGamescopeInfrastructureName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
            foreach (var name in GamescopeInfrastructureNames)
            {
                if (processName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
