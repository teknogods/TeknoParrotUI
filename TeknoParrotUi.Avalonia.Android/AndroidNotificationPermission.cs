using System;
using Android.App;
using Android.Content;
using Android.Content.PM;

namespace TeknoParrotUi.Avalonia.Android;

internal static class AndroidNotificationPermission
{
    private const string PreferencesName = "android-permission-prompts";
    private const string PromptedKey = "post-notifications-v1";

    public static bool RequestIfNeeded(Activity activity, int requestCode)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) ||
            activity.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) ==
            Permission.Granted)
            return false;

        var preferences = activity.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        if (preferences?.GetBoolean(PromptedKey, false) == true)
            return false;

        preferences?.Edit()?.PutBoolean(PromptedKey, true)?.Apply();
        activity.RequestPermissions(
            new[] { global::Android.Manifest.Permission.PostNotifications },
            requestCode);
        return true;
    }
}
