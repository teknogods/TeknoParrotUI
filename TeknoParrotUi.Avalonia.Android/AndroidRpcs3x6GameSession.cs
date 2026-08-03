using System;
using System.Threading;
using Android.Content;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Android;

internal sealed class AndroidRpcs3x6GameSession : IGameSession
{
    private readonly Context _context;
    private readonly GameProfile _profile;
    private readonly bool _isTest;
    private readonly bool _emuOnly;
    private int _started;
    private int _completed;

    internal AndroidRpcs3x6GameSession(Context context, GameProfile profile, bool isTest, bool emuOnly)
    {
        _context = context.ApplicationContext ?? context;
        _profile = profile;
        _isTest = isTest;
        _emuOnly = emuOnly;
        OutputReceived += GameSessionLogArchive.Append;
        StateChanged += value => GameSessionLogArchive.Append("[state] " + value);
        Exited += GameSessionLogArchive.EndRun;
    }

    public event Action<string>? OutputReceived;
    public event Action<string>? StateChanged;
    public event Action<int>? Exited;

    public bool Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return false;
        GameSessionLogArchive.BeginRun(_profile);
        try
        {
            if (!PlatformCapabilities.IsAndroidRpcs3ProfileSupported(_profile))
                throw new InvalidOperationException("This RPCS3 profile has not been qualified for RPCS3X6 Android.");
            if (_emuOnly) throw new NotSupportedException("RPCS3X6 emulator-only launch is unavailable on Android.");
            if (_isTest && _profile.HasSeparateTestMode)
                throw new NotSupportedException("This RPCS3X6 profile has no separate Android test executable.");
            var otherOwner = DolphinSessionService.TryGetActiveProfileName(_context) ??
                Pcsx2x6SessionService.TryGetActiveProfileName(_context) ??
                GameSessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(otherOwner))
                throw new InvalidOperationException($"{otherOwner} already owns the Android game session.");
            var gamePath = _profile.GamePath?.Trim() ?? string.Empty;
            if (!AndroidRpcs3x6GamePath.IsConfigured(gamePath))
                throw new InvalidOperationException(
                    "Select this RPCS3 game's EBOOT.BIN in Game Settings.");

            Rpcs3x6SessionService.StatusChanged += OnStatus;
            var active = Rpcs3x6SessionService.TryGetActiveProfileName(_context);
            if (!string.IsNullOrWhiteSpace(active))
            {
                if (!string.Equals(active, _profile.ProfileName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"{active} already owns the RPCS3X6 session.");
                _ = Rpcs3x6SessionService.TryRestoreActiveProfileName(_context);
                OnStatus(Rpcs3x6SessionService.CurrentStatus);
                return true;
            }
            OutputReceived?.Invoke($"[AndroidSession] RPCS3X6 EBOOT={gamePath}; input=profile-specific USIO arcade overlay");
            StateChanged?.Invoke("Starting RPCS3X6 arcade session");
            _context.StartForegroundService(Rpcs3x6SessionService.CreateStartIntent(
                _context, _profile.ProfileName ?? _profile.GameNameInternal ?? "RPCS3X6",
                _profile.ProfileName ?? string.Empty, gamePath));
            return true;
        }
        catch (Exception error)
        {
            StateChanged?.Invoke("Android RPCS3X6 launch failed: " + error.Message);
            Complete(-1);
            return false;
        }
    }

    public void ForceQuit()
    {
        var intent = new Intent(_context, typeof(Rpcs3x6SessionService));
        intent.SetAction(Rpcs3x6SessionService.StopAction);
        _context.StartService(intent);
    }

    public void Dispose() => Rpcs3x6SessionService.StatusChanged -= OnStatus;

    private void OnStatus(string status)
    {
        OutputReceived?.Invoke("[AndroidSession] " + status);
        if (status.StartsWith("state=running", StringComparison.Ordinal)) StateChanged?.Invoke("RPCS3X6 arcade game is running");
        else if (status.StartsWith("state=stopping", StringComparison.Ordinal)) StateChanged?.Invoke("Stopping RPCS3X6 arcade session");
        else if (status.StartsWith("state=stopped", StringComparison.Ordinal)) Complete(0);
        else if (status.StartsWith("state=fault", StringComparison.Ordinal)) Complete(-1);
    }

    private void Complete(int code)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0) return;
        Rpcs3x6SessionService.StatusChanged -= OnStatus;
        Exited?.Invoke(code);
    }
}
