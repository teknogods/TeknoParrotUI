using System;
using TeknoParrotUi.Common.Android;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Resolves a document-picker URI to a physical shared-storage path. Pure URI
/// forms are handled in shared code; opaque Android provider IDs are delegated
/// to the Android head and normalized again before they reach game settings.
/// </summary>
public static class PlatformDocumentPathResolver
{
    public static Func<string, string?>? AndroidResolver { private get; set; }

    public static bool TryResolve(string value, out string path)
    {
        if (AndroidDocumentPathResolver.TryResolve(value, out path))
            return true;

        path = string.Empty;
        if (!OperatingSystem.IsAndroid() || AndroidResolver == null)
            return false;

        string? candidate;
        try
        {
            candidate = AndroidResolver(value);
        }
        catch
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(candidate) &&
               AndroidDocumentPathResolver.TryNormalizeSharedPath(
                   candidate,
                   out path);
    }
}
