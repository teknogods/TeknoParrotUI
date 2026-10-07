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
        public string AnnouncementSourceUrl { get; set; } = "https://teknoparrot.com/Home/NewsPostUrl";
        // Last successfully displayed announcement feed text, not the page HTML.
        public string LastAnnouncementContent { get; set; } = "";

        public bool ConfirmExit { get; set; } = true;
        public bool DownloadIcons { get; set; } = true;
        public bool UiDisableHardwareAcceleration { get; set; } = false;
        public bool HideVanguardWarning { get; set; } = false;

        public string UiColour { get; set; } = "lightblue";
        public bool UiDarkMode { get; set; } = false;
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
        // Golden Tee online: the PCB ID and Card ID the teknoparrot.com account issues (api/User/Profile)
        public string GoldenTeePcbId { get; set; } = "";
        public string GoldenTeeCardId { get; set; } = "";
        // Initial D Online machine credential (PCB ID + secret) of this PC, from the teknoparrot.com account
        // (Account page, Initial D Online). Plain text by design, like the other online ids; it is copied into the
        // [Network] OnlineID / OnlineSecret fields of the Initial D profiles. Not cleared on logout: the PC stays
        // registered until the user removes or regenerates it.
        public string InitialDOnlineId { get; set; } = "";
        public string InitialDOnlineSecret { get; set; } = "";
        // Senjou no Kizuna Online: this PC's own Kizuna PCB ID (AAKZ-...) + secret from the same account (Account page,
        // Senjou no Kizuna Online), a pair apart from Initial D's. Kept the same way and copied into the [Network]
        // OnlineID / OnlineSecret fields of the Kizuna profiles (OnlineIdType.Kizuna).
        public string KizunaOnlineId { get; set; } = "";
        public string KizunaOnlineSecret { get; set; } = "";
        // Initial D matchmaking always uses automatic server selection. Keep the one-time notice,
        // registration prompt preference and migrated old-loader profile names. Old serialized
        // InitialDConnectToServer values are ignored when existing settings are loaded.
        public bool InitialDUnifiedNoticeShown { get; set; } = false;
        public bool InitialDRegisterPromptOff { get; set; } = false;
        public string InitialDOldLoaderMigrated { get; set; } = "";
        // Initial D LAN linking (UNIFIED_MODE.md 3.3 / 3.4, LAN_AGENT.md 7): the install id of this PC (32 hex, made
        // once and also handed to the game in TP_LAN_INSTALL, so a PC is one install to its peers whether its game or
        // its launcher is beaconing), the kept answers of the link question ("<install id>=always|never", comma
        // separated, at most 64), and whether the inbound firewall rules were offered once.
        public string InitialDLanInstallId { get; set; } = "";
        public string InitialDLanConsent { get; set; } = "";
        public bool InitialDLanFirewallAsked { get; set; } = false;
        public string Language { get; set; } = "en";
        public bool HideDolphinGUI { get; set; } = false;
        // Disable the "Are you sure you want to delete this game?" prompt
        public bool ConfirmGameDeletion { get; set; } = true;
    }
}
