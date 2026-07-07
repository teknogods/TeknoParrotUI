using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeknoParrotUi.Common.Updater
{
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
        public bool manualVersion { get; set; } = false;

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
                        else
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(location);
                            _localVersion = fvi.ProductVersion ?? "unknown";
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
        /// path of the UI executable/assembly used for the TeknoParrotUI component version.
        /// </summary>
        public static List<UpdaterComponent> BuildDefaultComponents(string uiLocation) => new()
        {
            new UpdaterComponent { name = "TeknoParrotUI", location = uiLocation },
            new UpdaterComponent { name = "OpenParrotWin32", location = Path.Combine("OpenParrotWin32", "OpenParrot.dll"), reponame = "OpenParrot" },
            new UpdaterComponent { name = "OpenParrotx64", location = Path.Combine("OpenParrotx64", "OpenParrot64.dll"), reponame = "OpenParrot" },
            new UpdaterComponent { name = "OpenSegaAPI", location = Path.Combine("TeknoParrot", "Opensegaapi.dll"), folderOverride = "TeknoParrot" },
            new UpdaterComponent { name = "TeknoParrot", location = Path.Combine("TeknoParrot", "TeknoParrot.dll"), opensource = false },
            new UpdaterComponent { name = "TeknoParrotN2", location = Path.Combine("N2", "TeknoParrot.dll"), reponame = "TeknoParrot", opensource = false, folderOverride = "N2" },
            new UpdaterComponent { name = "OpenSndGaelco", location = Path.Combine("TeknoParrot", "OpenSndGaelco.dll"), folderOverride = "TeknoParrot" },
            new UpdaterComponent { name = "OpenSndVoyager", location = Path.Combine("TeknoParrot", "OpenSndVoyager.dll"), folderOverride = "TeknoParrot" },
            new UpdaterComponent { name = "ScoreSubmission", location = Path.Combine("TeknoParrot", "ScoreSubmission.dll"), folderOverride = "TeknoParrot", reponame = "TeknoParrot" },
            new UpdaterComponent { name = "TeknoDraw", location = Path.Combine("TeknoParrot", "TeknoDraw64.dll"), folderOverride = "TeknoParrot", reponame = "TeknoParrot" },
            new UpdaterComponent { name = "TeknoParrotElfLdr2", location = Path.Combine("ElfLdr2", "TeknoParrot.dll"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "ElfLdr2" },
            new UpdaterComponent { name = "FFBBlaster", location = Path.Combine("FFBBlaster", "x64", "FFBBlaster64.dll"), reponame = "TeknoParrot", opensource = false, folderOverride = "FFBBlaster" },
            new UpdaterComponent { name = "CrediarDolphin", location = Path.Combine("CrediarDolphin", "Dolphin.exe"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "CrediarDolphin" },
            new UpdaterComponent { name = "Play", location = Path.Combine("Play", "Play.exe"), reponame = "TeknoParrot", opensource = false, manualVersion = true, folderOverride = "Play" },
            new UpdaterComponent { name = "RPCS3", location = Path.Combine("RPCS3", "rpcs3.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "RPCS3" },
            new UpdaterComponent { name = "cxbxr", location = Path.Combine("cxbxr", "cxbxr-ldr.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "cxbxr" },
            new UpdaterComponent { name = "pcsx2x6", location = Path.Combine("pcsx2x6", "pcsx2-qtx64.exe"), reponame = "TeknoParrot", opensource = false, folderOverride = "pcsx2x6" },
        };
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
            var response = await client.GetAsync($"{UpdateServerBase}/components");
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
            try
            {
                var serverUpdates = await GetServerUpdates();
                if (serverUpdates.TryGetValue(component.name, out var cached) && cached != null)
                    return cached;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update server unavailable ({ex.Message}), falling back to GitHub API");
            }

            using var client = CreateClient();
            var reponame = !string.IsNullOrEmpty(component.reponame) ? component.reponame : component.name;
            var owner = !string.IsNullOrEmpty(component.userName) ? component.userName : "teknogods";
            var url = $"https://api.github.com/repos/{owner}/{reponame}/releases/tags/{component.name}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"GitHub API returned {(int)response.StatusCode} for {component.name}");
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

                var onlineVersion = release.name;
                // fix for weird things like OpenParrotx64_1.0.0.30
                if (onlineVersion.Contains(component.name))
                    onlineVersion = onlineVersion.Split('_')[1];

                result.Release = release;
                result.OnlineVersion = onlineVersion;

                if (result.LocalVersion == UpdaterComponent.NotInstalled)
                    result.NeedsUpdate = true;
                else if (result.LocalVersion == "unknown")
                    result.NeedsUpdate = result.LocalVersion != onlineVersion;
                else
                    result.NeedsUpdate = GetVersionNumber(result.LocalVersion) < GetVersionNumber(onlineVersion);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Downloads the first release asset and extracts it. Extraction rules match
        /// the classic downloader: entries go under folderOverride when set, otherwise
        /// entry paths are used as-is relative to the working directory.
        /// </summary>
        public static async Task InstallUpdate(UpdateCheckResult update, IProgress<double> progress, CancellationToken ct = default)
        {
            var component = update.Component;
            var url = update.Release.assets.First().browser_download_url;

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
            bool usingOverride = !string.IsNullOrEmpty(component.folderOverride);
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Read))
            {
                int done = 0;
                foreach (var entry in zip.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = usingOverride ? Path.Combine(component.folderOverride, entry.FullName) : entry.FullName;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(name);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(name) ?? ".");
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
                    }
                    done++;
                    progress?.Report(90 + (double)done / zip.Entries.Count * 10);
                }
            }

            if (component.manualVersion && !string.IsNullOrEmpty(component.folderOverride))
                File.WriteAllText(Path.Combine(component.folderOverride, ".version"), update.OnlineVersion);

            component._localVersion = null; // re-read on next check
            progress?.Report(100);
        }
    }
}
