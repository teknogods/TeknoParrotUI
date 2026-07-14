using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Registry of pipehelper processes started by THIS TeknoParrotUI process.
    /// Replaces the old global name-wide stale-helper sweep: every cleanup
    /// path (bridge close, session cleanup, application-exit hook, crash
    /// recovery) only ever touches individually identity-verified helpers -
    /// never "all processes named pipehelper".
    ///
    /// Identity model per helper: PID + /proc start-time ticks (PID-reuse
    /// guard) + owning session token + Wine prefix + owning bridge id.
    /// </summary>
    public static class PipeHelperRegistry
    {
        /// <summary>Env var carrying the launch session token (see GameLaunchSessionIdentity).</summary>
        public const string SessionTokenEnvVar = "TP_LAUNCH_SESSION_ID";
        /// <summary>Env var carrying the TeknoParrotUI PID that launched the helper.</summary>
        public const string OwnerPidEnvVar = "TP_PIPEHELPER_OWNER_PID";
        /// <summary>Env var carrying the owner's /proc start-time ticks (owner PID-reuse guard).</summary>
        public const string OwnerStartEnvVar = "TP_PIPEHELPER_OWNER_START";

        public sealed class HelperRegistration
        {
            public int Pid { get; init; }
            public long? StartTimeTicks { get; init; }
            public string SessionToken { get; init; }
            public string WinePrefix { get; init; }
            public string BridgeId { get; init; }
        }

        private static readonly object Sync = new object();
        private static readonly List<HelperRegistration> Entries = new List<HelperRegistration>();

        public static void Register(int pid, long? startTimeTicks, string sessionToken, string winePrefix, string bridgeId)
        {
            lock (Sync)
            {
                Entries.RemoveAll(e => e.Pid == pid);
                Entries.Add(new HelperRegistration
                {
                    Pid = pid,
                    StartTimeTicks = startTimeTicks,
                    SessionToken = sessionToken,
                    WinePrefix = winePrefix,
                    BridgeId = bridgeId
                });
            }
        }

        public static void Unregister(int pid)
        {
            lock (Sync)
            {
                Entries.RemoveAll(e => e.Pid == pid);
            }
        }

        public static IReadOnlyList<HelperRegistration> Snapshot()
        {
            lock (Sync)
            {
                return Entries.ToList();
            }
        }

        /// <summary>Session tokens currently owned by live GameSessions of this process.</summary>
        public static IReadOnlyList<string> LiveSessionTokens()
        {
            lock (Sync)
            {
                return Entries.Select(e => e.SessionToken)
                              .Where(t => !string.IsNullOrEmpty(t))
                              .Distinct()
                              .ToList();
            }
        }

        /// <summary>
        /// Registered helpers that are still alive right now, with identity
        /// (start-time) re-verified. Used for beginning-of-launch "previous
        /// session resource still alive" diagnostics.
        /// </summary>
        public static IReadOnlyList<HelperRegistration> LiveRegisteredHelpers(IProcReader proc = null)
        {
            proc ??= new LinuxProcReader();
            var live = new List<HelperRegistration>();
            foreach (var entry in Snapshot())
            {
                var stat = proc.ReadStat(entry.Pid);
                if (stat == null)
                    continue; // exited
                if (entry.StartTimeTicks.HasValue && stat.StartTimeTicks != entry.StartTimeTicks.Value)
                    continue; // PID reused by an unrelated process
                live.Add(entry);
            }
            return live;
        }

        /// <summary>
        /// Application-exit hook: terminates ONLY helpers this process
        /// registered, each re-verified by PID + start-time immediately before
        /// the signal. Never a process-name-wide sweep.
        /// </summary>
        public static void TerminateRegisteredHelpers(Action<string> log = null)
        {
            if (!OperatingSystem.IsLinux())
                return;
            var proc = new LinuxProcReader();
            var signaler = new LinuxProcessSignaler();
            foreach (var entry in Snapshot())
            {
                var stat = proc.ReadStat(entry.Pid);
                if (stat == null)
                {
                    Unregister(entry.Pid);
                    continue;
                }
                if (entry.StartTimeTicks.HasValue && stat.StartTimeTicks != entry.StartTimeTicks.Value)
                {
                    // PID reused - absolutely never signal it.
                    Unregister(entry.Pid);
                    continue;
                }
                log?.Invoke($"[PipeHelperRegistry] terminating owned helper pid {entry.Pid} (bridge {entry.BridgeId})");
                signaler.SignalForce(entry.Pid);
                Unregister(entry.Pid);
            }
        }

        /// <summary>
        /// Finds live pipehelper processes carrying the given session token
        /// (direct helper, descendants, or reparented helpers - the inherited
        /// environment survives all of that). Returns (pid, startTimeTicks).
        /// </summary>
        public static IReadOnlyList<(int Pid, long StartTimeTicks)> FindSessionHelperProcesses(
            string sessionToken, IProcReader proc = null)
        {
            var result = new List<(int, long)>();
            if (string.IsNullOrEmpty(sessionToken) || !OperatingSystem.IsLinux())
                return result;
            proc ??= new LinuxProcReader();
            var needle = SessionTokenEnvVar + "=" + sessionToken;
            var ownPid = Environment.ProcessId;

            foreach (var pid in proc.EnumerateProcessIds())
            {
                if (pid == ownPid)
                    continue;
                var stat = proc.ReadStat(pid);
                if (stat == null)
                    continue;
                if (!stat.Comm.StartsWith("pipehelper", StringComparison.OrdinalIgnoreCase))
                    continue;
                var environ = proc.ReadEnvironRaw(pid, LinuxProcReader.DefaultMaxEnvironBytes);
                if (environ == null || !environ.Split('\0').Contains(needle))
                    continue;
                result.Add((pid, stat.StartTimeTicks));
            }
            return result;
        }

        /// <summary>
        /// Crash recovery ONLY (previous TeknoParrotUI process died without
        /// cleanup). Kills an orphan helper only when ALL of these hold:
        ///   1. it carries a TeknoParrot session token,
        ///   2. its recorded owner TeknoParrotUI PID no longer exists or has a
        ///      different start time (owner crashed; PID possibly reused),
        ///   3. it belongs to the given Wine prefix,
        ///   4. its session token is not owned by a live GameSession of THIS
        ///      process.
        /// Helpers that fail ANY check (including pre-token helpers that can't
        /// be verified) are left alone.
        /// </summary>
        public static int CleanupOrphanedHelpers(string winePrefix, Action<string> log = null, IProcReader proc = null)
        {
            if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(winePrefix))
                return 0;
            proc ??= new LinuxProcReader();
            var signaler = new LinuxProcessSignaler();
            var liveTokens = LiveSessionTokens();
            var ownPid = Environment.ProcessId;
            var killed = 0;

            foreach (var pid in proc.EnumerateProcessIds())
            {
                if (pid == ownPid)
                    continue;
                var stat = proc.ReadStat(pid);
                if (stat == null || !stat.Comm.StartsWith("pipehelper", StringComparison.OrdinalIgnoreCase))
                    continue;

                var environ = proc.ReadEnvironRaw(pid, LinuxProcReader.DefaultMaxEnvironBytes);
                if (environ == null)
                    continue;
                var env = ParseEnviron(environ);

                // 1. must carry a TeknoParrot session token
                if (!env.TryGetValue(SessionTokenEnvVar, out var token) || string.IsNullOrEmpty(token))
                    continue;

                // 4. must not belong to a live session of this process
                if (liveTokens.Contains(token))
                    continue;

                // 3. must belong to the relevant Wine prefix
                if (!env.TryGetValue("WINEPREFIX", out var prefix) ||
                    !string.Equals(prefix?.TrimEnd('/'), winePrefix.TrimEnd('/'), StringComparison.Ordinal))
                    continue;

                // 2. its owner TeknoParrotUI process must be verifiably dead
                if (!env.TryGetValue(OwnerPidEnvVar, out var ownerPidStr) || !int.TryParse(ownerPidStr, out var ownerPid))
                    continue; // can't verify ownership - leave it alone
                if (ownerPid == ownPid)
                    continue; // our own helper - never an orphan
                var ownerStat = proc.ReadStat(ownerPid);
                if (ownerStat != null)
                {
                    // Owner PID still exists - only treat as dead when the
                    // recorded start time proves the PID was reused.
                    if (!env.TryGetValue(OwnerStartEnvVar, out var ownerStartStr) ||
                        !long.TryParse(ownerStartStr, out var ownerStart) ||
                        ownerStat.StartTimeTicks == ownerStart)
                        continue; // owner is (or may be) alive - leave it alone
                }

                log?.Invoke($"[PipeHelperRegistry] crash recovery: terminating orphaned helper pid {pid} " +
                            $"(owner {ownerPid} gone, prefix {winePrefix})");
                signaler.SignalForce(pid);
                killed++;
            }
            return killed;
        }

        private static Dictionary<string, string> ParseEnviron(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in raw.Split('\0'))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0)
                    result[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            return result;
        }
    }
}
