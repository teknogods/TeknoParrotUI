using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Android.Content;
using Android.OS;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Production UI adapter for recipe-backed games launched by the managed
/// foreground Winlator backend. Recipe validation decides which profiles are
/// supported; the session lifetime is independent of the Activity/view.
/// </summary>
internal sealed class AndroidWinlatorGameSession : IGameSession
{
    private readonly Context _context;
    private readonly GameProfile _profile;
    private readonly bool _isTest;
    private readonly bool _emuOnly;
    private int _started;
    private int _completed;

    public AndroidWinlatorGameSession(
        Context context,
        GameProfile profile,
        bool isTest,
        bool emuOnly)
    {
        _context = context.ApplicationContext ?? context;
        _profile = profile;
        _isTest = isTest;
        _emuOnly = emuOnly;

        OutputReceived += GameSessionLogArchive.Append;
        StateChanged += state => GameSessionLogArchive.Append("[state] " + state);
        Exited += GameSessionLogArchive.EndRun;
    }

    public event Action<string>? OutputReceived;
    public event Action<string>? StateChanged;
    public event Action<int>? Exited;

    public bool Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return false;

        GameSessionLogArchive.BeginRun(_profile);
        try
        {
            var pcsx2x6Owner =
                Pcsx2x6SessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(pcsx2x6Owner))
                throw new InvalidOperationException(
                    $"{pcsx2x6Owner} already owns the Android game session.");
            var dolphinOwner =
                DolphinSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(dolphinOwner))
                throw new InvalidOperationException(
                    $"{dolphinOwner} already owns the Android game session.");

            GameSessionService.StatusChanged += OnServiceStatusChanged;
            var activeProfileName = GameSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrEmpty(activeProfileName))
            {
                if (!string.Equals(activeProfileName, _profile.ProfileName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"{activeProfileName} already owns the Android game session. Stop it before launching another game.");

                // A durable session record can outlive this app process. That is
                // expected after LMK/system process death, but an induced crash
                // can also suppress Android's automatic START_STICKY delivery.
                // When this is a fresh process (the static status is still idle),
                // make the same component-only foreground-service delivery from
                // our own UID. GameSessionService will load the saved port/token/
                // session id and restore the existing Winlator guest; it never
                // creates a second prepared launch while a record is present.
                var serviceStatus = GameSessionService.CurrentStatus;
                var serviceRestartRequested = false;
                if (string.Equals(serviceStatus, "state=idle", StringComparison.Ordinal) ||
                    serviceStatus.StartsWith("state=fault", StringComparison.Ordinal))
                {
                    serviceRestartRequested = true;
                    StateChanged?.Invoke(
                        serviceStatus.StartsWith("state=fault", StringComparison.Ordinal)
                            ? "Retrying Android game session"
                            : "Restoring Android game session");
                    _context.StartForegroundService(new Intent(_context, typeof(GameSessionService)));
                }

                OutputReceived?.Invoke("[AndroidSession] Reattached to the foreground Winlator session.");
                if (!serviceRestartRequested)
                {
                    StateChanged?.Invoke("Reattaching to Android game session");
                    OnServiceStatusChanged(GameSessionService.CurrentStatus);
                }
                return true;
            }

            InspectDeviceHealthBeforeLaunch(_context);

            var downloads = global::Android.OS.Environment
                .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)
                ?.AbsolutePath ?? "/storage/emulated/0/Download";
            var plan = AndroidWinlatorLaunchPlan.Create(
                _profile,
                _isTest,
                _emuOnly,
                downloads);

            OutputReceived?.Invoke(
                $"[AndroidSession] Winlator container {plan.ContainerId}; " +
                $"game={plan.GameExecutable}; runtime={plan.LibraryDirectory}; " +
                $"input={plan.InputProtocol}/layout-{plan.ControlsProfileId}; " +
                $"display={plan.DisplayMode}@{plan.ResolutionWidth}x{plan.ResolutionHeight}; " +
                $"fps={(plan.FrameRateLimit > 0 ? plan.FrameRateLimit.ToString() : "unlimited")}; " +
                $"gameLogging={(plan.DebugLoggingEnabled ? "on" : "off")}");
            StateChanged?.Invoke("Starting Android foreground game session");
            _context.StartActivity(AndroidGameSessionLauncherActivity.CreateIntent(_context, plan));
            return true;
        }
        catch (Exception error)
        {
            var message = "Android launch failed: " + error.Message;
            OutputReceived?.Invoke("ERROR: " + message);
            StateChanged?.Invoke(message);
            GameSessionLogArchive.EndRun(-1);
            Interlocked.Exchange(ref _completed, 1);
            GameSessionService.StatusChanged -= OnServiceStatusChanged;
            return false;
        }
    }

    public void ForceQuit()
    {
        if (Volatile.Read(ref _completed) != 0)
            return;
        StateChanged?.Invoke("Stopping Android game session");
        var intent = new Intent(_context, typeof(GameSessionService));
        intent.SetAction(GameSessionService.StopAction);
        _context.StartService(intent);
    }

    public void Dispose()
    {
        // The foreground service, not the Activity/view, owns the game. UI
        // destruction must only detach this observer so Android recreation or
        // hibernation cannot stop Wine. The notification Stop action and the
        // explicit Force Quit button remain the termination paths.
        GameSessionService.StatusChanged -= OnServiceStatusChanged;
    }

    private void OnServiceStatusChanged(string status)
    {
        OutputReceived?.Invoke("[AndroidSession] " + status);
        if (status.StartsWith("state=preparing", StringComparison.Ordinal))
            StateChanged?.Invoke("Preparing Winlator session");
        else if (status.StartsWith("state=restoring", StringComparison.Ordinal))
            StateChanged?.Invoke("Restoring Android game session");
        else if (status.StartsWith("state=running", StringComparison.Ordinal))
            StateChanged?.Invoke("Android game session is already active");
        else if (status.StartsWith("state=waiting", StringComparison.Ordinal))
            StateChanged?.Invoke("Winlator game is starting");
        else if (status.StartsWith("state=connected", StringComparison.Ordinal))
            StateChanged?.Invoke("Game controls connected");
        else if (status.StartsWith("state=reconnecting", StringComparison.Ordinal))
            StateChanged?.Invoke("Reconnecting game controls");
        else if (status.StartsWith("state=pressure", StringComparison.Ordinal))
            StateChanged?.Invoke("Android is low on memory - close other apps");
        else if (status.StartsWith("state=stopping", StringComparison.Ordinal))
            StateChanged?.Invoke("Stopping Android game session");
        else if (status.StartsWith("state=ended", StringComparison.Ordinal))
            Complete(0, "Game stopped");
        else if (status.StartsWith("state=stopped", StringComparison.Ordinal))
            // state=stopped is emitted only for an explicit user stop. The
            // request can originate from TPUI's button or the foreground
            // notification, so the local adapter may not have observed the
            // click that set _forceQuitRequested. In both cases this is a
            // clean user-requested end, never a game failure.
            Complete(0, "Game stopped");
        else if (status.StartsWith("state=fault", StringComparison.Ordinal))
            Complete(-1, "Android game session failed: " + ReadField(status, "error"));
    }

    private void Complete(int exitCode, string state)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;
        GameSessionService.StatusChanged -= OnServiceStatusChanged;
        StateChanged?.Invoke(state);
        Exited?.Invoke(exitCode);
    }

    private static string ReadField(string status, string name)
    {
        var marker = ";" + name + "=";
        var start = status.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return "unknown error";
        start += marker.Length;
        var end = status.IndexOf(';', start);
        return end < 0 ? status[start..] : status[start..end];
    }

    private void InspectDeviceHealthBeforeLaunch(Context context)
    {
        var memoryManager = context.GetSystemService(Context.ActivityService) as
            global::Android.App.ActivityManager;
        var memory = new global::Android.App.ActivityManager.MemoryInfo();
        memoryManager?.GetMemoryInfo(memory);
        const long bytesPerMiB = 1024L * 1024L;
        var availableMiB = memory.AvailMem / bytesPerMiB;
        var totalMiB = memory.TotalMem / bytesPerMiB;
        var thresholdMiB = memory.Threshold / bytesPerMiB;

        // Keep the pre-API-29 default as the raw NONE value. Referencing the
        // ThermalStatus enum itself outside the version guard makes the Android
        // analyzer correctly treat the call site as unavailable on API 26-28.
        var thermalStatus = 0;
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var powerManager = context.GetSystemService(Context.PowerService) as PowerManager;
            thermalStatus = (int)(powerManager?.CurrentThermalStatus ?? ThermalStatus.None);
        }

        OutputReceived?.Invoke(
            $"[AndroidHealth] availableMemory={availableMiB}MiB/{totalMiB}MiB; " +
            $"lowMemoryThreshold={thresholdMiB}MiB; lowMemory={memory.LowMemory}; " +
            $"thermal={thermalStatus}");

        if (memoryManager != null && memory.LowMemory)
            throw new InvalidOperationException(
                $"Android reports low memory before launch ({availableMiB} MiB available; " +
                $"threshold {thresholdMiB} MiB). Close other apps, then try Launch again.");

        // Android's CRITICAL level (4) already applies aggressive platform
        // throttling and can be reached by demanding games without making the
        // device unsafe. Keep that level visible as a warning, but reserve a
        // launch rejection for EMERGENCY (5) or SHUTDOWN (6).
        const int emergencyThermalStatus = 5;
        if (thermalStatus >= emergencyThermalStatus)
            throw new InvalidOperationException(
                $"The device is too hot to start a game (Android thermal status: {thermalStatus}). " +
                "Let it cool down, then try Launch again. An already-running game is never stopped by this check.");
    }
}

internal sealed record AndroidWinlatorLaunchPlan(
    int ContainerId,
    string ProfileName,
    string LoaderExecutable,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    string LibraryDirectory,
    string GameExecutable,
    string InputProtocol,
    int ControlsProfileId,
    int FrameRateLimit,
    int ResolutionWidth,
    int ResolutionHeight,
    string DisplayMode,
    bool DebugLoggingEnabled,
    string CompatibilityPreset,
    string ProfileConfigIni)
{
    public static AndroidWinlatorLaunchPlan Create(
        GameProfile profile,
        bool isTest,
        bool emuOnly,
        string downloadsDirectory)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (emuOnly)
            throw new NotSupportedException("Android emulator-only mode is not implemented.");
        if (!AndroidLaunchRecipeCatalog.TryGetValidated(
                profile.ProfileName ?? string.Empty, out var recipe, out var recipeError))
            throw new NotSupportedException(recipeError);
        var profileArchitecture = profile.Is64Bit || (isTest && profile.TestExecIs64Bit)
            ? "x64"
            : "x86";
        if (!string.Equals(recipe.GuestArchitecture, profileArchitecture,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"The Android recipe architecture ({recipe.GuestArchitecture}) does not match " +
                $"the game executable ({profileArchitecture}).");
        if (profile.HasTwoExecutables &&
            (!profile.LaunchSecondExecutableFirst ||
             recipe.CompatibilityPreset !=
                AndroidLaunchRecipe.CompatibilityPresetInitialDTheArcade))
            throw new NotSupportedException(
                "This two-executable Android profile has no managed prelaunch recipe.");
        if (isTest && (profile.HasSeparateTestMode ||
                       !string.IsNullOrWhiteSpace(profile.TestMenuParameter) ||
                       !string.IsNullOrWhiteSpace(profile.TestMenuExtraParameters)))
            throw new NotSupportedException("This profile's Android test-mode launch is not implemented yet.");
        if (!recipe.HandlesProfileArguments(
                profile.CustomArguments ?? string.Empty,
                profile.ExtraParameters ?? string.Empty))
            throw new NotSupportedException(
                "This profile's current arguments do not match its validated Android conversion.");

        var gameExecutable = AndroidWinlatorGamePath.ToDosPath(
            profile.GamePath,
            downloadsDirectory);
        var resolved = recipe.Resolve(gameExecutable);
        var displayMode = profile.AndroidDisplayMode switch
        {
            AndroidDisplayMode.Centered => AndroidLaunchRecipe.DisplayModeCentered,
            AndroidDisplayMode.AspectFit => AndroidLaunchRecipe.DisplayModeAspectFit,
            AndroidDisplayMode.Fullscreen => AndroidLaunchRecipe.DisplayModeFullscreen,
            _ => resolved.DisplayMode
        };
        var result = new AndroidWinlatorLaunchPlan(
            resolved.ContainerId,
            profile.ProfileName ?? string.Empty,
            resolved.LoaderExecutable,
            resolved.WorkingDirectory,
            resolved.Arguments,
            resolved.LibraryDirectory,
            gameExecutable,
            resolved.InputProtocol,
            resolved.ControlsProfileId,
            resolved.FrameRateLimit,
            resolved.ResolutionWidth,
            resolved.ResolutionHeight,
            displayMode,
            profile.AndroidDebugLogging ?? !resolved.PerformanceModeDefault,
            resolved.CompatibilityPreset,
            recipe.ApplyProfileConfigOverrides(TeknoParrotIniWriter.BuildConfigIni(profile)));

        // Reuse the exact cross-package schema validation now, before opening
        // a permission Activity or starting a foreground service.
        _ = WinlatorSessionContract.CreateActivityLaunch(new WinlatorActivityLaunchRequest(
            Guid.NewGuid(),
            result.ContainerId,
            WinlatorSessionContract.WindowsExecutableLaunchKind,
            result.LoaderExecutable,
            result.WorkingDirectory,
            result.Arguments,
            result.LibraryDirectory,
            result.ControlsProfileId,
            result.FrameRateLimit,
            result.ResolutionWidth,
            result.ResolutionHeight,
            result.DebugLoggingEnabled,
            result.CompatibilityPreset,
            result.DisplayMode,
            result.ProfileConfigIni));
        return result;
    }

}
