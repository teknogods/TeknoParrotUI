using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Honest result of a bounded, synchronous pipe shutdown
    /// (<see cref="ControlPipe.StopAndWait"/>, SerialPortHandler.StopAndWait,
    /// ProtonBridgePipe.CloseAndWait). Every field reports what was actually
    /// VERIFIED - never what was merely requested or assumed. A false value
    /// means the corresponding resource could still be alive and the caller
    /// must not pretend the session is fully torn down.
    /// </summary>
    public sealed class PipeShutdownResult
    {
        /// <summary>Everything below was verified: threads joined, helper exited, nothing remaining.</summary>
        public bool Completed { get; init; }

        /// <summary>The listener/worker thread was confirmed exited (joined within the timeout), or never existed.</summary>
        public bool ListenerThreadExited { get; init; } = true;

        /// <summary>The queue-processing thread was confirmed exited, or never existed.</summary>
        public bool QueueThreadExited { get; init; } = true;

        /// <summary>The in-prefix pipehelper process (and its session-token descendants) were confirmed exited, or none existed.</summary>
        public bool HelperExited { get; init; } = true;

        /// <summary>PIDs of session helper processes that could NOT be confirmed exited.</summary>
        public IReadOnlyList<int> RemainingHelperPids { get; init; } = Array.Empty<int>();

        /// <summary>Human-readable detail for diagnostics.</summary>
        public string Detail { get; init; } = string.Empty;

        public static PipeShutdownResult NothingToStop { get; } = new PipeShutdownResult
        {
            Completed = true,
            Detail = "nothing to stop"
        };
    }
}
