using System;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// Lets a platform head replace the desktop binding editor with its native
/// controls editor.  Android registers Winlator's touch/gamepad editor while
/// Windows and Linux continue to use <c>JoystickSetupView</c>.
/// </summary>
public static class PlatformControlsEditor
{
    public static Func<GameProfile, string?>? AndroidLauncher { private get; set; }

    /// <returns><see langword="null"/> on success; otherwise a user-facing error.</returns>
    public static string? OpenAndroidEditor(GameProfile profile)
    {
        var launcher = AndroidLauncher;
        if (launcher == null)
            return "The Winlator controls editor is not available. Restart TeknoParrot after installing the compatible companion.";

        try
        {
            return launcher(profile);
        }
        catch (Exception error)
        {
            return $"Could not open the Winlator controls editor: {error.Message}";
        }
    }
}
