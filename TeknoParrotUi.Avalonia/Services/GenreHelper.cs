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

    /// <summary>Install/subscription status filters (library section).</summary>
    public static List<string> GetStatusFilters(bool includeNotInstalled = false)
    {
        var statuses = new List<string> { "Installed", "Subscription" };
        if (includeNotInstalled)
            statuses.Insert(1, "Not Installed");
        return statuses;
    }

    /// <summary>The genre of a profile as declared in its metadata JSON.</summary>
    public static string GenreName(GameProfile p) =>
        p.GameInfo?.game_genre is { Length: > 0 } genre ? genre
        : p.GameGenreInternal is { Length: > 0 } internalGenre ? internalGenre
        : "Unknown";

    /// <summary>
    /// Distinct genres taken from the given profiles' metadata JSON files
    /// (nothing hardcoded) — every entry is guaranteed to match a game.
    /// </summary>
    public static List<string> GetGenreNames(IEnumerable<GameProfile> profiles) =>
        profiles
            .Select(GenreName)
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Flat single-select filter list (Add Game view): All + statuses + metadata genres.</summary>
    public static List<string> GetGenres(IEnumerable<GameProfile> profiles, bool includeNotInstalled = false)
    {
        var genres = new List<string> { "All" };
        genres.AddRange(GetStatusFilters(includeNotInstalled));
        genres.AddRange(GetGenreNames(profiles));
        return genres;
    }

    public static bool DoesGameMatchGenre(string genre, GameProfile gameProfile)
    {
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
            default:
                return genre.Equals(GenreName(gameProfile), System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
