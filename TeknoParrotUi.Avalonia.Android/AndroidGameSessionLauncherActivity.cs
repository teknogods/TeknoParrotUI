using System;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Non-exported, same-package permission trampoline. The notification prompt
/// is shown only in direct response to the user's Launch action. A declined
/// Winlator storage grant is handed to the service as an immediate visible
/// launch fault instead of entering the provisioning retry loop.
/// </summary>
[Activity(
    Name = "com.teknoparrot.session.GameSessionLauncherActivity",
    Exported = false,
    Theme = "@android:style/Theme.Material.NoActionBar")]
public sealed class AndroidGameSessionLauncherActivity : Activity
{
    private const int NotificationPermissionRequestCode = 0x5451;
    private const int WinlatorStoragePermissionRequestCode = 0x5452;
    private const string WaitingForNotificationPermissionState =
        "waiting-for-notification-permission";
    private const string WaitingForWinlatorPermissionState =
        "waiting-for-winlator-storage-permission";
    private const string WinlatorPackage = "com.teknoparrot.winlator";
    private const string WinlatorProvisioningActivity =
        "com.winlator.TeknoParrotProvisioningActivity";
    private bool _waitingForNotificationPermission;
    private bool _waitingForWinlatorPermission;
    private bool _serviceStarted;

    internal static Intent CreateIntent(Context context, AndroidWinlatorLaunchPlan plan)
        => CreateIntent(
            context,
            plan.ContainerId,
            plan.LoaderExecutable,
            plan.WorkingDirectory,
            plan.Arguments,
            plan.LibraryDirectory,
            plan.ProfileName,
            plan.InputProtocol,
            plan.ControlsProfileId,
            plan.FrameRateLimit,
            plan.ResolutionWidth,
            plan.ResolutionHeight,
            plan.DisplayMode,
            plan.DebugLoggingEnabled,
            plan.CompatibilityPreset,
            plan.ProfileConfigIni);

    internal static Intent CreateIntent(
        Context context,
        int containerId,
        string executable,
        string workingDirectory,
        System.Collections.Generic.IReadOnlyList<string> arguments,
        string? libraryDirectory,
        string? profileName = null,
        string? inputProtocol = null,
        int controlsProfileId = 0,
        int frameRateLimit = 0,
        int resolutionWidth = 0,
        int resolutionHeight = 0,
        string? displayMode = null,
        bool debugLoggingEnabled = true,
        string? compatibilityPreset = null,
        string? profileConfigIni = null)
    {
        var intent = new Intent(context, typeof(AndroidGameSessionLauncherActivity));
        // The Activity finishes itself immediately after the permission result
        // and service handoff. FLAG_ACTIVITY_NO_HISTORY is intentionally not
        // used: a system permission dialog can temporarily cover this Activity,
        // and Android must retain it long enough to deliver the callback.
        intent.AddFlags(ActivityFlags.NewTask);
        intent.PutExtra(GameSessionService.ProfileNameExtra, profileName);
        intent.PutExtra(GameSessionService.ContainerExtra, containerId);
        intent.PutExtra(GameSessionService.ExecutableExtra, executable);
        intent.PutExtra(GameSessionService.WorkingDirectoryExtra, workingDirectory);
        intent.PutExtra(GameSessionService.ArgumentsExtra, JsonSerializer.Serialize(arguments));
        intent.PutExtra(GameSessionService.LibraryDirectoryExtra, libraryDirectory);
        intent.PutExtra(GameSessionService.InputProtocolExtra, inputProtocol);
        intent.PutExtra(GameSessionService.ControlsProfileIdExtra, controlsProfileId);
        intent.PutExtra(GameSessionService.FrameRateLimitExtra, frameRateLimit);
        intent.PutExtra(GameSessionService.ResolutionWidthExtra, resolutionWidth);
        intent.PutExtra(GameSessionService.ResolutionHeightExtra, resolutionHeight);
        intent.PutExtra(GameSessionService.DisplayModeExtra, displayMode);
        intent.PutExtra(GameSessionService.DebugLoggingEnabledExtra, debugLoggingEnabled);
        intent.PutExtra(GameSessionService.CompatibilityPresetExtra, compatibilityPreset);
        intent.PutExtra(GameSessionService.ProfileConfigIniExtra, profileConfigIni);
        return intent;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _waitingForNotificationPermission = savedInstanceState?.GetBoolean(
            WaitingForNotificationPermissionState, false) == true;
        _waitingForWinlatorPermission = savedInstanceState?.GetBoolean(
            WaitingForWinlatorPermissionState, false) == true;
        if (_waitingForNotificationPermission || _waitingForWinlatorPermission)
            return;
        if (AndroidNotificationPermission.RequestIfNeeded(
                this,
                NotificationPermissionRequestCode))
        {
            _waitingForNotificationPermission = true;
            return;
        }
        RequestWinlatorStoragePermission();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutBoolean(
            WaitingForNotificationPermissionState,
            _waitingForNotificationPermission);
        outState.PutBoolean(
            WaitingForWinlatorPermissionState,
            _waitingForWinlatorPermission);
        base.OnSaveInstanceState(outState);
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != NotificationPermissionRequestCode)
            return;
        _waitingForNotificationPermission = false;
        RequestWinlatorStoragePermission();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != WinlatorStoragePermissionRequestCode)
            return;
        _waitingForWinlatorPermission = false;
        StartSessionAndFinish(resultCode == Result.Ok
            ? null
            : "Winlator storage permission was denied. Launch the game again and choose Allow.");
    }

    private void RequestWinlatorStoragePermission()
    {
        if (_waitingForWinlatorPermission || _serviceStarted)
            return;

        var intent = new Intent();
        intent.SetComponent(new ComponentName(
            WinlatorPackage,
            WinlatorProvisioningActivity));
        intent.AddFlags(ActivityFlags.NoAnimation);
        _waitingForWinlatorPermission = true;
        try
        {
            StartActivityForResult(intent, WinlatorStoragePermissionRequestCode);
        }
        catch (Exception error)
        {
            // Let the service produce the authoritative missing/incompatible
            // Winlator error so Game Running completes instead of hanging on
            // a trampoline Activity.
            _waitingForWinlatorPermission = false;
            global::Android.Util.Log.Warn(
                "TeknoParrotSession",
                "Could not open Winlator's permission trampoline: " + error.Message);
            StartSessionAndFinish();
        }
    }

    private void StartSessionAndFinish(string? launchError = null)
    {
        if (_serviceStarted)
            return;
        _serviceStarted = true;
        try
        {
            var containerId = Intent?.GetIntExtra(GameSessionService.ContainerExtra, 0) ?? 0;
            var profileName = Intent?.GetStringExtra(GameSessionService.ProfileNameExtra);
            var executable = Intent?.GetStringExtra(GameSessionService.ExecutableExtra);
            var workingDirectory = Intent?.GetStringExtra(GameSessionService.WorkingDirectoryExtra);
            var argumentsJson = Intent?.GetStringExtra(GameSessionService.ArgumentsExtra);
            var libraryDirectory = Intent?.GetStringExtra(GameSessionService.LibraryDirectoryExtra);
            var inputProtocol = Intent?.GetStringExtra(GameSessionService.InputProtocolExtra);
            var controlsProfileId = Intent?.GetIntExtra(
                GameSessionService.ControlsProfileIdExtra, 0) ?? 0;
            var frameRateLimit = Intent?.GetIntExtra(
                GameSessionService.FrameRateLimitExtra, 0) ?? 0;
            var resolutionWidth = Intent?.GetIntExtra(
                GameSessionService.ResolutionWidthExtra, 0) ?? 0;
            var resolutionHeight = Intent?.GetIntExtra(
                GameSessionService.ResolutionHeightExtra, 0) ?? 0;
            var displayMode = Intent?.GetStringExtra(GameSessionService.DisplayModeExtra);
            var debugLoggingEnabled = Intent?.GetBooleanExtra(
                GameSessionService.DebugLoggingEnabledExtra, true) ?? true;
            var compatibilityPreset = Intent?.GetStringExtra(
                GameSessionService.CompatibilityPresetExtra);
            var profileConfigIni = Intent?.GetStringExtra(
                GameSessionService.ProfileConfigIniExtra);
            var arguments = string.IsNullOrEmpty(argumentsJson)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(argumentsJson) ?? Array.Empty<string>();
            var serviceIntent = GameSessionService.CreateStartIntent(
                this,
                containerId,
                executable ?? string.Empty,
                workingDirectory ?? string.Empty,
                arguments,
                libraryDirectory,
                profileName,
                inputProtocol,
                controlsProfileId,
                frameRateLimit,
                resolutionWidth,
                resolutionHeight,
                displayMode,
                debugLoggingEnabled,
                compatibilityPreset,
                profileConfigIni);
            if (!string.IsNullOrWhiteSpace(launchError))
                serviceIntent.PutExtra(GameSessionService.LaunchErrorExtra, launchError);
            StartForegroundService(serviceIntent);
        }
        catch (Exception error)
        {
            var message =
                "Android could not start the foreground game service: " + error.Message;
            global::Android.Util.Log.Error(
                "TeknoParrotSession",
                message);
            GameSessionService.ReportLaunchFailure(message);
        }
        finally
        {
            Finish();
        }
    }
}
