using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Result of a single environment dependency check.</summary>
    public class EnvCheckResult
    {
        public bool Found { get; set; }
        public string Detail { get; set; } = "";
    }

    /// <summary>
    /// Distro-agnostic checks for the Linux/Wine dependencies TeknoParrotUI
    /// needs, surfaced on the Linux Setup page. Deliberately avoids depending
    /// on any specific package manager (dpkg/rpm/pacman/apk) - it only checks
    /// for the actual binaries/libraries/fonts on disk (or via `which`/
    /// `ldconfig`, which are near-universal), so it works the same way on
    /// Debian/Ubuntu, Fedora, Arch, openSUSE, Alpine, NixOS, etc.
    /// </summary>
    public static class LinuxEnvironmentCheck
    {
        public static string FindWinetricks() => FindOnPath("winetricks");
        public static string FindCabextract() => FindOnPath("cabextract");

        private static string FindOnPath(string name)
        {
            foreach (var dir in new[] { "/usr/bin", "/usr/local/bin", "/bin", "/opt/bin" })
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Fall back to `which` for non-standard install locations.
            try
            {
                var found = RunCaptured("which", name)?.Trim();
                return !string.IsNullOrEmpty(found) && File.Exists(found) ? found : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Reports the currently-resolved wine binary and its version string.</summary>
        public static EnvCheckResult CheckWine(string resolvedWine)
        {
            if (string.IsNullOrEmpty(resolvedWine) || !File.Exists(resolvedWine))
                return new EnvCheckResult { Found = false, Detail = "No wine binary found" };

            try
            {
                var version = RunCaptured(resolvedWine, "--version")?.Trim();
                if (string.IsNullOrEmpty(version))
                {
                    // Ran but produced nothing - could be a silently-failing
                    // architecture mismatch (e.g. an aarch64 build on an
                    // x86_64 host with no qemu-user binfmt registered).
                    var mismatch = ProtonPackageManager.DescribeArchitectureMismatch(resolvedWine);
                    if (mismatch != null)
                        return new EnvCheckResult { Found = false, Detail = mismatch };
                }
                return new EnvCheckResult
                {
                    Found = true,
                    Detail = string.IsNullOrEmpty(version) ? resolvedWine : $"{version}  ({resolvedWine})"
                };
            }
            catch (Exception ex)
            {
                // Process.Start itself throws (e.g. "Exec format error") when
                // the kernel refuses to exec a binary built for a different
                // architecture - exactly the aarch64-on-x86_64 failure mode.
                var mismatch = ProtonPackageManager.DescribeArchitectureMismatch(resolvedWine);
                return new EnvCheckResult { Found = false, Detail = mismatch ?? $"Failed to run wine: {ex.Message}" };
            }
        }

        public static EnvCheckResult CheckWinetricks()
        {
            var path = FindWinetricks();
            return new EnvCheckResult
            {
                Found = path != null,
                Detail = path ?? "Not found - required to install DirectX 9 compatibility libraries"
            };
        }

        public static EnvCheckResult CheckCabextract()
        {
            var path = FindCabextract();
            return new EnvCheckResult
            {
                Found = path != null,
                Detail = path ?? "Not found - required by winetricks to unpack redistributables"
            };
        }

        /// <summary>CJK (Japanese) font coverage - Nesica/Japan-region titles render Shift-JIS text as tofu boxes without it.</summary>
        public static EnvCheckResult CheckCjkFonts()
        {
            try
            {
                var output = RunCaptured("fc-list", ":lang=ja");
                var count = string.IsNullOrWhiteSpace(output)
                    ? 0
                    : output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                return new EnvCheckResult
                {
                    Found = count > 0,
                    Detail = count > 0
                        ? $"{count} Japanese-capable font(s) found"
                        : "No CJK fonts found - Japanese game text will render as empty boxes (tofu)"
                };
            }
            catch
            {
                return new EnvCheckResult { Found = false, Detail = "fontconfig (fc-list) not found" };
            }
        }

        /// <summary>GTK3 + WebKitGTK - needed for TeknoParrot Online's embedded browser on Linux.</summary>
        public static EnvCheckResult CheckWebView()
        {
            var found = LibraryExists("libgtk-3.so.0") &&
                        (LibraryExists("libwebkit2gtk-4.1.so.0") || LibraryExists("libwebkit2gtk-4.0.so.37"));
            return new EnvCheckResult
            {
                Found = found,
                Detail = found
                    ? "GTK3 + WebKitGTK found"
                    : "Missing GTK3/WebKitGTK - TeknoParrot Online's embedded browser won't load"
            };
        }

        private static string[] _ldconfigCache;

        private static bool LibraryExists(string soName)
        {
            try
            {
                _ldconfigCache ??= RunCaptured("ldconfig", "-p")?.Split('\n') ?? Array.Empty<string>();
                if (_ldconfigCache.Any(l => l.Contains(soName, StringComparison.Ordinal)))
                    return true;
            }
            catch
            {
                // ldconfig missing (musl/Alpine and a few others) - fall through to a direct path probe.
            }

            foreach (var dir in new[]
                     {
                         "/usr/lib64", "/usr/lib", "/usr/lib/x86_64-linux-gnu",
                         "/lib64", "/lib", "/lib/x86_64-linux-gnu"
                     })
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, soName + "*").Any())
                    return true;
            }
            return false;
        }

        private static string RunCaptured(string file, string args)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd();
            proc?.WaitForExit(5000);
            return output;
        }

        /// <summary>
        /// Best-effort distro-family install hint parsed from /etc/os-release
        /// (ID/ID_LIKE) shown next to missing dependencies - purely
        /// informational text; TeknoParrotUI never invokes a package manager
        /// itself (no sudo automation).
        /// </summary>
        public static string GetInstallHint()
        {
            const string generic = "Install winetricks, cabextract, gtk3 and webkit2gtk via your distro's package manager.";
            try
            {
                if (!File.Exists("/etc/os-release"))
                    return generic;

                var lines = File.ReadAllLines("/etc/os-release");
                string Get(string key) => lines.FirstOrDefault(l => l.StartsWith(key + "="))?.Split('=', 2)[1].Trim('"');
                var family = ((Get("ID") ?? "") + " " + (Get("ID_LIKE") ?? "")).ToLowerInvariant();

                if (family.Contains("arch"))
                    return "sudo pacman -S winetricks cabextract gtk3 webkit2gtk";
                if (family.Contains("fedora") || family.Contains("rhel"))
                    return "sudo dnf install winetricks cabextract gtk3 webkit2gtk4.1";
                if (family.Contains("debian") || family.Contains("ubuntu"))
                    return "sudo apt install winetricks cabextract libgtk-3-0 libwebkit2gtk-4.1-0";
                if (family.Contains("suse"))
                    return "sudo zypper install winetricks cabextract gtk3 webkit2gtk3";

                return generic;
            }
            catch
            {
                return generic;
            }
        }
    }
}
