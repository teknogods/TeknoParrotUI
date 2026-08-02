using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeknoParrotUi.Common.Updater
{
    public enum UpdaterDeliveryKind
    {
        DesktopArchive,
        AndroidApk,
        AndroidRuntimeArchive
    }

    /// <summary>
    /// A single updatable component. Mirrors the classic UI's component model
    /// (versions read from file metadata or .version files, releases tagged per component).
    /// </summary>
    public class UpdaterComponent
    {
        public string name { get; set; }
        public string location { get; set; }
        public string reponame { get; set; }
        public bool opensource { get; set; } = true;
        public string folderOverride { get; set; }
        public string userName { get; set; }

        /// <summary>
        /// True for TeknoParrotUI itself: a managed .NET assembly, safe to read
        /// with <see cref="FileVersionInfo"/> on every OS (unlike the other
        /// components, which are foreign native Windows PE binaries that only
        /// parse reliably with FileVersionInfo on Windows - see <see cref="localVersion"/>).
        /// </summary>
        public bool isManagedAssembly { get; set; }
        public bool manualVersion { get; set; } = false;
        /// <summary>
        /// Optional filename suffix for package-managed platforms. Android uses
        /// this to distinguish the UI and Winlator companion APKs attached to
        /// the same rolling release.
        /// </summary>
        public string assetNameMarker { get; set; }
        /// <summary>
        /// Optional filename prefix. It may be combined with
        /// <see cref="assetNameMarker"/> for a platform-specific package, or used
        /// alone for a shared versioned archive.
        /// </summary>
        public string assetNamePrefix { get; set; }
        /// <summary>
        /// Optional exact release asset filename. This takes precedence over
        /// prefix/suffix matching and prevents similarly named artifacts from
        /// being selected accidentally.
        /// </summary>
        public string assetNameExact { get; set; }
        public UpdaterDeliveryKind deliveryKind { get; set; }
        public string packageIdentity { get; set; }
        public string runtimePackageId { get; set; }

        /// <summary>
        /// Overrides the release tag/lookup key used by <see cref="UpdaterCore.GetRelease"/>
        /// (both the teknoparrot.com server cache key and the GitHub "releases/tags/&lt;tag&gt;"
        /// lookup), which otherwise default to <see cref="name"/>. Used to give the
        /// net8-migration branch's TeknoParrotUI build its own rolling release/tag
        /// ("TeknoParrotUI-net8") completely separate from the official release's
        /// "TeknoParrotUI" tag - the two channels never see or overwrite each other,
        /// and a lookup for a tag the teknoparrot.com server doesn't know about just
        /// falls through to the GitHub API (see GetRelease), which is what we want.
        /// </summary>
        public string releaseTag { get; set; }

        public string fullUrl =>
            "https://github.com/" + (!string.IsNullOrEmpty(userName) ? userName : "teknogods") + "/" +
            (!string.IsNullOrEmpty(reponame) ? reponame : name) + "/";

        public const string NotInstalled = "Not installed";

        public string _localVersion;
        public string localVersion
        {
            get
            {
                if (_localVersion == null)
                {
                    if (File.Exists(location))
                    {
                        if (manualVersion)
                        {
                            var versionFile = Path.Combine(Path.GetDirectoryName(location) ?? ".", ".version");
                            _localVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile) : "unknown";
                        }
                        else if (OperatingSystem.IsWindows() || isManagedAssembly)
                        {
                            // Managed .NET assemblies (TeknoParrotUi.dll itself) embed a
                            // real PE version resource the framework can always read,
                            // on any OS - unlike the *foreign* native Windows PE binaries
                            // below (OpenParrot.dll etc.), which is what the Linux-only
                            // PeVersionReader fallback exists for.
                            var fvi = FileVersionInfo.GetVersionInfo(location);
                            _localVersion = fvi.ProductVersion ?? "unknown";
                        }
                        else
                        {
                            // FileVersionInfo's cross-platform PE parser doesn't reliably
                            // read version resources from real Windows binaries on Linux
                            // (returns empty), which made every component show "unknown"
                            // and constantly nag for updates. Parse VS_FIXEDFILEINFO by hand.
                            _localVersion = PeVersionReader.ReadProductVersion(location) ?? "unknown";
                        }
                    }
                    else
                    {
                        _localVersion = NotInstalled;
                    }
                }
                return _localVersion;
            }
        }

        /// <summary>
        /// The standard TeknoParrot component set. <paramref name="uiLocation"/> is the
        /// path of the UI executable/assembly used for the TeknoParrotUI component version -
        /// on Linux this should be the managed TeknoParrotUi.dll (not the native apphost
        /// launcher stub, which carries no readable version resource), see UpdatesView.
        /// TeknoParrotUI now ships a Linux release (self-contained zip) alongside the
        /// Windows one under the same GitHub release/tag - UpdaterCore.InstallUpdate picks
        /// the matching platform's asset automatically.
        /// </summary>
        public static List<UpdaterComponent> BuildDefaultComponents(string uiLocation)
        {
            var components = new List<UpdaterComponent>
            {
                // net8-migration branch: own rolling release/tag, kept fully separate
                // from the official "TeknoParrotUI" release channel (see releaseTag docs).
                new UpdaterComponent { name = "TeknoParrotUI", location = uiLocation, isManagedAssembly = true, releaseTag = "TeknoParrotUI-net8" }
            };

            components.AddRange(new List<UpdaterComponent>
            {
            // OpenParrot publishes one shared archive per architecture. Windows,
            // Linux/Wine, and Android all consume these exact same release assets.
            new UpdaterComponent { name = "OpenParrotWin32", location = Path.Combine("OpenParrotWin32", "OpenParrot.dll"), reponame = "OpenParrot", assetNameExact = "OpenParrotWin32.zip" },
            new UpdaterComponent { name = "OpenParrotx64", location = Path.Combine("OpenParrotx64", "OpenParrot64.dll"), reponame = "OpenParrot", assetNameExact = "OpenParrotx64.zip" },
            new UpdaterComponent { name = "OpenSegaAPI", location = Path.Combine("TeknoParrot", "Opensegaapi.dll"), folderOverride = "TeknoParrot" },
            // These versioned Windows runtime archives are also shared with
            // Linux/Wine; their published names do not have platform suffixes.
            new UpdaterComponent { name = "TeknoParrot", location = Path.Combine("TeknoParrot", "TeknoParrot.dll"), opensource = false, assetNamePrefix = "TeknoParrotCore_" },
            new UpdaterComponent { name = "TeknoParrotN2", location = Path.Combine("N2", "TeknoParrot.dll"), reponame = "TeknoParrot", opensource = false, folderOverride = "N2" },
            new UpdaterComponent { name = "OpenSndGaelco", location = Path.Combine("TeknoParrot", "OpenSndGaelco.dll"), folderOverride = "TeknoParrot" },
            new UpdaterComponent { name = "OpenSndVoyager", location = Path.Combine("TeknoParrot", "OpenSndVoyager.dll"), folderOverride = "TeknoParrot" },
            new UpdaterComponent { name = "ScoreSubmission", location = Path.Combine("TeknoParrot", "ScoreSubmission.dll"), folderOverride = "TeknoParrot", reponame = "TeknoParrot" },
            new UpdaterComponent { name = "TeknoDraw", location = Path.Combine("TeknoParrot", "TeknoDraw64.dll"), folderOverride = "TeknoParrot", reponame = "TeknoParrot" },
            new UpdaterComponent { name = "TeknoParrotElfLdr2", location = Path.Combine("ElfLdr2", "TeknoParrot.dll"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "ElfLdr2", assetNamePrefix = "TeknoParrotElfLdr2Core_" },
            new UpdaterComponent { name = "FFBBlaster", location = Path.Combine("FFBBlaster", "x64", "FFBBlaster64.dll"), reponame = "TeknoParrot", opensource = false, folderOverride = "FFBBlaster" },
            new UpdaterComponent { name = "CrediarDolphin", location = Path.Combine("CrediarDolphin", "Dolphin.exe"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "CrediarDolphin" },
            new UpdaterComponent { name = "Play", location = Path.Combine("Play", "Play.exe"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "Play" },
            new UpdaterComponent { name = "RPCS3", location = Path.Combine("RPCS3", "rpcs3.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "RPCS3" },
            new UpdaterComponent { name = "cxbxr", location = Path.Combine("cxbxr", "cxbxr-ldr.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "cxbxr", assetNamePrefix = "cxbxr_" },
            new UpdaterComponent { name = "pcsx2x6", location = Path.Combine("pcsx2x6", "pcsx2-qtx64.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "pcsx2x6", assetNamePrefix = "pcsx2x6_" },
            });

            return components;
        }
    }

    /// <summary>Result of an update check for one component.</summary>
    public class UpdateCheckResult
    {
        public UpdaterComponent Component { get; set; }
        public GithubRelease Release { get; set; }
        public string LocalVersion { get; set; }
        public string OnlineVersion { get; set; }
        public bool NeedsUpdate { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Checks and installs component updates. Uses the teknoparrot.com update cache
    /// (single request, no GitHub rate limit) and falls back to the GitHub API.
    /// </summary>
    public class UpdaterCore
    {
        private const string UpdateServerBase = "https://teknoparrot.com/api/updates";
        private static Dictionary<string, GithubRelease> _serverUpdateCache;
        private static DateTime _serverUpdateCacheTime = DateTime.MinValue;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrot");
            return client;
        }

        public static void InvalidateCache() => _serverUpdateCache = null;

        private static async Task<Dictionary<string, GithubRelease>> GetServerUpdates()
        {
            if (_serverUpdateCache != null && DateTime.UtcNow - _serverUpdateCacheTime < TimeSpan.FromMinutes(5))
                return _serverUpdateCache;

            using var client = CreateClient();
            var platformPath = OperatingSystem.IsAndroid()
                ? "/android"
                : OperatingSystem.IsLinux()
                    ? "/linux"
                    : string.Empty;
            var response = await client.GetAsync(
                $"{UpdateServerBase}{platformPath}/components");
            response.EnsureSuccessStatusCode();

            var entries = JArray.Parse(await response.Content.ReadAsStringAsync());
            var updates = new Dictionary<string, GithubRelease>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var name = entry["component"]?.ToString();
                var release = entry["release"];
                if (string.IsNullOrEmpty(name) || release == null || release.Type == JTokenType.Null)
                    continue;
                updates[name] = release.ToObject<GithubRelease>();
            }

            _serverUpdateCache = updates;
            _serverUpdateCacheTime = DateTime.UtcNow;
            return updates;
        }

        public static async Task<GithubRelease> GetRelease(UpdaterComponent component)
        {
            // A component's release tag defaults to its display name, but can be
            // overridden (releaseTag) to point at a different tag/lookup key -
            // e.g. the net8-migration TeknoParrotUI component uses its own tag,
            // so it neither reads nor overwrites the official release's cache
            // entry/tag of the same component name.
            var lookupKey = !string.IsNullOrEmpty(component.releaseTag) ? component.releaseTag : component.name;

            try
            {
                var serverUpdates = await GetServerUpdates();
                if (serverUpdates.TryGetValue(lookupKey, out var cached) && cached != null)
                    return cached;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update server unavailable ({ex.Message}), falling back to GitHub API");
            }

            using var client = CreateClient();
            var reponame = !string.IsNullOrEmpty(component.reponame) ? component.reponame : component.name;
            var owner = !string.IsNullOrEmpty(component.userName) ? component.userName : "teknogods";
            var url = $"https://api.github.com/repos/{owner}/{reponame}/releases/tags/{lookupKey}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"GitHub API returned {(int)response.StatusCode} for {lookupKey}");
            var body = await response.Content.ReadAsStringAsync();
            return JObject.Parse(body).ToObject<GithubRelease>();
        }

        public static int GetVersionNumber(string version)
        {
            var split = version.Split('.');
            if (split.Length != 4 || string.IsNullOrEmpty(split[3]) || !int.TryParse(split[3], out var ver))
                return 0;
            return ver;
        }

        public static int CompareVersions(string left, string right)
        {
            if (TryParseFourPartVersion(left, out var leftParts) &&
                TryParseFourPartVersion(right, out var rightParts))
            {
                for (var index = 0; index < leftParts.Length; index++)
                {
                    var comparison = leftParts[index].CompareTo(rightParts[index]);
                    if (comparison != 0)
                        return comparison;
                }

                return 0;
            }

            // Preserve the historical final-component comparison for unusual
            // legacy release strings that are not strict four-part versions.
            return GetVersionNumber(left).CompareTo(GetVersionNumber(right));
        }

        private static bool TryParseFourPartVersion(
            string version,
            out long[] parts)
        {
            parts = Array.Empty<long>();
            var split = version?.Split('.');
            if (split?.Length != 4)
                return false;

            var parsed = new long[4];
            for (var index = 0; index < split.Length; index++)
            {
                if (!long.TryParse(split[index], out parsed[index]) ||
                    parsed[index] < 0)
                    return false;
            }

            parts = parsed;
            return true;
        }

        public static string GetReleaseVersion(
            UpdaterComponent component,
            GithubRelease release)
        {
            var onlineVersion = release?.name ?? string.Empty;

            // Historical runtime releases use names such as
            // OpenParrotx64_1.0.0.783. Only strip that exact prefix: a display
            // title such as "RPCS3X6 Android 0.0.116.35" also contains the
            // component name, but has no underscore to split on.
            var legacyPrefix = component?.name + "_";
            if (!string.IsNullOrEmpty(component?.name) &&
                onlineVersion.StartsWith(
                    legacyPrefix,
                    StringComparison.OrdinalIgnoreCase))
                onlineVersion = onlineVersion.Substring(legacyPrefix.Length);

            // Android release titles are presentation metadata and may include
            // a product label or lag behind the package version. The selected
            // APK filename is deterministic and is the same package the
            // installer will consume, so derive the displayed/comparable
            // version from it.
            if (component?.deliveryKind == UpdaterDeliveryKind.AndroidApk)
            {
                var asset = PickAssetForPlatform(component, release, "android");
                var fileName = GetAssetFileName(asset);
                var prefix = component.assetNamePrefix ?? string.Empty;
                var marker = component.assetNameMarker ?? string.Empty;
                if (!string.IsNullOrEmpty(fileName) &&
                    (!string.IsNullOrEmpty(prefix) ||
                     !string.IsNullOrEmpty(marker)) &&
                    fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(marker, StringComparison.OrdinalIgnoreCase) &&
                    fileName.Length > prefix.Length + marker.Length)
                {
                    return fileName.Substring(
                        prefix.Length,
                        fileName.Length - prefix.Length - marker.Length);
                }
            }

            return onlineVersion;
        }

        public static async Task<UpdateCheckResult> CheckComponent(UpdaterComponent component)
        {
            var result = new UpdateCheckResult { Component = component, LocalVersion = component.localVersion };
            try
            {
                var release = await GetRelease(component);
                if (release?.assets == null || release.assets.Count == 0)
                {
                    result.Error = "No release assets";
                    return result;
                }

                var onlineVersion = GetReleaseVersion(component, release);

                result.Release = release;
                result.OnlineVersion = onlineVersion;

                if (result.LocalVersion == UpdaterComponent.NotInstalled)
                    result.NeedsUpdate = true;
                else if (result.LocalVersion == "unknown")
                    result.NeedsUpdate = result.LocalVersion != onlineVersion;
                else
                    result.NeedsUpdate = CompareVersions(
                        result.LocalVersion,
                        onlineVersion) < 0;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Picks the release asset for the current platform. Every component
        /// except TeknoParrotUI itself ships exactly one asset (they're all
        /// Windows binaries run under Wine on both platforms). TeknoParrotUI
        /// ships a separate Windows and Linux build attached to the SAME
        /// release/tag, so its asset must be matched by filename - grabbing
        /// "the first asset" would non-deterministically download the wrong
        /// platform's build (whichever GitHub happened to list first).
        /// </summary>
        private static GithubAsset PickAssetForCurrentPlatform(UpdaterComponent component, GithubRelease release)
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
                return null;
            var marker = OperatingSystem.IsWindows() ? "win" : "linux";
            return PickAssetForPlatform(component, release, marker);
        }

        public static GithubAsset PickAssetForPlatform(
            UpdaterComponent component,
            GithubRelease release,
            string platformMarker)
        {
            var assets = release?.assets ?? new List<GithubAsset>();
            if (!string.IsNullOrWhiteSpace(component?.assetNameExact))
            {
                return assets.FirstOrDefault(asset =>
                    string.Equals(
                        GetAssetFileName(asset),
                        component.assetNameExact,
                        StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(component?.assetNameMarker) ||
                !string.IsNullOrWhiteSpace(component?.assetNamePrefix))
            {
                return assets.FirstOrDefault(a =>
                {
                    var fileName = GetAssetFileName(a);
                    return (string.IsNullOrWhiteSpace(component.assetNameMarker) ||
                            fileName.EndsWith(
                                component.assetNameMarker,
                                StringComparison.OrdinalIgnoreCase)) &&
                           (string.IsNullOrWhiteSpace(component.assetNamePrefix) ||
                            fileName.StartsWith(
                                component.assetNamePrefix,
                                StringComparison.OrdinalIgnoreCase));
                });
            }
            if (component?.name != "TeknoParrotUI")
                return assets.FirstOrDefault();

            // CI publishes these exact platform segments:
            // TeknoParrotUi-<version>-win-x64.zip and
            // TeknoParrotUi-<version>-linux-x64.zip. Never fall back to the
            // first UI asset: a temporarily incomplete release must produce a
            // clear error instead of installing the other platform's build.
            var segment = $"-{platformMarker}-";
            return assets.FirstOrDefault(a =>
                a?.browser_download_url?.Contains(segment, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static string GetAssetFileName(GithubAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset?.name))
                return asset.name;
            var path = Uri.TryCreate(
                asset?.browser_download_url,
                UriKind.Absolute,
                out var uri)
                ? uri.AbsolutePath
                : asset?.browser_download_url;
            return Path.GetFileName(path ?? string.Empty);
        }

        /// <summary>
        /// Downloads the first release asset and extracts it. Zip assets follow the
        /// classic rules (entries under folderOverride unless the component is the UI).
        /// Tarball assets (.tar.gz/.tgz/.tar.xz - e.g. the Linux Proton runtime) are
        /// streamed to disk and extracted with system tar to preserve permissions.
        /// </summary>
        public static async Task InstallUpdate(UpdateCheckResult update, IProgress<double> progress, CancellationToken ct = default)
        {
            var component = update.Component;
            var asset = PickAssetForCurrentPlatform(component, update.Release)
                ?? throw new InvalidOperationException($"No matching release asset found for {component.name}.");
            var url = asset.browser_download_url;

            var isTarball = url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                            url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
                            url.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase);
            if (isTarball)
            {
                await InstallTarball(update, url, progress, ct);
                return;
            }

            using var client = CreateClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            using var memory = new MemoryStream();
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    memory.Write(buffer, 0, read);
                    readTotal += read;
                    if (total > 0)
                        progress?.Report((double)readTotal / total * 90);
                }
            }

            memory.Position = 0;
            // Classic extraction rule: TeknoParrotUI extracts to the root,
            // everything else goes under folderOverride ?? component name.
            string destinationFolder = !string.IsNullOrEmpty(component.folderOverride) ? component.folderOverride : component.name;
            bool isUI = component.name == "TeknoParrotUI";
            var extractionRoot = Path.GetFullPath(isUI ? Environment.CurrentDirectory : destinationFolder);
            Directory.CreateDirectory(extractionRoot);
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Read))
            {
                int done = 0;
                foreach (var entry in zip.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = SafeArchivePath.Resolve(extractionRoot, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        if (!string.IsNullOrEmpty(name))
                            Directory.CreateDirectory(name);
                    }
                    else
                    {
                        var directory = Path.GetDirectoryName(name);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        try
                        {
                            entry.ExtractToFile(name, overwrite: true);
                        }
                        catch (IOException)
                        {
                            // File in use (e.g. running executable) — swap trick
                            var bak = name + ".bak";
                            if (File.Exists(bak)) File.Delete(bak);
                            File.Move(name, bak);
                            entry.ExtractToFile(name, overwrite: true);
                        }
                        SafeArchivePath.RestoreUnixPermissions(entry, name);
                    }
                    done++;
                    progress?.Report(90 + (double)done / zip.Entries.Count * 10);
                }
            }

            if (component.manualVersion && !string.IsNullOrEmpty(component.folderOverride))
                File.WriteAllText(Path.Combine(extractionRoot, ".version"), update.OnlineVersion);

            component._localVersion = null; // re-read on next check
            progress?.Report(100);
        }

        /// <summary>
        /// TeknoParrotUI can't replace its own running EXE/DLLs in-process (they're
        /// locked in memory - <see cref="InstallUpdate"/>'s ".bak swap" trick doesn't
        /// reliably survive a restart). Instead: download the release zip to cache/
        /// under the name ParrotPatcher expects ("{name}{version}.zip"), write a
        /// ".lastupdate" marker (component|version|base64-changelog) for the next
        /// launch to show a "what's new" dialog, then hand off to ParrotPatcher -
        /// which waits for this process to exit, extracts the zip and restarts the
        /// app. Caller is expected to close/exit the app right after this returns.
        /// </summary>
        public static async Task LaunchSelfUpdate(UpdateCheckResult update, IProgress<double> progress, CancellationToken ct = default)
        {
            var component = update.Component;
            var asset = PickAssetForCurrentPlatform(component, update.Release)
                ?? throw new InvalidOperationException($"No matching release asset found for {component.name}.");

            var exeName = OperatingSystem.IsWindows() ? "ParrotPatcher.exe" : "ParrotPatcher";
            var patcherPath = Path.Combine(AppContext.BaseDirectory, exeName);
            if (!File.Exists(patcherPath))
                throw new FileNotFoundException("ParrotPatcher not found next to TeknoParrotUI - cannot complete self-update.", patcherPath);

            using var client = CreateClient();
            using var response = await client.GetAsync(asset.browser_download_url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;

            var cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            Directory.CreateDirectory(cacheDir);
            // ParrotPatcher matches zip names against "^{name}\d+\.\d+\.\d+\.\d+\.zip".
            var zipPath = Path.Combine(cacheDir, $"{component.name}{update.OnlineVersion}.zip");

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var file = File.Create(zipPath))
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await file.WriteAsync(buffer, 0, read, ct);
                    readTotal += read;
                    if (total > 0)
                        progress?.Report((double)readTotal / total * 90);
                }
            }

            var changelogB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(update.Release?.body ?? ""));
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ".lastupdate"),
                $"{component.name}|{update.OnlineVersion}|{changelogB64}{Environment.NewLine}");
            progress?.Report(95);

            Process.Start(new ProcessStartInfo(patcherPath) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = true });
            progress?.Report(100);
        }

        /// <summary>
        /// Streams a large tarball asset to a temp file (no in-memory buffering) and
        /// extracts it with system tar so executable bits survive (Linux only).
        /// </summary>
        private static async Task InstallTarball(UpdateCheckResult update, string url, IProgress<double> progress, CancellationToken ct)
        {
            var component = update.Component;
            var destination = !string.IsNullOrEmpty(component.folderOverride) ? component.folderOverride : component.name;
            Directory.CreateDirectory(destination);

            var tmp = Path.Combine(Path.GetTempPath(), $"tp-{component.name}-{Guid.NewGuid():N}{Path.GetExtension(url)}");
            try
            {
                using (var client = CreateClient())
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? -1;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var file = File.Create(tmp);
                    var buffer = new byte[1 << 20];
                    long readTotal = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await file.WriteAsync(buffer, 0, read, ct);
                        readTotal += read;
                        if (total > 0)
                            progress?.Report((double)readTotal / total * 85);
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "tar",
                    UseShellExecute = false,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("-xf");
                psi.ArgumentList.Add(tmp);
                psi.ArgumentList.Add("-C");
                psi.ArgumentList.Add(destination);

                using var proc = Process.Start(psi);
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode != 0)
                    throw new IOException($"tar extraction failed ({proc.ExitCode}): {stderr}");

                if (component.manualVersion)
                    File.WriteAllText(Path.Combine(destination, ".version"), update.OnlineVersion);

                component._localVersion = null;
                progress?.Report(100);
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* ignored */ }
            }
        }
    }
}
