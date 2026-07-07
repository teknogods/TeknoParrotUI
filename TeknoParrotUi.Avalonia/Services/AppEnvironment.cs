using System;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Locates the TeknoParrot installation (profiles, icons, launcher exe) and
/// points the process working directory at it so all relative paths in
/// TeknoParrotUi.Common resolve against the same data the WPF app uses.
/// </summary>
public static class AppEnvironment
{
    public static string? LauncherExe { get; private set; }

    public static void Initialize()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TeknoParrotUi.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\bin\x86\Debug\TeknoParrotUi.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\bin\x86\Release\TeknoParrotUi.exe")),
        };
        LauncherExe = candidates.FirstOrDefault(File.Exists);

        // Share the WPF app's data folder (GameProfiles, UserProfiles, Icons, ParrotData.xml)
        var dataDir = LauncherExe != null ? Path.GetDirectoryName(LauncherExe)! : AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(dataDir);
    }
}
