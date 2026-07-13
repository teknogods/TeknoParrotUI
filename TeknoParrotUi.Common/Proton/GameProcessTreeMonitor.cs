using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Session-scoped process-tree operations for a Gamescope-wrapped
    /// launch. Exists so GameSession can determine "has the real game
    /// exited, even though the Gamescope wrapper itself is still running"
    /// and terminate ONLY the specific wrapper process tree it launched -
    /// never any other Gamescope/Wine process on the system - without
    /// needing real Wine/Gamescope processes in unit tests (see
    /// <see cref="IGameProcessTreeMonitor"/> for the injectable seam).
    /// </summary>
    public interface IGameProcessTreeMonitor
    {
        /// <summary>All descendant PIDs of <paramref name="rootPid"/> (children, grandchildren, ...), NOT including the root itself.</summary>
        IReadOnlyCollection<int> GetDescendantProcessIds(int rootPid);

        /// <summary>
        /// True when the game session should still be considered alive.
        /// When <paramref name="knownGameProcessIds"/> is non-empty, this
        /// checks whether ANY of those specific pids are still alive
        /// (that's the real signal - the game, not the wrapper). When empty
        /// (no game process observed yet), falls back to whether the
        /// wrapper itself is still alive.
        /// </summary>
        bool IsGameSessionAlive(int wrapperPid, IReadOnlyCollection<int> knownGameProcessIds);

        /// <summary>
        /// Gracefully terminates ONLY <paramref name="wrapperProcess"/> (the
        /// specific process this session launched) - SIGTERM first, waiting
        /// up to <paramref name="gracefulTimeout"/>, then SIGKILL
        /// (<see cref="Process.Kill()"/>) only if it is still alive
        /// afterwards. Never touches any other process.
        /// </summary>
        Task TerminateWrapperAsync(Process wrapperProcess, TimeSpan gracefulTimeout, CancellationToken cancellationToken);
    }

    /// <summary>Real /proc-based implementation - Linux only.</summary>
    public sealed class LinuxProcessTreeMonitor : IGameProcessTreeMonitor
    {
        private const int SIGTERM = 15;

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        public IReadOnlyCollection<int> GetDescendantProcessIds(int rootPid)
        {
            var childrenByParent = new Dictionary<int, List<int>>();
            if (!Directory.Exists("/proc"))
                return Array.Empty<int>();

            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(dir), out var pid))
                    continue;

                try
                {
                    var stat = File.ReadAllText(Path.Combine(dir, "stat"));
                    // Format: "pid (comm) state ppid ..." - comm may itself contain
                    // spaces/parentheses, so split on the LAST ')' rather than the first.
                    var closeParen = stat.LastIndexOf(')');
                    if (closeParen < 0 || closeParen + 2 >= stat.Length)
                        continue;
                    var rest = stat.Substring(closeParen + 2);
                    var fields = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length < 2 || !int.TryParse(fields[1], out var ppid))
                        continue;

                    if (!childrenByParent.TryGetValue(ppid, out var list))
                        childrenByParent[ppid] = list = new List<int>();
                    list.Add(pid);
                }
                catch
                {
                    // process exited mid-scan - skip
                }
            }

            var result = new List<int>();
            var visited = new HashSet<int> { rootPid };
            var queue = new Queue<int>();
            queue.Enqueue(rootPid);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!childrenByParent.TryGetValue(current, out var children))
                    continue;
                foreach (var child in children)
                {
                    if (visited.Add(child))
                    {
                        result.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }
            return result;
        }

        public bool IsGameSessionAlive(int wrapperPid, IReadOnlyCollection<int> knownGameProcessIds)
        {
            if (knownGameProcessIds != null && knownGameProcessIds.Count > 0)
            {
                foreach (var pid in knownGameProcessIds)
                {
                    if (Directory.Exists($"/proc/{pid}"))
                        return true;
                }
                return false;
            }

            // No game process observed yet - fall back to the wrapper's own liveness.
            return Directory.Exists($"/proc/{wrapperPid}");
        }

        public async Task TerminateWrapperAsync(Process wrapperProcess, TimeSpan gracefulTimeout, CancellationToken cancellationToken)
        {
            if (wrapperProcess == null)
                return;

            try
            {
                if (wrapperProcess.HasExited)
                    return;
            }
            catch
            {
                return;
            }

            var pid = wrapperProcess.Id;
            try { kill(pid, SIGTERM); }
            catch { /* P/Invoke unavailable (non-Linux) or process already gone */ }

            var deadline = DateTime.UtcNow + (gracefulTimeout < TimeSpan.Zero ? TimeSpan.Zero : gracefulTimeout);
            while (DateTime.UtcNow < deadline)
            {
                bool exited;
                try { exited = wrapperProcess.HasExited; }
                catch { return; }
                if (exited)
                    return;

                try { await Task.Delay(200, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }

            try
            {
                if (!wrapperProcess.HasExited)
                    wrapperProcess.Kill();
            }
            catch
            {
                // already gone, or permission/race - nothing more we can safely do
            }
        }
    }
}
