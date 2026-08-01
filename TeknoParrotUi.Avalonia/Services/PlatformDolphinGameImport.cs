using System;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

public static class PlatformDolphinGameImport
{
    public static Func<string, Task<bool>>? AndroidImporter { private get; set; }
    public static bool IsAvailable => AndroidImporter != null;

    public static Task<bool> ImportAsync(string fileName) =>
        AndroidImporter?.Invoke(fileName) ??
        throw new PlatformNotSupportedException(
            "The Android TeknoDolphin game importer is not registered.");
}
