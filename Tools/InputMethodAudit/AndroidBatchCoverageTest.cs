using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;

namespace InputMethodAudit
{
    internal static class AndroidBatchCoverageTest
    {
        private static readonly string[] DefaultRelativeRoots =
        {
            Path.Combine("linuxtest", "next_test"),
            Path.Combine("linuxtest", "next_test2"),
            Path.Combine("linuxtest", "next_test3")
        };

        public static int Run(IReadOnlyList<string> requestedRoots)
        {
            try
            {
                var repositoryRoot = FindRepositoryRoot();
                var recipes = AndroidLaunchRecipeCatalog.LoadAll(Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Common",
                    AndroidLaunchRecipeCatalog.DirectoryName));
                var profiles = recipes.Select(recipe => new GameProfile
                {
                    ProfileName = recipe.ProfileName,
                    GameNameInternal = recipe.ProfileName
                }).ToArray();
                var roots = requestedRoots.Count > 0
                    ? requestedRoots.ToArray()
                    : GetConfiguredRoots();
                if (roots.Length == 0)
                {
                    Console.WriteLine(
                        "Android batch coverage: SKIP (no local dump roots; " +
                        "recipe/schema coverage still ran)");
                    return 0;
                }

                var failed = false;
                var totalFolders = 0;
                var totalMatched = 0;
                foreach (var root in roots)
                {
                    if (!Directory.Exists(root))
                    {
                        Console.Error.WriteLine($"MISSING ROOT | {root}");
                        failed = true;
                        continue;
                    }

                    var folders = Directory.GetDirectories(root)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(Snapshot)
                        .ToArray();
                    var diagnostics = new List<string>();
                    var matches = ManagedAndroidGameImporter.Scan(
                        folders, recipes, profiles, diagnostics.Add);
                    totalFolders += folders.Length;
                    totalMatched += matches.Count;
                    var matchedPaths = matches.Select(match => match.FolderPath)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    Console.WriteLine(
                        $"{Path.GetFileName(root)}: {matches.Count}/{folders.Length} folders matched");
                    foreach (var match in matches.OrderBy(
                            value => value.FolderPath,
                                 StringComparer.OrdinalIgnoreCase))
                    {
                        var recipe = recipes.Single(value => string.Equals(
                            value.RecipeId, match.RecipeId,
                            StringComparison.OrdinalIgnoreCase));
                        var folderName = Path.GetFileName(match.FolderPath);
                        if (!HasTitleHint(folderName, recipe.Import.FolderNameHints))
                        {
                            Console.Error.WriteLine(
                                $"  FAIL | {folderName} | {match.ProfileName} has no title-specific hint");
                            failed = true;
                        }
                        var relativeExecutable = match.GameExecutablePath[
                            (match.FolderPath.Length + 1)..];
                        Console.WriteLine(
                            $"  PASS | {Path.GetFileName(match.FolderPath)} | " +
                            $"{match.ProfileName} | {relativeExecutable}");
                    }

                    foreach (var folder in folders.Where(folder =>
                                 !matchedPaths.Contains(folder.FullPath)))
                    {
                        Console.Error.WriteLine($"  FAIL | {folder.Name} | no recipe match");
                        failed = true;
                    }
                    if (matches.Count != folders.Length)
                    {
                        foreach (var diagnostic in diagnostics.Take(40))
                            Console.Error.WriteLine("    " + diagnostic);
                        failed = true;
                    }
                }

                Console.WriteLine($"Android batch coverage: {totalMatched}/{totalFolders}");
                return failed ? 1 : 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android batch coverage test failed: " + error.Message);
                return 1;
            }
        }

        private static bool HasTitleHint(string folderName, IReadOnlyList<string> hints)
        {
            var metadataStart = folderName.IndexOfAny(new[] { '(', '[' });
            var title = metadataStart < 0 ? folderName : folderName[..metadataStart];
            var normalizedTitle = NormalizeIdentity(title);
            return hints.Any(hint =>
            {
                var normalizedHint = NormalizeIdentity(hint);
                return normalizedHint.Length >= 4 &&
                       (normalizedTitle.Contains(normalizedHint,
                            StringComparison.Ordinal) ||
                        normalizedHint.Contains(normalizedTitle,
                            StringComparison.Ordinal));
            });
        }

        private static string NormalizeIdentity(string value) =>
            new(value.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());

        private static string[] GetConfiguredRoots()
        {
            var arcadeRoot = Environment.GetEnvironmentVariable(
                "TEKNOPARROT_ARCADE_ROOT");
            if (string.IsNullOrWhiteSpace(arcadeRoot))
                return Array.Empty<string>();
            return DefaultRelativeRoots
                .Select(relative => Path.Combine(arcadeRoot, relative))
                .Where(Directory.Exists)
                .ToArray();
        }

        private static AndroidGameFolderSnapshot Snapshot(string directory)
        {
            var androidRoot = "/storage/emulated/0/Download/TeknoParrotGames/" +
                              Path.GetFileName(directory);
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
                    return new AndroidGameFileSnapshot(relative, androidRoot + "/" + relative);
                })
                .ToArray();
            return new AndroidGameFolderSnapshot(
                Path.GetFileName(directory), androidRoot, files);
        }

        private static string FindRepositoryRoot()
        {
            var directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory, "TeknoParrotUi.Common")) &&
                    File.Exists(Path.Combine(directory, "TeknoParrotUI.sln")))
                    return directory;
                directory = Path.GetDirectoryName(directory);
            }
            throw new DirectoryNotFoundException("TeknoParrotUI repository root was not found.");
        }
    }
}
