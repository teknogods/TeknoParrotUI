using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TeknoParrotUi.Common.Android
{
    public sealed record AndroidGameFileSnapshot(string RelativePath, string FullPath);

    public sealed record AndroidGameFolderSnapshot(
        string Name,
        string FullPath,
        IReadOnlyList<AndroidGameFileSnapshot> Files);

    public sealed record ManagedAndroidGame(
        string ProfileName,
        string DisplayName,
        string RecipeId,
        string FolderPath,
        string GameExecutablePath);

    public sealed record ManagedAndroidImportResult(
        int Added,
        int Updated,
        int Unchanged,
        int Conflicts,
        int Failed)
    {
        public int Changed => Added + Updated;
    }

    /// <summary>
    /// Deterministic matcher/configurator for the Android managed-import flow.
    /// Storage enumeration stays in the UI because Android may expose a SAF
    /// tree instead of a normal directory; matching and profile updates remain
    /// platform-neutral and directly testable.
    /// </summary>
    public static class ManagedAndroidGameImporter
    {
        public static IReadOnlyList<string> GetCandidateExecutablePaths(
            string folderName,
            IEnumerable<AndroidLaunchRecipe> recipes)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return Array.Empty<string>();

            return (recipes ?? Array.Empty<AndroidLaunchRecipe>())
                .Where(recipe => recipe != null && recipe.Validated &&
                    ScoreFolderName(folderName, recipe.Import.FolderNameHints) > 0)
                .SelectMany(recipe => recipe.Import.ExecutableCandidates)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<ManagedAndroidGame> Scan(
            IEnumerable<AndroidGameFolderSnapshot> folders,
            IEnumerable<AndroidLaunchRecipe> recipes,
            IEnumerable<GameProfile> profiles,
            Action<string> log = null)
        {
            var profileMap = (profiles ?? Array.Empty<GameProfile>())
                .Where(profile => !string.IsNullOrWhiteSpace(profile?.ProfileName))
                .GroupBy(profile => profile.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var validatedRecipes = (recipes ?? Array.Empty<AndroidLaunchRecipe>())
                .Where(recipe => recipe != null && recipe.Validated)
                .ToArray();
            foreach (var recipe in validatedRecipes)
                recipe.Validate();

            var matches = new List<(ManagedAndroidGame Game, int Score)>();
            foreach (var folder in folders ?? Array.Empty<AndroidGameFolderSnapshot>())
            {
                if (folder == null || string.IsNullOrWhiteSpace(folder.Name) || folder.Files == null)
                    continue;

                var candidates = new List<(AndroidLaunchRecipe Recipe, AndroidGameFileSnapshot File, int Score)>();
                foreach (var recipe in validatedRecipes)
                {
                    var hintScore = ScoreFolderName(folder.Name, recipe.Import.FolderNameHints);
                    if (hintScore == 0)
                        continue;
                    var executable = FindExecutable(folder.Files, recipe.Import.ExecutableCandidates);
                    if (executable == null || !IsWinlatorSharedGamePath(executable.FullPath))
                        continue;
                    candidates.Add((recipe, executable, hintScore));
                }

                if (candidates.Count == 0)
                    continue;
                var bestScore = candidates.Max(candidate => candidate.Score);
                var best = candidates.Where(candidate => candidate.Score == bestScore).ToArray();
                if (best.Length != 1)
                {
                    log?.Invoke($"Skipped ambiguous Android game folder: {folder.Name}");
                    continue;
                }

                var selected = best[0];
                if (!profileMap.TryGetValue(selected.Recipe.ProfileName, out var profile))
                {
                    log?.Invoke($"Skipped {folder.Name}: profile {selected.Recipe.ProfileName} is missing.");
                    continue;
                }
                var displayName = string.IsNullOrWhiteSpace(profile.GameNameInternal)
                    ? profile.ProfileName
                    : profile.GameNameInternal;
                matches.Add((new ManagedAndroidGame(
                    profile.ProfileName,
                    displayName,
                    selected.Recipe.RecipeId,
                    folder.FullPath,
                    selected.File.FullPath), selected.Score));
            }

            var result = new List<ManagedAndroidGame>();
            foreach (var group in matches.GroupBy(match =>
                         match.Game.ProfileName, StringComparer.OrdinalIgnoreCase))
            {
                var highest = group.Max(match => match.Score);
                var best = group.Where(match => match.Score == highest).ToArray();
                if (best.Length != 1)
                {
                    log?.Invoke(
                        $"Skipped {group.Key}: more than one Android game folder matched equally well.");
                    continue;
                }
                result.Add(best[0].Game);
                log?.Invoke($"Found Android game: {best[0].Game.DisplayName} — {best[0].Game.GameExecutablePath}");
            }
            return result.OrderBy(game => game.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static ManagedAndroidImportResult ConfigureFoundGames(
            IEnumerable<ManagedAndroidGame> games,
            string stockProfilesDirectory = "GameProfiles",
            string userProfilesDirectory = "UserProfiles",
            Action<string> log = null)
        {
            Directory.CreateDirectory(userProfilesDirectory);
            var added = 0;
            var updated = 0;
            var unchanged = 0;
            var conflicts = 0;
            var failed = 0;

            foreach (var game in games ?? Array.Empty<ManagedAndroidGame>())
            {
                var stockPath = Path.Combine(stockProfilesDirectory, game.ProfileName + ".xml");
                var userPath = Path.Combine(userProfilesDirectory, game.ProfileName + ".xml");
                var alreadyExists = File.Exists(userPath);
                var profile = JoystickHelper.DeSerializeGameProfile(
                    alreadyExists ? userPath : stockPath, alreadyExists);
                if (profile == null)
                {
                    failed++;
                    log?.Invoke($"Skipped {game.ProfileName}: its game profile could not be loaded.");
                    continue;
                }

                if (alreadyExists && !string.IsNullOrWhiteSpace(profile.GamePath))
                {
                    if (PathsEqual(profile.GamePath, game.GameExecutablePath))
                    {
                        unchanged++;
                        log?.Invoke($"Already configured: {game.DisplayName}");
                    }
                    else
                    {
                        conflicts++;
                        log?.Invoke(
                            $"Kept existing path for {game.DisplayName}; change it in Game Settings to use {game.GameExecutablePath}");
                    }
                    continue;
                }

                profile.GamePath = game.GameExecutablePath;
                try
                {
                    JoystickHelper.SerializeGameProfile(profile, userPath);
                    if (alreadyExists)
                    {
                        updated++;
                        log?.Invoke($"Completed existing profile: {game.DisplayName}");
                    }
                    else
                    {
                        added++;
                        log?.Invoke($"Imported: {game.DisplayName}");
                    }
                }
                catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    failed++;
                    log?.Invoke($"Could not import {game.DisplayName}: {error.Message}");
                }
            }

            return new ManagedAndroidImportResult(added, updated, unchanged, conflicts, failed);
        }

        public static bool IsWinlatorSharedGamePath(string path)
        {
            return AndroidWinlatorGamePath.IsAllowedSharedPath(
                path,
                "/storage/emulated/0/Download");
        }

        // Retain the public entry point used by older tooling while applying
        // the same restricted Downloads-or-Games-library policy.
        public static bool IsWinlatorDownloadPath(string path) =>
            IsWinlatorSharedGamePath(path);

        private static AndroidGameFileSnapshot FindExecutable(
            IReadOnlyList<AndroidGameFileSnapshot> files,
            IReadOnlyList<string> executableCandidates)
        {
            foreach (var candidate in executableCandidates)
            {
                var normalizedCandidate = NormalizeRelativePath(candidate);
                var exact = files.Where(file => string.Equals(
                    NormalizeRelativePath(file.RelativePath), normalizedCandidate,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                if (exact.Length == 1)
                    return exact[0];
                if (exact.Length > 1)
                    return null;

                if (normalizedCandidate.Contains('/'))
                    continue;
                var byName = files.Where(file => string.Equals(
                    Path.GetFileName(NormalizeRelativePath(file.RelativePath)), normalizedCandidate,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                if (byName.Length == 1)
                    return byName[0];
                if (byName.Length > 1)
                    return null;
            }
            return null;
        }

        private static int ScoreFolderName(string folderName, IReadOnlyList<string> hints)
        {
            var normalizedFolder = NormalizeIdentity(folderName);
            var score = 0;
            var scoredHints = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hint in hints)
            {
                var normalizedHint = NormalizeIdentity(hint);
                // Formatting-only aliases such as "MachStorm" / "Mach Storm"
                // normalize to the same identity and must not receive duplicate
                // weight in recipe selection.
                if (normalizedHint.Length == 0 || !scoredHints.Add(normalizedHint) ||
                    !normalizedFolder.Contains(
                        normalizedHint, StringComparison.Ordinal))
                    continue;
                // Dump folders normally begin with the game title and end with
                // version/platform metadata. Prefer a title match decisively so a
                // generic platform hint (for example, "NESiCAxLive") can never
                // steal a folder from the game's own recipe.
                var titleBonus = normalizedFolder.StartsWith(
                    normalizedHint, StringComparison.Ordinal) ? 1000 : 0;
                score += 100 + normalizedHint.Length + titleBonus;
            }
            return score;
        }

        private static string NormalizeIdentity(string value)
        {
            var result = new StringBuilder(value?.Length ?? 0);
            foreach (var character in value ?? "")
            {
                if (char.IsLetterOrDigit(character))
                    result.Append(char.ToLowerInvariant(character));
            }
            return result.ToString();
        }

        private static string NormalizeRelativePath(string value) =>
            (value ?? "").Replace('\\', '/').TrimStart('/');

        private static bool PathsEqual(string left, string right) =>
            string.Equals(
                left?.Replace('\\', '/').TrimEnd('/'),
                right?.Replace('\\', '/').TrimEnd('/'),
                StringComparison.Ordinal);
    }
}
