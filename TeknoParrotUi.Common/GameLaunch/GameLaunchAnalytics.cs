using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Sends the same anonymous five-minute launch qualification used by the
    /// legacy Windows UI, with an explicit host operating system.
    /// </summary>
    internal static class GameLaunchAnalytics
    {
        private const string StartEndpoint =
            "https://teknoparrot.com/Home/SimpleAnonData";
        private const string EndEndpoint =
            "https://teknoparrot.com/Home/SimpleAnonEnd";
        private static readonly TimeSpan QualifiedSessionLength =
            TimeSpan.FromMinutes(5);
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        private static readonly ConcurrentDictionary<string, ActiveSession> ActiveSessions =
            new ConcurrentDictionary<string, ActiveSession>(StringComparer.OrdinalIgnoreCase);

        internal static string CurrentOperatingSystem
        {
            get
            {
                if (OperatingSystem.IsAndroid())
                    return "Android";
                if (OperatingSystem.IsWindows())
                    return "Windows";
                if (OperatingSystem.IsLinux())
                    return "Linux";
                if (OperatingSystem.IsMacOS())
                    return "macOS";
                return "Other";
            }
        }

        internal static Uri BuildStartUri(
            string gameName,
            EmulatorType emulatorType,
            string operatingSystem)
        {
            var normalizedName = gameName ?? string.Empty;
            if (normalizedName.Length > 30)
                normalizedName = normalizedName.Substring(0, 30);

            return new Uri(
                $"{StartEndpoint}?emulatorModule={(int)emulatorType}" +
                $"&gameName={Uri.EscapeDataString(normalizedName)}" +
                $"&operatingSystem={Uri.EscapeDataString(operatingSystem ?? string.Empty)}");
        }

        internal static void Start(string gameName, EmulatorType emulatorType)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return;

            var key = BuildKey(gameName, emulatorType);
            if (ActiveSessions.ContainsKey(key))
                return;

            var session = new ActiveSession(gameName, emulatorType);
            if (ActiveSessions.TryAdd(key, session))
                session.Start();
        }

        internal static void Stop(string gameName, EmulatorType emulatorType)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return;

            if (ActiveSessions.TryRemove(BuildKey(gameName, emulatorType), out var session))
                session.Cancel();
        }

        private static string BuildKey(string gameName, EmulatorType emulatorType) =>
            $"{(int)emulatorType}:{gameName}";

        private sealed class ActiveSession
        {
            private readonly string _gameName;
            private readonly EmulatorType _emulatorType;
            private readonly CancellationTokenSource _cancellation =
                new CancellationTokenSource();

            internal ActiveSession(string gameName, EmulatorType emulatorType)
            {
                _gameName = gameName;
                _emulatorType = emulatorType;
            }

            internal void Start() => _ = RunAsync();

            internal void Cancel()
            {
                try
                {
                    _cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // A completed request can race a process-exit notification.
                }
            }

            private async Task RunAsync()
            {
                try
                {
                    var token = _cancellation.Token;
                    var startUri = BuildStartUri(
                        _gameName,
                        _emulatorType,
                        CurrentOperatingSystem);
                    var sessionId = (await Client.GetStringAsync(startUri, token)
                            .ConfigureAwait(false))
                        .Trim();
                    if (string.IsNullOrWhiteSpace(sessionId))
                        return;

                    await Task.Delay(QualifiedSessionLength, token)
                        .ConfigureAwait(false);
                    var endUri = new Uri(
                        $"{EndEndpoint}?generatedGuid={Uri.EscapeDataString(sessionId)}");
                    _ = await Client.GetStringAsync(endUri, token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Includes short sessions and HttpClient timeouts.
                }
                catch (HttpRequestException)
                {
                    // Analytics must never affect launching or playing a game.
                }
                catch (Exception)
                {
                    // Keep analytics strictly best-effort, matching the legacy client.
                }
            }
        }
    }

    /// <summary>
    /// Adds analytics to every desktop and platform-owned session without
    /// coupling the individual launch backends to HTTP.
    /// </summary>
    internal sealed class AnalyticsGameSession : IGameSession
    {
        private readonly IGameSession _inner;
        private readonly string _gameName;
        private readonly EmulatorType _emulatorType;
        private int _started;
        private int _exited;
        private int _disposed;

        internal AnalyticsGameSession(IGameSession inner, GameProfile profile)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            _gameName = profile.ProfileName ?? profile.GameNameInternal ?? string.Empty;
            _emulatorType = profile.EmulatorType;
            _inner.OutputReceived += OnOutputReceived;
            _inner.StateChanged += OnStateChanged;
            _inner.Exited += OnExited;
        }

        public event Action<string> OutputReceived;
        public event Action<string> StateChanged;
        public event Action<int> Exited;

        public bool Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                return false;

            var started = _inner.Start();
            if (started && Volatile.Read(ref _exited) == 0)
                GameLaunchAnalytics.Start(_gameName, _emulatorType);
            return started;
        }

        public void ForceQuit()
        {
            GameLaunchAnalytics.Stop(_gameName, _emulatorType);
            _inner.ForceQuit();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Disposing an Android view only detaches from its foreground
            // service; it does not end the game. Completion/ForceQuit is the
            // authoritative point at which a pending analytic is cancelled.
            _inner.OutputReceived -= OnOutputReceived;
            _inner.StateChanged -= OnStateChanged;
            _inner.Exited -= OnExited;
            _inner.Dispose();
        }

        private void OnOutputReceived(string line) => OutputReceived?.Invoke(line);

        private void OnStateChanged(string state) => StateChanged?.Invoke(state);

        private void OnExited(int exitCode)
        {
            Interlocked.Exchange(ref _exited, 1);
            GameLaunchAnalytics.Stop(_gameName, _emulatorType);
            Exited?.Invoke(exitCode);
        }
    }
}
