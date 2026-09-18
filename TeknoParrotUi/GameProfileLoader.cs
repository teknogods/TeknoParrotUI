using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Common
{
    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; }
        public static List<GameProfile> UserProfiles { get; set; }

        public static void LoadProfiles(bool onlyUserProfiles)
        {
            var origProfiles = Directory.GetFiles("GameProfiles\\", "*.xml")
                .ToDictionary(Path.GetFileName, StringComparer.Ordinal);
            Directory.CreateDirectory("UserProfiles");
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml")
                .ToDictionary(Path.GetFileName, StringComparer.Ordinal);
            var profileList = new List<GameProfile>();
            var userProfileList = new List<GameProfile>();
            var lockObject = new object();
            var metadataCatalog = JoystickHelper.LoadMetadataCatalog();

            var files = onlyUserProfiles
                ? userProfiles.Keys.Where(origProfiles.ContainsKey).Select(name => origProfiles[name])
                : origProfiles.Values.AsEnumerable();
            Parallel.ForEach(files, file =>
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (gameProfile == null)
                    return;

                var hasUserProfile = userProfiles.TryGetValue(Path.GetFileName(file), out var userFile);
                var migrated = false;
                if (hasUserProfile)
                {
                    var other = JoystickHelper.DeSerializeGameProfile(userFile, true);
                    if (other == null)
                        return;

                    if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                    {
                        gameProfile = other;
                    }
                    else if (!onlyUserProfiles)
                    {
                        ProfileOperations.MergeUserSettings(gameProfile, other);
                        gameProfile.FileName = userFile;
                        migrated = true;
                    }
                }

                ProfileOperations.PopulateMetadata(gameProfile, file, metadataCatalog);
                if (migrated)
                    JoystickHelper.SerializeGameProfile(gameProfile);

                var libraryProfile = hasUserProfile && !onlyUserProfiles ? gameProfile.Clone() : gameProfile;
                lock (lockObject)
                {
                    if (!onlyUserProfiles)
                        profileList.Add(gameProfile);
                    if (hasUserProfile)
                        userProfileList.Add(libraryProfile);
                }

                if (!hasUserProfile && !File.Exists(gameProfile.IconName))
                    Debug.WriteLine($"{gameProfile.FileName} icon is missing! - {gameProfile.IconName}");
            });

            if (!onlyUserProfiles)
                GameProfiles = profileList.OrderBy(x => x.GameNameInternal).ToList();
            UserProfiles = userProfileList.OrderBy(x => x.GameNameInternal).ToList();
        }

        private static class ProfileOperations
        {
            public static void PopulateMetadata(GameProfile profile, string file,
                IReadOnlyDictionary<string, Metadata> metadataCatalog)
            {
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                profile.IconName = "Icons/" + profile.ProfileName + ".png";
                profile.GameInfo = metadataCatalog != null &&
                    metadataCatalog.TryGetValue(profile.ProfileName, out var metadata) && metadata != null
                    ? metadata
                    : JoystickHelper.DeSerializeMetadata(file);
                if (profile.GameInfo != null)
                {
                    profile.GameNameInternal = profile.GameInfo.game_name;
                    profile.GameGenreInternal = profile.GameInfo.game_genre;
                    if (profile.GameInfo.icon_name != "")
                        profile.IconName = "Icons/" + profile.GameInfo.icon_name;
                }
                else
                {
                    profile.GameNameInternal = profile.ProfileName + " (Metadata Missing)";
                }
            }

            public static void MergeUserSettings(GameProfile gameProfile, GameProfile other)
            {
                for (int i = 0; i < other.JoystickButtons.Count; i++)
                {
                    var button = gameProfile.JoystickButtons.FirstOrDefault(x => x.ButtonName == other.JoystickButtons[i].ButtonName);

                    if (button != null)
                    {
                        button.DirectInputButton = other.JoystickButtons[i].DirectInputButton;
                        button.XInputButton = other.JoystickButtons[i].XInputButton;
                        button.RawInputButton = other.JoystickButtons[i].RawInputButton;
                        button.BindNameDi = other.JoystickButtons[i].BindNameDi;
                        button.BindNameXi = other.JoystickButtons[i].BindNameXi;
                        button.BindNameRi = other.JoystickButtons[i].BindNameRi;
                        button.BindName = other.JoystickButtons[i].BindName;

                        // Clear DolphinBar binds without DevicePath
                        if (button.BindNameRi != null && button.BindNameRi.Contains("DolphinBar") && string.IsNullOrWhiteSpace(button.RawInputButton?.DevicePath))
                        {
                            var riButton = new RawInputButton
                            {
                                DevicePath = "",
                                DeviceType = RawDeviceType.None,
                                MouseButton = RawMouseButton.None,
                                KeyboardKey = Keys.None
                            };

                            button.RawInputButton = riButton;
                            button.BindNameRi = "";
                        }
                    }
                }

                for (int i = 0; i < gameProfile.ConfigValues.Count; i++)
                {
                    for (int j = 0; j < other.ConfigValues.Count; j++)
                    {
                        if (gameProfile.ConfigValues[i].FieldName == other.ConfigValues[j].FieldName)
                        {
                            gameProfile.ConfigValues[i].FieldValue = other.ConfigValues[j].FieldValue;
                        }
                    }
                }

                gameProfile.GamePath = other.GamePath;
                gameProfile.GamePath2 = other.GamePath2;
            }
        }

        static GameProfileLoader()
        {
            LoadProfiles(false);
        }
    }
}
