using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Fetches and installs GE-Proton releases (GloriousEggroll/proton-ge-custom)
    /// for the Linux Setup page. Mirrors <see cref="Updater.UpdaterCore"/>'s
    /// GitHub download pattern; extraction is delegated to
    /// <see cref="ProtonPackageManager.InstallFromArchive"/>.
    /// </summary>
    public static class ProtonReleaseManager
    {
        private const string ReleasesUrl = "https://api.github.com/repos/GloriousEggroll/proton-ge-custom/releases?per_page=10";

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrotUI");
            return client;
        }

        /// <summary>Most recent GE-Proton releases, newest first (as published by GitHub).</summary>
        public static async Task<List<GithubRelease>> FetchReleases(CancellationToken ct = default)
        {
            using var client = CreateClient();
            var response = await client.GetAsync(ReleasesUrl, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            return JArray.Parse(body).Select(x => x.ToObject<GithubRelease>()).ToList();
        }

        /// <summary>Downloads a release's tarball asset (matching the HOST architecture) and installs it via <see cref="ProtonPackageManager"/>.</summary>
        public static async Task InstallRelease(GithubRelease release, IProgress<double> progress = null, CancellationToken ct = default)
        {
            var asset = PickTarballAsset(release)
                ?? throw new InvalidOperationException($"No {RuntimeInformation.OSArchitecture} tarball asset found for release {release.tag_name}");

            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(asset.browser_download_url));
            using (var client = CreateClient())
            using (var response = await client.GetAsync(asset.browser_download_url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(tempFile);
                var buffer = new byte[81920];
                long readSoFar = 0;
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    readSoFar += read;
                    if (total > 0)
                        progress?.Report((double)readSoFar / total);
                }
            }

            try
            {
                ProtonPackageManager.InstallFromArchive(tempFile);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }

        /// <summary>Non-tarball marker; only .tar.gz/.tar.xz assets are ever candidates.</summary>
        private static readonly string[] TarballExtensions = { ".tar.gz", ".tar.xz" };

        /// <summary>
        /// Picks the tarball asset matching the HOST's architecture, rejecting
        /// any other architecture's build. Some releases publish multiple
        /// tarballs (e.g. an aarch64 build alongside the normal x86_64 one) and
        /// the GitHub API's asset order isn't guaranteed, so picking "the first
        /// tarball" could silently grab the wrong architecture - this is
        /// exactly what can leave an incompatible package installed alongside
        /// a compatible one (see ProtonPackageManager's architecture-aware
        /// selection, which is the actual fix for auto-detection never
        /// picking one of these up by mistake). Reuses <see cref="ProtonPackageManager"/>'s
        /// arch classification so download-time and selection-time filtering
        /// can never disagree.
        /// </summary>
        private static GithubAsset PickTarballAsset(GithubRelease release)
        {
            var host = RuntimeInformation.OSArchitecture;
            var tarballs = release.assets?
                .Where(a => TarballExtensions.Any(ext => a.browser_download_url.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList() ?? new List<GithubAsset>();

            var compatible = tarballs
                .Where(a => ProtonPackageManager.GetCompatibility(ProtonPackageManager.ClassifyArchitectureFromName(a.browser_download_url), host)
                            != ArchitectureCompatibility.Incompatible)
                .ToList();

            // Prefer an asset that explicitly names the host's architecture;
            // most GE-Proton releases only ship one (unmarked, x86_64) tarball,
            // so falling back to "the only compatible one" covers that case.
            var explicitMarkers = host == Architecture.Arm64
                ? new[] { "aarch64", "arm64" }
                : new[] { "x86_64", "amd64", "x64" };

            return compatible.FirstOrDefault(a => explicitMarkers.Any(m => a.browser_download_url.Contains(m, StringComparison.OrdinalIgnoreCase)))
                   ?? compatible.FirstOrDefault();
        }
    }
}
