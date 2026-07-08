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
    /// <summary>Localized display name for a canonical genre/category (classic LibraryGenre* keys).</summary>
    public static string LocalizeGenre(string genre) => genre switch
    {
        "All" => Loc.T("LibraryGenreAll", genre),
        "Installed" => Loc.T("AddGameInstalledFilter", genre),
        "Not Installed" => Loc.T("AddGameNotInstalledFilter", genre),
        "Subscription" => Loc.T("LibraryGenreSubscription", genre),
        "Action" => Loc.T("LibraryGenreAction", genre),
        "Card" => Loc.T("LibraryGenreCard", genre),
        "Compilation" => Loc.T("LibraryGenreCompilation", genre),
        "Fighting" => Loc.T("LibraryGenreFighting", genre),
        "Flying" => Loc.T("LibraryGenreFlying", genre),
        "Platform" => Loc.T("LibraryGenrePlatform", genre),
        "Puzzle" => Loc.T("LibraryGenrePuzzle", genre),
        "Racing" => Loc.T("LibraryGenreRacing", genre),
        "Rhythm" => Loc.T("LibraryGenreRhythm", genre),
        "Shoot 'Em Up" => Loc.T("LibraryGenreShootEmUp", genre),
        "Shooter" => Loc.T("LibraryGenreShooter", genre),
        "Sports" => Loc.T("LibraryGenreSports", genre),
        _ => genre
    };

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
