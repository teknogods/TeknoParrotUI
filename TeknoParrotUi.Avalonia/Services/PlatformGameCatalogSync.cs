using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Optional platform hook for discovering installed games which are managed by
/// a companion application, including persisted user-selected documents.
/// </summary>
public static class PlatformGameCatalogSync
{
    private static readonly System.Threading.SemaphoreSlim RefreshGate = new(1, 1);
    private static int _backgroundRefreshInFlight;
    private static readonly object ReadySync = new();
    private static HashSet<string> _readyExecutables =
        new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _readyProfileNames =
        new(StringComparer.OrdinalIgnoreCase);

    public static Func<Task<int>>? RefreshAsync { private get; set; }

    public static event Action<int>? CatalogUpdated;

    public static IReadOnlyCollection<string> ReadyExecutables
    {
        get
        {
            lock (ReadySync)
                return _readyExecutables.ToArray();
        }
    }

    public static IReadOnlyCollection<string> ReadyProfileNames
    {
        get
        {
            lock (ReadySync)
                return _readyProfileNames.ToArray();
        }
    }

    public static void PublishReadyExecutables(IEnumerable<string> executableNames)
        => PublishReadyGames(executableNames, Array.Empty<string>());

    public static void PublishReadyGames(
        IEnumerable<string> executableNames,
        IEnumerable<string> profileNames)
    {
        var ready = executableNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var readyProfiles = profileNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        lock (ReadySync)
        {
            changed = !_readyExecutables.SetEquals(ready) ||
                !_readyProfileNames.SetEquals(readyProfiles);
            _readyExecutables = ready;
            _readyProfileNames = readyProfiles;
        }
        if (changed)
            CatalogUpdated?.Invoke(ready.Count + readyProfiles.Count);
    }

    public static void RequestRefresh()
    {
        if (Interlocked.Exchange(ref _backgroundRefreshInFlight, 1) != 0)
            return;
        _ = RunBackgroundRefreshAsync();
    }

    private static async Task RunBackgroundRefreshAsync()
    {
        try
        {
            await RefreshNowAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _backgroundRefreshInFlight, 0);
        }
    }

    public static async Task<int> RefreshNowAsync()
    {
        var refresh = RefreshAsync;
        if (refresh == null)
            return ReadyExecutables.Count;

        await RefreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await refresh().ConfigureAwait(false);
        }
        catch
        {
            // Catalog discovery is opportunistic. Add Game remains available
            // when a companion is absent or not responding. Clear stale
            // app-scoped results: an uninstall/reinstall creates an empty
            // companion store even while this TPUI process stays alive.
            PublishReadyGames(Array.Empty<string>(), Array.Empty<string>());
            return 0;
        }
        finally
        {
            RefreshGate.Release();
        }
    }
}
