namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Linux fullscreen presentation/scaling behaviour for the Gamescope
    /// wrapper (see <see cref="GamescopeLauncher"/>). Resolution-agnostic:
    /// this never encodes a game's actual width/height - the game always
    /// picks its own resolution and Gamescope scales the resulting surface.
    ///
    /// EXPERIMENTAL: <see cref="AutomaticFit"/>'s command was corrected after
    /// controlled Win32/Wine probe testing proved the original
    /// `--force-windows-fullscreen`-based command did NOT actually scale a
    /// fixed-resolution game surface (it only forced the window's own
    /// dimensions, leaving non-adaptive content pinned in a corner). The
    /// corrected command (`-S fit`, no `--force-windows-fullscreen`, explicit
    /// `--backend sdl`) was verified to genuinely scale a fixed-canvas Win32
    /// test client, preserving aspect ratio, centering, and following runtime
    /// resolution changes - but this has NOT yet been confirmed against a
    /// real TeknoParrot game's full loader/JVS pipeline, multiple GPU
    /// vendors, or lightgun/pointer input. Both the global and per-game
    /// defaults therefore stay <see cref="Disabled"/> until that validation
    /// happens (see <see cref="ParrotData.FullscreenScalingMode"/>).
    ///
    /// - <see cref="Default"/>: only meaningful as a PER-GAME value - inherit
    ///   whatever the global setting currently is. Never valid as the global
    ///   setting itself (there's nothing further for the global value to
    ///   inherit from) - see <see cref="ParrotData.FullscreenScalingMode"/>.
    /// - <see cref="AutomaticFit"/>: wrap the launch with Gamescope
    ///   (`-W &lt;monitor width&gt; -H &lt;monitor height&gt; -S fit -f --backend sdl`),
    ///   letting the game choose its own resolution while Gamescope scales
    ///   the resulting surface to fill the monitor, preserving aspect ratio.
    /// - <see cref="Disabled"/>: exact original (unwrapped) launch path -
    ///   the compatibility fallback for games that dislike forced fullscreen
    ///   behaviour (focus issues, multi-window titles, input quirks, etc.),
    ///   and currently also the default everywhere until AutomaticFit is
    ///   validated (see above).
    /// </summary>
    public enum LinuxFullscreenScalingMode
    {
        Default,
        AutomaticFit,
        Disabled
    }
}
