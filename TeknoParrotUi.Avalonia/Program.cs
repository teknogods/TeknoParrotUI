using Avalonia;
using System;
using System.Runtime.CompilerServices;

namespace TeknoParrotUi.Avalonia;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Dependencies live in libs\ in published builds — must be registered
        // before any dependency type is touched (hence the NoInlining split).
        LibsResolver.Register();
        RunApp(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApp(string[] args)
    {
        // Locate the TeknoParrot data folder and set CWD
        Services.AppEnvironment.Initialize();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
