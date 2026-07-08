using System.Globalization;
using System.Resources;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Localized string lookup backed by the classic TeknoParrot translations
/// (Properties/Resources*.resx — 13 languages). Falls back to the key name if
/// a string is missing so untranslated UI never crashes.
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Resources =
        new("TeknoParrotUi.Avalonia.Properties.Resources", typeof(Loc).Assembly);

    /// <summary>Raised after the UI language changes so views can re-apply strings.</summary>
    public static event System.Action? LanguageChanged;

    /// <summary>Gets the localized string for a resource key.</summary>
    public static string T(string key)
    {
        try
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>Gets the localized string for a key, or the fallback when missing.</summary>
    public static string T(string key, string fallback)
    {
        try
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>Switches the UI language live: applies the culture, persists it and notifies all views.</summary>
    public static void SetLanguage(string cultureName)
    {
        ApplyCulture(cultureName);
        TeknoParrotUi.Common.Lazydata.ParrotData.Language = cultureName;
        TeknoParrotUi.Common.JoystickHelper.Serialize();
        LanguageChanged?.Invoke();
    }

    /// <summary>Applies the saved language (e.g. "fi-FI") to the process.</summary>
    public static void ApplyCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // unknown culture saved — stay on system language
        }
    }
}
