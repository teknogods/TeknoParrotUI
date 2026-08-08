namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Holds which player is currently allowed to have their input accepted as a new binding,
    /// during controller setup. Meant for tournament-style setups with multiple streamed
    /// players connected at once - without this, everyone's input capture at once, so whoever
    /// happens to press a button first during "press any key" wins the binding, regardless of
    /// who the host actually meant to configure.
    ///
    /// Shared, static, process-wide: both JoystickControl (single-game setup) and
    /// MultiGameButtonConfig (bulk/tournament setup) route their capture through the same
    /// underlying JoystickControlRawInput/JoystickControlXInput/JoystickControlDirectInput
    /// classes, so setting this here applies everywhere capture happens, regardless of which
    /// screen is open.
    /// </summary>
    public static class ActiveCaptureSource
    {
        /// <summary>
        /// Player slot 1 = the host's own local keyboard/mouse/controller, matching
        /// SunshinePlayerInput's existing convention. 0 = no restriction (any connected
        /// source's input is accepted - the original, unrestricted behavior).
        /// </summary>
        public const int Any = 0;
        public const int Host = 1;

        /// <summary>
        /// The currently-allowed player. Defaults to Any, so nothing changes unless the host
        /// explicitly restricts capture on the setup screen.
        /// </summary>
        public static int AllowedPlayer { get; set; } = Any;

        /// <summary>
        /// Whether input claiming to be from the given player should currently be accepted as
        /// a binding-capture candidate.
        /// </summary>
        public static bool IsAllowed(int player) => AllowedPlayer == Any || AllowedPlayer == player;
    }
}
