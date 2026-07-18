using System;
using System.Collections.Generic;
using System.IO;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class ProfilePathResolverTest
    {
        public static int Run()
        {
            var failures = new List<string>();
            var root = FindProfilesDirectory();
            if (root == null)
            {
                Console.Error.WriteLine("Profile path resolver: FAIL (GameProfiles not found)");
                return 1;
            }

            ExpectAllowed(root, "SR3.xml", failures);
            ExpectRejected(root, "missing-profile.xml", failures);
            ExpectRejected(root, "../SR3.xml", failures);
            ExpectRejected(root, @"..\SR3.xml", failures);
            ExpectRejected(root, Path.Combine(root, "SR3.xml"), failures);
            ExpectRejected(root, "SR3", failures);

            if (failures.Count == 0)
            {
                Console.WriteLine("Profile path resolver: PASS (6/6)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"Profile path resolver: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static string FindProfilesDirectory()
        {
            var directory = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                var candidate = Path.Combine(directory, "TeknoParrotUi.Common", "GameProfiles");
                if (Directory.Exists(candidate))
                    return candidate;
                directory = Path.GetDirectoryName(directory);
            }
            return null;
        }

        private static void ExpectAllowed(
            string root,
            string fileName,
            ICollection<string> failures)
        {
            if (!GameProfilePathResolver.TryResolveExisting(root, fileName, out var path) ||
                !File.Exists(path))
                failures.Add($"Valid profile was rejected: {fileName}");
        }

        private static void ExpectRejected(
            string root,
            string fileName,
            ICollection<string> failures)
        {
            if (GameProfilePathResolver.TryResolveExisting(root, fileName, out _))
                failures.Add($"Invalid profile was accepted: {fileName}");
        }
    }
}
