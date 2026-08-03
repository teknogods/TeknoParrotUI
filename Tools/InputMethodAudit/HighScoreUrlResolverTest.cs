using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class HighScoreUrlResolverTest
    {
        public static int Run()
        {
            var failures = new List<string>();

            Expect(
                "https://teknoparrot.com/en/Highscore/GameSpecificExternal/IDACS3",
                HighScoreUrlResolver.Resolve("IDTA", "en-US"),
                "Initial D Season 3 external score page",
                failures);
            Expect(
                "https://teknoparrot.com/de/Highscore/GameSpecificExternal/IDACS5",
                HighScoreUrlResolver.Resolve("IDTAS5", "de-DE"),
                "Initial D Season 5 localized score page",
                failures);
            Expect(
                "https://teknoparrot.com/fr/Highscore/GameSpecificExternal/WMMT6RR",
                HighScoreUrlResolver.Resolve("WMMT6RR", "fr-FR"),
                "WMMT6RR external score page",
                failures);
            Expect(
                "https://teknoparrot.com/en/Highscore/GameSpecific/DeadHeatRiders",
                HighScoreUrlResolver.Resolve("DeadHeatRiders", "unknown"),
                "Dead Heat Riders score submission page",
                failures);

            if (HighScoreUrlResolver.Resolve("UnsupportedProfile", "en-US") != null)
                failures.Add("unsupported profile: expected no high-score URL.");

            if (failures.Count == 0)
            {
                Console.WriteLine("High-score URL resolver: PASS (5/5)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"High-score URL resolver: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static void Expect(
            string expected,
            Uri actual,
            string scenario,
            ICollection<string> failures)
        {
            if (!string.Equals(expected, actual?.AbsoluteUri, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{scenario}: expected '{expected}', got '{actual?.AbsoluteUri ?? "<null>"}'.");
            }
        }
    }
}
