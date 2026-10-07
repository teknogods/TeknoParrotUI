using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace TeknoParrotUi.Common
{
    /// <summary>Resolves a room's immutable game ID without changing offline selections.</summary>
    public static class OnlineGameRevisionProfiles
    {
        public static GameProfile Load(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("Missing online game ID");
            if (gameId.StartsWith("mvs_", StringComparison.Ordinal) ||
                gameId.StartsWith("cps_", StringComparison.Ordinal) ||
                gameId.StartsWith("ss32_", StringComparison.Ordinal))
                return ArcadeGameRevisions.LoadOnlineProfile(gameId);

            // Existing non-revision games retain their normal profile-loading path.
            if (gameId.Any(c => !char.IsLetterOrDigit(c)) || !Directory.Exists("GameProfiles")) return null;
            foreach (var file in Directory.GetFiles("GameProfiles", "*.xml").OrderBy(path => path, StringComparer.Ordinal))
            {
                var profileId = Path.GetFileNameWithoutExtension(file);
                foreach (var emulator in new[] { EmulatorType.TeknoS22, EmulatorType.TeknoS23 })
                {
                    var identity = new GameProfile { ProfileName = profileId, EmulatorType = emulator };
                    if (!NamcoGameRevisions.GetOptions(identity).Any(option => option.Value == gameId)) continue;
                    var stock = Read(file);
                    if (stock.EmulatorType != emulator) continue;
                    if (!stock.HasTpoSupport) throw new ArgumentException("This game has no online multiplayer mode");
                    var userPath = Path.Combine("UserProfiles", profileId + ".xml");
                    var profile = File.Exists(userPath) ? Read(userPath) : stock.Clone();
                    profile.ProfileName = profileId;
                    profile.FileName = File.Exists(userPath) ? userPath : file;
                    profile.EmulatorType = stock.EmulatorType;
                    profile.EmulationProfile = stock.EmulationProfile;
                    profile.HasTpoSupport = stock.HasTpoSupport;
                    profile.ConfigValues = profile.ConfigValues ?? new List<FieldInformation>();
                    var selection = profile.ConfigValues.FirstOrDefault(field => field.FieldName == NamcoGameRevisions.SettingName);
                    if (selection == null)
                    {
                        selection = new FieldInformation
                        {
                            CategoryName = "General", FieldName = NamcoGameRevisions.SettingName,
                            FieldType = FieldType.DynamicDropdown
                        };
                        profile.ConfigValues.Add(selection);
                    }
                    selection.FieldValue = gameId;
                    NamcoGameRevisions.Populate(profile);
                    var metadata = Path.Combine("Metadata", profileId + ".json");
                    if (File.Exists(metadata)) profile.GameInfo = JsonConvert.DeserializeObject<Metadata>(File.ReadAllText(metadata));
                    profile.IconName = "Icons/" + (string.IsNullOrEmpty(profile.GameInfo?.icon_name) ? profileId + ".png" : profile.GameInfo.icon_name);
                    profile.GameNameInternal = profile.GameInfo?.game_name ?? stock.GameNameInternal;
                    profile.GameGenreInternal = profile.GameInfo?.game_genre;
                    return profile;
                }
            }
            return null;
        }

        private static GameProfile Read(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(reader);
        }
    }
}
