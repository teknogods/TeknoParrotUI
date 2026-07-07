using System;
using System.IO;
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

    /// <summary>
    /// Returns the local path of the profile's icon, downloading it if allowed and
    /// missing. Returns null when unavailable.
    /// </summary>
    public static async Task<string?> EnsureIconAsync(GameProfile profile)
    {
        var iconName = profile.IconName;
        if (string.IsNullOrWhiteSpace(iconName))
            return null;

        var localPath = Path.GetFullPath(iconName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
            return localPath;

        if (!Lazydata.ParrotData.DownloadIcons)
            return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            var data = await Client.GetByteArrayAsync(IconBaseUrl + iconName.Replace('\\', '/'));
            await File.WriteAllBytesAsync(localPath, data);
            return localPath;
        }
        catch
        {
            // 404 / offline — caller shows default
            return null;
        }
    }
}
