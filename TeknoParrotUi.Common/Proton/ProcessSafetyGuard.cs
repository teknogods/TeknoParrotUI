using System;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Verdict for one candidate signal target.</summary>
    public sealed class ProcessSignalDecision
    {
        public bool Allowed { get; init; }
        public string Reason { get; init; } = string.Empty;
        /// <summary>True when identity had to be accepted without a start-time check (start time unreadable) - logged as reduced confidence.</summary>
        public bool ReducedConfidence { get; init; }

        public static ProcessSignalDecision Allow(string reason, bool reducedConfidence = false) =>
            new ProcessSignalDecision { Allowed = true, Reason = reason, ReducedConfidence = reducedConfidence };

        public static ProcessSignalDecision Deny(string reason) =>
            new ProcessSignalDecision { Allowed = false, Reason = reason };
    }

    /// <summary>
    /// PURE pre-signal safety rules (task: process safety rules). Applied by
    /// <see cref="GameSessionTerminator"/> immediately before EVERY signal,
    /// against a FRESH re-description of the process - never against stale
    /// observations. Fully unit-testable with fabricated inputs.
    ///
    /// A signal is allowed only when ALL of these hold:
    ///  - PID &gt; 1 (never PID 0/1)
    ///  - PID is not the current TeknoParrotUI process
    ///  - the process still exists (fresh re-description succeeded)
    ///  - start-time identity still matches what was previously observed
    ///    (PID-reuse guard; PID-only match is allowed but flagged as
    ///    reduced confidence when either side has no start time)
    ///  - the process still carries THIS session's token, or is the
    ///    identity-verified original wrapper (a process belonging to another
    ///    session fails this automatically: its environ contains a different
    ///    token value, so HasSessionToken is false for OUR token)
    /// </summary>
    public static class ProcessSafetyGuard
    {
        public static ProcessSignalDecision Validate(
            SessionProcessInfo previouslyObserved,
            SessionProcessInfo revalidated,
            int currentProcessPid)
        {
            if (previouslyObserved == null)
                return ProcessSignalDecision.Deny("no previously observed identity");

            if (previouslyObserved.Pid <= 1)
                return ProcessSignalDecision.Deny($"pid {previouslyObserved.Pid} is protected (never signal pid 0/1)");

            if (previouslyObserved.Pid == currentProcessPid)
                return ProcessSignalDecision.Deny("refusing to signal the current TeknoParrotUI process");

            if (revalidated == null)
                return ProcessSignalDecision.Deny($"pid {previouslyObserved.Pid} no longer exists (already exited)");

            if (previouslyObserved.StartTimeTicks.HasValue && revalidated.StartTimeTicks.HasValue)
            {
                if (previouslyObserved.StartTimeTicks.Value != revalidated.StartTimeTicks.Value)
                    return ProcessSignalDecision.Deny(
                        $"pid {previouslyObserved.Pid} start time changed ({previouslyObserved.StartTimeTicks.Value} -> {revalidated.StartTimeTicks.Value}) - PID was reused by a different process");
            }

            if (!revalidated.HasSessionToken && !revalidated.IsWrapper)
                return ProcessSignalDecision.Deny(
                    $"pid {previouslyObserved.Pid} no longer carries this session's token and is not the verified wrapper - may belong to another session");

            bool reduced = !previouslyObserved.StartTimeTicks.HasValue || !revalidated.StartTimeTicks.HasValue;
            return ProcessSignalDecision.Allow(
                revalidated.IsWrapper ? "verified original wrapper" : "verified session member (token + identity match)",
                reducedConfidence: reduced);
        }
    }
}
