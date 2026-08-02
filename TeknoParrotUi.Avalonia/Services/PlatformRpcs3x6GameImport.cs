using System;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

public static class PlatformRpcs3x6GameImport
{
    public static Func<Task<bool>>? AndroidImporter { private get; set; }
    public static bool IsAvailable => AndroidImporter != null;
    public static Task<bool> ImportAsync() => AndroidImporter?.Invoke() ??
        throw new PlatformNotSupportedException("The Android RPCS3X6 arcade importer is not registered.");
}
