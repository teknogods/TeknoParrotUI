using System;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Platform bridge for importing one user-selected System 246/256 package into
/// the Android companion's scoped storage.
/// </summary>
public static class PlatformPcsx2x6GameImport
{
    public static Func<string, Task<bool>>? AndroidImporter { private get; set; }

    public static bool IsAvailable =>
        OperatingSystem.IsAndroid() && AndroidImporter != null;

    public static Task<bool> ImportAsync(string manifestName)
    {
        var importer = AndroidImporter
            ?? throw new PlatformNotSupportedException(
                "The Android Tekno2x6 game importer is not registered.");
        return importer(manifestName);
    }
}
