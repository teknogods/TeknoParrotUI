using System;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// How confident the lifecycle is that a session process is the actual
    /// main game (vs. a loader, helper, or Wine infrastructure process).
    /// An arbitrary .exe NEVER becomes ConfirmedMainGame immediately - see
    /// <see cref="GameProcessClassifier"/> (static classification) and
    /// <see cref="WrappedGameLifecycleStateMachine"/> (candidate
    /// stabilization / promotion over time).
    /// </summary>
    public enum GameProcessConfidence
    {
        None,
        Infrastructure,
        Candidate,
        ExpectedSecondaryExecutable,
        ExpectedPrimaryExecutable,
        ConfirmedMainGame
    }

    /// <summary>
    /// Identity of one process observed to belong to a specific launch
    /// session. PID alone is NOT sufficient identity on Linux (PIDs are
    /// reused) - <see cref="StartTimeTicks"/> (the raw per-boot start time
    /// from /proc/&lt;pid&gt;/stat field 22) disambiguates: the same PID with a
    /// different start time is a DIFFERENT process and must never be
    /// signaled. See <see cref="ProcessSafetyGuard"/>.
    /// </summary>
    public sealed class SessionProcessInfo
    {
        public int Pid { get; init; }

        public int ParentPid { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public string ExecutablePath { get; init; }

        /// <summary>Absolute start time when derivable; null when /proc start-time data was unreadable.</summary>
        public DateTimeOffset? StartTime { get; init; }

        /// <summary>
        /// Raw start time in clock ticks since boot (/proc/&lt;pid&gt;/stat field
        /// 22). This is the PID-reuse guard: stable for the life of a
        /// process, different for any later process that reuses the PID
        /// within the same boot. Null when unreadable (reduced confidence -
        /// logged by callers).
        /// </summary>
        public long? StartTimeTicks { get; init; }

        /// <summary>True when /proc/&lt;pid&gt;/environ contained the exact TP_LAUNCH_SESSION_ID entry for this session.</summary>
        public bool HasSessionToken { get; init; }

        /// <summary>True when the process is currently reachable from the wrapper via parent-child /proc links (reparented session members are NOT).</summary>
        public bool IsDescendantOfWrapper { get; init; }

        /// <summary>True when this is the original, identity-verified Gamescope wrapper process itself.</summary>
        public bool IsWrapper { get; init; }

        /// <summary>Wine/Gamescope plumbing (wineserver, services.exe, gamescopereaper, ...) - never counts as the running game.</summary>
        public bool IsInfrastructureProcess { get; init; }

        public GameProcessConfidence Confidence { get; init; }

        /// <summary>
        /// True when the executable name matches a KNOWN loader/launcher
        /// (OpenParrotLoader, BudgieLoader, ...). Launchers are legitimate
        /// session members and may even host the game during startup handoff,
        /// but once a game was confirmed they can never be promoted to the
        /// confirmed game again or hold the session open after the game exits
        /// (some games keep their loader alive for the whole session).
        /// </summary>
        public bool IsKnownLauncher { get; init; }

        /// <summary>Copy with a different confidence (records are init-only).</summary>
        public SessionProcessInfo WithConfidence(GameProcessConfidence confidence) => WithClassification(confidence, IsKnownLauncher);

        /// <summary>Copy with a different confidence and launcher flag.</summary>
        public SessionProcessInfo WithClassification(GameProcessConfidence confidence, bool isKnownLauncher) => new SessionProcessInfo
        {
            Pid = Pid,
            ParentPid = ParentPid,
            ProcessName = ProcessName,
            ExecutablePath = ExecutablePath,
            StartTime = StartTime,
            StartTimeTicks = StartTimeTicks,
            HasSessionToken = HasSessionToken,
            IsDescendantOfWrapper = IsDescendantOfWrapper,
            IsWrapper = IsWrapper,
            IsInfrastructureProcess = IsInfrastructureProcess,
            Confidence = confidence,
            IsKnownLauncher = isKnownLauncher
        };

        /// <summary>Same live process? PID plus start-time identity (start time compared only when both sides have one).</summary>
        public bool SameIdentityAs(SessionProcessInfo other)
        {
            if (other == null || other.Pid != Pid)
                return false;
            if (StartTimeTicks.HasValue && other.StartTimeTicks.HasValue)
                return StartTimeTicks.Value == other.StartTimeTicks.Value;
            return true; // start time unavailable on one side - PID-only match (reduced confidence)
        }

        public override string ToString() => $"{Pid} ({ProcessName}, {Confidence})";
    }
}
