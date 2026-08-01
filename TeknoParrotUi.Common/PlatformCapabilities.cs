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
        /// Winlator cores plus the external PCSX2X6 and TeknoDolphin companions. OpenParrot uses
        /// the profile's Is64Bit flag to select OpenParrotWin32 or
        /// OpenParrotx64, so one emulator type intentionally covers both. TeknoDolphin is
        /// deliberately limited to the five qualified Triforce profiles below.
        /// </summary>
        public static bool IsAndroidGameProfileSupported(GameProfile profile) =>
            profile != null &&
            (profile.EmulatorType is EmulatorType.OpenParrot or EmulatorType.pcsx2x6 ||
             IsAndroidDolphinProfileSupported(profile));

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
