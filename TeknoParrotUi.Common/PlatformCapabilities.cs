using System;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Central platform feature policy shared by the UI and launch layer.
    /// Android game execution is available only when its platform head has
    /// registered the platform session factory.
    /// </summary>
    public static class PlatformCapabilities
    {
        public const string AndroidLaunchUnavailableMessage =
            "The Android game-launch backend is unavailable. Install the required TeknoParrot companion and restart TeknoParrot.";

        public static bool IsAndroidShell => OperatingSystem.IsAndroid();
        public static bool CanLaunchGames =>
            !IsAndroidShell || GameLaunch.GameSessionFactory.IsPlatformFactoryRegistered;
        // Android uses its native package installer rather than the desktop
        // ZIP/ParrotPatcher path. The Android head registers that backend
        // before MainView is constructed.
        public static bool CanSelfUpdate =>
            OperatingSystem.IsWindows() ||
            OperatingSystem.IsLinux() ||
            OperatingSystem.IsAndroid();
        public static bool CanManageDesktopComponents => !IsAndroidShell;

        /// <summary>
        /// The first public Android release installs only the open-source
        /// Winlator cores plus the external PCSX2X6, TeknoDolphin, and RPCS3X6 companions. OpenParrot uses
        /// the profile's Is64Bit flag to select OpenParrotWin32 or
        /// OpenParrotx64, so one emulator type intentionally covers both. TeknoDolphin is
        /// deliberately limited to the five qualified Triforce profiles below.
        /// </summary>
        public static bool IsAndroidGameProfileSupported(GameProfile profile)
        {
            if (profile == null)
                return false;
            return profile.EmulatorType is EmulatorType.OpenParrot or EmulatorType.pcsx2x6 ||
                IsAndroidRpcs3ProfileSupported(profile) ||
                IsAndroidDolphinProfileSupported(profile);
        }

        /// <summary>
        /// RPCS3X6 supports only the qualified System 357/369 arcade roots.
        /// Profile names are used because every dump has the same SCEEXE000
        /// title id and must remain isolated in companion-owned storage.
        /// </summary>
        public static bool IsAndroidRpcs3ProfileSupported(GameProfile profile)
        {
            if (profile?.EmulatorType != EmulatorType.RPCS3)
                return false;

            return profile.ProfileName is
                "DarkEscape4D" or
                "DSPS" or
                "dbzenkai" or
                "RazingStorm" or
                "AKB48" or
                "taikogreen" or
                "taikoyellow" or
                "Tekken6" or
                "Tekken6BR" or
                "ttt2" or
                "ttt2u";
        }

        public static bool IsAndroidDolphinProfileSupported(GameProfile profile)
        {
            if (profile?.EmulatorType != EmulatorType.Dolphin)
                return false;

            return profile.EmulationProfile is
                EmulationProfile.MarioKartGP or
                EmulationProfile.MarioKartGP2 or
                EmulationProfile.FZeroAX or
                EmulationProfile.VirtuaStriker3 or
                EmulationProfile.VirtuaStriker4;
        }
    }
}
