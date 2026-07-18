using System;
using System.Collections.Generic;
using TeknoParrotUi.Common.GameLaunch;

namespace InputMethodAudit
{
    internal static class BoundedLineBufferTest
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

            var buffer = new BoundedLineBuffer(
                maxLines: 4,
                maxCharacters: 160,
                maxLineCharacters: 48);
            buffer.AppendLine("first");
            Check("Normal line is retained",
                buffer.GetText().Contains("first", StringComparison.Ordinal));

            buffer.AppendLine(new string('X', 200));
            var oversized = buffer.GetText();
            Check("Oversized line is marked",
                oversized.Contains("[line truncated]",
                    StringComparison.Ordinal));
            Check("Oversized line is bounded",
                buffer.CharacterCount <= 160);

            for (var i = 0; i < 20; i++)
                buffer.AppendLine($"noise-{i:D2}-{new string('N', 24)}");
            buffer.AppendLine("FINAL_FAILURE_MARKER");
            var bounded = buffer.GetText();
            Check("Line count is bounded", buffer.Count <= 4);
            Check("Character count is bounded",
                buffer.CharacterCount <= 160);
            Check("Newest output is retained",
                bounded.Contains("FINAL_FAILURE_MARKER",
                    StringComparison.Ordinal));
            Check("Dropped history is disclosed",
                bounded.Contains("earlier launch output",
                    StringComparison.Ordinal));
            Check("Oldest output was dropped",
                !bounded.Contains("first", StringComparison.Ordinal));

            buffer.Clear();
            Check("Clear resets content and truncation",
                buffer.Count == 0 &&
                buffer.CharacterCount == 0 &&
                !buffer.WasTruncated &&
                buffer.GetText().Length == 0);

            if (failures.Count == 0)
            {
                Console.WriteLine(
                    $"Bounded UI console buffer: PASS ({checks}/{checks})");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine("FAIL: " + failure);
            Console.Error.WriteLine(
                $"Bounded UI console buffer: FAIL ({checks - failures.Count}/{checks})");
            return 1;
        }
    }
}
