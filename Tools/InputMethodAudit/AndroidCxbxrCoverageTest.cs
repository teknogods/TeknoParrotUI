using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;

namespace InputMethodAudit
{
    internal static class AndroidCxbxrCoverageTest
    {
        private sealed record ExpectedTitle(
            string Folder,
            string Profile,
            string Executable,
            string RuntimeDirectory,
            string InputProtocol,
            int FrameRateLimit);

        private static readonly ExpectedTitle[] ExpectedTitles =
        {
            new("hod33", "HOTD3", "hod3xb.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGun, 0),
            new("mt1e", "WMMT1", "V307.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt, 60),
            new("mt1j", "WMMT1J", "V307.xbe", "cxbxr-japan",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt, 60),
            new("mt2e", "WMMT2", "V322.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt, 60),
            new("mt2j", "WMMT2j", "V322.xbe", "cxbxr-japan",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt, 60),
            new("ollie", "OllieKing", "OllieKing.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrOllie, 60),
            new("or2", "or2", "outrun2.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun, 60),
            new("or2b", "or2b", "outrun2.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun, 60),
            new("or2sp", "or2sp", "outrun2.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun, 60),
            new("vc3", "vc3", "vc3.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGun, 0),
            new("Gundam Battle Operating Simulator", "GBOS", "gs.xbe", "cxbxr-japan",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGundam, 0),
            new("taxi", "CTHR", "ctx_ac[r].xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrDriving, 60),
            new("Sega Golf Club Network Pro Tour 2005 (Rev C)", "SGC05",
                "golf.xbe", "cxbxr-japan",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf, 0),
            new("Sega Golf Club Next Tours 2006 (Rev.A)", "SGC06",
                "golf.xbe", "cxbxr-japan",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf, 0),
            new("gs", "GhostSquad", "vsg.xbe", "cxbxr-export",
                AndroidLaunchRecipe.InputProtocolSharedCxbxrGun, 0)
        };

        private static readonly string[] DeferredTitleFolders =
        {
            "Quest of D (CDV-10005C)",
            "Quest of D Oukoku no Syugosya Ver. 3.02",
            "Quest of D The Battle Kingdom (CDV-10035B)",
            "Sega Network Taisen Mahjong MJ2 (Japan) (Rev C)",
            "Sega Network Taisen Mahjong MJ3 (Japan) (Rev D)",
            "Sega Network Taisen Mahjong MJ3 Evolution (Japan) (Rev B)"
        };

        private static readonly string[] InfrastructureFolders =
        {
            "EmuDisk",
            "EmuMediaBoard",
            "EmuMediaBoard_",
            "EmuMu",
            "factorycheck",
            "hlsl",
            "SymbolCache"
        };

        private static readonly string[] DuplicateTitleFolders =
        {
            // The XBE is byte-identical to the fully named SGC06 dump.
            "golf"
        };

        public static int Run()
        {
            try
            {
                var arcadeRoot = Environment.GetEnvironmentVariable(
                    "TEKNOPARROT_ARCADE_ROOT");
                if (string.IsNullOrWhiteSpace(arcadeRoot))
                {
                    Console.WriteLine(
                        "Android CXBXR dump coverage: SKIP " +
                        "(TEKNOPARROT_ARCADE_ROOT is not configured)");
                    return 0;
                }
                var dumpRoot = Path.Combine(arcadeRoot, "cxbx");
                if (!Directory.Exists(dumpRoot))
                {
                    Console.WriteLine(
                        "Android CXBXR dump coverage: SKIP (local CXBXR dump root unavailable)");
                    return 0;
                }

                ValidateRootInventory(dumpRoot);

                var repositoryRoot = FindRepositoryRoot();
                var recipeDirectory = Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Common",
                    AndroidLaunchRecipeCatalog.DirectoryName);
                var recipes = AndroidLaunchRecipeCatalog.LoadAll(recipeDirectory);
                var profiles = recipes.Select(recipe => new GameProfile
                {
                    ProfileName = recipe.ProfileName,
                    GameNameInternal = recipe.ProfileName
                }).ToArray();
                var diagnostics = new List<string>();
                var snapshots = ExpectedTitles.Select(expected =>
                    Snapshot(Path.Combine(dumpRoot, expected.Folder))).ToArray();
                var matches = ManagedAndroidGameImporter.Scan(
                    snapshots, recipes, profiles, diagnostics.Add);

                foreach (var expected in ExpectedTitles)
                {
                    var folderPath =
                        "/storage/emulated/0/Download/TeknoParrotGames/" +
                        expected.Folder;
                    var match = matches.SingleOrDefault(value =>
                        string.Equals(
                            value.FolderPath, folderPath,
                            StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        throw new InvalidOperationException(
                            $"CXBXR folder '{expected.Folder}' did not match a launch recipe.");
                    if (!string.Equals(
                            match.ProfileName, expected.Profile,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"CXBXR folder '{expected.Folder}' matched '{match.ProfileName}', " +
                            $"expected '{expected.Profile}'.");

                    var relativeExecutable = match.GameExecutablePath[
                        (match.FolderPath.TrimEnd('/').Length + 1)..];
                    if (!string.Equals(
                            relativeExecutable, expected.Executable,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"CXBXR folder '{expected.Folder}' selected " +
                            $"'{relativeExecutable}', expected the root game XBE " +
                            $"'{expected.Executable}'.");

                    var recipe = recipes.Single(value => string.Equals(
                        value.RecipeId, match.RecipeId,
                        StringComparison.OrdinalIgnoreCase));
                    if (!string.Equals(
                            recipe.WorkingDirectory, expected.RuntimeDirectory,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            recipe.InputProtocol, expected.InputProtocol,
                            StringComparison.Ordinal) ||
                        recipe.FrameRateLimit != expected.FrameRateLimit)
                        throw new InvalidOperationException(
                            $"CXBXR profile '{expected.Profile}' changed its region runtime " +
                            "shared-page protocol, or frame-rate policy.");
                    var expectsCardService = string.Equals(
                        expected.InputProtocol,
                        AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                        StringComparison.Ordinal);
                    if (expectsCardService != string.Equals(
                            recipe.CompatibilityPreset,
                            AndroidLaunchRecipe.CompatibilityPresetCxbxrWmmtYaCard,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"CXBXR profile '{expected.Profile}' has an invalid " +
                            "WMMT1/2 YACardEmu lifecycle preset.");

                    Console.WriteLine(
                        $"  PASS | {expected.Folder} | {expected.Profile} | " +
                        $"{relativeExecutable} | {expected.RuntimeDirectory}");
                }

                if (matches.Count != ExpectedTitles.Length)
                    throw new InvalidOperationException(
                        $"CXBXR coverage returned {matches.Count} matches for " +
                        $"{ExpectedTitles.Length} requested title folders.");

                Console.WriteLine(
                    $"Android CXBXR dump coverage: PASS ({matches.Count}/{ExpectedTitles.Length})");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(
                    "Android CXBXR dump coverage failed: " + error.Message);
                return 1;
            }
        }

        private static void ValidateRootInventory(string dumpRoot)
        {
            var supported = ExpectedTitles.Select(value => value.Folder);
            var classified = supported
                .Concat(DeferredTitleFolders)
                .Concat(InfrastructureFolders)
                .Concat(DuplicateTitleFolders)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedCount = ExpectedTitles.Length +
                                DeferredTitleFolders.Length +
                                InfrastructureFolders.Length +
                                DuplicateTitleFolders.Length;
            if (classified.Count != expectedCount)
                throw new InvalidOperationException(
                    "CXBXR inventory classifications overlap.");

            var actual = Directory.GetDirectories(dumpRoot)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unclassified = actual.Except(classified)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var missing = classified.Except(actual)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unclassified.Length != 0 || missing.Length != 0)
                throw new InvalidOperationException(
                    "CXBXR root inventory drifted. Unclassified: " +
                    (unclassified.Length == 0
                        ? "(none)"
                        : string.Join(", ", unclassified)) +
                    "; missing: " +
                    (missing.Length == 0 ? "(none)" : string.Join(", ", missing)) +
                    ".");

            Console.WriteLine(
                $"CXBXR root inventory: {ExpectedTitles.Length} supported, " +
                $"{DeferredTitleFolders.Length} deferred titles, " +
                $"{InfrastructureFolders.Length} infrastructure folders, " +
                $"{DuplicateTitleFolders.Length} duplicate title folder");
        }

        private static AndroidGameFolderSnapshot Snapshot(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(
                    "CXBXR dump folder was not found: " + directory);
            var androidRoot = "/storage/emulated/0/Download/TeknoParrotGames/" +
                              Path.GetFileName(directory);
            var files = Directory.EnumerateFiles(
                    directory, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(directory, path)
                        .Replace('\\', '/');
                    return new AndroidGameFileSnapshot(
                        relative, androidRoot + "/" + relative);
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
            throw new DirectoryNotFoundException(
                "TeknoParrotUI repository root was not found.");
        }
    }
}
