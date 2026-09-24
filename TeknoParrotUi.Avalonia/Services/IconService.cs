using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// On-demand game icon download from the TeknoParrotUIThumbnails repository,
/// matching the classic UI behaviour (honors the DownloadIcons setting,
/// 5 second timeout, corrupt files deleted by callers).
/// </summary>
public static class IconService
{
    private const string IconBaseUrl = "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<string, byte> MissingUrls = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Task<string?>> Pending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the local path of the profile's icon, downloading it if allowed and
    /// missing. Returns null when unavailable.
    /// </summary>
    public static async Task<string?> EnsureIconAsync(GameProfile profile)
    {
        var iconName = profile.IconName;
        if (string.IsNullOrWhiteSpace(iconName))
            return null;

        var icon = await EnsureOneAsync(iconName);
        if (icon != null)
            return icon;

        var placeholder = profile.EmulatorType switch
        {
            EmulatorType.TeknoVegas => "Icons/TeknoVegas.png",
            EmulatorType.TeknoViper => "Icons/TeknoViper.png",
            _ => null
        };
        return placeholder == null || string.Equals(iconName, placeholder, StringComparison.OrdinalIgnoreCase)
            ? null : await EnsureOneAsync(placeholder);
    }

    private static async Task<string?> EnsureOneAsync(string iconName)
    {
        var task = Pending.GetOrAdd(iconName, DownloadIconAsync);
        try { return await task; }
        finally { Pending.TryRemove(new System.Collections.Generic.KeyValuePair<string, Task<string?>>(iconName, task)); }
    }

    private static async Task<string?> DownloadIconAsync(string iconName)
    {
        var url = IconBaseUrl + iconName.Replace('\\', '/');

        var localPath = Path.GetFullPath(iconName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
            return localPath;

        if (!Lazydata.ParrotData.DownloadIcons || MissingUrls.ContainsKey(url))
            return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            using var response = await Client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                MissingUrls.TryAdd(url, 0);
                return null;
            }
            if (!response.IsSuccessStatusCode)
                return null;
            var data = await response.Content.ReadAsByteArrayAsync();
            var temporary = localPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, data);
                File.Move(temporary, localPath, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return localPath;
        }
        catch
        {
            // 404 / offline — caller shows default
            return null;
        }
    }
}
