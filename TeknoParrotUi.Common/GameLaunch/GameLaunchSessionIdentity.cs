using System;
using System.Diagnostics;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// A unique, per-launch session token. Every <see cref="GameSession"/>
    /// launch creates exactly one of these (never stored in global mutable
    /// state, never reused) and injects it as the TP_LAUNCH_SESSION_ID
    /// environment variable into the original Wine/Proton ProcessStartInfo
    /// BEFORE any Gamescope wrapping - so the Gamescope wrapper AND every
    /// process it spawns (gamescopereaper, wine, the loader, the game, any
    /// helper the game itself launches) inherit the exact same token.
    ///
    /// This is the ONLY reliable way to associate Wine/Proton processes with
    /// a specific launch on Linux: parent-child /proc traversal alone breaks
    /// as soon as a process forks, double-forks/detaches, or reparents to
    /// init/wineserver - but the inherited environment survives all of that.
    /// See <see cref="Proton.ProcSessionProcessLocator"/> for the
    /// /proc/&lt;pid&gt;/environ-based discovery that consumes this token.
    /// </summary>
    public sealed class GameLaunchSessionIdentity
    {
        public Guid SessionId { get; }

        public string EnvironmentVariableName => "TP_LAUNCH_SESSION_ID";

        public string EnvironmentVariableValue => SessionId.ToString("D");

        public GameLaunchSessionIdentity(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public static GameLaunchSessionIdentity Create()
        {
            return new GameLaunchSessionIdentity(Guid.NewGuid());
        }

        /// <summary>
        /// Adds the token to <paramref name="info"/>'s environment. No-op
        /// (returns false) when the ProcessStartInfo uses shell execution -
        /// modifying EnvironmentVariables there would make Process.Start
        /// throw, and shell-executed launches (a Windows-only situation in
        /// this codebase) never get wrapped session tracking anyway.
        /// </summary>
        public bool TryApplyTo(ProcessStartInfo info)
        {
            if (info == null || info.UseShellExecute)
                return false;
            info.Environment[EnvironmentVariableName] = EnvironmentVariableValue;
            return true;
        }
    }
}
