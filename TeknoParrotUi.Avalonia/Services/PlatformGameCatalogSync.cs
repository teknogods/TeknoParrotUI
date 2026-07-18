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
    private static int _refreshInFlight;
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
        var refresh = RefreshAsync;
        if (refresh == null || Interlocked.Exchange(ref _refreshInFlight, 1) != 0)
            return;

        _ = RunRefreshAsync(refresh);
    }

    private static async Task RunRefreshAsync(Func<Task<int>> refresh)
    {
        try
        {
            await refresh().ConfigureAwait(false);
        }
        catch
        {
            // Catalog discovery is opportunistic. Add Game remains available
            // when a companion is absent or not responding.
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }
}
