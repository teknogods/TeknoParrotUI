using System;
using System.IO;
using System.Threading;
using Android.Content;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>UI adapter for Triforce/Wii arcade profiles hosted by TeknoDolphin.</summary>
internal sealed class AndroidDolphinGameSession : IGameSession
{
    private const string GameRoot =
        "/storage/emulated/0/Android/data/com.teknogods.teknodolphin/files/TeknoParrot/games";
    private static readonly string[] SupportedExtensions =
        [".iso", ".gcm", ".wbfs", ".rvz", ".wad", ".dol", ".elf"];

    private readonly Context _context;
    private readonly GameProfile _profile;
    private readonly bool _isTest;
    private readonly bool _emuOnly;
    private int _started;
    private int _completed;

    public AndroidDolphinGameSession(
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
            if (!PlatformCapabilities.IsAndroidDolphinProfileSupported(_profile))
                throw new InvalidOperationException(
                    "TeknoDolphin on Android supports only Mario Kart Arcade GP 1/2, " +
                    "F-ZERO AX Rev E, and Virtua Striker 3/4.");
            if (_emuOnly)
                throw new NotSupportedException(
                    "TeknoDolphin emulator-only launch is not available on Android.");
            if (_isTest && _profile.HasSeparateTestMode)
                throw new NotSupportedException(
                    "This TeknoDolphin profile has no separate Android test image.");

            var otherOwner =
                Pcsx2x6SessionService.TryGetActiveProfileName(_context) ??
                GameSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(otherOwner))
                throw new InvalidOperationException(
                    $"{otherOwner} already owns the Android game session.");

            DolphinSessionService.StatusChanged += OnServiceStatusChanged;
            var activeProfile = DolphinSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(activeProfile))
            {
                if (!string.Equals(activeProfile, _profile.ProfileName,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"{activeProfile} already owns the TeknoDolphin session.");
                StateChanged?.Invoke("Restoring TeknoDolphin arcade session");
                _ = DolphinSessionService.TryRestoreActiveProfileName(_context);
                OnServiceStatusChanged(DolphinSessionService.CurrentStatus);
                return true;
            }

            var gamePath = ResolveGamePath(_profile);
            OutputReceived?.Invoke(
                $"[AndroidSession] TeknoDolphin game={gamePath}; " +
                "input=TeknoParrot JVS page and arcade overlay");
            StateChanged?.Invoke("Starting TeknoDolphin arcade session");
            _context.StartForegroundService(
                DolphinSessionService.CreateStartIntent(
                    _context,
                    _profile.ProfileName ?? _profile.GameNameInternal ?? "TeknoDolphin",
                    _profile.EmulationProfile.ToString(),
                    gamePath));
            return true;
        }
        catch (Exception error)
        {
            var message = "Android TeknoDolphin launch failed: " + error.Message;
            OutputReceived?.Invoke("ERROR: " + message);
            StateChanged?.Invoke(message);
            Complete(-1);
            return false;
        }
    }

    public void ForceQuit()
    {
        if (Volatile.Read(ref _completed) != 0)
            return;
        StateChanged?.Invoke("Stopping TeknoDolphin arcade session");
        var intent = new Intent(_context, typeof(DolphinSessionService));
        intent.SetAction(DolphinSessionService.StopAction);
        _context.StartService(intent);
    }

    public void Dispose() =>
        DolphinSessionService.StatusChanged -= OnServiceStatusChanged;

    private void OnServiceStatusChanged(string status)
    {
        OutputReceived?.Invoke("[AndroidSession] " + status);
        if (status.StartsWith("state=starting", StringComparison.Ordinal))
            StateChanged?.Invoke("Opening TeknoDolphin");
        else if (status.StartsWith("state=accepted", StringComparison.Ordinal))
            StateChanged?.Invoke("TeknoDolphin accepted the game");
        else if (status.StartsWith("state=restoring", StringComparison.Ordinal))
            StateChanged?.Invoke("Restoring TeknoDolphin arcade session");
        else if (status.StartsWith("state=running", StringComparison.Ordinal))
            StateChanged?.Invoke("TeknoDolphin arcade game is running");
        else if (status.StartsWith("state=stopping", StringComparison.Ordinal))
            StateChanged?.Invoke("Stopping TeknoDolphin arcade session");
        else if (status.StartsWith("state=stopped", StringComparison.Ordinal))
            Complete(0);
        else if (status.StartsWith("state=fault", StringComparison.Ordinal))
        {
            StateChanged?.Invoke(status);
            Complete(-1);
        }
    }

    private void Complete(int exitCode)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;
        DolphinSessionService.StatusChanged -= OnServiceStatusChanged;
        Exited?.Invoke(exitCode);
    }

    internal static string ResolveGamePath(GameProfile profile)
    {
        var fileName = profile.ExecutableName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Array.FindIndex(SupportedExtensions, extension =>
                fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) < 0)
            throw new InvalidDataException(
                "The TeknoDolphin profile does not contain a canonical supported image name.");
        return GameRoot + "/" + fileName;
    }
}
