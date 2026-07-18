using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Updater;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Android cannot replace an installed APK with the desktop ZIP updater.
/// The Android head registers package-aware component discovery and an
/// installer launcher here before the shared UI is created.
/// </summary>
public static class PlatformAppUpdater
{
    public static Func<IReadOnlyList<UpdaterComponent>>? AndroidComponentsFactory
    {
        private get;
        set;
    }

    public static Func<
        UpdateCheckResult,
        IProgress<double>,
        CancellationToken,
        Task<string>>? AndroidInstaller
    {
        private get;
        set;
    }

    public static Func<
        IReadOnlyList<UpdaterComponent>,
        CancellationToken,
        Task>? AndroidComponentRefresher
    {
        private get;
        set;
    }

    public static bool IsAndroidAvailable =>
        OperatingSystem.IsAndroid() &&
        AndroidComponentsFactory != null &&
        AndroidInstaller != null;

    public static IReadOnlyList<UpdaterComponent> BuildAndroidComponents()
    {
        var factory = AndroidComponentsFactory
            ?? throw new PlatformNotSupportedException(
                "The Android package updater is not registered.");
        return factory();
    }

    public static Task<string> InstallAndroidPackageAsync(
        UpdateCheckResult update,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        var installer = AndroidInstaller
            ?? throw new PlatformNotSupportedException(
                "The Android package installer is not registered.");
        return installer(update, progress, cancellationToken);
    }

    public static Task RefreshAndroidComponentsAsync(
        IReadOnlyList<UpdaterComponent> components,
        CancellationToken cancellationToken = default)
    {
        var refresher = AndroidComponentRefresher;
        return refresher == null
            ? Task.CompletedTask
            : refresher(components, cancellationToken);
    }
}
