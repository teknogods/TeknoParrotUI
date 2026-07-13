using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Manages the optional Proton/Wine runtime packages on Linux.
    ///
    /// Layout (multiple versions can be installed side by side):
    ///   ~/.local/share/TeknoParrotUI/proton/
    ///   ├─ proton-ge-9.26/
    ///   │   └─ bin/wine
    ///   └─ proton-ge-9.27/
    ///       └─ bin/wine
    ///
    /// Games can pin a specific version via GameProfile.ProtonVersion;
    /// otherwise the newest installed version is used.
    ///
    /// pipehelper.exe/pipehelper32.exe are NOT part of this package - they
    /// ship directly next to the app (see publish.sh) since they're a tiny
    /// (~550 KB combined) static build with no reason to be a separate
    /// download. ProtonHelper.ResolveHelperPath() finds them there.
    ///
    /// Architecture safety: some machines end up with both an x86_64 AND an
    /// aarch64/arm64 Proton-GE package installed side by side (e.g. a stray
    /// manual extract, or an older TeknoParrotUI build that didn't filter
    /// downloads by arch - see <see cref="ProtonReleaseManager"/>). Auto
    /// selection (<see cref="ListInstalledVersions"/>/<see cref="ResolveWineBinary(string)"/>)
    /// must never silently pick an incompatible-architecture package just
    /// because it happens to sort first - see <see cref="IsCompatibleProtonPackage"/>.
    ///
    /// Supported hosts: Linux x86_64 ONLY (see <see cref="IsSupportedHost"/>).
    /// TeknoParrot and the Windows game executables it wraps are x86/x86_64;
    /// running them on an ARM64 host would need an x86_64 emulation/
    /// translation layer (FEX, Box64, or similar), which isn't implemented.
    /// Picking an ARM64-native Proton/wine build does not make ARM64 hosts
    /// work - it only lets Wine itself start, not the (still x86/x86_64)
    /// game inside it - so ARM64 is never offered as a solution here, even
    /// when an ARM64 package is installed and would otherwise match the
    /// host CPU.
    /// </summary>
    public static class ProtonPackageManager
    {
        /// <summary>Root directory of installed Proton packages.</summary>
        public static string PackageRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeknoParrotUI", "proton");

        /// <summary>
        /// Installed version directory names (e.g. "GE-Proton11-1"), newest first,
        /// regardless of architecture - includes incompatible packages (the Linux
        /// Setup page lists them all, flagging which ones are usable). For actually
        /// picking a wine binary to run, see <see cref="ResolveWineBinary(string)"/>,
        /// which filters by host architecture.
        /// A version is considered installed when its wine binary exists
        /// (plain builds: bin/wine, Proton dists: files/bin/wine).
        /// </summary>
        public static List<string> ListInstalledVersions() => ListInstalledVersions(PackageRoot);

        /// <summary>
        /// Package-root-parametrized core of <see cref="ListInstalledVersions()"/> -
        /// the single production implementation; the public overload just
        /// supplies the real <see cref="PackageRoot"/>. Internal (via
        /// <c>InternalsVisibleTo</c>) so tests exercise this exact method
        /// against a temporary directory instead of re-implementing directory
        /// enumeration/version ordering themselves.
        /// </summary>
        internal static List<string> ListInstalledVersions(string packageRoot)
        {
            if (!Directory.Exists(packageRoot))
                return new List<string>();

            return Directory.GetDirectories(packageRoot)
                .Where(d => FindWineInVersionDir(d) != null)
                .Select(Path.GetFileName)
                .OrderByDescending(v => v, Comparer<string>.Create(CompareVersionNames))
                .ToList();
        }

        /// <summary>
        /// Installed packages with architecture/compatibility info attached, newest
        /// first - what the Linux Setup page displays ("GE-Proton11-1  x86_64 —
        /// compatible" / "GE-Proton11-1-aarch64  ARM64 — incompatible with this system").
        /// </summary>
        public static List<ProtonPackageInfo> ListInstalledPackages(Architecture? hostArchitecture = null) =>
            ListInstalledPackages(PackageRoot, hostArchitecture ?? RuntimeInformation.OSArchitecture);

        /// <summary>
        /// Package-root-parametrized core of <see cref="ListInstalledPackages(Architecture?)"/> -
        /// the single production implementation (see <see cref="ListInstalledVersions(string)"/>
        /// for why this is internal rather than mirrored in tests).
        /// </summary>
        internal static List<ProtonPackageInfo> ListInstalledPackages(string packageRoot, Architecture hostArchitecture) =>
            ListInstalledVersions(packageRoot)
                .Select(version => BuildPackageInfo(packageRoot, version, hostArchitecture))
                .ToList();

        /// <summary>
        /// Builds a <see cref="ProtonPackageInfo"/> for one installed version -
        /// the single place that combines architecture detection with host
        /// compatibility, shared by <see cref="ListInstalledPackages(string,Architecture)"/>,
        /// <see cref="PickBestForHost(string,Architecture)"/> and
        /// <see cref="FindConfirmedCompatibleAlternative"/> so they can never
        /// disagree about a package's status.
        /// </summary>
        private static ProtonPackageInfo BuildPackageInfo(string packageRoot, string version, Architecture hostArchitecture)
        {
            var dir = Path.Combine(packageRoot, version);
            var detection = DetectPackageArchitectureDetailed(dir);
            return new ProtonPackageInfo
            {
                Version = version,
                WineBinary = FindWineInVersionDir(dir),
                Architecture = detection.Architecture,
                DetectionSource = detection.Source,
                Compatibility = GetCompatibility(detection.Architecture, hostArchitecture),
                HostSupported = IsSupportedHost(hostArchitecture)
            };
        }

        /// <summary>
        /// Path to the wine binary of a specific installed version, or null.
        /// Honors an explicit version pin verbatim (no architecture filtering) -
        /// a user/game that names a specific version is making a deliberate
        /// choice; only the auto-selection path (<see cref="ResolveWineBinary(string)"/>
        /// with no pin) filters by architecture.
        /// </summary>
        public static string GetWineBinary(string version) => GetWineBinary(PackageRoot, version);

        /// <summary>Package-root-parametrized core of <see cref="GetWineBinary(string)"/>.</summary>
        internal static string GetWineBinary(string packageRoot, string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            return FindWineInVersionDir(Path.Combine(packageRoot, version));
        }

        private static string FindWineInVersionDir(string versionDir)
        {
            // Plain wine build layout and Proton dist layout (GE-Proton etc.).
            foreach (var relative in new[]
                     {
                         Path.Combine("bin", "wine"),
                         Path.Combine("files", "bin", "wine")
                     })
            {
                var candidate = Path.Combine(versionDir, relative);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Wine binary for a game: the pinned version when set and installed,
        /// otherwise the best installed version for the host architecture,
        /// otherwise null. A pinned version is honored even if its architecture
        /// doesn't match (explicit choice - see <see cref="GetWineBinary(string)"/>);
        /// only the "otherwise" auto-pick is architecture-filtered/tiered - see
        /// <see cref="PickBestForHost(Architecture)"/> for the actual fix for the
        /// aarch64-selected-on-x86_64 bug (plus its symlink/shell-script/unknown
        /// edge cases).
        /// </summary>
        public static string ResolveWineBinary(string pinnedVersion = null)
        {
            if (!string.IsNullOrEmpty(pinnedVersion))
            {
                var pinned = GetWineBinary(pinnedVersion);
                if (pinned != null)
                    return pinned;
                // Pinned version missing - fall through to newest so the game
                // still starts; the UI can warn about the mismatch.
            }

            var best = PickBestForHost(PackageRoot, RuntimeInformation.OSArchitecture);
            return best?.WineBinary;
        }

        /// <summary>
        /// Picks the best installed version for <paramref name="hostArchitecture"/>,
        /// newest-first WITHIN each confidence tier:
        ///   1. Confirmed compatible by reading a real ELF header (ground truth).
        ///   2. Compatible by package/folder-name fallback (explicit arch marker).
        /// A package confirmed (by either method) INCOMPATIBLE is never picked.
        /// A package whose architecture couldn't be determined at all (Unknown)
        /// is likewise never auto-selected - an unproven architecture must not
        /// be treated as automatically compatible (unlike <see cref="IsCompatibleProtonPackage"/>,
        /// which stays permissive on Unknown for informational/UI purposes).
        /// Returns null outright on an unsupported host (see <see cref="IsSupportedHost"/>) -
        /// there is no "best" Proton package on a host TeknoParrotUI can't run
        /// games on at all, regardless of what's installed.
        /// </summary>
        public static string PickBestForHost(Architecture hostArchitecture) =>
            PickBestForHost(PackageRoot, hostArchitecture)?.Version;

        /// <summary>
        /// Package-root-parametrized core of <see cref="PickBestForHost(Architecture)"/> -
        /// the ONE production selection algorithm (numeric version ordering via
        /// <see cref="ListInstalledVersions(string)"/>, ELF-header-then-name-marker
        /// confidence tiers, confirmed-compatible-only). Internal so tests call
        /// this exact method against a temporary package root instead of
        /// re-implementing the tiering/filtering rules themselves. Returns the
        /// full <see cref="ProtonPackageInfo"/> (version, binary path, detection
        /// source, architecture, compatibility) rather than just a version
        /// string so tests/callers can assert on any of those directly.
        /// </summary>
        internal static ProtonPackageInfo PickBestForHost(string packageRoot, Architecture hostArchitecture)
        {
            if (!IsSupportedHost(hostArchitecture))
                return null;

            // ListInstalledVersions() is already newest-first, so a plain
            // FirstOrDefault within each tier below preserves that ordering.
            var detected = ListInstalledVersions(packageRoot)
                .Select(v => BuildPackageInfo(packageRoot, v, hostArchitecture))
                .ToList();

            var confirmedElf = detected.FirstOrDefault(p =>
                p.DetectionSource == ArchitectureDetectionSource.ElfHeader &&
                p.Compatibility == ArchitectureCompatibility.Compatible);
            if (confirmedElf != null)
                return confirmedElf;

            return detected.FirstOrDefault(p =>
                p.DetectionSource == ArchitectureDetectionSource.PackageName &&
                p.Compatibility == ArchitectureCompatibility.Compatible);
        }

        /// <summary>
        /// Extracts a Proton package archive (.tar.gz/.tar.xz from Proton-GE
        /// releases, or .zip) into the package root, preserving executable
        /// permissions (system tar is used for tarballs).
        /// </summary>
        public static void InstallFromArchive(string archivePath)
        {
            Directory.CreateDirectory(PackageRoot);

            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, PackageRoot, overwriteFiles: true);
                return;
            }

            // tar preserves the +x bits .NET's ZipArchive would lose.
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                UseShellExecute = false,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-xf");
            psi.ArgumentList.Add(archivePath);
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(PackageRoot);

            using var proc = Process.Start(psi);
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new IOException($"tar extraction failed ({proc.ExitCode}): {stderr}");
        }

        // ---------------------------------------------------------------
        // Supported-host gate
        // ---------------------------------------------------------------

        /// <summary>
        /// User-facing explanation shown wherever an unsupported host would
        /// otherwise try to set up/launch Proton - see <see cref="IsSupportedHost"/>.
        /// </summary>
        public const string UnsupportedHostMessage =
            "TeknoParrotUI currently supports Linux x86_64 systems only.\n" +
            "ARM64 systems require an x86_64 emulation layer, which is not yet implemented.";

        /// <summary>
        /// True only for a Linux x86_64 host - the only architecture TeknoParrotUI
        /// (and the x86/x86_64 Windows game executables it wraps via Proton)
        /// currently supports. An ARM64 host would need an x86_64 emulation/
        /// translation layer (FEX, Box64, or similar) that isn't implemented yet;
        /// simply picking an ARM64-native Proton/wine build does NOT make ARM64
        /// hosts work, so this is a hard gate independent of package selection -
        /// see <see cref="PickBestForHost"/>, <see cref="ProtonReleaseManager"/>,
        /// and <see cref="ProtonLauncher.WrapWithProton"/>, all of which check this
        /// first, before ever looking at what Proton/wine is installed or selected.
        /// </summary>
        public static bool IsSupportedHost(Architecture? hostArchitecture = null) =>
            (hostArchitecture ?? RuntimeInformation.OSArchitecture) == Architecture.X64;

        /// <summary><see cref="UnsupportedHostMessage"/> when <paramref name="hostArchitecture"/> isn't supported, otherwise null.</summary>
        public static string GetUnsupportedHostError(Architecture? hostArchitecture = null) =>
            IsSupportedHost(hostArchitecture) ? null : UnsupportedHostMessage;

        /// <summary>
        /// Single reusable hard-gate helper: throws <see cref="PlatformNotSupportedException"/>
        /// with <see cref="UnsupportedHostMessage"/> when <paramref name="hostArchitecture"/>
        /// (defaults to the real host) isn't supported. Every public/reusable method that
        /// prepares, initializes, repairs or launches a Wine/Proton environment should call
        /// this first rather than re-implementing the check inline - see
        /// <see cref="ProtonLauncher.WrapWithProton"/>, <see cref="ProtonLauncher.PrepareSession"/>,
        /// and <see cref="WinePrefixManager.EnsureDirectories"/>. The optional parameter exists
        /// purely so tests can simulate an unsupported (e.g. ARM64) host without needing to run
        /// on one - production call sites always use the default (the real host).
        /// </summary>
        public static void ThrowIfUnsupportedHost(Architecture? hostArchitecture = null)
        {
            if (!IsSupportedHost(hostArchitecture))
                throw new PlatformNotSupportedException(UnsupportedHostMessage);
        }

        // ---------------------------------------------------------------
        // Architecture detection / compatibility
        // ---------------------------------------------------------------

        /// <summary>
        /// True when the Proton/wine package at <paramref name="packagePath"/> (a
        /// version directory under <see cref="PackageRoot"/>) is NOT confirmed
        /// incompatible with <paramref name="hostArchitecture"/> - i.e. it's either
        /// confirmed compatible OR its architecture couldn't be determined at all
        /// (permissive on unknown - this is a general-purpose/UI compatibility
        /// check; auto-selection is stricter and never picks an Unknown-architecture
        /// package at all - see <see cref="PickBestForHost"/>).
        /// </summary>
        public static bool IsCompatibleProtonPackage(string packagePath, Architecture hostArchitecture) =>
            GetCompatibility(DetectPackageArchitectureDetailed(packagePath).Architecture, hostArchitecture)
                != ArchitectureCompatibility.Incompatible;

        /// <summary>
        /// Pure compatibility rule for a KNOWN architecture, no I/O - kept separate
        /// so it's directly unit-testable. Unknown HOST architectures (anything but
        /// x64/arm64) are treated permissively (matches the previous unfiltered
        /// behavior on those platforms - we have no evidence either way there).
        /// For an unknown PACKAGE architecture, use <see cref="GetCompatibility"/> instead.
        /// </summary>
        public static bool IsArchCompatible(Architecture packageArch, Architecture hostArchitecture) =>
            hostArchitecture switch
            {
                Architecture.X64 => packageArch == Architecture.X64 || packageArch == Architecture.X86,
                Architecture.Arm64 => packageArch == Architecture.Arm64 || packageArch == Architecture.Arm,
                _ => true
            };

        /// <summary>
        /// Tri-state compatibility for a package whose architecture may not be
        /// determinable at all (<paramref name="packageArch"/> null - see
        /// <see cref="ArchitectureDetectionSource.Unknown"/>). This is the version
        /// every caller should use once a package's <see cref="ArchitectureDetection"/>
        /// is in hand (as opposed to <see cref="IsArchCompatible"/>, which requires
        /// already knowing the package's architecture).
        /// </summary>
        public static ArchitectureCompatibility GetCompatibility(Architecture? packageArch, Architecture hostArchitecture)
        {
            if (!packageArch.HasValue)
                return ArchitectureCompatibility.Unknown;
            return IsArchCompatible(packageArch.Value, hostArchitecture)
                ? ArchitectureCompatibility.Compatible
                : ArchitectureCompatibility.Incompatible;
        }

        /// <summary>
        /// Pure name-based architecture classification, no I/O - the fallback used
        /// by <see cref="DetectPackageArchitectureDetailed"/> when no ELF header
        /// could be read from the wine binary (or any sibling binary probed - see
        /// <see cref="FindElfProbeCandidates"/>), and directly unit-testable on its
        /// own. Returns null (genuinely ambiguous/unknown) when the name has no
        /// recognizable architecture marker at all - deliberately does NOT default
        /// to x86_64 anymore: silently assuming an unproven architecture is exactly
        /// the class of bug this whole fix removes. Unmarked GE-Proton releases are
        /// still handled correctly in practice because their real wine binary is a
        /// genuine ELF executable, so ELF-header detection (tier 1) resolves them
        /// before this fallback (tier 3) is ever reached.
        /// </summary>
        public static Architecture? ClassifyArchitectureFromName(string packageName)
        {
            if (packageName.Contains("aarch64", StringComparison.OrdinalIgnoreCase) ||
                packageName.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                return Architecture.Arm64;
            if (packageName.Contains("armhf", StringComparison.OrdinalIgnoreCase) ||
                packageName.Contains("armv7", StringComparison.OrdinalIgnoreCase))
                return Architecture.Arm;
            if (packageName.Contains("x86_64", StringComparison.OrdinalIgnoreCase) ||
                packageName.Contains("amd64", StringComparison.OrdinalIgnoreCase) ||
                packageName.Contains("x64", StringComparison.OrdinalIgnoreCase))
                return Architecture.X64;
            return null;
        }

        /// <summary>
        /// Best-effort architecture of an installed package - convenience wrapper
        /// around <see cref="DetectPackageArchitectureDetailed"/> for callers that
        /// don't need the confidence tier (e.g. just labelling a log line).
        /// </summary>
        public static Architecture? DetectPackageArchitecture(string versionDir) =>
            DetectPackageArchitectureDetailed(versionDir).Architecture;

        /// <summary>
        /// Detects a package's architecture with a confidence tier attached, in
        /// priority order:
        ///   1. Read the wine binary's own ELF header (resolving symlinks first -
        ///      some wine builds ship "wine" as a symlink to "wine64" or vice versa).
        ///   2. If that's not a real ELF (e.g. a shell-script launcher some custom/
        ///      distro builds use), probe likely sibling binaries in the same
        ///      package - wine64/wineserver/wine-preloader - for a real ELF header.
        ///   3. Fall back to package/folder-name classification (explicit arch marker).
        ///   4. Otherwise, genuinely unknown - callers must not treat this as
        ///      confirmed compatible (see <see cref="PickBestForHost"/>/<see cref="GetCompatibility"/>).
        /// </summary>
        public static ArchitectureDetection DetectPackageArchitectureDetailed(string versionDir)
        {
            var wine = FindWineInVersionDir(versionDir);
            if (wine != null)
            {
                var elfArch = DetectElfArchitecture(wine);
                if (elfArch.HasValue)
                    return new ArchitectureDetection(elfArch, ArchitectureDetectionSource.ElfHeader);
            }

            foreach (var candidate in FindElfProbeCandidates(versionDir))
            {
                var elfArch = DetectElfArchitecture(candidate);
                if (elfArch.HasValue)
                    return new ArchitectureDetection(elfArch, ArchitectureDetectionSource.ElfHeader);
            }

            var name = Path.GetFileName(versionDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var nameArch = ClassifyArchitectureFromName(name);
            if (nameArch.HasValue)
                return new ArchitectureDetection(nameArch, ArchitectureDetectionSource.PackageName);

            return new ArchitectureDetection(null, ArchitectureDetectionSource.Unknown);
        }

        /// <summary>
        /// Sibling binaries worth ELF-probing when "wine" itself isn't a real ELF
        /// executable (e.g. a shell-script launcher) - real wine/Proton builds that
        /// do this still ship at least one genuine ELF binary alongside it.
        /// </summary>
        private static IEnumerable<string> FindElfProbeCandidates(string versionDir)
        {
            foreach (var relative in new[]
                     {
                         Path.Combine("bin", "wine64"),
                         Path.Combine("files", "bin", "wine64"),
                         Path.Combine("bin", "wine64-preloader"),
                         Path.Combine("files", "bin", "wine64-preloader"),
                         Path.Combine("bin", "wine-preloader"),
                         Path.Combine("files", "bin", "wine-preloader"),
                         Path.Combine("bin", "wineserver"),
                         Path.Combine("files", "bin", "wineserver")
                     })
            {
                var candidate = Path.Combine(versionDir, relative);
                if (File.Exists(candidate))
                    yield return candidate;
            }
        }

        /// <summary>
        /// Reads the ELF header's e_machine field directly - real metadata rather
        /// than guessing from the file/folder name. Resolves symlinks (following
        /// the whole chain) before reading, since several wine builds ship "wine"
        /// as a symlink to "wine64" (or vice versa) rather than a real file - reading
        /// the link itself would just see a few bytes of path text, not an ELF
        /// header. Returns null for non-ELF files (e.g. a shell-script wrapper) or
        /// on any read failure; callers fall back to probing sibling binaries, then
        /// name-based classification, in that case.
        /// </summary>
        public static Architecture? DetectElfArchitecture(string binaryPath)
        {
            try
            {
                var resolvedPath = ResolveFinalTarget(binaryPath);
                using var fs = File.OpenRead(resolvedPath);
                Span<byte> header = stackalloc byte[20];
                if (fs.Read(header) < 20)
                    return null;
                // ELF magic: 0x7F 'E' 'L' 'F'.
                if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                    return null;

                // e_machine, offset 18, little-endian (both ELF32/ELF64 agree on this offset).
                ushort machine = (ushort)(header[18] | (header[19] << 8));
                return machine switch
                {
                    0x3E => Architecture.X64,   // EM_X86_64
                    0xB7 => Architecture.Arm64, // EM_AARCH64
                    0x03 => Architecture.X86,   // EM_386
                    0x28 => Architecture.Arm,   // EM_ARM
                    _ => (Architecture?)null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a (possibly multi-hop) symlink to its final target using the
        /// BCL's own chain-following logic - many wine builds ship "wine" as a
        /// symlink to the real "wine64" (or vice versa). Returns the original path
        /// unchanged when it isn't a symlink at all, or on any resolution failure
        /// (broken link, permission error, etc.) - callers already handle a read
        /// failure on whatever path comes back.
        /// </summary>
        private static string ResolveFinalTarget(string path)
        {
            try
            {
                var final = File.ResolveLinkTarget(path, returnFinalTarget: true);
                return final?.FullName ?? path;
            }
            catch
            {
                return path;
            }
        }

        /// <summary>
        /// If <paramref name="wineBinary"/> is confirmed incompatible with
        /// <paramref name="hostArchitecture"/>, or its architecture couldn't be
        /// determined at all, returns a user-facing explanation - e.g. for the
        /// Linux Setup page or a pre-launch check. Returns null when it's
        /// confirmed compatible. Works for any wine binary, not just installed
        /// Proton packages under <see cref="PackageRoot"/> - a custom path or
        /// system wine picked explicitly by the user is still validated (see
        /// <see cref="ProtonLauncher"/>'s pre-launch check), it just has no
        /// package name to classify from, so only its ELF header is read.
        /// </summary>
        public static string DescribeArchitectureMismatch(string wineBinary, Architecture? hostArchitecture = null)
        {
            if (string.IsNullOrEmpty(wineBinary))
                return null;

            var host = hostArchitecture ?? RuntimeInformation.OSArchitecture;

            // Unsupported host takes priority over any per-binary architecture
            // comparison - a "matches this host's CPU" Wine/Proton build still
            // can't run the (x86/x86_64) game on a host TeknoParrotUI doesn't
            // support at all (see IsSupportedHost).
            if (!IsSupportedHost(host))
                return UnsupportedHostMessage;

            var versionDir = FindVersionDirForBinary(wineBinary);

            ArchitectureDetection detection;
            string label;
            if (versionDir != null)
            {
                detection = DetectPackageArchitectureDetailed(versionDir);
                label = Path.GetFileName(versionDir);
            }
            else
            {
                // Not a packaged install (system wine / a fully custom path) -
                // no package name to fall back on, so only a real ELF read counts.
                var elfArch = DetectElfArchitecture(wineBinary);
                detection = new ArchitectureDetection(elfArch, ArchitectureDetectionSource.ElfHeader);
                label = wineBinary;
            }

            var compatibility = GetCompatibility(detection.Architecture, host);
            if (compatibility == ArchitectureCompatibility.Compatible)
                return null;

            if (compatibility == ArchitectureCompatibility.Unknown)
                return $"Could not determine the architecture of the selected Wine/Proton binary ({label}). " +
                       $"If it fails to launch with an architecture-related error, use a build explicitly for {ArchLabel(host)}.";

            var message = $"The selected Wine/Proton binary ({label}) is built for {ArchLabel(detection.Architecture!.Value)}, but this system is {ArchLabel(host)}.";

            if (versionDir == null)
                return message;

            // Only ever suggest a CONFIRMED-compatible alternative - unlike
            // IsCompatibleProtonPackage (permissive on Unknown for general/UI
            // use), a concrete suggestion must not point the user at a package
            // whose architecture couldn't even be determined. Delegates to the
            // same production selection helper PickBestForHost/ListInstalledPackages
            // use, rather than re-deriving the "confirmed compatible, newest
            // wins" rule here.
            var alternative = FindConfirmedCompatibleAlternative(PackageRoot, label, host);
            return alternative != null ? $"{message} Select {alternative.Version} instead of {label}." : message;
        }

        /// <summary>
        /// The best (newest, per the same numeric version ordering
        /// <see cref="PickBestForHost(string,Architecture)"/> uses) CONFIRMED-compatible
        /// installed package to suggest as a fix for <paramref name="excludedVersion"/>
        /// (the current/mismatched version - never suggested back to itself), or
        /// null when none exists. The single production implementation behind
        /// <see cref="DescribeArchitectureMismatch"/>'s "Select X instead" text -
        /// never suggests an ARM64 package on an x86_64 host, an Unknown-architecture
        /// package (an unproven architecture must not be recommended as "the fix"
        /// any more than it can be auto-selected - see <see cref="PickBestForHost(string,Architecture)"/>),
        /// or anything at all on an unsupported host.
        /// </summary>
        internal static ProtonPackageInfo FindConfirmedCompatibleAlternative(string packageRoot, string excludedVersion, Architecture hostArchitecture)
        {
            if (!IsSupportedHost(hostArchitecture))
                return null;

            // ListInstalledVersions is already newest-first, so the first
            // compatible match after excluding the current version is the
            // newest confirmed-compatible alternative.
            return ListInstalledVersions(packageRoot)
                .Where(v => excludedVersion == null || !v.Equals(excludedVersion, StringComparison.OrdinalIgnoreCase))
                .Select(v => BuildPackageInfo(packageRoot, v, hostArchitecture))
                .FirstOrDefault(p => p.Compatibility == ArchitectureCompatibility.Compatible);
        }

        /// <summary>
        /// True when <paramref name="wineBinary"/>'s architecture is CONFIRMED
        /// (a real ELF header, or - for a packaged install - a package-name
        /// marker) to be incompatible with <paramref name="hostArchitecture"/>.
        /// Used to hard-block a launch (see <see cref="ProtonLauncher.WrapWithProton"/>)
        /// for an explicitly selected (custom path or pinned) wine binary that's
        /// definitely wrong for this system - e.g. an ARM64 build picked on an
        /// x86_64 host. Unlike <see cref="DescribeArchitectureMismatch"/>, this
        /// never blocks on Unknown architecture (still permissive there - we
        /// can't confirm it's wrong, so it isn't treated as a hard failure).
        /// </summary>
        public static bool IsConfirmedIncompatibleWineBinary(string wineBinary, Architecture? hostArchitecture = null)
        {
            if (string.IsNullOrEmpty(wineBinary) || !File.Exists(wineBinary))
                return false;

            var host = hostArchitecture ?? RuntimeInformation.OSArchitecture;
            var versionDir = FindVersionDirForBinary(wineBinary);
            var arch = versionDir != null
                ? DetectPackageArchitectureDetailed(versionDir).Architecture
                : DetectElfArchitecture(wineBinary);

            return GetCompatibility(arch, host) == ArchitectureCompatibility.Incompatible;
        }


        private static string FindVersionDirForBinary(string wineBinary)
        {
            try
            {
                var full = Path.GetFullPath(wineBinary);
                var root = Path.GetFullPath(PackageRoot) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(root, StringComparison.Ordinal))
                    return null;

                var relative = full.Substring(root.Length);
                var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                return string.IsNullOrEmpty(firstSegment) ? null : Path.Combine(PackageRoot, firstSegment);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The installed package version name (e.g. "GE-Proton11-1") that owns
        /// <paramref name="wineBinary"/>, or null when it isn't a packaged
        /// Proton build (system wine, a custom path outside <see cref="PackageRoot"/>).
        /// </summary>
        public static string GetPackageVersionForBinary(string wineBinary)
        {
            var dir = FindVersionDirForBinary(wineBinary);
            return dir != null ? Path.GetFileName(dir) : null;
        }

        internal static string ArchLabel(Architecture arch) => arch switch
        {
            Architecture.X64 => "x86_64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "ARM64",
            Architecture.Arm => "ARM",
            _ => arch.ToString()
        };

        /// <summary>
        /// Compares two Proton/GE-Proton version directory names by the numeric
        /// runs they contain (e.g. "GE-Proton11-1" → [11, 1]), NOT as plain
        /// strings - a string sort would place "GE-Proton10-5" before
        /// "GE-Proton9-20" (and, worse, "GE-Proton11-1-aarch64" after
        /// "GE-Proton11-1" since it's a longer string with the same prefix -
        /// exactly how the aarch64 package won "newest" on an x86_64 host in
        /// the original bug). Falls back to an ordinal string compare only
        /// once every extracted number matches (stable tiebreak).
        /// </summary>
        public static int CompareVersionNames(string a, string b)
        {
            var na = ExtractVersionNumbers(a);
            var nb = ExtractVersionNumbers(b);
            var len = Math.Max(na.Count, nb.Count);
            for (var i = 0; i < len; i++)
            {
                var va = i < na.Count ? na[i] : 0;
                var vb = i < nb.Count ? nb[i] : 0;
                var cmp = va.CompareTo(vb);
                if (cmp != 0)
                    return cmp;
            }
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static List<int> ExtractVersionNumbers(string s)
        {
            var result = new List<int>();
            var i = 0;
            while (i < s.Length)
            {
                if (char.IsDigit(s[i]))
                {
                    var start = i;
                    while (i < s.Length && char.IsDigit(s[i]))
                        i++;
                    result.Add(int.Parse(s.Substring(start, i - start)));
                }
                else
                {
                    i++;
                }
            }
            return result;
        }
    }

    /// <summary>Installed Proton package info shown on the Linux Setup page.</summary>
    public class ProtonPackageInfo
    {
        public string Version { get; init; }
        public string WineBinary { get; init; }
        public Architecture? Architecture { get; init; }
        public ArchitectureDetectionSource DetectionSource { get; init; }
        public ArchitectureCompatibility Compatibility { get; init; }

        /// <summary>
        /// True when the host CPU/OS itself is one TeknoParrotUI supports at all
        /// (see <see cref="ProtonPackageManager.IsSupportedHost"/>) - independent
        /// of whether THIS package's architecture happens to match that host.
        /// An ARM64 package can perfectly match an ARM64 host's CPU while that
        /// host is still unsupported (no x86/x86_64 translation layer), so this
        /// is tracked separately from <see cref="Compatibility"/>.
        /// </summary>
        public bool HostSupported { get; init; }

        /// <summary>Strict "definitely fine to auto-select" flag - true only when confirmed compatible (see <see cref="Compatibility"/> for the tri-state).</summary>
        public bool IsCompatible => Compatibility == ArchitectureCompatibility.Compatible;

        /// <summary>
        /// True only when the host is supported AND this package is confirmed
        /// compatible with it - the actual "can TeknoParrotUI use this package"
        /// answer. Never true on an unsupported host, no matter how well the
        /// package's own architecture matches the host CPU.
        /// </summary>
        public bool IsUsable => HostSupported && Compatibility == ArchitectureCompatibility.Compatible;

        public override string ToString()
        {
            var archLabel = Architecture.HasValue ? ProtonPackageManager.ArchLabel(Architecture.Value) : "unknown";
            string status;
            if (!HostSupported)
                status = "host architecture unsupported";
            else
                status = Compatibility switch
                {
                    ArchitectureCompatibility.Compatible => "compatible",
                    ArchitectureCompatibility.Incompatible => "incompatible with this system",
                    _ => "architecture undetermined"
                };
            return $"{Version}    {archLabel} — {status}";
        }
    }

    /// <summary>How confidently a package's architecture was determined - see <see cref="ProtonPackageManager.DetectPackageArchitectureDetailed"/>.</summary>
    public enum ArchitectureDetectionSource
    {
        /// <summary>Read directly from a real ELF binary's header (ground truth) - the wine binary itself, or a sibling (wine64/wineserver/etc.) when wine wasn't a real ELF file.</summary>
        ElfHeader,
        /// <summary>Inferred from an explicit architecture marker in the package/folder name (e.g. "-aarch64") - used only when no ELF header could be read.</summary>
        PackageName,
        /// <summary>Couldn't determine the architecture either way - no readable ELF binary and no recognizable name marker.</summary>
        Unknown
    }

    /// <summary>Tri-state compatibility result - see <see cref="ProtonPackageManager.GetCompatibility"/>.</summary>
    public enum ArchitectureCompatibility
    {
        Compatible,
        Incompatible,
        Unknown
    }

    /// <summary>Result of <see cref="ProtonPackageManager.DetectPackageArchitectureDetailed"/> - the detected architecture (if any) plus how confidently it was determined.</summary>
    public readonly struct ArchitectureDetection
    {
        public Architecture? Architecture { get; }
        public ArchitectureDetectionSource Source { get; }

        public ArchitectureDetection(Architecture? architecture, ArchitectureDetectionSource source)
        {
            Architecture = architecture;
            Source = source;
        }
    }
}
