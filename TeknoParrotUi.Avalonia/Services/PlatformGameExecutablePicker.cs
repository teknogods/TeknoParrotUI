using System;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Opens the platform game-executable picker. Android supplies a native
/// ACTION_OPEN_DOCUMENT implementation so the picker always starts at the
/// shared-storage root instead of inheriting a previously selected library
/// folder as an apparent navigation boundary.
/// </summary>
public static class PlatformGameExecutablePicker
{
    public static Func<string, Task<string?>>? AndroidPicker { private get; set; }

    public static bool IsAvailable =>
        OperatingSystem.IsAndroid() && AndroidPicker != null;

    public static Task<string?> PickAsync(string title)
    {
        if (!OperatingSystem.IsAndroid() || AndroidPicker == null)
            throw new PlatformNotSupportedException(
                "The Android game-executable picker is not available.");
        return AndroidPicker(title);
    }
}
