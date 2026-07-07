using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Scans a romset folder (traditional layout: {romDir}/{gameId}/...) for installable
    /// games using the GameSetup definitions, and bulk-configures user profiles.
    /// UI-agnostic — used by both frontends.
    /// </summary>
    public static class GameScannerCore
    {
        public class FoundGame
        {
            public string GameId { get; set; }
            public string DisplayName { get; set; }
            public GameSetup Setup { get; set; }
        }

        /// <summary>
        /// Finds games in <paramref name="scanDir"/> whose executables (per GameSetup)
        /// exist under {scanDir}/{gameId}/.
        /// </summary>
        public static List<FoundGame> ScanRomFolder(string scanDir, Action<string> log = null)
        {
            var found = new List<FoundGame>();
            if (!Directory.Exists(scanDir) || !Directory.Exists("GameSetup"))
                return found;

            foreach (var gameSetupFile in Directory.GetFiles("GameSetup", "*.xml"))
            {
                var gameSetup = JoystickHelper.DeSerializeGameSetup(gameSetupFile);
                if (gameSetup == null)
                    continue;

                var gameId = Path.GetFileNameWithoutExtension(gameSetupFile);

                bool foundExe = string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation) ||
                                File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameExecutableLocation));
                bool foundExe2 = string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation2) ||
                                 File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameExecutableLocation2));
                bool foundTest = string.IsNullOrWhiteSpace(gameSetup.GameTestExecutableLocation) ||
                                 File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameTestExecutableLocation));

                if (foundExe && foundExe2 && foundTest)
                {
                    var metadata = JoystickHelper.DeSerializeMetadata(gameId);
                    var displayName = metadata != null ? $"{metadata.game_name} ({metadata.platform})" : gameId;
                    found.Add(new FoundGame { GameId = gameId, DisplayName = displayName, Setup = gameSetup });
                    log?.Invoke($"Found: {displayName}");
                }
            }

            return found;
        }

        /// <summary>
        /// Creates user profiles with game paths set for all found games that are not
        /// already configured. Returns the number of games added.
        /// </summary>
        public static int ConfigureFoundGames(List<FoundGame> foundGames, string romDir, Action<string> log = null)
        {
            Directory.CreateDirectory("UserProfiles");
            var existing = Directory.GetFiles("UserProfiles", "*.xml")
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (var game in foundGames)
            {
                if (existing.Contains(game.GameId))
                {
                    log?.Invoke($"Skipped (already configured): {game.GameId}");
                    continue;
                }

                var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("GameProfiles", game.GameId + ".xml"), false);
                if (profile == null)
                {
                    log?.Invoke($"Skipped (no game profile): {game.GameId}");
                    continue;
                }

                var gameDir = Path.Combine(romDir, game.GameId);
                if (!Directory.Exists(gameDir))
                {
                    log?.Invoke($"Skipped (directory not found): {game.GameId}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(game.Setup.GameExecutableLocation))
                {
                    var exePath = Path.Combine(gameDir, game.Setup.GameExecutableLocation);
                    if (File.Exists(exePath))
                        profile.GamePath = exePath;
                    else
                        log?.Invoke($"Warning: executable not found: {exePath}");
                }

                if (!string.IsNullOrWhiteSpace(game.Setup.GameExecutableLocation2))
                {
                    var exe2Path = Path.Combine(gameDir, game.Setup.GameExecutableLocation2);
                    if (File.Exists(exe2Path))
                        profile.GamePath2 = exe2Path;
                    else
                        log?.Invoke($"Warning: second executable not found: {exe2Path}");
                }

                JoystickHelper.SerializeGameProfile(profile);
                log?.Invoke($"Configured: {game.GameId}");
                added++;
            }

            return added;
        }
    }
}
