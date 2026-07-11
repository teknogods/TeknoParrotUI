using System;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Runtime state for Proton game sessions. The game launcher sets
    /// <see cref="Enabled"/> (and optionally <see cref="CurrentGame"/>) when it
    /// starts a game via Proton, so factories know to create Proton bridges
    /// instead of local pipes. Can also be forced with the TP_PROTON=1
    /// environment variable for manual testing.
    /// </summary>
    public static class ProtonRuntime
    {
        /// <summary>Set by the launcher when the current game runs in Proton.</summary>
        public static bool Enabled { get; set; }

        /// <summary>Detected/launched game process info, if known.</summary>
        public static ProtonGameInfo CurrentGame { get; set; }

        /// <summary>
        /// Expected game executable name (e.g. "Rally.exe") used by process detection.
        /// </summary>
        public static string ExpectedExecutable { get; set; }

        /// <summary>
        /// Wine binary that will run the game (known before the game starts).
        /// Lets bridges create their in-prefix endpoints EARLY - games like
        /// TGM3 probe JVS immediately at boot and never retry.
        /// </summary>
        public static string WineBinary { get; set; }

        /// <summary>Wine prefix the game will run in (known before start).</summary>
        public static string WinePrefix { get; set; }

        /// <summary>
        /// True when Proton bridging should be used for this session.
        /// </summary>
        public static bool IsActive =>
            Enabled || Environment.GetEnvironmentVariable("TP_PROTON") == "1";
    }
}
