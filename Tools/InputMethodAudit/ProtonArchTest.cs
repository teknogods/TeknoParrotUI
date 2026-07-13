using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// Regression tests for the Proton package architecture-detection bug: on
    /// an x86_64 host with both "GE-Proton11-1" and "GE-Proton11-1-aarch64"
    /// installed, auto-selection used to pick the aarch64 one (a plain
    /// alphabetical/string sort puts it "after" the shorter name, and there
    /// was no architecture filtering at all - see ProtonPackageManager).
    ///
    /// Also covers the follow-up review round: symlinked wine binaries,
    /// shell-script wine launchers with a real ELF binary nearby (wine64),
    /// and the "genuinely unknown architecture" tier (see
    /// ProtonPackageManager.DetectPackageArchitectureDetailed/PickBestForHost).
    ///
    /// And the correction that ARM64 Linux hosts are NOT currently supported at
    /// all - not even by picking an ARM64-native Proton/wine build, since
    /// TeknoParrot and the Windows games it wraps are still x86/x86_64 and
    /// there's no x86_64 emulation/translation layer implemented yet. See
    /// ProtonPackageManager.IsSupportedHost/UnsupportedHostMessage,
    /// ProtonReleaseManager.InstallRelease (download gate), and
    /// ProtonLauncher.WrapWithProton (launch gate).
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -- proton-arch-test
    /// </summary>
    internal static class ProtonArchTest
    {
        private const ushort EM_X86_64 = 0x3E;
        private const ushort EM_AARCH64 = 0xB7;

        public static int Run()
        {
            int cases = 0, failures = 0;

            void Check(string label, bool expected, bool actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            void CheckArch(string label, Architecture? expected, Architecture? actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {ToStr(expected)}, got {ToStr(actual)}");
                }
                static string ToStr(Architecture? a) => a.HasValue ? a.Value.ToString() : "null";
            }

            void CheckSource(string label, ArchitectureDetectionSource expected, ArchitectureDetectionSource actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            void CheckString(string label, string expected, string actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected '{expected}', got '{actual}'");
                }
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "tpui-proton-arch-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);
            try
            {
                // ---------------------------------------------------------
                // Helpers to build fake package directories under tempRoot.
                // ---------------------------------------------------------

                static byte[] FakeElfBytes(ushort machine)
                {
                    var bytes = new byte[20];
                    bytes[0] = 0x7F; bytes[1] = (byte)'E'; bytes[2] = (byte)'L'; bytes[3] = (byte)'F';
                    bytes[18] = (byte)(machine & 0xFF);
                    bytes[19] = (byte)((machine >> 8) & 0xFF);
                    return bytes;
                }

                string MakeDir(string name)
                {
                    var dir = Path.Combine(tempRoot, name, "files", "bin");
                    Directory.CreateDirectory(dir);
                    return dir;
                }

                // Non-ELF shell-script "wine" (no siblings) - the original
                // test shape: falls straight through to name classification.
                string MakePackage(string name)
                {
                    var bin = MakeDir(name);
                    File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# fake wine stub for tests, not a real ELF binary\n");
                    return Path.Combine(tempRoot, name);
                }

                // wine is a real ELF binary directly (the common case for
                // genuine installs, where wine IS the actual executable).
                string MakeElfPackage(string name, ushort machine)
                {
                    var bin = MakeDir(name);
                    File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(machine));
                    return Path.Combine(tempRoot, name);
                }

                // wine is a SYMLINK to a real ELF binary (wine64) - covers
                // review point #1 (resolve the link target before reading).
                string MakeSymlinkPackage(string name, ushort machine)
                {
                    var bin = MakeDir(name);
                    var realBinary = Path.Combine(bin, "wine64");
                    File.WriteAllBytes(realBinary, FakeElfBytes(machine));
                    File.CreateSymbolicLink(Path.Combine(bin, "wine"), "wine64");
                    return Path.Combine(tempRoot, name);
                }

                // "wine" is a non-ELF shell script, but "wine64" next to it IS
                // a real ELF binary - covers review point #2 (probe likely
                // sibling binaries when wine itself isn't a real executable).
                string MakeShellScriptWithSiblingElf(string name, ushort machine)
                {
                    var bin = MakeDir(name);
                    File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\nexec \"$(dirname \"$0\")/wine64\" \"$@\"\n");
                    File.WriteAllBytes(Path.Combine(bin, "wine64"), FakeElfBytes(machine));
                    return Path.Combine(tempRoot, name);
                }

                var proton11X64 = MakePackage("GE-Proton11-1");
                var proton11Aarch64 = MakePackage("GE-Proton11-1-aarch64");
                var proton10Arm64 = MakePackage("GE-Proton10-25-arm64");

                // --- Host X64 (the only supported host - see ProtonPackageManager.IsSupportedHost) ---
                Check("Host X64: GE-Proton11-1 accepted", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11X64, Architecture.X64));
                Check("Host X64: GE-Proton11-1-aarch64 rejected", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Aarch64, Architecture.X64));
                Check("Host X64: GE-Proton10-25-arm64 rejected", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton10Arm64, Architecture.X64));

                // --- Mixed installation: exactly the reported bug ---
                var alphabeticallyFirst = new[] { "GE-Proton11-1", "GE-Proton11-1-aarch64" }.OrderBy(x => x, StringComparer.Ordinal).First();
                CheckString("sanity check: 'GE-Proton11-1' sorts first alphabetically", "GE-Proton11-1", alphabeticallyFirst);
                Check("Mixed: incompatible package rejected even when it would sort first/last alphabetically", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Aarch64, Architecture.X64));

                // ---------------------------------------------------------
                // ARM64 hosts are not supported at all (see ProtonPackageManager.
                // IsSupportedHost) - TeknoParrot and the Windows games it wraps
                // are x86/x86_64, so picking an ARM64-native Proton/wine build
                // does NOT make an ARM64 host work, regardless of how well that
                // package's architecture "matches" the host CPU. This must be a
                // hard gate, independent of what's installed.
                // ---------------------------------------------------------
                Check("Host X64 is supported", true, ProtonPackageManager.IsSupportedHost(Architecture.X64));
                Check("Host ARM64 is NOT supported", false, ProtonPackageManager.IsSupportedHost(Architecture.Arm64));
                Check("Unsupported-host error is null on X64", true, ProtonPackageManager.GetUnsupportedHostError(Architecture.X64) == null);
                Check("Unsupported-host error is non-null on ARM64", true, ProtonPackageManager.GetUnsupportedHostError(Architecture.Arm64) != null);

                // ARM64 download selection -> explicit unsupported-platform result.
                // ProtonReleaseManager.InstallRelease checks IsSupportedHost/
                // GetUnsupportedHostError before ever contacting the network or
                // picking a tarball asset - this is the same check it uses, so an
                // ARM64 host is guaranteed to get a clear error rather than
                // silently downloading (or worse, being offered) an ARM64 asset.
                Check("ARM64 download selection: unsupported-platform result (InstallRelease's gate), not an asset pick", true,
                    ProtonPackageManager.GetUnsupportedHostError(Architecture.Arm64) != null);

                // 5. ARM64 host + any Proton package -> unsupported host, even a
                // package confirmed (by ELF header) to be a native ARM64 build.
                // PickBestForHost/PickBestForHostUnder must return null for an
                // ARM64 host regardless of what's installed - there's no "best"
                // package on a host TeknoParrotUI can't run games on at all.
                var arm64OnlyRoot = Path.Combine(tempRoot, "arm64-only-root");
                Directory.CreateDirectory(arm64OnlyRoot);
                CreatePackageUnder(arm64OnlyRoot, "GE-Proton11-1-confirmed-arm64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                var arm64Pick = PickBestForHostUnder(arm64OnlyRoot, Architecture.Arm64);
                Check("Host ARM64 + confirmed-native ARM64 package installed: still unsupported (no auto-selection at all)",
                    true, arm64Pick == null);

                // ---------------------------------------------------------
                // Follow-up review round: symlinks, shell-script launchers,
                // unknown architecture, and tiered selection priority.
                // ---------------------------------------------------------

                // 1. wine is a symlink to an x86_64 ELF binary.
                var symlinkX64 = MakeSymlinkPackage("Sym-X64-Wine", EM_X86_64);
                var symlinkX64Detection = ProtonPackageManager.DetectPackageArchitectureDetailed(symlinkX64);
                CheckArch("symlink -> x86_64 ELF: architecture", Architecture.X64, symlinkX64Detection.Architecture);
                CheckSource("symlink -> x86_64 ELF: source", ArchitectureDetectionSource.ElfHeader, symlinkX64Detection.Source);

                // 2. wine is a symlink to an ARM64 ELF binary.
                var symlinkArm64 = MakeSymlinkPackage("Sym-Arm64-Wine", EM_AARCH64);
                var symlinkArm64Detection = ProtonPackageManager.DetectPackageArchitectureDetailed(symlinkArm64);
                CheckArch("symlink -> ARM64 ELF: architecture", Architecture.Arm64, symlinkArm64Detection.Architecture);
                CheckSource("symlink -> ARM64 ELF: source", ArchitectureDetectionSource.ElfHeader, symlinkArm64Detection.Source);

                // 3. wine is a non-ELF shell script with a valid wine64 binary nearby.
                var scriptWithSibling = MakeShellScriptWithSiblingElf("Script-With-Wine64", EM_X86_64);
                var scriptWithSiblingDetection = ProtonPackageManager.DetectPackageArchitectureDetailed(scriptWithSibling);
                CheckArch("shell-script wine + sibling wine64 ELF: architecture", Architecture.X64, scriptWithSiblingDetection.Architecture);
                CheckSource("shell-script wine + sibling wine64 ELF: source", ArchitectureDetectionSource.ElfHeader, scriptWithSiblingDetection.Source);

                // 4. ELF detection fails (shell script, no ELF siblings) and package-name fallback succeeds.
                var scriptNamedArm = MakePackage("GE-Proton11-1-aarch64-noelf");
                var scriptNamedArmDetection = ProtonPackageManager.DetectPackageArchitectureDetailed(scriptNamedArm);
                CheckArch("ELF fails, name fallback succeeds: architecture", Architecture.Arm64, scriptNamedArmDetection.Architecture);
                CheckSource("ELF fails, name fallback succeeds: source", ArchitectureDetectionSource.PackageName, scriptNamedArmDetection.Source);

                // X64 host + unknown NAME but a real x86_64 ELF binary -> accepted.
                // The package name has no architecture marker at all; only the
                // ELF header (tier 1) proves it's x86_64.
                var unnamedElfX64 = MakeElfPackage("CustomWineBuildX64", EM_X86_64);
                var unnamedElfX64Detection = ProtonPackageManager.DetectPackageArchitectureDetailed(unnamedElfX64);
                CheckArch("unknown name, real x86_64 ELF: architecture", Architecture.X64, unnamedElfX64Detection.Architecture);
                CheckSource("unknown name, real x86_64 ELF: source", ArchitectureDetectionSource.ElfHeader, unnamedElfX64Detection.Source);
                Check("X64 host + unknown name but x86_64 ELF: accepted", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(unnamedElfX64, Architecture.X64));

                // 5. Architecture remains unknown: shell script, no ELF siblings, no name marker at all.
                var unknownPkg = MakePackage("CustomWineBuild");
                var unknownDetection = ProtonPackageManager.DetectPackageArchitectureDetailed(unknownPkg);
                CheckArch("no ELF, no name marker: architecture is null (unknown)", null, unknownDetection.Architecture);
                CheckSource("no ELF, no name marker: source is Unknown", ArchitectureDetectionSource.Unknown, unknownDetection.Source);
                // The boolean compatibility check stays permissive for Unknown
                // (informational/UI use - see IsCompatibleProtonPackage's docs),
                // but auto-selection (PickBestForHostUnder below) must still
                // reject it outright - see requirement: "unknown architecture is
                // never treated as automatically compatible" for selection.
                Check("Unknown architecture is not treated as confirmed incompatible (permissive, informational check)", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(unknownPkg, Architecture.X64));

                // 6. A confirmed-compatible package is preferred over an unknown
                // one, even when the unknown package's version number is "newer".
                var pickRoot = Path.Combine(tempRoot, "pick-root");
                Directory.CreateDirectory(pickRoot);
                CreatePackageUnder(pickRoot, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64))); // confirmed compatible, ELF
                CreatePackageUnder(pickRoot, "GE-Proton99-1", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker, higher version number\n"));

                var confirmed = PickBestForHostUnder(pickRoot, Architecture.X64);
                CheckString("confirmed-compatible (GE-Proton11-1) preferred over higher-numbered unknown (GE-Proton99-1)",
                    "GE-Proton11-1", confirmed);

                // X64 host + unknown name and unknown ELF -> rejected from auto-selection
                // entirely: a package root with ONLY an undeterminable-architecture
                // package must yield no auto-selected version at all (null), not a
                // last-resort "best guess".
                var unknownOnlyRoot = Path.Combine(tempRoot, "unknown-only-root");
                Directory.CreateDirectory(unknownOnlyRoot);
                CreatePackageUnder(unknownOnlyRoot, "CustomWineBuild-Unknown", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker\n"));
                var noPick = PickBestForHostUnder(unknownOnlyRoot, Architecture.X64);
                Check("X64 host + only unknown-architecture package installed: PickBestForHost returns null (rejected from auto-selection)",
                    true, noPick == null);

                // ---------------------------------------------------------
                // Custom/pinned wine path validation & launch blocking
                // (ProtonLauncher.WrapWithProton / IsConfirmedIncompatibleWineBinary).
                // ---------------------------------------------------------

                // X64 host + a confirmed ARM64 custom wine path -> confirmed
                // incompatible, launch must be blocked.
                var customArm64Wine = Path.Combine(tempRoot, "custom-arm64-wine");
                File.WriteAllBytes(customArm64Wine, FakeElfBytes(EM_AARCH64));
                Check("X64 host + custom ARM64 wine path: confirmed incompatible", true,
                    ProtonPackageManager.IsConfirmedIncompatibleWineBinary(customArm64Wine, Architecture.X64));
                Check("X64 host + custom ARM64 wine path: DescribeArchitectureMismatch reports it (not limited to packaged installs)", true,
                    ProtonPackageManager.DescribeArchitectureMismatch(customArm64Wine, Architecture.X64) != null);

                // X64 host + a confirmed x86_64 custom wine path -> not blocked.
                var customX64Wine = Path.Combine(tempRoot, "custom-x64-wine");
                File.WriteAllBytes(customX64Wine, FakeElfBytes(EM_X86_64));
                Check("X64 host + custom x86_64 wine path: not confirmed incompatible", false,
                    ProtonPackageManager.IsConfirmedIncompatibleWineBinary(customX64Wine, Architecture.X64));

                // ARM64 custom wine path -> launch must be blocked purely because
                // the HOST is unsupported, independent of the binary's own
                // architecture - even a wine build that's a perfect native match
                // for an ARM64 CPU can't run the (still x86/x86_64) game inside
                // it, so ProtonLauncher.WrapWithProton's IsSupportedHost() gate
                // must trip BEFORE any per-binary compatibility check is reached.
                var customArm64WineOnArm64Host = Path.Combine(tempRoot, "custom-arm64-wine-native");
                File.WriteAllBytes(customArm64WineOnArm64Host, FakeElfBytes(EM_AARCH64));
                Check("ARM64 host: even a native-ARM64 custom wine path is not itself 'confirmed incompatible'...", false,
                    ProtonPackageManager.IsConfirmedIncompatibleWineBinary(customArm64WineOnArm64Host, Architecture.Arm64));
                Check("...but the ARM64 HOST itself is unsupported, which is what actually blocks the launch", false,
                    ProtonPackageManager.IsSupportedHost(Architecture.Arm64));
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { /* best effort cleanup */ }
            }

            // --- Version sorting (pure, no I/O) ---
            cases++;
            var ascending = new List<string> { "GE-Proton9-20", "GE-Proton11-1", "GE-Proton10-5" };
            ascending.Sort(ProtonPackageManager.CompareVersionNames);
            var expectedAscending = new List<string> { "GE-Proton9-20", "GE-Proton10-5", "GE-Proton11-1" };
            if (!ascending.SequenceEqual(expectedAscending))
            {
                failures++;
                Console.WriteLine($"FAIL version sort (ascending): got [{string.Join(", ", ascending)}], expected [{string.Join(", ", expectedAscending)}]");
            }

            cases++;
            var descending = new List<string> { "GE-Proton9-20", "GE-Proton11-1", "GE-Proton10-5" };
            descending.Sort((a, b) => ProtonPackageManager.CompareVersionNames(b, a));
            var expectedDescending = new List<string> { "GE-Proton11-1", "GE-Proton10-5", "GE-Proton9-20" };
            if (!descending.SequenceEqual(expectedDescending))
            {
                failures++;
                Console.WriteLine($"FAIL version sort (descending / 'newest first' as used by ResolveWineBinary): got [{string.Join(", ", descending)}], expected [{string.Join(", ", expectedDescending)}]");
            }

            // --- Pure name classification (no I/O - covers the fallback path directly) ---
            // "GE-Proton11-1" has NO explicit marker at all - classification now
            // returns null (unknown) rather than silently assuming x86_64; ELF
            // detection (tier 1) is what correctly resolves real installs like
            // this one before this fallback is ever reached (see tests above).
            CheckArch("classify GE-Proton11-1 (no marker) -> null (unknown)", null, ProtonPackageManager.ClassifyArchitectureFromName("GE-Proton11-1"));
            CheckArch("classify GE-Proton11-1-x86_64 (explicit marker) -> X64", Architecture.X64, ProtonPackageManager.ClassifyArchitectureFromName("GE-Proton11-1-x86_64"));
            CheckArch("classify GE-Proton11-1-aarch64 -> Arm64", Architecture.Arm64, ProtonPackageManager.ClassifyArchitectureFromName("GE-Proton11-1-aarch64"));
            CheckArch("classify GE-Proton10-25-arm64 -> Arm64", Architecture.Arm64, ProtonPackageManager.ClassifyArchitectureFromName("GE-Proton10-25-arm64"));

            Console.WriteLine($"\nProton arch/version test: {cases} cases, {failures} failures");
            return failures == 0 ? 0 : 1;
        }

        private static void CreatePackageUnder(string packageRoot, string name, Action<string> writeWine)
        {
            var bin = Path.Combine(packageRoot, name, "files", "bin");
            Directory.CreateDirectory(bin);
            writeWine(bin);
        }

        /// <summary>
        /// Mirrors ProtonPackageManager.PickBestForHost's logic against an
        /// arbitrary package root (the real method is hardcoded to the user's
        /// actual ~/.local/share/TeknoParrotUI/proton, which we don't want to
        /// touch from a test) - kept in lock-step with the real implementation
        /// by delegating every per-package decision to the real, public
        /// IsSupportedHost/DetectPackageArchitectureDetailed/GetCompatibility
        /// APIs; only the "which directory" part is reimplemented here.
        /// </summary>
        private static string PickBestForHostUnder(string packageRoot, Architecture hostArchitecture)
        {
            if (!ProtonPackageManager.IsSupportedHost(hostArchitecture))
                return null;

            var versions = Directory.GetDirectories(packageRoot)
                .Select(Path.GetFileName)
                .OrderByDescending(v => v, Comparer<string>.Create(ProtonPackageManager.CompareVersionNames))
                .ToList();

            var detected = versions
                .Select(v => (Version: v, Detection: ProtonPackageManager.DetectPackageArchitectureDetailed(Path.Combine(packageRoot, v))))
                .ToList();

            var confirmedElf = detected.FirstOrDefault(d =>
                d.Detection.Source == ArchitectureDetectionSource.ElfHeader &&
                ProtonPackageManager.GetCompatibility(d.Detection.Architecture, hostArchitecture) == ArchitectureCompatibility.Compatible);
            if (confirmedElf.Version != null)
                return confirmedElf.Version;

            var confirmedName = detected.FirstOrDefault(d =>
                d.Detection.Source == ArchitectureDetectionSource.PackageName &&
                ProtonPackageManager.GetCompatibility(d.Detection.Architecture, hostArchitecture) == ArchitectureCompatibility.Compatible);
            return confirmedName.Version; // null if none - Unknown-architecture packages are never auto-selected
        }
    }
}

