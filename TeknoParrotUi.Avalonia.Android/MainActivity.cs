using System;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using TeknoParrotUi.Avalonia;

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
        // to the app's private writable storage and pre-create the data layout.
        var dataDir = FilesDir!.AbsolutePath;
        System.Environment.CurrentDirectory = dataDir;
        foreach (var folder in new[] { "GameProfiles", "UserProfiles", "Metadata", "Icons", "InputProfiles" })
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDir, folder));

        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .LogToTrace();
}

/// <summary>
/// Android entry point for the full TeknoParrot UI. Hosts the shared
/// <c>App</c> (and through it <c>Views/MainView</c> — the complete application
/// shell) via Avalonia's single-view lifetime. Desktop-only features
/// (game launching, Win32 input capture, registry) are runtime-guarded in the
/// shared code and simply unavailable here.
/// </summary>
[Activity(
    Label = "TeknoParrot",
    Theme = "@style/TeknoParrotTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
