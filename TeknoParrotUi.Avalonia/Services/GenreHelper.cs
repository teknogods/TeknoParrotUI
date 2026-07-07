using System.Collections.Generic;
using System.Linq;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// The standard TeknoParrot category list and matching rules, ported from the
/// classic GenreTranslationHelper (English display names for now).
/// </summary>
public static class GenreHelper
{
    public static List<string> GetGenres(bool includeNotInstalled = false)
    {
        var genres = new List<string>
        {
            "All", "Installed", "Subscription", "System 246/256", "System 357/359/369", "Triforce",
            "Action", "Card", "Compilation", "Fighting", "Flying",
            "Platform", "Puzzle", "Racing", "Rhythm", "Shoot 'Em Up",
            "Shooter", "Sports"
        };
        if (includeNotInstalled)
            genres.Insert(2, "Not Installed");
        return genres;
    }

    public static bool DoesGameMatchGenre(string genre, GameProfile gameProfile)
    {
        var gameGenre = gameProfile.GameInfo?.game_genre ?? gameProfile.GameGenreInternal ?? "Unknown";

        switch (genre)
        {
            case null:
            case "All":
                return true;
            case "Subscription":
                return gameProfile.Patreon;
            case "Installed":
                return GameProfileLoader.UserProfiles.Any(p => p.ProfileName == gameProfile.ProfileName);
            case "Not Installed":
                return !GameProfileLoader.UserProfiles.Any(p => p.ProfileName == gameProfile.ProfileName);
            case "Triforce":
                return gameProfile.EmulatorType == EmulatorType.Dolphin;
            case "System 246/256":
                return gameProfile.EmulatorType == EmulatorType.Play;
            case "System 357/359/369":
                return gameProfile.EmulatorType == EmulatorType.RPCS3;
            default:
                return genre.Equals(gameGenre, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
