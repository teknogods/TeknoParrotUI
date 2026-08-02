using System;
using System.Threading.Tasks;

namespace TeknoParrotUi.Avalonia.Services;

public static class PlatformRpcs3x6Firmware
{
    public static Func<Task<bool>>? AndroidReadinessCheck { private get; set; }
    public static Func<Task<bool>>? AndroidConfigurator { private get; set; }
    public static bool IsAvailable => AndroidReadinessCheck != null && AndroidConfigurator != null;
    public static Task<bool> IsConfiguredAsync() => AndroidReadinessCheck?.Invoke() ?? Task.FromResult(false);
    public static Task<bool> ConfigureAsync() => AndroidConfigurator?.Invoke() ??
        throw new PlatformNotSupportedException("RPCS3X6 firmware setup is not registered.");
}
