using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Helpers
{
    public static class GenreTranslationHelper
    {
        private static readonly Dictionary<string, string> InternalToResourceMap = new Dictionary<string, string>
        {
            { "All", nameof(Resources.LibraryGenreAll) },
            { "Installed", nameof(Resources.AddGameInstalledFilter) },
            { "Not Installed", nameof(Resources.AddGameNotInstalledFilter) },
            { "Subscription", nameof(Resources.LibraryGenreSubscription) },
            { "Action", nameof(Resources.LibraryGenreAction) },
            { "Card", nameof(Resources.LibraryGenreCard) },
            { "Compilation", nameof(Resources.LibraryGenreCompilation) },
            { "Fighting", nameof(Resources.LibraryGenreFighting) },
            { "Flying", nameof(Resources.LibraryGenreFlying) },
            { "Platform", nameof(Resources.LibraryGenrePlatform) },
            { "Puzzle", nameof(Resources.LibraryGenrePuzzle) },
            { "Racing", nameof(Resources.LibraryGenreRacing) },
            { "Rhythm", nameof(Resources.LibraryGenreRhythm) },
            { "Shoot Em Up", nameof(Resources.LibraryGenreShootEmUp) },
            { "Shooter", nameof(Resources.LibraryGenreShooter) },
            { "Sports", nameof(Resources.LibraryGenreSports) }
        };
        
        public static List<GenreItem> GetGenreItems(bool includeNotInstalled = false)
        {
            var items = new List<GenreItem>();

            var orderedKeys = new List<string>
            {
                "All", "Installed", "Subscription",
                "Action", "Card", "Compilation", "Fighting", "Flying",
                "Platform", "Puzzle", "Racing", "Rhythm", "Shoot Em Up",
                "Shooter", "Sports"
            };

            if (includeNotInstalled)
            {
                orderedKeys.Insert(2, "Not Installed");
            }

            foreach (var key in orderedKeys)
            {
                if (InternalToResourceMap.ContainsKey(key))
                {
                    var localizedText = GetLocalizedString(InternalToResourceMap[key]);
                    items.Add(new GenreItem
                    {
                        InternalName = key,
                        DisplayName = localizedText
                    });
                }
            }

            return items;
        }

        private static string GetLocalizedString(string resourceName)
        {
            var property = typeof(Resources).GetProperty(resourceName);
            return property?.GetValue(null)?.ToString() ?? resourceName;
        }

        public static bool DoesGameMatchGenre(string internalGenreName, TeknoParrotUi.Common.GameProfile gameProfile)
        {
            string gameGenre = gameProfile.GameInfo?.game_genre ?? gameProfile.GameGenreInternal ?? "Unknown";
            Debug.WriteLine($"Game: {gameProfile.GameNameInternal} | GameGenre: {gameGenre} | Filter: {internalGenreName}");
            
            if (internalGenreName == "All")
                return true;
            
            if (internalGenreName == "Subscription")
                return gameProfile.Patreon;

            if (internalGenreName == "Installed")
            {
                var existing = TeknoParrotUi.Common.GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.ProfileName == gameProfile.ProfileName) != null;
                return existing;
            }
            
            if (internalGenreName == "Not Installed")
            {
                var existing = TeknoParrotUi.Common.GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.ProfileName == gameProfile.ProfileName) != null;
                return !existing;
            }
            
            bool matches = internalGenreName.Equals(gameGenre, System.StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"  -> Matches: {matches}");
            return matches;
        }
    }

    public class GenreItem
    {
        public string InternalName { get; set; }
        public string DisplayName { get; set; }
        
        public override string ToString() => DisplayName;
    }
}