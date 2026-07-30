using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Platform bridge for the Android PCSX2X6 companion's private BIOS storage.
/// The shared UI owns the launch policy and dialog; the Android head owns the
/// signed cross-package query and configuration activity.
/// </summary>
public static class PlatformPcsx2x6Bios
{
    public static Func<CancellationToken, Task<bool>>? AndroidReadinessCheck
    {
        private get;
        set;
    }

    public static Func<Task<bool>>? AndroidConfigurator
    {
        private get;
        set;
    }

    public static bool IsAvailable =>
        OperatingSystem.IsAndroid() &&
        AndroidReadinessCheck != null &&
        AndroidConfigurator != null;

    public static Task<bool> IsConfiguredAsync(
        CancellationToken cancellationToken = default)
    {
        var check = AndroidReadinessCheck
            ?? throw new PlatformNotSupportedException(
                "The Android PCSX2X6 BIOS bridge is not registered.");
        return check(cancellationToken);
    }

    public static Task<bool> ConfigureAsync()
    {
        var configure = AndroidConfigurator
            ?? throw new PlatformNotSupportedException(
                "The Android PCSX2X6 BIOS configurator is not registered.");
        return configure();
    }
}
