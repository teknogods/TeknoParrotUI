using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Optional platform hook for discovering installed games which live in a
/// companion application's private storage.
/// </summary>
public static class PlatformGameCatalogSync
{
    private static readonly System.Threading.SemaphoreSlim RefreshGate = new(1, 1);
    private static int _backgroundRefreshInFlight;
    private static readonly object ReadySync = new();
    private static HashSet<string> _readyExecutables =
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

    public static void PublishReadyExecutables(IEnumerable<string> executableNames)
    {
        var ready = executableNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (ReadySync)
            _readyExecutables = ready;
        CatalogUpdated?.Invoke(ready.Count);
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
            PublishReadyExecutables(Array.Empty<string>());
            return 0;
        }
        finally
        {
            RefreshGate.Release();
        }
    }
}
