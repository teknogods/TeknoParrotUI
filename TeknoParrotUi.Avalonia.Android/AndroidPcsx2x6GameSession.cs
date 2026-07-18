using System;
using System.IO;
using System.Threading;
using Android.Content;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// UI adapter for System 246/256 profiles hosted by the PCSX2X6 ARM companion.
/// The foreground service owns the run; this object only observes it for the
/// Game Running view.
/// </summary>
internal sealed class AndroidPcsx2x6GameSession : IGameSession
{
    private const string GameRoot =
        "/storage/emulated/0/Android/data/com.armsx2/files/TeknoParrot/games";

    private readonly Context _context;
    private readonly GameProfile _profile;
    private readonly bool _isTest;
    private readonly bool _emuOnly;
    private int _started;
    private int _completed;

    public AndroidPcsx2x6GameSession(
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
            if (_profile.EmulatorType != EmulatorType.pcsx2x6)
                throw new InvalidOperationException(
                    "This profile is not configured for PCSX2X6.");
            if (_emuOnly)
                throw new NotSupportedException(
                    "PCSX2X6 emulator-only launch is not available on Android.");
            if (_isTest && _profile.HasSeparateTestMode)
                throw new NotSupportedException(
                    "This PCSX2X6 profile has no Android test-mode manifest.");

            var winlatorOwner = GameSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(winlatorOwner))
                throw new InvalidOperationException(
                    $"{winlatorOwner} already owns the Android game session.");

            Pcsx2x6SessionService.StatusChanged += OnServiceStatusChanged;
            var activeProfile =
                Pcsx2x6SessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(activeProfile))
            {
                if (!string.Equals(
                        activeProfile,
                        _profile.ProfileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{activeProfile} already owns the PCSX2X6 session.");
                }

                OutputReceived?.Invoke(
                    "[AndroidSession] Reattached to the foreground PCSX2X6 session.");
                StateChanged?.Invoke("Restoring PCSX2X6 arcade session");
                _ = Pcsx2x6SessionService.TryRestoreActiveProfileName(_context);
                OnServiceStatusChanged(Pcsx2x6SessionService.CurrentStatus);
                return true;
            }

            var manifestPath = ResolveManifestPath(_profile);
            OutputReceived?.Invoke(
                $"[AndroidSession] PCSX2X6 manifest={manifestPath}; " +
                "input=TeknoParrot shared page and arcade overlay");
            StateChanged?.Invoke("Starting PCSX2X6 arcade session");
            _context.StartForegroundService(
                Pcsx2x6SessionService.CreateStartIntent(
                    _context,
                    _profile.ProfileName ?? _profile.GameNameInternal ?? "PCSX2X6",
                    manifestPath));
            return true;
        }
        catch (Exception error)
        {
            var message = "Android PCSX2X6 launch failed: " + error.Message;
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
        StateChanged?.Invoke("Stopping PCSX2X6 arcade session");
        var intent = new Intent(_context, typeof(Pcsx2x6SessionService));
        intent.SetAction(Pcsx2x6SessionService.StopAction);
        _context.StartService(intent);
    }

    public void Dispose() =>
        Pcsx2x6SessionService.StatusChanged -= OnServiceStatusChanged;

    private void OnServiceStatusChanged(string status)
    {
        OutputReceived?.Invoke("[AndroidSession] " + status);
        if (status.StartsWith("state=starting", StringComparison.Ordinal))
            StateChanged?.Invoke("Opening PCSX2X6");
        else if (status.StartsWith("state=accepted", StringComparison.Ordinal))
            StateChanged?.Invoke("PCSX2X6 accepted the game");
        else if (status.StartsWith("state=restoring", StringComparison.Ordinal))
            StateChanged?.Invoke("Restoring PCSX2X6 arcade session");
        else if (status.StartsWith("state=running", StringComparison.Ordinal))
            StateChanged?.Invoke("PCSX2X6 arcade game is running");
        else if (status.StartsWith("state=stopping", StringComparison.Ordinal))
            StateChanged?.Invoke("Stopping PCSX2X6 arcade session");
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
        Pcsx2x6SessionService.StatusChanged -= OnServiceStatusChanged;
        Exited?.Invoke(exitCode);
    }

    internal static string ResolveManifestPath(GameProfile profile)
    {
        var executableName = profile.ExecutableName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executableName) ||
            !executableName.EndsWith(".acgame", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFileName(executableName),
                executableName,
                StringComparison.Ordinal) ||
            executableName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException(
                "The PCSX2X6 profile does not contain a canonical .acgame manifest name.");
        }

        return GameRoot + "/" + executableName;
    }
}
