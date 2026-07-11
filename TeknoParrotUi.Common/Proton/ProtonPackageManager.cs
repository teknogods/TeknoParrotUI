using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Manages the optional Proton/Wine runtime packages on Linux.
    ///
    /// Layout (multiple versions can be installed side by side):
    ///   ~/.local/share/TeknoParrotUI/proton/
    ///   ├─ pipehelper.exe            (bridge helper, shared)
    ///   ├─ pipehelper32.exe
    ///   ├─ proton-ge-9.26/
    ///   │   └─ bin/wine
    ///   └─ proton-ge-9.27/
    ///       └─ bin/wine
    ///
    /// Games can pin a specific version via GameProfile.ProtonVersion;
    /// otherwise the newest installed version is used.
    /// </summary>
    public static class ProtonPackageManager
    {
        /// <summary>Root directory of installed Proton packages.</summary>
        public static string PackageRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeknoParrotUI", "proton");

        /// <summary>
        /// Installed version directory names (e.g. "GE-Proton11-1"), newest first.
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
                .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Path to the wine binary of a specific installed version, or null.
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
        /// otherwise the newest installed version, otherwise null.
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

            var newest = ListInstalledVersions().FirstOrDefault();
            return newest != null ? GetWineBinary(newest) : null;
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

        /// <summary>
        /// Path to pipehelper.exe inside the package root, or null when not installed.
        /// </summary>
        public static string GetPipeHelper()
        {
            var path = Path.Combine(PackageRoot, "pipehelper.exe");
            return File.Exists(path) ? path : null;
        }
    }
}
