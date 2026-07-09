using Avalonia;
using Avalonia.Styling;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Theme selection persisted in UiOptions.json ("System", "Light" or "Dark").
/// Uses Avalonia's native ThemeVariant system: "System" follows the OS
/// preference live on Windows, Linux, macOS and Android; the palette's
/// ThemeDictionaries re-resolve every Tp* token instantly — no restart,
/// no manual resource swapping.
/// </summary>
public static class ThemeManager
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    /// <summary>Applies a theme choice to the running application.</summary>
    public static void Apply(string? theme)
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant = theme switch
        {
            Light => ThemeVariant.Light,
            Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // follow the OS
        };
    }

    /// <summary>Applies the theme stored in UiOptions.json (startup).</summary>
    public static void ApplySaved() => Apply(UiOptions.Load().Theme);
}
