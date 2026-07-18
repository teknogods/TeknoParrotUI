using System;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Central platform feature policy shared by the UI and launch layer.
    /// Android game execution is available only when its platform head has
    /// registered the Winlator-backed session factory.
    /// </summary>
    public static class PlatformCapabilities
    {
        public const string AndroidLaunchUnavailableMessage =
            "The Android game-launch backend is unavailable. Install a compatible TeknoParrot Winlator companion and restart TeknoParrot.";

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
        /// Winlator cores and the external PCSX2X6 companion. OpenParrot uses
        /// the profile's Is64Bit flag to select OpenParrotWin32 or
        /// OpenParrotx64, so one emulator type intentionally covers both.
        /// </summary>
        public static bool IsAndroidGameProfileSupported(GameProfile profile) =>
            profile != null &&
            profile.EmulatorType is EmulatorType.OpenParrot or EmulatorType.pcsx2x6;
    }
}
