using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
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
                // The real production ProtonPackageManager.PickBestForHost(string,Architecture)
                // overload must return null for an ARM64 host regardless of what's
                // installed - there's no "best" package on a host TeknoParrotUI
                // can't run games on at all. Calling the production method
                // directly (package-root overload) rather than a test mirror.
                var arm64OnlyRoot = Path.Combine(tempRoot, "arm64-only-root");
                Directory.CreateDirectory(arm64OnlyRoot);
                CreatePackageUnder(arm64OnlyRoot, "GE-Proton11-1-confirmed-arm64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                var arm64Pick = ProtonPackageManager.PickBestForHost(arm64OnlyRoot, Architecture.Arm64);
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
                // but auto-selection (ProtonPackageManager.PickBestForHost below) must still
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

                // Real production method (package-root overload) - single
                // implementation, no test-mirrored tiering/filtering logic.
                var confirmed = ProtonPackageManager.PickBestForHost(pickRoot, Architecture.X64);
                CheckString("confirmed-compatible (GE-Proton11-1) preferred over higher-numbered unknown (GE-Proton99-1)",
                    "GE-Proton11-1", confirmed?.Version);

                // X64 host + unknown name and unknown ELF -> rejected from auto-selection
                // entirely: a package root with ONLY undeterminable-architecture
                // packages (including one with the HIGHEST version number present)
                // must yield no auto-selected version at all (null), not a
                // last-resort "best guess" or a "pick whichever has the highest
                // version number" fallback.
                var unknownOnlyRoot = Path.Combine(tempRoot, "unknown-only-root");
                Directory.CreateDirectory(unknownOnlyRoot);
                CreatePackageUnder(unknownOnlyRoot, "CustomWineBuild-Unknown-v1", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker\n"));
                CreatePackageUnder(unknownOnlyRoot, "CustomWineBuild-Unknown-v99", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker, highest version number present\n"));
                var noPick = ProtonPackageManager.PickBestForHost(unknownOnlyRoot, Architecture.X64);
                Check("X64 host + only unknown-architecture packages installed (incl. the highest version number present): production PickBestForHost returns null (never auto-selects Unknown merely for having the highest version)",
                    true, noPick == null);

                // ---------------------------------------------------------
                // Production package-selection behavior, called directly
                // through ProtonPackageManager.PickBestForHost(string,Architecture)
                // (the ONE production algorithm - no test-mirrored tiering,
                // filtering, or version-ordering logic anywhere below).
                // ---------------------------------------------------------

                // 1. X64 host selects a package confirmed as x86_64 by ELF.
                var x64OnlyRoot = Path.Combine(tempRoot, "x64-only-root");
                Directory.CreateDirectory(x64OnlyRoot);
                CreatePackageUnder(x64OnlyRoot, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                var x64Pick = ProtonPackageManager.PickBestForHost(x64OnlyRoot, Architecture.X64);
                CheckString("Production selection: X64 host selects the package confirmed x86_64 by ELF", "GE-Proton11-1", x64Pick?.Version);
                CheckArch("Production selection: selected package's Architecture is X64", Architecture.X64, x64Pick?.Architecture);
                CheckSource("Production selection: selected package's DetectionSource is ElfHeader", ArchitectureDetectionSource.ElfHeader, x64Pick?.DetectionSource ?? ArchitectureDetectionSource.Unknown);

                // 2. X64 host rejects a package confirmed as ARM64 by ELF (no
                // other candidate installed at all - production selection must
                // yield null, not fall back to the only package present).
                var armElfOnlyRoot = Path.Combine(tempRoot, "arm-elf-only-root");
                Directory.CreateDirectory(armElfOnlyRoot);
                CreatePackageUnder(armElfOnlyRoot, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                Check("Production selection: X64 host rejects a package confirmed ARM64 by ELF (no selection at all)", true,
                    ProtonPackageManager.PickBestForHost(armElfOnlyRoot, Architecture.X64) == null);

                // 3/4/5. Newest package wins by NUMERIC version ordering, not
                // alphabetical - "GE-Proton11-1" must win over "GE-Proton9-20"
                // even though "11" < "9" as a plain string compare (the exact
                // bug class this whole fix closes: an aarch64-suffixed name
                // being a "longer string" also fooled ordinal sort).
                var versionOrderRoot = Path.Combine(tempRoot, "version-order-root");
                Directory.CreateDirectory(versionOrderRoot);
                CreatePackageUnder(versionOrderRoot, "GE-Proton9-20", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                CreatePackageUnder(versionOrderRoot, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                var alphabeticalWinnerWouldBe = new[] { "GE-Proton9-20", "GE-Proton11-1" }.OrderByDescending(v => v, StringComparer.Ordinal).First();
                CheckString("sanity check: plain alphabetical order would incorrectly pick GE-Proton9-20 first", "GE-Proton9-20", alphabeticalWinnerWouldBe);
                var numericWinner = ProtonPackageManager.PickBestForHost(versionOrderRoot, Architecture.X64);
                CheckString("Production selection: GE-Proton11-1 wins over GE-Proton9-20 via numeric version ordering (plain alphabetical ordering cannot cause the wrong package to win)",
                    "GE-Proton11-1", numericWinner?.Version);

                // 10. A package name containing an ARM marker (aarch64/arm64/
                // armhf/armv7) is rejected on X64 when ELF data is unavailable
                // (non-ELF shell-script "wine", no ELF siblings - falls through
                // to the name-marker tier, which is still Incompatible on X64).
                var nameMarkedArmOnlyRoot = Path.Combine(tempRoot, "name-marked-arm-only-root");
                Directory.CreateDirectory(nameMarkedArmOnlyRoot);
                CreatePackageUnder(nameMarkedArmOnlyRoot, "GE-Proton11-1-aarch64", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unmarked ELF, only the '-aarch64' name marker identifies this package\n"));
                Check("Production selection: X64 host rejects a name-marked ARM64 package with no ELF data available (aarch64/arm64/armhf/armv7 markers never auto-selected on X64)", true,
                    ProtonPackageManager.PickBestForHost(nameMarkedArmOnlyRoot, Architecture.X64) == null);

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

                // ---------------------------------------------------------
                // ARM64 host-gate follow-up round: the centralized
                // ThrowIfUnsupportedHost helper, and every prefix/compat-
                // library/download entry point that must defend itself with
                // it independently of the main launch gate (see
                // GameSession.StartInner, ProtonLauncher.PrepareSession/
                // WrapWithProton, WinePrefixManager.EnsureDirectories,
                // ProtonLauncher.InstallCompatLibraries, ProtonReleaseManager.
                // InstallRelease, and ProtonPackageInfo's Host/Compatibility split).
                // ---------------------------------------------------------

                // --- ThrowIfUnsupportedHost: the single reusable gate helper ---
                {
                    var threwOnX64 = false;
                    try { ProtonPackageManager.ThrowIfUnsupportedHost(Architecture.X64); }
                    catch (PlatformNotSupportedException) { threwOnX64 = true; }
                    Check("ThrowIfUnsupportedHost(X64): does not throw", false, threwOnX64);

                    var threwOnArm64 = false;
                    string arm64Message = null;
                    try { ProtonPackageManager.ThrowIfUnsupportedHost(Architecture.Arm64); }
                    catch (PlatformNotSupportedException ex) { threwOnArm64 = true; arm64Message = ex.Message; }
                    Check("ThrowIfUnsupportedHost(ARM64): throws PlatformNotSupportedException", true, threwOnArm64);
                    CheckString("ThrowIfUnsupportedHost(ARM64): exception message is the centralized UnsupportedHostMessage",
                        ProtonPackageManager.UnsupportedHostMessage, arm64Message);
                }

                // --- ShouldUseProtonFor: ProtonLauncher.ShouldUseProton's fully pure/deterministic core ---
                // (the real property reads OperatingSystem.IsLinux()/RuntimeInformation.
                // OSArchitecture itself and can't be simulated on ARM64 or a non-Linux
                // host without actually running on one - this overload takes every
                // input explicitly and is exactly what the real property delegates to,
                // so every combination is testable directly.)
                Check("21. Linux + X64 + wine available -> true", true, ProtonLauncher.ShouldUseProtonFor(true, Architecture.X64, true));
                Check("22. Linux + X64 + wine unavailable -> false", false, ProtonLauncher.ShouldUseProtonFor(true, Architecture.X64, false));
                Check("23. Linux + ARM64 + native system wine available -> false (host gate wins even though a wine binary exists - the exact aarch64-system-wine bug this closes)",
                    false, ProtonLauncher.ShouldUseProtonFor(true, Architecture.Arm64, true));
                Check("24. Linux + ARM64 + wine unavailable -> false", false, ProtonLauncher.ShouldUseProtonFor(true, Architecture.Arm64, false));
                Check("25. Non-Linux + X64 + wine available -> false", false, ProtonLauncher.ShouldUseProtonFor(false, Architecture.X64, true));
                Check("26. Non-Linux + ARM64 + wine available -> false", false, ProtonLauncher.ShouldUseProtonFor(false, Architecture.Arm64, true));

                // --- ShouldUseProtonFor(Architecture, bool): the restored public ---
                // source-compatibility overload (external projects referencing
                // TeknoParrotUi.Common directly must still compile against it) -
                // reads the REAL OperatingSystem.IsLinux() internally, so unlike
                // the pure 3-arg overload above it can't simulate a non-Linux host.
                // Platform-independent: the expected value follows whatever OS
                // this test suite actually runs on (Linux, Windows, or macOS),
                // rather than hardcoding Linux - only "no wine" and "ARM64" are
                // asserted as unconditionally false, since those hold regardless
                // of host OS. Not [Obsolete] (see ProtonLauncher.cs), so no
                // warning-suppression pragma is needed to call it here.
                Check("Compatibility ShouldUseProtonFor: X64 + wine available follows the current operating system",
                    OperatingSystem.IsLinux(), ProtonLauncher.ShouldUseProtonFor(Architecture.X64, wineBinaryAvailable: true));
                Check("Compatibility ShouldUseProtonFor: no Wine is always false",
                    false, ProtonLauncher.ShouldUseProtonFor(Architecture.X64, wineBinaryAvailable: false));
                Check("Compatibility ShouldUseProtonFor: ARM64 remains unsupported",
                    false, ProtonLauncher.ShouldUseProtonFor(Architecture.Arm64, wineBinaryAvailable: true));

                // The compatibility overload must delegate to exactly the same
                // policy as the pure 3-arg overload once isLinux is pinned to the
                // real OperatingSystem.IsLinux() value - i.e. it must never apply
                // any additional/different rule of its own.
                foreach (var arch in new[] { Architecture.X64, Architecture.Arm64 })
                foreach (var wineAvailable in new[] { true, false })
                {
                    var viaCompatOverload = ProtonLauncher.ShouldUseProtonFor(arch, wineAvailable);
                    var viaPureOverload = ProtonLauncher.ShouldUseProtonFor(OperatingSystem.IsLinux(), arch, wineAvailable);
                    Check($"Compatibility ShouldUseProtonFor delegates to the pure overload's policy (arch={arch}, wine={wineAvailable})",
                        viaPureOverload, viaCompatOverload);
                }

                // --- GameLaunchPlatformGuard.ThrowIfUnsupported: the exact production ---
                // helper GameSession.StartInner() calls as its very first statement
                // (see GameLaunch/GameLaunchPlatformGuard.cs) - calling it directly
                // here means these tests exercise the identical implementation the
                // real launch path uses, without needing to construct a full
                // GameSession (heavy real dependencies: serial port handler, input
                // listeners manager, actual process launch).
                {
                    var threw = false;
                    try { GameLaunchPlatformGuard.ThrowIfUnsupported(true, Architecture.X64); }
                    catch (PlatformNotSupportedException) { threw = true; }
                    Check("27. Linux X64: GameLaunchPlatformGuard.ThrowIfUnsupported does not throw", false, threw);
                }
                {
                    var threw = false;
                    string message = null;
                    try { GameLaunchPlatformGuard.ThrowIfUnsupported(true, Architecture.Arm64); }
                    catch (PlatformNotSupportedException ex) { threw = true; message = ex.Message; }
                    Check("28. Linux ARM64: GameLaunchPlatformGuard.ThrowIfUnsupported throws PlatformNotSupportedException", true, threw);
                    CheckString("29. Linux ARM64: exception message exactly equals the centralized UnsupportedHostMessage",
                        ProtonPackageManager.UnsupportedHostMessage, message);
                }
                {
                    var threw = false;
                    try { GameLaunchPlatformGuard.ThrowIfUnsupported(false, Architecture.Arm64); }
                    catch (PlatformNotSupportedException) { threw = true; }
                    Check("30. Non-Linux ARM64: GameLaunchPlatformGuard.ThrowIfUnsupported does not throw (Linux-specific gate; other OSes never route through Proton/Wine)", false, threw);
                }
                {
                    var threw = false;
                    try { GameLaunchPlatformGuard.ThrowIfUnsupported(false, Architecture.X64); }
                    catch (PlatformNotSupportedException) { threw = true; }
                    Check("31. Non-Linux X64: GameLaunchPlatformGuard.ThrowIfUnsupported does not throw", false, threw);
                }
                // 32. This whole block calls GameLaunchPlatformGuard.ThrowIfUnsupported
                // directly - the exact same production method GameSession.StartInner()
                // invokes as its first statement (see GameSession.cs), not a copy of it.

                // --- WinePrefixManager.EnsureDirectories: both shared AND isolated prefix creation blocked on ARM64 ---
                var sharedEnvArm64 = WinePrefixManager.Resolve("GateTestGame", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, tempRoot);
                {
                    var threw = false;
                    try { WinePrefixManager.EnsureDirectories(sharedEnvArm64, Architecture.Arm64); }
                    catch (PlatformNotSupportedException) { threw = true; }
                    Check("WinePrefixManager.EnsureDirectories: ARM64 host cannot initialize a SHARED prefix", true, threw);
                }
                var isolatedEnvArm64 = WinePrefixManager.Resolve("GateTestGame", WinePrefixMode.Isolated, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, tempRoot);
                {
                    var threw = false;
                    try { WinePrefixManager.EnsureDirectories(isolatedEnvArm64, Architecture.Arm64); }
                    catch (PlatformNotSupportedException) { threw = true; }
                    Check("WinePrefixManager.EnsureDirectories: ARM64 host cannot initialize an ISOLATED prefix", true, threw);
                }
                {
                    // Sanity: a supported host must still work exactly as before -
                    // the gate must never block a legitimate X64 host.
                    var okEnv = WinePrefixManager.Resolve("GateTestGameOk", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, tempRoot);
                    WinePrefixManager.EnsureDirectories(okEnv, Architecture.X64);
                    Check("WinePrefixManager.EnsureDirectories: X64 host still creates the directory normally", true, Directory.Exists(okEnv.WinePrefixPath));
                }

                // --- ProtonLauncher.InstallCompatLibraries: blocked on ARM64, no winetricks/marker touched ---
                {
                    var compatPrefixDir = Path.Combine(tempRoot, "compat-gate-prefix");
                    Directory.CreateDirectory(compatPrefixDir);
                    var loggedLines = new List<string>();
                    var installed = ProtonLauncher.InstallCompatLibraries(compatPrefixDir, wine: "/bin/true", onOutput: loggedLines.Add, hostArchitecture: Architecture.Arm64);
                    Check("InstallCompatLibraries: ARM64 host returns false (blocked)", false, installed);
                    Check("InstallCompatLibraries: ARM64 host logs the centralized unsupported-host message", true,
                        loggedLines.Contains(ProtonPackageManager.UnsupportedHostMessage));
                    Check("InstallCompatLibraries: ARM64 host never writes the compat-installed marker (no winetricks run at all)", false,
                        File.Exists(Path.Combine(compatPrefixDir, ".tpui-compat-installed")));
                }

                // --- ProtonReleaseManager.InstallRelease: blocked before any network access ---
                {
                    var release = new GithubRelease { tag_name = "GE-Proton-Test", assets = new List<GithubAsset>() };
                    Exception thrown = null;
                    try { ProtonReleaseManager.InstallRelease(release, hostArchitecture: Architecture.Arm64).GetAwaiter().GetResult(); }
                    catch (Exception ex) { thrown = ex; }
                    Check("ProtonReleaseManager.InstallRelease: ARM64 host throws PlatformNotSupportedException (no download attempted)",
                        true, thrown is PlatformNotSupportedException);
                }

                // --- ProtonPackageInfo: HostSupported/IsUsable separate "package matches host CPU" from "host is supported at all" ---
                {
                    var unsupportedHostPkg = new ProtonPackageInfo { Version = "GE-Proton11-1-aarch64", Architecture = Architecture.Arm64, Compatibility = ArchitectureCompatibility.Compatible, HostSupported = false };
                    Check("ProtonPackageInfo: unsupported host -> IsUsable false even when Compatibility says Compatible (package matches ARM64 CPU, host still unsupported)",
                        false, unsupportedHostPkg.IsUsable);
                    CheckString("ProtonPackageInfo.ToString: unsupported-host status text",
                        "GE-Proton11-1-aarch64    ARM64 \u2014 host architecture unsupported", unsupportedHostPkg.ToString());

                    var compatiblePkg = new ProtonPackageInfo { Version = "GE-Proton11-1", Architecture = Architecture.X64, Compatibility = ArchitectureCompatibility.Compatible, HostSupported = true };
                    Check("ProtonPackageInfo: supported host + Compatible -> IsUsable true", true, compatiblePkg.IsUsable);
                    CheckString("ProtonPackageInfo.ToString: compatible status text",
                        "GE-Proton11-1    x86_64 \u2014 compatible", compatiblePkg.ToString());

                    var incompatiblePkg = new ProtonPackageInfo { Version = "GE-Proton11-1-aarch64", Architecture = Architecture.Arm64, Compatibility = ArchitectureCompatibility.Incompatible, HostSupported = true };
                    Check("ProtonPackageInfo: supported host + Incompatible -> IsUsable false", false, incompatiblePkg.IsUsable);
                    CheckString("ProtonPackageInfo.ToString: incompatible status text",
                        "GE-Proton11-1-aarch64    ARM64 \u2014 incompatible with this system", incompatiblePkg.ToString());

                    var unknownPkg2 = new ProtonPackageInfo { Version = "GE-ProtonCustom", Architecture = null, Compatibility = ArchitectureCompatibility.Unknown, HostSupported = true };
                    Check("ProtonPackageInfo: supported host + Unknown -> IsUsable false", false, unknownPkg2.IsUsable);
                    CheckString("ProtonPackageInfo.ToString: unknown-architecture status text",
                        "GE-ProtonCustom    unknown \u2014 architecture undetermined", unknownPkg2.ToString());
                }

                // --- FindConfirmedCompatibleAlternative: the real production helper behind ---
                // DescribeArchitectureMismatch's "Select X instead" text - only a
                // CONFIRMED-compatible package may ever be suggested; an Unknown-
                // architecture package must never be offered as "the fix", an ARM64
                // package must never be suggested on an X64 host, the current/
                // excluded version must never be suggested back to itself, the
                // newest confirmed-compatible package wins via real numeric
                // ordering, and an unsupported host must short-circuit to null
                // before any package is even considered.
                {
                    var altRoot = Path.Combine(tempRoot, "alt-root");
                    Directory.CreateDirectory(altRoot);
                    CreatePackageUnder(altRoot, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    CreatePackageUnder(altRoot, "GE-ProtonUnknown", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker\n"));

                    // 15. Unknown-architecture alternative is never suggested (only
                    // an ARM64-ELF package and an Unknown package exist so far -
                    // neither is Compatible on an X64 host).
                    Check("15. FindConfirmedCompatibleAlternative: no confirmed-compatible package installed yet -> null (unknown/ARM64 packages never suggested)",
                        true, ProtonPackageManager.FindConfirmedCompatibleAlternative(altRoot, "GE-Proton11-1-aarch64", Architecture.X64) == null);

                    CreatePackageUnder(altRoot, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));

                    // 13. A confirmed x86_64 alternative may be suggested on an x86_64 host.
                    var suggestion = ProtonPackageManager.FindConfirmedCompatibleAlternative(altRoot, "GE-Proton11-1-aarch64", Architecture.X64);
                    CheckString("13. FindConfirmedCompatibleAlternative: a confirmed-compatible package IS suggested once installed",
                        "GE-Proton11-1", suggestion?.Version);

                    // 14. An ARM64 alternative is never suggested on an x86_64 host,
                    // even when it's the only OTHER package and isn't the excluded one.
                    var armOnlyAltRoot = Path.Combine(tempRoot, "arm-only-alt-root");
                    Directory.CreateDirectory(armOnlyAltRoot);
                    CreatePackageUnder(armOnlyAltRoot, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    Check("14. FindConfirmedCompatibleAlternative: an ARM64 package is never suggested on an X64 host", true,
                        ProtonPackageManager.FindConfirmedCompatibleAlternative(armOnlyAltRoot, null, Architecture.X64) == null);

                    // 16. The current/excluded version is never suggested back to
                    // itself, even when it's the only (otherwise confirmed-compatible) package installed.
                    var soleVersionRoot = Path.Combine(tempRoot, "sole-version-root");
                    Directory.CreateDirectory(soleVersionRoot);
                    CreatePackageUnder(soleVersionRoot, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                    Check("16. FindConfirmedCompatibleAlternative: the excluded/current version is never suggested back to itself", true,
                        ProtonPackageManager.FindConfirmedCompatibleAlternative(soleVersionRoot, "GE-Proton11-1", Architecture.X64) == null);

                    // 17. The newest confirmed-compatible alternative wins using
                    // real numeric ordering (reuses versionOrderRoot: GE-Proton11-1 + GE-Proton9-20, both confirmed x86_64).
                    var newestAlternative = ProtonPackageManager.FindConfirmedCompatibleAlternative(versionOrderRoot, "SomeOtherMismatchedPackage", Architecture.X64);
                    CheckString("17. FindConfirmedCompatibleAlternative: newest confirmed-compatible alternative wins via real numeric ordering",
                        "GE-Proton11-1", newestAlternative?.Version);

                    // 18/19. An unsupported ARM64 host returns null from the
                    // alternative lookup outright - even when a package installed
                    // is a perfect ELF-confirmed match for THAT (unsupported) host
                    // architecture, proving the host-supported gate is checked
                    // before any per-package compatibility consideration at all.
                    var armHostRoot = Path.Combine(tempRoot, "arm-host-alt-root");
                    Directory.CreateDirectory(armHostRoot);
                    CreatePackageUnder(armHostRoot, "GE-Proton11-1-native-arm64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    Check("18/19. FindConfirmedCompatibleAlternative: unsupported ARM64 host -> null (no alternative lookup result at all, not even a native-ARM64-matching package)", true,
                        ProtonPackageManager.FindConfirmedCompatibleAlternative(armHostRoot, null, Architecture.Arm64) == null);
                }

                // --- DescribeArchitectureMismatch(packageRoot, wineBinary, hostArchitecture): ---
                // end-to-end tests against the COMPLETE production message-selection
                // implementation (package-root-aware internal overload), using
                // temporary package directories and fake ELF binaries - never
                // recreating its message-selection or alternative-selection logic
                // here, only building fixtures and asserting on the real output.
                {
                    // 1. Unsupported ARM64 host: exact centralized UnsupportedHostMessage,
                    // no alternative suggestion, independent of what's installed.
                    var mismatchWineFile = Path.Combine(tempRoot, "any-wine-for-mismatch-test");
                    File.WriteAllBytes(mismatchWineFile, FakeElfBytes(EM_X86_64));
                    var arm64HostResult = ProtonPackageManager.DescribeArchitectureMismatch(tempRoot, mismatchWineFile, Architecture.Arm64);
                    CheckString("1. DescribeArchitectureMismatch: ARM64 host -> centralized unsupported-host message (not an architecture-mismatch message)",
                        ProtonPackageManager.UnsupportedHostMessage, arm64HostResult);
                    Check("1. DescribeArchitectureMismatch: ARM64 host message includes no alternative suggestion", false,
                        (arm64HostResult ?? "").Contains("Select ", StringComparison.Ordinal));

                    // 2. X64 host, selected ARM64 managed package, one confirmed X64 alternative:
                    // reports incompatible AND suggests the confirmed alternative.
                    var scenario2Root = Path.Combine(tempRoot, "e2e-scenario2");
                    Directory.CreateDirectory(scenario2Root);
                    CreatePackageUnder(scenario2Root, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    CreatePackageUnder(scenario2Root, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                    var scenario2Wine = Path.Combine(scenario2Root, "GE-Proton11-1-aarch64", "files", "bin", "wine");
                    var scenario2Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario2Root, scenario2Wine, Architecture.X64);
                    Check("2. DescribeArchitectureMismatch: selected ARM64 package reported incompatible", true,
                        scenario2Result != null && scenario2Result.Contains("ARM64", StringComparison.Ordinal));
                    Check("2. DescribeArchitectureMismatch: suggests the confirmed X64 alternative (GE-Proton11-1)", true,
                        scenario2Result != null && scenario2Result.Contains("Select GE-Proton11-1 instead of GE-Proton11-1-aarch64.", StringComparison.Ordinal));
                    // 8. Current package exclusion: the selected/current package must
                    // never be suggested as its own replacement.
                    Check("8. DescribeArchitectureMismatch: never suggests the selected/current package as its own replacement", false,
                        scenario2Result != null && scenario2Result.Contains("Select GE-Proton11-1-aarch64 instead of GE-Proton11-1-aarch64", StringComparison.Ordinal));

                    // 3. X64 host, selected ARM64 managed package, only an unknown
                    // alternative installed: reports incompatible, no "select the
                    // unknown package instead" recommendation.
                    var scenario3Root = Path.Combine(tempRoot, "e2e-scenario3");
                    Directory.CreateDirectory(scenario3Root);
                    CreatePackageUnder(scenario3Root, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    CreatePackageUnder(scenario3Root, "GE-ProtonUnknown", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker\n"));
                    var scenario3Wine = Path.Combine(scenario3Root, "GE-Proton11-1-aarch64", "files", "bin", "wine");
                    var scenario3Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario3Root, scenario3Wine, Architecture.X64);
                    Check("3. DescribeArchitectureMismatch: selected ARM64 package reported incompatible (unknown-only alternative present)", true,
                        scenario3Result != null && scenario3Result.Contains("ARM64", StringComparison.Ordinal));
                    Check("3. DescribeArchitectureMismatch: does not suggest the unknown-architecture package", false,
                        scenario3Result != null && scenario3Result.Contains("Select GE-ProtonUnknown instead", StringComparison.Ordinal));

                    // 4. X64 host, selected ARM64 managed package, no alternative at
                    // all: reports incompatible, no replacement suggestion whatsoever.
                    var scenario4Root = Path.Combine(tempRoot, "e2e-scenario4");
                    Directory.CreateDirectory(scenario4Root);
                    CreatePackageUnder(scenario4Root, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    var scenario4Wine = Path.Combine(scenario4Root, "GE-Proton11-1-aarch64", "files", "bin", "wine");
                    var scenario4Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario4Root, scenario4Wine, Architecture.X64);
                    Check("4. DescribeArchitectureMismatch: selected ARM64 package reported incompatible (no alternative installed)", true,
                        scenario4Result != null && scenario4Result.Contains("ARM64", StringComparison.Ordinal));
                    Check("4. DescribeArchitectureMismatch: no replacement suggestion when none exists", false,
                        scenario4Result != null && scenario4Result.Contains("Select ", StringComparison.Ordinal));

                    // 5. X64 host, confirmed-compatible selected package -> null.
                    var scenario5Root = Path.Combine(tempRoot, "e2e-scenario5");
                    Directory.CreateDirectory(scenario5Root);
                    CreatePackageUnder(scenario5Root, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                    var scenario5Wine = Path.Combine(scenario5Root, "GE-Proton11-1", "files", "bin", "wine");
                    Check("5. DescribeArchitectureMismatch: confirmed-compatible selected package -> null", true,
                        ProtonPackageManager.DescribeArchitectureMismatch(scenario5Root, scenario5Wine, Architecture.X64) == null);

                    // 6. X64 host, unknown selected package -> architecture-undetermined
                    // warning, never presented as compatible (non-null).
                    var scenario6Root = Path.Combine(tempRoot, "e2e-scenario6");
                    Directory.CreateDirectory(scenario6Root);
                    CreatePackageUnder(scenario6Root, "GE-ProtonCustom", bin => File.WriteAllText(Path.Combine(bin, "wine"), "#!/bin/sh\n# unknown arch, no name marker\n"));
                    var scenario6Wine = Path.Combine(scenario6Root, "GE-ProtonCustom", "files", "bin", "wine");
                    var scenario6Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario6Root, scenario6Wine, Architecture.X64);
                    Check("6. DescribeArchitectureMismatch: unknown selected package returns a non-null (not 'compatible') result", true,
                        scenario6Result != null);
                    Check("6. DescribeArchitectureMismatch: unknown selected package returns the architecture-undetermined warning", true,
                        scenario6Result != null && scenario6Result.Contains("Could not determine the architecture", StringComparison.Ordinal));

                    // 7. Multiple confirmed-compatible alternatives: newest wins via
                    // real numeric Proton version ordering (GE-Proton11-1 over GE-Proton9-20).
                    var scenario7Root = Path.Combine(tempRoot, "e2e-scenario7");
                    Directory.CreateDirectory(scenario7Root);
                    CreatePackageUnder(scenario7Root, "GE-Proton11-1-aarch64", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_AARCH64)));
                    CreatePackageUnder(scenario7Root, "GE-Proton9-20", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                    CreatePackageUnder(scenario7Root, "GE-Proton11-1", bin => File.WriteAllBytes(Path.Combine(bin, "wine"), FakeElfBytes(EM_X86_64)));
                    var scenario7Wine = Path.Combine(scenario7Root, "GE-Proton11-1-aarch64", "files", "bin", "wine");
                    var scenario7Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario7Root, scenario7Wine, Architecture.X64);
                    Check("7. DescribeArchitectureMismatch: newest confirmed-compatible alternative (GE-Proton11-1) suggested over GE-Proton9-20", true,
                        scenario7Result != null && scenario7Result.Contains("Select GE-Proton11-1 instead of GE-Proton11-1-aarch64.", StringComparison.Ordinal));
                    Check("7. DescribeArchitectureMismatch: older confirmed-compatible alternative (GE-Proton9-20) is not suggested", false,
                        scenario7Result != null && scenario7Result.Contains("Select GE-Proton9-20", StringComparison.Ordinal));

                    // 9. External custom Wine binary outside the supplied package
                    // root: checked directly via ELF detection (no package name to
                    // fall back on), independent of whatever else is under packageRoot.
                    var externalBinDir = Path.Combine(tempRoot, "e2e-external-bin-dir");
                    Directory.CreateDirectory(externalBinDir);
                    var scenario9PackageRoot = Path.Combine(tempRoot, "e2e-scenario9-empty-package-root");
                    Directory.CreateDirectory(scenario9PackageRoot);

                    var externalX64Wine = Path.Combine(externalBinDir, "custom-x64-wine");
                    File.WriteAllBytes(externalX64Wine, FakeElfBytes(EM_X86_64));
                    Check("9. DescribeArchitectureMismatch: external confirmed-x86_64 binary outside package root -> null on X64", true,
                        ProtonPackageManager.DescribeArchitectureMismatch(scenario9PackageRoot, externalX64Wine, Architecture.X64) == null);

                    var externalArm64Wine = Path.Combine(externalBinDir, "custom-arm64-wine");
                    File.WriteAllBytes(externalArm64Wine, FakeElfBytes(EM_AARCH64));
                    var externalArm64Result = ProtonPackageManager.DescribeArchitectureMismatch(scenario9PackageRoot, externalArm64Wine, Architecture.X64);
                    Check("9. DescribeArchitectureMismatch: external confirmed-ARM64 binary outside package root -> incompatibility message on X64", true,
                        externalArm64Result != null && externalArm64Result.Contains("ARM64", StringComparison.Ordinal));

                    var externalUnknownWine = Path.Combine(externalBinDir, "custom-unknown-wine");
                    File.WriteAllText(externalUnknownWine, "#!/bin/sh\n# unknown arch, no ELF, no name marker, and not inside any package root\n");
                    var externalUnknownResult = ProtonPackageManager.DescribeArchitectureMismatch(scenario9PackageRoot, externalUnknownWine, Architecture.X64);
                    Check("9. DescribeArchitectureMismatch: external unknown-architecture binary outside package root -> undetermined warning", true,
                        externalUnknownResult != null && externalUnknownResult.Contains("Could not determine the architecture", StringComparison.Ordinal));
                }
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
    }
}

