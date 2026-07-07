using System;
using System.IO;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Points the process working directory at the TeknoParrot data folder
/// (GameProfiles, UserProfiles, Icons, ParrotData.xml) so all relative paths
/// in TeknoParrotUi.Common resolve correctly. Normally that is the exe's own
/// folder; when running from a dev build tree it falls back to bin\x86\Debug.
/// </summary>
public static class AppEnvironment
{
    public static void Initialize()
    {
        var baseDir = AppContext.BaseDirectory;

        if (!Directory.Exists(Path.Combine(baseDir, "GameProfiles")))
        {
            var dev = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\bin\x86\Debug"));
            if (Directory.Exists(Path.Combine(dev, "GameProfiles")))
                baseDir = dev;
        }

        Directory.SetCurrentDirectory(baseDir);
    }
}
