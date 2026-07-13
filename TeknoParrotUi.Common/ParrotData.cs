using System;
using System.ComponentModel;

namespace TeknoParrotUi.Common
{
    public class ParrotData
    {
        public bool UseSto0ZDrivingHack { get; set; }
        public int StoozPercent { get; set; }
        public bool FullAxisGas { get; set; }
        public bool FullAxisBrake { get; set; }
        public bool ReverseAxisGas { get; set; }
        public bool ReverseAxisBrake { get; set; }

        public string LastPlayed { get; set; }
        public string ExitGameKey { get; set; } = "0x1B";
        public string PauseGameKey { get; set; } = "0x13";

        public string ScoreSubmissionID { get; set; }
        public string ScoreCollapseGUIKey { get; set; } = "0x79";

        public bool SaveLastPlayed { get; set; }

        public bool UseDiscordRPC { get; set; }
        public bool SilentMode { get; set; }
        public bool CheckForUpdates { get; set; } = true;

        public bool ConfirmExit { get; set; } = true;
        public bool DownloadIcons { get; set; } = true;
        public bool UiDisableHardwareAcceleration { get; set; } = false;
        public bool HideVanguardWarning { get; set; } = false;

        public string UiColour { get; set; } = "lightblue";
        public bool UiDarkMode { get; set; } = false;
        public bool UiFollowSystemTheme { get; set; } = false;
        public bool UiHolidayThemes { get; set; } = true;
        [DefaultValue("Ethernet")]
        public string Elfldr2NetworkAdapterName { get; set; } = "";
        public bool HasReadPolicies { get; set; }
        public bool HasReadPoliciesNew { get; set; }
        public bool DisableAnalytics { get; set; } = false;
        public bool Elfldr2LogToFile { get; set; } = false;
        public string DatXmlLocation { get; set; } = "";
        public bool FirstTimeSetupComplete { get; set; } = false;
        public bool IsLoggedIn { get; set; } = false;
        // These are set via the account login and shouldn't be manually modified
        // They're here so we can prefill the ids in game profiles automatically
        public string SegaId {get; set; } = "";
        public string NamcoId { get; set; } = "";
        public string MarioKartId { get; set; } = "";
        public string Language { get; set; } = "en";
        public bool HideDolphinGUI { get; set; } = false;
        // Disable the "Are you sure you want to delete this game?" prompt
        public bool ConfirmGameDeletion { get; set; } = true;

        // Linux only: explicit wine/Proton binary path (overrides auto-detection
        // in ProtonLauncher.ResolveWineBinary), for systems where wine lives
        // somewhere other than /usr/bin/wine or the packaged Proton directory.
        public string CustomWinePath { get; set; } = "";

        /// <summary>
        /// Linux only: global default Wine/Proton PREFIX mode for games that
        /// don't set an explicit per-game override (GameProfile.WinePrefixMode
        /// is null or Default) - see Proton.WinePrefixManager. Starts at Shared
        /// so ordinary new games reuse a common environment instead of each
        /// getting their own ~1.5 GB prefix; only Shared/Isolated are valid here
        /// (Default doesn't make sense as a GLOBAL setting - there's nothing
        /// further for it to inherit from).
        /// </summary>
        public Proton.WinePrefixMode DefaultWinePrefixMode { get; set; } = Proton.WinePrefixMode.Shared;

        /// <summary>
        /// Linux only: global default for the Gamescope automatic fullscreen
        /// scaling feature (see <see cref="Proton.GamescopeLauncher"/>). Only
        /// <see cref="Proton.LinuxFullscreenScalingMode.AutomaticFit"/> and
        /// <see cref="Proton.LinuxFullscreenScalingMode.Disabled"/> are valid
        /// values here - <c>Default</c> makes no sense as a global setting
        /// (nothing further to inherit from) and is never written by the UI.
        ///
        /// Deliberately nullable with NO field initializer, distinct from
        /// <see cref="DefaultWinePrefixMode"/>'s non-nullable pattern: the
        /// migration concern here is at the whole-SETTINGS-FILE level (an
        /// existing ParrotData.xml saved before this feature existed simply
        /// lacks the element and deserializes to null), not a per-profile
        /// concern. Resolution treats null conservatively as
        /// <see cref="Proton.LinuxFullscreenScalingMode.Disabled"/>.
        ///
        /// EXPERIMENTAL - stays null (-&gt; Disabled) for BOTH genuinely new
        /// installs AND pre-existing ones (see <see cref="JoystickHelper.DeSerialize"/>)
        /// until the feature's central "Gamescope automatically scales a
        /// fixed-size game surface" assumption is validated against a real
        /// TeknoParrot game's full loader/JVS pipeline, multiple GPU vendors,
        /// and lightgun/pointer input - controlled Win32/Wine probe testing
        /// already corrected the command itself (dropped the broken
        /// `--force-windows-fullscreen` assumption for `-S fit`) but that is
        /// not yet enough evidence to make this the default. Users may still
        /// explicitly opt in via the Linux Setup page; <c>TP_GAMESCOPE=1</c>
        /// still forces it for testing. Only flip the default to
        /// <c>AutomaticFit</c> in a separate, clearly justified commit once
        /// that validation succeeds.
        /// </summary>
        public Proton.LinuxFullscreenScalingMode? FullscreenScalingMode { get; set; }
    }
}
