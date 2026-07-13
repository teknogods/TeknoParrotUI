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
        public static List<string> ListInstalledVersions()
        {
            if (!Directory.Exists(PackageRoot))
                return new List<string>();

            return Directory.GetDirectories(PackageRoot)
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
        public static List<ProtonPackageInfo> ListInstalledPackages(Architecture? hostArchitecture = null)
        {
            var host = hostArchitecture ?? RuntimeInformation.OSArchitecture;
            return ListInstalledVersions()
                .Select(version =>
                {
                    var dir = Path.Combine(PackageRoot, version);
                    var detection = DetectPackageArchitectureDetailed(dir);
                    return new ProtonPackageInfo
                    {
                        Version = version,
                        WineBinary = FindWineInVersionDir(dir),
                        Architecture = detection.Architecture,
                        DetectionSource = detection.Source,
                        Compatibility = GetCompatibility(detection.Architecture, host)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Path to the wine binary of a specific installed version, or null.
        /// Honors an explicit version pin verbatim (no architecture filtering) -
        /// a user/game that names a specific version is making a deliberate
        /// choice; only the auto-selection path (<see cref="ResolveWineBinary(string)"/>
        /// with no pin) filters by architecture.
        /// </summary>
        public static string GetWineBinary(string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            return FindWineInVersionDir(Path.Combine(PackageRoot, version));
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
        /// doesn't match (explicit choice - see <see cref="GetWineBinary"/>);
        /// only the "otherwise" auto-pick is architecture-filtered/tiered - see
        /// <see cref="PickBestForHost"/> for the actual fix for the
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

            var best = PickBestForHost(RuntimeInformation.OSArchitecture);
            return best != null ? GetWineBinary(best) : null;
        }

        /// <summary>
        /// Picks the best installed version for <paramref name="hostArchitecture"/>,
        /// newest-first WITHIN each confidence tier:
        ///   1. Confirmed compatible by reading a real ELF header (ground truth).
        ///   2. Compatible by package/folder-name fallback (explicit arch marker).
        ///   3. Unknown architecture (couldn't determine either way) - used ONLY
        ///      when no confirmed-compatible package exists at all, as a last resort.
        /// A package confirmed (by either method) INCOMPATIBLE is never picked,
        /// regardless of tier - that's the actual aarch64-on-x86_64 bug fix.
        /// </summary>
        public static string PickBestForHost(Architecture hostArchitecture)
        {
            // ListInstalledVersions() is already newest-first, so a plain
            // FirstOrDefault within each tier below preserves that ordering.
            var detected = ListInstalledVersions()
                .Select(v => (Version: v, Detection: DetectPackageArchitectureDetailed(Path.Combine(PackageRoot, v))))
                .ToList();

            var confirmedElf = detected.FirstOrDefault(d =>
                d.Detection.Source == ArchitectureDetectionSource.ElfHeader &&
                GetCompatibility(d.Detection.Architecture, hostArchitecture) == ArchitectureCompatibility.Compatible);
            if (confirmedElf.Version != null)
                return confirmedElf.Version;

            var confirmedName = detected.FirstOrDefault(d =>
                d.Detection.Source == ArchitectureDetectionSource.PackageName &&
                GetCompatibility(d.Detection.Architecture, hostArchitecture) == ArchitectureCompatibility.Compatible);
            if (confirmedName.Version != null)
                return confirmedName.Version;

            var unknown = detected.FirstOrDefault(d => d.Detection.Source == ArchitectureDetectionSource.Unknown);
            return unknown.Version;
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
        // Architecture detection / compatibility
        // ---------------------------------------------------------------

        /// <summary>
        /// True when the Proton/wine package at <paramref name="packagePath"/> (a
        /// version directory under <see cref="PackageRoot"/>) is NOT confirmed
        /// incompatible with <paramref name="hostArchitecture"/> - i.e. it's either
        /// confirmed compatible OR its architecture couldn't be determined at all
        /// (permissive on unknown; see <see cref="PickBestForHost"/> for the actual
        /// tiered priority used by auto-selection, where "unknown" only ever loses
        /// to a CONFIRMED compatible package, never silently wins over one).
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
        /// If <paramref name="wineBinary"/> belongs to an installed Proton package
        /// (i.e. lives under <see cref="PackageRoot"/>) whose architecture is
        /// confirmed incompatible, or couldn't be determined at all, returns a
        /// user-facing explanation - e.g. for the Linux Setup page or a pre-launch
        /// check. Returns null when the package is confirmed compatible, or for
        /// binaries outside the package root (system wine, a custom path) - those
        /// are a deliberate user choice, not auto-selected, so there's nothing to
        /// second-guess UNLESS they're actually wrong/uncertain (both cases are
        /// still reported here so the UI can warn without ever silently replacing
        /// the user's choice).
        /// </summary>
        public static string DescribeArchitectureMismatch(string wineBinary, Architecture? hostArchitecture = null)
        {
            if (string.IsNullOrEmpty(wineBinary))
                return null;

            var versionDir = FindVersionDirForBinary(wineBinary);
            if (versionDir == null)
                return null;

            var host = hostArchitecture ?? RuntimeInformation.OSArchitecture;
            var detection = DetectPackageArchitectureDetailed(versionDir);
            var compatibility = GetCompatibility(detection.Architecture, host);
            if (compatibility == ArchitectureCompatibility.Compatible)
                return null;

            var version = Path.GetFileName(versionDir);

            if (compatibility == ArchitectureCompatibility.Unknown)
                return $"Could not determine the architecture of the selected Proton package ({version}). " +
                       $"If it fails to launch with an architecture-related error, install a package explicitly built for {ArchLabel(host)}.";

            var alternative = ListInstalledVersions()
                .FirstOrDefault(v => !v.Equals(version, StringComparison.OrdinalIgnoreCase) &&
                                      IsCompatibleProtonPackage(Path.Combine(PackageRoot, v), host));

            var message = $"The selected Proton package ({version}) is built for {ArchLabel(detection.Architecture!.Value)}, but this system is {ArchLabel(host)}.";
            return alternative != null ? $"{message} Select {alternative} instead of {version}." : message;
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

        /// <summary>Strict "definitely fine to auto-select" flag - true only when confirmed compatible (see <see cref="Compatibility"/> for the tri-state).</summary>
        public bool IsCompatible => Compatibility == ArchitectureCompatibility.Compatible;

        public override string ToString()
        {
            var archLabel = Architecture.HasValue ? ProtonPackageManager.ArchLabel(Architecture.Value) : "unknown";
            var status = Compatibility switch
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
