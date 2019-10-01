using System;

namespace TeknoParrotUi.Common
{
    public enum UiColour
    {
        // default
        LIGHTBLUE,
        RED,
        LIMEGREEN
    }

    public class ParrotData
    {
        public bool UseSto0ZDrivingHack { get; set; }
        public int StoozPercent { get; set; }
        public int GunSensitivityPlayer1 { get; set; }
        public int GunSensitivityPlayer2 { get; set; }
        public bool FullAxisGas { get; set; }
        public bool FullAxisBrake { get; set; }
        public bool ReverseAxisGas { get; set; }
        public bool ReverseAxisBrake { get; set; }

        public bool SaveLastPlayed { get; set; }
        public string LastPlayed { get; set; }

        public bool UseDiscordRPC { get; set; }
        public bool SilentMode { get; set; }
        public bool CheckForUpdates { get; set; } = true;

        public bool ConfirmExit { get; set; } = true;
        public bool DownloadIcons { get; set; } = true;
        public bool UiDisableHardwareAcceleration { get; set; } = false;

        public UiColour UiColour { get; set; } = UiColour.LIGHTBLUE;
    }
}
