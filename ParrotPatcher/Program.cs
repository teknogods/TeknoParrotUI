using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Avalonia;

namespace ParrotPatcher;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Dependencies live in libs\ in published builds — must be registered
        // before any dependency type is touched (hence the NoInlining split).
        RegisterLibsResolver();
        RunApp(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApp(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RegisterLibsResolver()
    {
        var libsDir = Path.Combine(AppContext.BaseDirectory, "libs");
        if (!Directory.Exists(libsDir))
            return;

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(libsDir, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, libraryName) =>
        {
            foreach (var candidate in new[] { Path.Combine(libsDir, libraryName), Path.Combine(libsDir, libraryName + ".dll") })
            {
                if (File.Exists(candidate) &&
                    System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                    return handle;
            }
            return IntPtr.Zero;
        };
    }
}
