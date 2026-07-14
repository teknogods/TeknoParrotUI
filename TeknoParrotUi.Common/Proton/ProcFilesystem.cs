using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>One process's /proc/&lt;pid&gt;/stat essentials.</summary>
    public sealed class ProcStatRecord
    {
        public int Pid { get; init; }
        public string Comm { get; init; } = string.Empty;
        public int ParentPid { get; init; }
        /// <summary>Field 22: start time in clock ticks since boot (per-boot PID-reuse guard).</summary>
        public long StartTimeTicks { get; init; }
    }

    /// <summary>
    /// Injectable /proc filesystem seam so session-process discovery
    /// (<see cref="ProcSessionProcessLocator"/>) is fully unit-testable with
    /// fake data - including permission-denied, process-exited-mid-scan and
    /// malformed-environ cases - without real Wine/Gamescope processes.
    /// All methods return null (or an empty list) instead of throwing when
    /// the underlying data is gone or unreadable.
    /// </summary>
    public interface IProcReader
    {
        IReadOnlyList<int> EnumerateProcessIds();

        /// <summary>
        /// Raw null-separated environ content, bounded to
        /// <paramref name="maxBytes"/>. Null when the process is gone,
        /// permission is denied, or any read error occurs.
        /// </summary>
        string ReadEnvironRaw(int pid, int maxBytes);

        /// <summary>Parsed stat record, or null when gone/unreadable/malformed.</summary>
        ProcStatRecord ReadStat(int pid);

        /// <summary>Resolved /proc/&lt;pid&gt;/exe target, or null.</summary>
        string ReadExecutablePath(int pid);

        bool ProcessExists(int pid);
    }

    /// <summary>Real Linux /proc implementation. Every read is defensive - processes can vanish at any instant.</summary>
    public sealed class LinuxProcReader : IProcReader
    {
        /// <summary>Upper bound for one environ read - environments are small; anything larger is pathological.</summary>
        public const int DefaultMaxEnvironBytes = 1024 * 1024;

        public IReadOnlyList<int> EnumerateProcessIds()
        {
            var result = new List<int>();
            if (!Directory.Exists("/proc"))
                return result;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories("/proc"))
                {
                    if (int.TryParse(Path.GetFileName(dir), out var pid) && pid > 0)
                        result.Add(pid);
                }
            }
            catch
            {
                // /proc itself unreadable - return what we have
            }
            return result;
        }

        public string ReadEnvironRaw(int pid, int maxBytes)
        {
            if (pid <= 0)
                return null;
            try
            {
                // /proc files report size 0 - must stream-read with a bound,
                // never trust Length.
                using var stream = new FileStream($"/proc/{pid}/environ", FileMode.Open, FileAccess.Read);
                var buffer = new byte[Math.Min(maxBytes, DefaultMaxEnvironBytes)];
                var total = 0;
                int read;
                while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                    total += read;
                return Encoding.UTF8.GetString(buffer, 0, total);
            }
            catch
            {
                // gone, permission denied, or read raced with process exit
                return null;
            }
        }

        public ProcStatRecord ReadStat(int pid)
        {
            if (pid <= 0)
                return null;
            try
            {
                var stat = File.ReadAllText($"/proc/{pid}/stat");
                return ParseStatLine(pid, stat);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses one /proc stat line: "pid (comm) state ppid ... starttime(22) ...".
        /// comm may contain spaces/parentheses, so split on the LAST ')'.
        /// Exposed for direct testing against malformed input.
        /// </summary>
        public static ProcStatRecord ParseStatLine(int pid, string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return null;
            var open = stat.IndexOf('(');
            var close = stat.LastIndexOf(')');
            if (open < 0 || close < open || close + 2 >= stat.Length)
                return null;
            var comm = stat.Substring(open + 1, close - open - 1);
            var rest = stat.Substring(close + 2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // rest[0]=state(3), rest[1]=ppid(4), ... rest[19]=starttime(22)
            if (rest.Length < 20 || !int.TryParse(rest[1], out var ppid) || !long.TryParse(rest[19], out var startTicks))
                return null;
            return new ProcStatRecord { Pid = pid, Comm = comm, ParentPid = ppid, StartTimeTicks = startTicks };
        }

        public string ReadExecutablePath(int pid)
        {
            if (pid <= 0)
                return null;
            try
            {
                return new FileInfo($"/proc/{pid}/exe").LinkTarget;
            }
            catch
            {
                return null;
            }
        }

        public bool ProcessExists(int pid) => pid > 0 && Directory.Exists($"/proc/{pid}");
    }
}
