namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Linux fullscreen presentation/scaling behaviour for the Gamescope
    /// wrapper (see <see cref="GamescopeLauncher"/>). Resolution-agnostic:
    /// this never encodes a game's actual width/height - the game always
    /// picks its own resolution and Gamescope scales the resulting surface.
    ///
    /// - <see cref="Default"/>: only meaningful as a PER-GAME value - inherit
    ///   whatever the global setting currently is. Never valid as the global
    ///   setting itself (there's nothing further for the global value to
    ///   inherit from) - see <see cref="ParrotData.FullscreenScalingMode"/>.
    /// - <see cref="AutomaticFit"/>: wrap the launch with Gamescope
    ///   (`-W &lt;monitor width&gt; -H &lt;monitor height&gt; -f --force-windows-fullscreen`),
    ///   letting the game choose its own resolution while Gamescope scales
    ///   the resulting surface to fill the monitor, preserving aspect ratio.
    /// - <see cref="Disabled"/>: exact original (unwrapped) launch path -
    ///   the compatibility fallback for games that dislike forced fullscreen
    ///   behaviour (focus issues, multi-window titles, input quirks, etc.).
    /// </summary>
    public enum LinuxFullscreenScalingMode
    {
        Default,
        AutomaticFit,
        Disabled
    }
}
