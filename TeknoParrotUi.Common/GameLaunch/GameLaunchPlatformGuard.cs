using System;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Extracted, directly-testable production implementation of the Linux
    /// unsupported-host launch gate used as the very first statement of
    /// <see cref="GameSession.StartInner"/>. Kept as its own tiny pure helper
    /// (rather than only living inline in <see cref="GameSession"/>) because
    /// <see cref="GameSession"/> itself has heavy real-world dependencies
    /// (serial port handler, input listeners manager, actual process launch)
    /// that make constructing one purely to test this one gate impractical -
    /// tests call <see cref="ThrowIfUnsupported"/> directly instead.
    /// </summary>
    internal static class GameLaunchPlatformGuard
    {
        /// <summary>
        /// Throws <see cref="PlatformNotSupportedException"/> (with the
        /// centralized <see cref="ProtonPackageManager.UnsupportedHostMessage"/>)
        /// when <paramref name="isLinux"/> is true and <paramref name="hostArchitecture"/>
        /// isn't supported (see <see cref="ProtonPackageManager.IsSupportedHost"/>).
        /// Direct use of the desktop <see cref="GameSession"/> is rejected on
        /// Android; Android callers must enter through <see cref="GameSessionFactory"/>
        /// so the Winlator backend owns the session.
        /// Windows remains a no-op regardless of architecture.
        ///
        /// Policy: Linux ARM64 is unsupported for every TeknoParrot game-session
        /// launch mode, including emulation-only launch, until an x86/x86_64
        /// translation backend is implemented.
        /// </summary>
        internal static void ThrowIfUnsupported(bool isLinux, Architecture hostArchitecture, bool isAndroid = false)
        {
            if (isAndroid)
                throw new PlatformNotSupportedException(PlatformCapabilities.AndroidLaunchUnavailableMessage);
            if (isLinux && !ProtonPackageManager.IsSupportedHost(hostArchitecture))
                throw new PlatformNotSupportedException(ProtonPackageManager.UnsupportedHostMessage);
        }
    }
}
