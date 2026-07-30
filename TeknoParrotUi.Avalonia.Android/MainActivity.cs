using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using TeknoParrotUi.Avalonia;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.GameLaunch;
using Bundle = Android.OS.Bundle;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Android Application: owns the Avalonia app configuration (Avalonia 12
/// pattern — the builder hook lives on AvaloniaAndroidApplication, not the
/// activity). Mirrors Program.BuildAvaloniaApp minus desktop-only pieces
/// (UsePlatformDetect/DeveloperTools); Inter font keeps typography identical
/// to the desktop app.
/// </summary>
[Application]
public class TeknoParrotApplication : AvaloniaAndroidApplication<App>
{
    public TeknoParrotApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        // The shared code reads/writes its data (ParrotData.xml, GameProfiles/,
        // UserProfiles/, Metadata/, Icons/) relative to the current directory —
        // on desktop AppEnvironment.Initialize() points CWD at the TeknoParrot
        // folder. On Android CWD defaults to the read-only '/', so redirect it
        // to the app's private writable storage, create the mutable layout, and
        // seed the stock catalogs packaged as Android assets.
        var dataDir = FilesDir!.AbsolutePath;
        Environment.CurrentDirectory = dataDir;
        foreach (var folder in new[]
                 { "GameProfiles", "UserProfiles", "Metadata", "GameSetup", "AndroidLaunchRecipes", "Icons", "InputProfiles", "InputBindings" })
            Directory.CreateDirectory(Path.Combine(dataDir, folder));

        SeedBundledCatalog(dataDir);
        PlatformGameCatalogSync.RefreshAsync =
            new AndroidPcsx2x6CatalogSync(this).RefreshAsync;
        var appUpdater = new AndroidAppUpdater(this);
        PlatformAppUpdater.AndroidComponentsFactory =
            appUpdater.BuildComponents;
        PlatformAppUpdater.AndroidInstaller =
            appUpdater.DownloadAndLaunchInstallerAsync;
        PlatformAppUpdater.AndroidComponentRefresher =
            appUpdater.RefreshComponentsAsync;
        var pcsx2x6Bios = new AndroidPcsx2x6Bios(this);
        PlatformPcsx2x6Bios.AndroidReadinessCheck =
            pcsx2x6Bios.IsConfiguredAsync;
        PlatformPcsx2x6Bios.AndroidConfigurator =
            MainActivity.ConfigurePcsx2x6BiosAsync;
        PlatformPcsx2x6GameImport.AndroidImporter =
            MainActivity.ImportPcsx2x6GameAsync;
        PlatformDocumentPathResolver.AndroidResolver =
            new AndroidDocumentProviderPathResolver(this).Resolve;

        GameSessionFactory.RegisterPlatformFactory(
            (profile, isTest, emuOnly) =>
                profile.EmulatorType == EmulatorType.pcsx2x6
                    ? new AndroidPcsx2x6GameSession(this, profile, isTest, emuOnly)
                    : new AndroidWinlatorGameSession(this, profile, isTest, emuOnly),
            () => Pcsx2x6SessionService.TryRestoreActiveProfileName(this)
                  ?? GameSessionService.TryRestoreActiveProfileName(this));

        PlatformControlsEditor.AndroidLauncher = profile =>
        {
            var controlsProfileId = 9001;
            if (AndroidLaunchRecipeCatalog.TryGetValidated(
                    profile.ProfileName ?? string.Empty,
                    out var recipe,
                    out _))
                controlsProfileId = recipe.ControlsProfileId;
            var editor = new Intent(Intent.ActionMain);
            editor.SetClassName(
                "com.teknoparrot.winlator",
                "com.winlator.MainActivity");
            editor.PutExtra("edit_input_controls", true);
            editor.PutExtra("selected_profile_id", controlsProfileId);
            editor.PutExtra(
                "teknoparrot_profile_name",
                profile.ProfileName ?? profile.GameNameInternal ?? string.Empty);
            editor.PutExtra(
                "teknoparrot_native_controls",
                profile.EmulatorType is EmulatorType.OpenParrot or EmulatorType.TeknoParrot);
            editor.AddFlags(ActivityFlags.NewTask);

            try
            {
                StartActivity(editor);
                return null;
            }
            catch (ActivityNotFoundException)
            {
                return "Compatible TeknoParrot Winlator controls were not found. Install or update the Winlator companion.";
            }
            catch (Java.Lang.SecurityException)
            {
                return "TeknoParrotUI and the Winlator companion do not have matching signatures. Reinstall both official packages so they use the same TeknoParrot signing key.";
            }
        };

        base.OnCreate();
    }

    private void SeedBundledCatalog(string dataDir)
    {
        // AssemblyVersion is intentionally stable and therefore cannot identify
        // an updated APK. Use Android's monotonically increasing version code so
        // updated stock profiles are copied before user-profile migration runs.
        var packageInfo = PackageManager?.GetPackageInfo(PackageName!, PackageInfoFlags.MetaData);
        var packageVersion = packageInfo is null
            ? null
            : OperatingSystem.IsAndroidVersionAtLeast(28)
                ? packageInfo.LongVersionCode.ToString()
#pragma warning disable CS0618 // Required only on supported Android API 26-27.
                : packageInfo.VersionCode.ToString();
#pragma warning restore CS0618
        var version = packageInfo is null
            ? GetType().Assembly.GetName().Version?.ToString() ?? "unknown"
            : $"android:{packageVersion}";
        var markerPath = Path.Combine(dataDir, ".bundled-catalog-version");
        var replaceStockFiles = !string.Equals(
            TryReadAllText(markerPath),
            version,
            StringComparison.Ordinal);

        foreach (var folder in new[]
                 { "GameProfiles", "Metadata", "GameSetup", "AndroidLaunchRecipes", "InputProfiles" })
        {
            if ((Assets!.List(folder) ?? Array.Empty<string>()).Length == 0)
                throw new InvalidDataException(
                    $"The bundled Android catalog '{folder}' is empty or unavailable.");

            // Recipes are executable launch policy, not user data. Always
            // refresh them so an in-place APK update cannot retain stale
            // container/runtime paths when the assembly version is unchanged.
            CopyAssetDirectory(
                folder,
                Path.Combine(dataDir, folder),
                replaceStockFiles || folder == "AndroidLaunchRecipes");
        }

        WriteBytesAtomically(markerPath, Encoding.UTF8.GetBytes(version));
    }

    private void CopyAssetDirectory(string assetDirectory, string destinationDirectory, bool overwrite)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var name in Assets!.List(assetDirectory) ?? Array.Empty<string>())
        {
            var assetPath = $"{assetDirectory}/{name}";
            var destinationPath = Path.Combine(destinationDirectory, name);
            var children = Assets.List(assetPath) ?? Array.Empty<string>();
            if (children.Length > 0)
            {
                CopyAssetDirectory(assetPath, destinationPath, overwrite);
                continue;
            }

            // A zero-byte stock file is never useful and most likely records
            // an interrupted install. Repair it even when this APK version has
            // already completed its catalog migration.
            if (!overwrite && File.Exists(destinationPath) &&
                new FileInfo(destinationPath).Length > 0)
                continue;

            using var source = Assets.Open(assetPath);
            WriteStreamAtomically(destinationPath, source);
        }
    }

    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            // A torn marker is equivalent to no marker: refresh the stock
            // catalog and replace it only after every asset succeeds.
            return null;
        }
    }

    private static void WriteStreamAtomically(string destinationPath, Stream source)
    {
        var temporaryPath = destinationPath + ".installing";
        using (var destination = new FileStream(
                   temporaryPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            source.CopyTo(destination);
            destination.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, destinationPath, overwrite: true);
    }

    private static void WriteBytesAtomically(string destinationPath, byte[] data)
    {
        using var source = new MemoryStream(data, writable: false);
        WriteStreamAtomically(destinationPath, source);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        var configured = base.CustomizeAppBuilder(builder).WithInterFont();
#if DEBUG
        // Keep Avalonia diagnostics in developer builds, but do not leave the
        // trace sink active behind a running game in the player APK.
        return configured.LogToTrace();
#else
        return configured;
#endif
    }
}

/// <summary>
/// Android entry point for the full TeknoParrot UI. Hosts the shared
/// <c>App</c> (and through it <c>Views/MainView</c> — the complete application
/// shell) via Avalonia's single-view lifetime. The platform session factory
/// routes validated recipe-backed profiles through the managed Winlator
/// environment; unsupported desktop facilities remain runtime-guarded in the
/// shared code.
/// </summary>
[Activity(
    Label = "TeknoParrot",
    Theme = "@style/TeknoParrotTheme",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const int Pcsx2x6BiosRequestCode = 246;
    private const int Pcsx2x6GameImportRequestCode = 247;
    private static WeakReference<MainActivity>? _current;
    private TaskCompletionSource<bool>? _biosConfiguration;
    private TaskCompletionSource<bool>? _gameImport;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _current = new WeakReference<MainActivity>(this);
#if DEBUG
        if (Intent?.GetBooleanExtra(WinlatorBridgeProbeActivity.LaunchExtra, false) == true ||
            Intent?.GetBooleanExtra(WinlatorBridgeProbeActivity.GuestLaunchExtra, false) == true ||
            Intent?.GetBooleanExtra(WinlatorBridgeProbeActivity.ProfileLaunchExtra, false) == true ||
            Intent?.GetBooleanExtra(GameSessionService.RestoreExtra, false) == true ||
            !string.IsNullOrEmpty(
                Intent?.GetStringExtra(WinlatorBridgeProbeActivity.WindowsExecutableExtra)))
        {
            var probe = new Intent(this, typeof(WinlatorBridgeProbeActivity));
            probe.PutExtra(
                WinlatorBridgeProbeActivity.GuestLaunchExtra,
                Intent?.GetBooleanExtra(WinlatorBridgeProbeActivity.GuestLaunchExtra, false) == true);
            probe.PutExtra(
                WinlatorBridgeProbeActivity.ProfileLaunchExtra,
                Intent?.GetBooleanExtra(WinlatorBridgeProbeActivity.ProfileLaunchExtra, false) == true);
            probe.PutExtra(
                GameSessionService.RestoreExtra,
                Intent?.GetBooleanExtra(GameSessionService.RestoreExtra, false) == true);
            probe.PutExtra(
                WinlatorBridgeProbeActivity.GuestContainerExtra,
                Intent?.GetIntExtra(WinlatorBridgeProbeActivity.GuestContainerExtra, 1) ?? 1);
            probe.PutExtra(
                GameSessionService.ProfileNameExtra,
                Intent?.GetStringExtra(GameSessionService.ProfileNameExtra));
            probe.PutExtra(
                WinlatorBridgeProbeActivity.WindowsExecutableExtra,
                Intent?.GetStringExtra(WinlatorBridgeProbeActivity.WindowsExecutableExtra));
            probe.PutExtra(
                WinlatorBridgeProbeActivity.WindowsWorkingDirectoryExtra,
                Intent?.GetStringExtra(WinlatorBridgeProbeActivity.WindowsWorkingDirectoryExtra));
            probe.PutExtra(
                WinlatorBridgeProbeActivity.WindowsArgumentsExtra,
                Intent?.GetStringExtra(WinlatorBridgeProbeActivity.WindowsArgumentsExtra));
            probe.PutExtra(
                WinlatorBridgeProbeActivity.WindowsLibraryDirectoryExtra,
                Intent?.GetStringExtra(WinlatorBridgeProbeActivity.WindowsLibraryDirectoryExtra));
            StartActivity(probe);
            // This Activity is only a debug-intent trampoline for these
            // requests. Leaving it below the probe lets it become foreground
            // after the one-shot launcher exits, which pauses the separate
            // Winlator game task and suspends every Wine process.
            Finish();
        }
#endif
    }

    internal static Task<bool> ConfigurePcsx2x6BiosAsync()
    {
        if (_current == null ||
            !_current.TryGetTarget(out var activity) ||
            activity.IsFinishing)
        {
            throw new InvalidOperationException(
                "The TeknoParrot Android activity is not available.");
        }

        if (activity._biosConfiguration is { Task.IsCompleted: false } pending)
            return pending.Task;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        activity._biosConfiguration = completion;
        try
        {
            var intent = new Intent();
            intent.SetClassName(
                "com.teknogods.tekno2x6",
                "com.armsx2.TeknoParrotBiosImportActivity");
            activity.StartActivityForResult(intent, Pcsx2x6BiosRequestCode);
        }
        catch (Exception error)
        {
            activity._biosConfiguration = null;
            completion.TrySetException(error);
        }

        return completion.Task;
    }

    internal static Task<bool> ImportPcsx2x6GameAsync(string manifestName)
    {
        if (_current == null ||
            !_current.TryGetTarget(out var activity) ||
            activity.IsFinishing)
        {
            throw new InvalidOperationException(
                "The TeknoParrot Android activity is not available.");
        }

        if (activity._gameImport is { Task.IsCompleted: false } pending)
            return pending.Task;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        activity._gameImport = completion;
        try
        {
            var intent = new Intent();
            intent.SetClassName(
                "com.teknogods.tekno2x6",
                "com.armsx2.TeknoParrotGameImportActivity");
            intent.PutExtra(
                "com.teknoparrot.pcsx2x6.extra.EXPECTED_MANIFEST",
                manifestName);
            activity.StartActivityForResult(
                intent,
                Pcsx2x6GameImportRequestCode);
        }
        catch (Exception error)
        {
            activity._gameImport = null;
            completion.TrySetException(error);
        }

        return completion.Task;
    }

#pragma warning disable CS0672 // Android still dispatches activity results here.
    protected override void OnActivityResult(
        int requestCode,
        Result resultCode,
        Intent? data)
#pragma warning restore CS0672
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == Pcsx2x6BiosRequestCode)
        {
            var completion = _biosConfiguration;
            _biosConfiguration = null;
            completion?.TrySetResult(resultCode == Result.Ok);
        }
        else if (requestCode == Pcsx2x6GameImportRequestCode)
        {
            var completion = _gameImport;
            _gameImport = null;
            completion?.TrySetResult(resultCode == Result.Ok);
        }
    }

    protected override void OnDestroy()
    {
        if (_current != null &&
            _current.TryGetTarget(out var activity) &&
            ReferenceEquals(activity, this))
            _current = null;
        _biosConfiguration?.TrySetCanceled();
        _biosConfiguration = null;
        _gameImport?.TrySetCanceled();
        _gameImport = null;
        base.OnDestroy();
    }
}
