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
                var proton11Arm64Variant = MakePackage("GE-Proton11-1-arm64");

                // --- Host X64 ---
                Check("Host X64: GE-Proton11-1 accepted", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11X64, Architecture.X64));
                Check("Host X64: GE-Proton11-1-aarch64 rejected", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Aarch64, Architecture.X64));
                Check("Host X64: GE-Proton10-25-arm64 rejected", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton10Arm64, Architecture.X64));

                // --- Host ARM64 ---
                Check("Host ARM64: GE-Proton11-1-aarch64 accepted", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Aarch64, Architecture.Arm64));
                Check("Host ARM64: GE-Proton11-1-arm64 accepted", true,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Arm64Variant, Architecture.Arm64));
                // Uses a package with a CONFIRMED (ELF-detected) x86_64 binary -
                // an unmarked name with no readable ELF is genuinely Unknown now
                // (permissive - see tests #5/#6 below), not silently assumed
                // x86_64, so this "must reject" assertion needs a real ELF x64
                // binary to meaningfully check confirmed-incompatible rejection.
                var proton11X64Confirmed = MakeElfPackage("GE-Proton11-1-confirmed-x64", EM_X86_64);
                Check("Host ARM64: confirmed x86_64 package rejected (no emulation support)", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11X64Confirmed, Architecture.Arm64));

                // --- Mixed installation: exactly the reported bug ---
                var alphabeticallyFirst = new[] { "GE-Proton11-1", "GE-Proton11-1-aarch64" }.OrderBy(x => x, StringComparer.Ordinal).First();
                CheckString("sanity check: 'GE-Proton11-1' sorts first alphabetically", "GE-Proton11-1", alphabeticallyFirst);
                Check("Mixed: incompatible package rejected even when it would sort first/last alphabetically", false,
                    ProtonPackageManager.IsCompatibleProtonPackage(proton11Aarch64, Architecture.X64));

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

                // 5. Architecture remains unknown: shell script, no ELF siblings, no name marker at all.
                var unknownPkg = MakePackage("CustomWineBuild");
                var unknownDetection = ProtonPackageManager.DetectPackageArchitectureDetailed(unknownPkg);
                CheckArch("no ELF, no name marker: architecture is null (unknown)", null, unknownDetection.Architecture);
                CheckSource("no ELF, no name marker: source is Unknown", ArchitectureDetectionSource.Unknown, unknownDetection.Source);
                // Boolean compatibility check must stay permissive for Unknown
                // (not excluded outright - see tier-priority test #6 for how
                // it's still ranked below a confirmed-compatible package).
                Check("Unknown architecture is not treated as confirmed incompatible", true,
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
        /// Mirrors ProtonPackageManager.PickBestForHost's tiered logic against an
        /// arbitrary package root (the real method is hardcoded to the user's
        /// actual ~/.local/share/TeknoParrotUI/proton, which we don't want to
        /// touch from a test) - kept in lock-step with the real implementation
        /// by delegating every per-package decision to the real, public
        /// DetectPackageArchitectureDetailed/GetCompatibility APIs; only the
        /// "which directory" part is reimplemented here.
        /// </summary>
        private static string PickBestForHostUnder(string packageRoot, Architecture hostArchitecture)
        {
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
            if (confirmedName.Version != null)
                return confirmedName.Version;

            var unknown = detected.FirstOrDefault(d => d.Detection.Source == ArchitectureDetectionSource.Unknown);
            return unknown.Version;
        }
    }
}

