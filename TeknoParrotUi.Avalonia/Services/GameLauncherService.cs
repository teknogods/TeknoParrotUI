using System.Diagnostics;
using System.IO;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Launches games through the TeknoParrot CLI. This is the compatibility path:
/// it reuses the battle-tested per-game launch pipeline until that pipeline is
/// fully extracted into TeknoParrotUi.Common.
/// </summary>
public static class GameLauncherService
{
    public static bool CanLaunch => AppEnvironment.LauncherExe != null;

    public static bool Launch(GameProfile profile, bool testMode = false)
    {
        var launcher = AppEnvironment.LauncherExe;
        if (launcher == null || profile.FileName == null)
            return false;

        var args = $"--profile={Path.GetFileName(profile.FileName)}";
        if (testMode)
            args += " --test";

        var psi = new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher)!,
            Arguments = args,
            UseShellExecute = false
        };
        Process.Start(psi);
        return true;
    }
}
