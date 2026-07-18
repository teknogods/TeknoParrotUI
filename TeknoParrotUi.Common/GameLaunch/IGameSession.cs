using System;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// UI-facing lifetime for one game run. Desktop uses <see cref="GameSession"/>;
    /// platform heads may register a native backend without introducing their
    /// framework types into the shared UI or Common assembly.
    /// </summary>
    public interface IGameSession : IDisposable
    {
        event Action<string> OutputReceived;
        event Action<string> StateChanged;
        event Action<int> Exited;

        bool Start();
        void ForceQuit();
    }

    public static class GameSessionFactory
    {
        private static readonly object Sync = new object();
        private static Func<GameProfile, bool, bool, IGameSession> _platformFactory;
        private static Func<string> _activeProfileNameProvider;

        public static bool IsPlatformFactoryRegistered
        {
            get
            {
                lock (Sync)
                    return _platformFactory != null;
            }
        }

        public static void RegisterPlatformFactory(
            Func<GameProfile, bool, bool, IGameSession> factory,
            Func<string> activeProfileNameProvider = null)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            lock (Sync)
            {
                _platformFactory = factory;
                _activeProfileNameProvider = activeProfileNameProvider;
            }
        }

        /// <summary>
        /// Reports a durable platform-owned session that the recreated UI can
        /// attach to. Desktop backends deliberately have no active-session
        /// provider because their process owns both the UI and game session.
        /// </summary>
        public static bool TryGetActivePlatformProfileName(out string profileName)
        {
            profileName = string.Empty;
            if (!OperatingSystem.IsAndroid())
                return false;

            Func<string> provider;
            lock (Sync)
                provider = _activeProfileNameProvider;

            var value = provider?.Invoke();
            if (string.IsNullOrWhiteSpace(value))
                return false;
            profileName = value;
            return true;
        }

        public static IGameSession Create(
            GameProfile profile,
            bool isTest = false,
            bool emuOnly = false)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            Func<GameProfile, bool, bool, IGameSession> platformFactory;
            lock (Sync)
                platformFactory = _platformFactory;

            if (OperatingSystem.IsAndroid())
            {
                if (platformFactory == null)
                    throw new PlatformNotSupportedException(
                        PlatformCapabilities.AndroidLaunchUnavailableMessage);
                return platformFactory(profile, isTest, emuOnly);
            }

            return new GameSession(profile, isTest, emuOnly);
        }
    }
}
