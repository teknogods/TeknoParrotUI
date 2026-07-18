using System;
using System.Collections.Generic;
using System.IO;
using TeknoParrotUi.Common.Updater;

namespace InputMethodAudit
{
    internal static class ArchivePathTest
    {
        public static int Run()
        {
            var root = Path.Combine(Path.GetTempPath(), "tp-archive-test-root");
            var failures = new List<string>();

            ExpectAllowed(root, "nested/file.bin", failures);
            ExpectAllowed(root, "nested\\windows-style.bin", failures);
            ExpectRejected(root, "../outside.bin", failures);
            ExpectRejected(root, "nested/../../outside.bin", failures);
            ExpectRejected(root, "nested\\..\\..\\outside.bin", failures);
            ExpectRejected(root, Path.GetPathRoot(root) + "outside.bin", failures);
            ExpectUnixMode(0, null, failures);
            ExpectUnixMode(unchecked((int)0x81ED0000), UnixFileMode.UserRead |
                UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute, failures);
            ExpectUnixMode(unchecked((int)0x81A40000), UnixFileMode.UserRead |
                UnixFileMode.UserWrite | UnixFileMode.GroupRead |
                UnixFileMode.OtherRead, failures);
            ExpectUnixMode(0x20, null, failures);

            if (failures.Count == 0)
            {
                Console.WriteLine("Archive path and Unix mode validation: PASS (10/10)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"Archive path validation: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static void ExpectAllowed(string root, string entry, ICollection<string> failures)
        {
            try
            {
                var destination = SafeArchivePath.Resolve(root, entry);
                var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(prefix, OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
                    failures.Add($"Allowed path escaped root: {entry} -> {destination}");
            }
            catch (Exception ex)
            {
                failures.Add($"Safe path was rejected: {entry} ({ex.Message})");
            }
        }

        private static void ExpectRejected(string root, string entry, ICollection<string> failures)
        {
            try
            {
                SafeArchivePath.Resolve(root, entry);
                failures.Add($"Unsafe path was accepted: {entry}");
            }
            catch (InvalidDataException)
            {
                // Expected.
            }
        }

        private static void ExpectUnixMode(
            int externalAttributes,
            UnixFileMode? expected,
            ICollection<string> failures)
        {
            var actual = SafeArchivePath.GetStoredUnixFileMode(externalAttributes);
            if (actual != expected)
                failures.Add(
                    $"Unix mode mismatch for 0x{externalAttributes:X8}: expected {expected}, got {actual}");
        }
    }
}
