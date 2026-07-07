using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Avalonia-specific UI options (fullscreen, controller navigation bindings).
/// Stored as UiOptions.json next to ParrotData.xml in the shared data folder,
/// so the classic UI's settings file is left untouched.
/// </summary>
public sealed class UiOptions
{
    public bool StartFullscreen { get; set; }
    public bool EnableControllerNavigation { get; set; }

    /// <summary>Action name (UiNavAction) → captured input display name.</summary>
    public Dictionary<string, string> NavigationBindings { get; set; } = new();

    private const string FileName = "UiOptions.json";

    public static UiOptions Load()
    {
        try
        {
            if (File.Exists(FileName))
                return JsonSerializer.Deserialize<UiOptions>(File.ReadAllText(FileName)) ?? new UiOptions();
        }
        catch
        {
            // corrupt file — fall back to defaults
        }
        return new UiOptions();
    }

    public void Save()
    {
        File.WriteAllText(FileName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
