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
        public bool UiHolidayThemes { get; set; } = true;
        [DefaultValue("Ethernet")]
        public string Elfldr2NetworkAdapterName { get; set; } = "";
        public bool HasReadPolicies { get; set; }
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
    }
}
