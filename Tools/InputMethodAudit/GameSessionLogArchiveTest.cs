using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace InputMethodAudit
{
    internal static class GameSessionLogArchiveTest
    {
        public static int Run()
        {
            var failures = new List<string>();
            var checks = 0;

            void Check(string name, bool condition)
            {
                checks++;
                if (!condition)
                    failures.Add(name);
            }

            GameSessionLogArchive.BeginRun(new GameProfile
            {
                GameNameInternal = "LogArchiveProbe",
                GamePath = "probe.exe"
            });

            var oversized = new string('X',
                GameSessionLogArchive.MaxLineCharacters * 2);
            GameSessionLogArchive.Append(oversized);
            var afterOversized = GameSessionLogArchive.GetLastRunLog();
            Check("Oversized line is marked truncated",
                afterOversized.Contains("[line truncated]",
                    StringComparison.Ordinal));
            Check("Oversized line is bounded",
                afterOversized.Length <
                GameSessionLogArchive.MaxLineCharacters * 2);

            var noisyLine = new string('N', 4096);
            for (var i = 0; i < 1400; i++)
                GameSessionLogArchive.Append($"{i:D4}:{noisyLine}");
            GameSessionLogArchive.Append("FINAL_FAILURE_MARKER");
            GameSessionLogArchive.EndRun(23);

            var report = GameSessionLogArchive.GetLastRunLog();
            Check("Total retained report stays near the configured cap",
                report.Length <
                GameSessionLogArchive.MaxTotalCharacters +
                GameSessionLogArchive.MaxLineCharacters * 2);
            Check("Newest failure output is retained",
                report.Contains("FINAL_FAILURE_MARKER",
                    StringComparison.Ordinal));
            Check("Exit code remains available",
                report.Contains("exit code 23",
                    StringComparison.OrdinalIgnoreCase));
            Check("Total-cap truncation is disclosed",
                report.Contains("log was truncated",
                    StringComparison.Ordinal));

            if (failures.Count == 0)
            {
                Console.WriteLine(
                    $"Game session log archive: PASS ({checks}/{checks})");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine("FAIL: " + failure);
            Console.Error.WriteLine(
                $"Game session log archive: FAIL ({checks - failures.Count}/{checks})");
            return 1;
        }
    }
}
