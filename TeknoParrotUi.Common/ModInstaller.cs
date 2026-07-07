using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Downloads the community mod catalog and installs mods (zip + xdelta patches)
    /// into game directories. UI-agnostic — used by both frontends.
    /// </summary>
    public static class ModInstaller
    {
        private const string ModListUrl = "https://github.com/nzgamer41/tpgamemods/releases/latest/download/mods.xml";
        private const string ModZipBase = "https://github.com/nzgamer41/tpgamemods/raw/master/";
        private const string InstalledModsFile = "InstalledMods.xml";

        public class AvailableMod
        {
            public ModData Data { get; set; }
            public GameProfile Game { get; set; }
            public bool Installed { get; set; }
            public string ZipUrl => ModZipBase + Data.GUID + ".zip";
        }

        public static async Task<List<AvailableMod>> GetAvailableModsAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrot");
            var xml = await client.GetByteArrayAsync(ModListUrl);
            var mods = Deserialize<List<ModData>>(xml);
            var installed = GetInstalledGuids();

            var result = new List<AvailableMod>();
            foreach (var mod in mods)
            {
                var userProfilePath = Path.Combine("UserProfiles", mod.GameXML);
                if (!File.Exists(userProfilePath))
                    continue; // only offer mods for installed games

                var profile = JoystickHelper.DeSerializeGameProfile(userProfilePath, true);
                if (profile == null)
                    continue;

                result.Add(new AvailableMod
                {
                    Data = mod,
                    Game = profile,
                    Installed = installed.Contains(mod.GUID)
                });
            }
            return result;
        }

        /// <summary>
        /// Downloads and installs a mod into the game's directory: zip entries are
        /// extracted (top-level folder stripped) and xdelta patches applied
        /// (.xdeltanew = new file from patch, .xdelta = patch existing file).
        /// </summary>
        public static async Task InstallModAsync(AvailableMod mod, Action<string> log = null)
        {
            var gameRoot = Path.GetDirectoryName(mod.Game.GamePath);
            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
                throw new DirectoryNotFoundException("Game directory does not exist — set the game path first.");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrot");
            var data = await client.GetByteArrayAsync(mod.ZipUrl);

            using var memoryStream = new MemoryStream(data);
            using var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                // strip the repository root folder from entry paths
                var name = entry.FullName.Substring(entry.FullName.IndexOf('/') + 1);
                if (string.IsNullOrEmpty(name))
                    continue;

                log?.Invoke($"Extracting {name}");
                var target = Path.Combine(gameRoot, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? gameRoot);

                using (var entryStream = entry.Open())
                using (var file = File.Create(target))
                {
                    entryStream.CopyTo(file);
                }

                try
                {
                    if (name.Contains(".xdeltanew"))
                    {
                        var patched = XDelta3.ApplyPatch(File.ReadAllBytes(target), Array.Empty<byte>());
                        File.WriteAllBytes(target.Replace(".xdeltanew", ""), patched);
                    }
                    else if (name.Contains(".xdelta"))
                    {
                        var original = target.Replace(".xdelta", "");
                        var patched = XDelta3.ApplyPatch(File.ReadAllBytes(target), File.ReadAllBytes(original));
                        File.WriteAllBytes(original, patched);
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Patch warning for {name}: {ex.Message}");
                }
            }

            var installed = GetInstalledGuids();
            if (!installed.Contains(mod.Data.GUID))
            {
                installed.Add(mod.Data.GUID);
                SaveInstalledGuids(installed);
            }
            mod.Installed = true;
        }

        private static List<string> GetInstalledGuids()
        {
            if (!File.Exists(InstalledModsFile))
                return new List<string>();
            try
            {
                return Deserialize<List<string>>(File.ReadAllBytes(InstalledModsFile));
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void SaveInstalledGuids(List<string> guids)
        {
            var serializer = new XmlSerializer(typeof(List<string>));
            using var writer = File.Create(InstalledModsFile);
            serializer.Serialize(writer, guids);
        }

        private static T Deserialize<T>(byte[] xml) where T : new()
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new MemoryStream(xml);
            return (T)serializer.Deserialize(reader);
        }
    }
}
