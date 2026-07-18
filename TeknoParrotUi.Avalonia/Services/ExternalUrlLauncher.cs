using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace TeknoParrotUi.Avalonia.Services;

internal static class ExternalUrlLauncher
{
    public static async Task<bool> OpenAsync(Control owner, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return false;

        try
        {
            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel != null)
                return await topLevel.Launcher.LaunchUriAsync(uri);

            // This fallback is useful while a desktop view is still attaching.
            // Android should always have a TopLevel and intentionally has no
            // process-based fallback.
            if (OperatingSystem.IsLinux())
                return Process.Start(new ProcessStartInfo("xdg-open", uri.ToString())
                {
                    UseShellExecute = false
                }) != null;
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                return Process.Start(new ProcessStartInfo(uri.ToString())
                {
                    UseShellExecute = true
                }) != null;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"Could not open external URL: {error.Message}");
        }
        return false;
    }
}
