using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Drives one Gamescope-wrapped session to completion around the pure
    /// <see cref="WrappedGameLifecycleStateMachine"/>: polls, discovers
    /// session processes (locator), classifies them, and executes
    /// termination decisions through the ONE centralized
    /// <see cref="IGameSessionTerminator"/>. Every side-effecting dependency
    /// (locator, terminator, clock, delay, force-quit flag, wrapper
    /// liveness) is injected, so the FULL production loop - including
    /// "cleanup exactly once" behavior - runs unmodified in unit tests with
    /// fakes.
    /// </summary>
    public sealed class WrappedGameLifecycleRunner
    {
        private readonly WrappedGameLifecycleOptions _options;
        private readonly IGameSessionProcessLocator _locator;
        private readonly IGameSessionTerminator _terminator;
        private readonly GameLaunchSessionIdentity _identity;
        private readonly GameExecutableExpectations _expectations;
        private readonly SessionProcessInfo _wrapperDescriptor;
        private readonly Func<bool> _forceQuitRequested;
        private readonly Func<bool> _wrapperAlive;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Action<string> _log;
        private readonly Action<SessionProcessInfo> _onConfirmedGameChanged;

        public WrappedGameLifecycleRunner(
            WrappedGameLifecycleOptions options,
            IGameSessionProcessLocator locator,
            IGameSessionTerminator terminator,
            GameLaunchSessionIdentity identity,
            GameExecutableExpectations expectations,
            SessionProcessInfo wrapperDescriptor,
            Func<bool> forceQuitRequested,
            Func<bool> wrapperAlive,
            Func<DateTimeOffset> clock = null,
            Func<TimeSpan, CancellationToken, Task> delay = null,
            Action<string> log = null,
            Action<SessionProcessInfo> onConfirmedGameChanged = null)
        {
            _options = options ?? new WrappedGameLifecycleOptions();
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _terminator = terminator ?? throw new ArgumentNullException(nameof(terminator));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _expectations = expectations ?? new GameExecutableExpectations();
            _wrapperDescriptor = wrapperDescriptor ?? throw new ArgumentNullException(nameof(wrapperDescriptor));
            _forceQuitRequested = forceQuitRequested ?? (() => false);
            _wrapperAlive = wrapperAlive ?? (() => false);
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _delay = delay ?? Task.Delay;
            _log = log ?? (_ => { });
            _onConfirmedGameChanged = onConfirmedGameChanged ?? (_ => { });
        }

        public async Task<WrappedGameLifecycleResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var machine = new WrappedGameLifecycleStateMachine(_options, _log);
            SessionProcessInfo lastReportedGame = null;

            _log($"[GameSessionLifecycle] SessionId: {_identity.EnvironmentVariableValue}. WrapperPid: {_wrapperDescriptor.Pid}. " +
                 $"WrapperStartTimeTicks: {(_wrapperDescriptor.StartTimeTicks?.ToString() ?? "unknown")}. " +
                 $"ExpectedPrimaryExecutable: {NameOrNone(_expectations.PrimaryExecutable)}. " +
                 $"ExpectedSecondaryExecutable: {NameOrNone(_expectations.SecondaryExecutable)}.");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var observation = new WrappedSessionObservation
                {
                    Now = _clock(),
                    ForceQuitRequested = _forceQuitRequested(),
                    WrapperAlive = _wrapperAlive(),
                    SessionProcesses = DiscoverAndClassify()
                };

                var decision = machine.Advance(observation);

                if (machine.ConfirmedGame != null && !machine.ConfirmedGame.SameIdentityAs(lastReportedGame))
                {
                    lastReportedGame = machine.ConfirmedGame;
                    _onConfirmedGameChanged(lastReportedGame);
                }

                switch (decision.Action)
                {
                    case LifecycleTickActionKind.TerminateSession:
                        SessionTerminationResult terminationResult;
                        try
                        {
                            terminationResult = await _terminator.TerminateSessionAsync(
                                _identity, _wrapperDescriptor, observation.SessionProcesses,
                                decision.TerminationReason, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log($"[GameSessionTermination] Termination threw: {ex.Message}");
                            terminationResult = null;
                        }
                        machine.NotifyTerminationCompleted(terminationResult, decision.TerminationReason);
                        return machine.BuildResult();

                    case LifecycleTickActionKind.CompleteSession:
                        return machine.BuildResult();

                    case LifecycleTickActionKind.None:
                    default:
                        break;
                }

                try { await _delay(_options.PollInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
            }
        }

        private IReadOnlyList<SessionProcessInfo> DiscoverAndClassify()
        {
            IReadOnlyList<SessionProcessInfo> raw;
            try
            {
                raw = _locator.FindSessionProcesses(_identity);
            }
            catch (Exception ex)
            {
                _log($"[GameSessionLifecycle] Session process discovery failed this tick: {ex.Message}");
                return Array.Empty<SessionProcessInfo>();
            }

            return raw.Select(p => p.Confidence == GameProcessConfidence.None
                    ? p.WithConfidence(GameProcessClassifier.Classify(p, _expectations))
                    : p)
                .ToList();
        }

        private static string NameOrNone(string value) => string.IsNullOrEmpty(value) ? "(none)" : ExecutableNameMatcher.NormalizeBaseName(value);
    }
}
