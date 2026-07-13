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

        /// <summary>Downloads a release's x86_64 tarball asset and installs it via <see cref="ProtonPackageManager"/>.</summary>
        /// <param name="hostArchitecture">
        /// Overrides the host architecture used by the unsupported-host gate
        /// below - defaults to the real host. Exists purely so tests can
        /// simulate an unsupported (e.g. ARM64) host without needing to run on one.
        /// </param>
        public static async Task InstallRelease(GithubRelease release, IProgress<double> progress = null, CancellationToken ct = default, Architecture? hostArchitecture = null)
        {
            // Hard gate before touching the network at all - see
            // ProtonPackageManager.IsSupportedHost. There is no ARM64 asset to
            // download here: picking an ARM64-native Proton build would let Wine
            // itself start, but not the (still x86/x86_64) game inside it.
            ProtonPackageManager.ThrowIfUnsupportedHost(hostArchitecture);

            var asset = PickTarballAsset(release)
                ?? throw new InvalidOperationException($"No x86_64 tarball asset found for release {release.tag_name}");

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

        /// <summary>Marker substrings for non-x86_64 tarballs - always rejected, regardless of host, since ARM64 hosts aren't supported (see ProtonPackageManager.IsSupportedHost) and there is no other non-x86_64 host to ever want one of these for.</summary>
        private static readonly string[] NonX64Markers = { "aarch64", "arm64", "armhf", "armv7" };

        /// <summary>
        /// Picks the x86_64 tarball asset, rejecting any ARM/ARM64 build. Some
        /// releases publish multiple tarballs (e.g. an aarch64 build alongside
        /// the normal x86_64 one) and the GitHub API's asset order isn't
        /// guaranteed, so picking "the first tarball" could silently grab the
        /// wrong one. Deliberately NOT host-aware (unlike a previous version of
        /// this method) - TeknoParrotUI only supports Linux x86_64 hosts (see
        /// ProtonPackageManager.IsSupportedHost, checked by the caller before
        /// this is ever reached), so there's no scenario where an ARM64 asset
        /// is ever the right one to download.
        /// </summary>
        private static GithubAsset PickTarballAsset(GithubRelease release)
        {
            var tarballs = release.assets?
                .Where(a => TarballExtensions.Any(ext => a.browser_download_url.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Where(a => !NonX64Markers.Any(m => a.browser_download_url.Contains(m, StringComparison.OrdinalIgnoreCase)))
                .ToList() ?? new List<GithubAsset>();

            // Prefer an asset that explicitly names x86_64; most GE-Proton
            // releases only ship one (unmarked) tarball, which is x86_64 by
            // convention, so falling back to "the only (already-filtered)
            // tarball" covers that case.
            var explicitMarkers = new[] { "x86_64", "amd64", "x64" };
            return tarballs.FirstOrDefault(a => explicitMarkers.Any(m => a.browser_download_url.Contains(m, StringComparison.OrdinalIgnoreCase)))
                   ?? tarballs.FirstOrDefault();
        }
    }
}
