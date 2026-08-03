using System;

namespace TeknoParrotUi.Common.Android;

public static class AndroidRpcs3x6GamePath
{
    public const string EbootSuffix =
        "/dev_hdd0/game/SCEEXE000/USRDIR/EBOOT.BIN";

    public static bool IsConfigured(string path)
    {
        var normalized = path?.Trim().Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(normalized) &&
               normalized.StartsWith("/storage/", StringComparison.Ordinal) &&
               normalized.EndsWith(EbootSuffix, StringComparison.OrdinalIgnoreCase);
    }
}
