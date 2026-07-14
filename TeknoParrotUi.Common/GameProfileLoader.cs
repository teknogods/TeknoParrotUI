using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common
{
    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; }
        public static List<GameProfile> UserProfiles { get; set; }

        public static void LoadProfiles(bool onlyUserProfiles)
        {
            Directory.CreateDirectory("GameProfiles");
            var origProfiles = Directory.GetFiles("GameProfiles", "*.xml");
            Directory.CreateDirectory("UserProfiles");
            var userProfiles = Directory.GetFiles("UserProfiles", "*.xml");

            var profileList = new List<GameProfile>();
            var userprofileList = new List<GameProfile>();

            if (!onlyUserProfiles)
            {
                var lockObject = new object();
                Parallel.ForEach(origProfiles, file =>
                {
                    var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);

                    if (gameProfile == null)
                        return;

                    var isThereOther = userProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                    if (!string.IsNullOrWhiteSpace(isThereOther))
                    {
                        var other = JoystickHelper.DeSerializeGameProfile(isThereOther, true);

                        if (other == null)
                            return;

                        if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                        {
                            other.FileName = isThereOther;
                            other.ProfileName = Path.GetFileNameWithoutExtension(file);
                            other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                            // Linux support flags live in the stock profile, not in
                            // user copies saved before the field existed.
                            other.LinuxOk = gameProfile.LinuxOk;
                            other.ProtonVersion ??= gameProfile.ProtonVersion;
                            // Gamescope compatibility policy is stock-profile metadata
                            // (verified per game), never a user setting.
                            other.GamescopeGameWindowCompatibility = gameProfile.GamescopeGameWindowCompatibility;
                            other.GameInfo = JoystickHelper.DeSerializeMetadata(file);
                            if (other.GameInfo != null)
                            {
                                other.GameNameInternal = other.GameInfo.game_name;
                                other.GameGenreInternal = other.GameInfo.game_genre;
                                if (other.GameInfo.icon_name != "")
                                {
                                    other.IconName = "Icons/" + other.GameInfo.icon_name;
                                }
                            }
                            else
                            {
                                other.GameNameInternal = Path.GetFileNameWithoutExtension(file) + " (Metadata Missing)";
                            }

                            lock (lockObject)
                            {
                                profileList.Add(other);
                            }
                            return;
                        }
                        else
                        {
                            //woah automapper
                            Debug.WriteLine("gameprofile " + gameProfile.GameProfileRevision + " userprofile " + other.GameProfileRevision);

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

                            gameProfile.FileName = isThereOther;
                            gameProfile.ProfileName = Path.GetFileNameWithoutExtension(file);
                            gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                            gameProfile.GameInfo = JoystickHelper.DeSerializeMetadata(file);
                            if (gameProfile.GameInfo != null)
                            {
                                gameProfile.GameNameInternal = gameProfile.GameInfo.game_name;
                                gameProfile.GameGenreInternal = gameProfile.GameInfo.game_genre;
                                if (gameProfile.GameInfo.icon_name != "")
                                {
                                    gameProfile.IconName = "Icons/" + gameProfile.GameInfo.icon_name;
                                }
                            }
                            gameProfile.GamePath = other.GamePath;
                            gameProfile.GamePath2 = other.GamePath2;
                            JoystickHelper.SerializeGameProfile(gameProfile);
                            lock (lockObject)
                            {
                                profileList.Add(gameProfile);
                            }
                            return;
                        }
                    }
                    gameProfile.FileName = file;
                    gameProfile.ProfileName = Path.GetFileNameWithoutExtension(file);
                    gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                    gameProfile.GameInfo = JoystickHelper.DeSerializeMetadata(file);
                    if (gameProfile.GameInfo != null)
                    {
                        if (gameProfile.GameInfo.icon_name != "")
                        {
                            gameProfile.IconName = "Icons/" + gameProfile.GameInfo.icon_name;
                        }
                        gameProfile.GameNameInternal = gameProfile.GameInfo.game_name;
                        gameProfile.GameGenreInternal = gameProfile.GameInfo.game_genre;
                    }
                    else
                    {
                        gameProfile.GameNameInternal = Path.GetFileNameWithoutExtension(file) + " (Metadata Missing)";
                    }

                    lock (lockObject)
                    {
                        profileList.Add(gameProfile);
                    }

                    if (!File.Exists(gameProfile.IconName))
                    {
                        Debug.WriteLine($"{gameProfile.FileName} icon is missing! - {gameProfile.IconName}");
                    }
                });

                GameProfiles = profileList
                    .Where(IsVisibleOnThisPlatform)
                    .OrderBy(x => x.GameNameInternal)
                    .ToList();
            }

            Parallel.ForEach(userProfiles, file =>
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (gameProfile == null) return;
                var isThereOther = origProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                if (!string.IsNullOrWhiteSpace(isThereOther))
                {
                    var other = JoystickHelper.DeSerializeGameProfile(isThereOther, true);
                    if (other == null) return;

                    if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                    {
                        gameProfile.FileName = file;
                        gameProfile.ProfileName = Path.GetFileNameWithoutExtension(file);
                        gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                        // Stock profile is authoritative for Linux support flags.
                        gameProfile.LinuxOk = other.LinuxOk;
                        gameProfile.ProtonVersion ??= other.ProtonVersion;
                        gameProfile.GameInfo = JoystickHelper.DeSerializeMetadata(file);
                        if (gameProfile.GameInfo != null)
                        {
                            if (gameProfile.GameInfo.icon_name != "")
                            {
                                gameProfile.IconName = "Icons/" + gameProfile.GameInfo.icon_name;
                            }
                            gameProfile.GameNameInternal = gameProfile.GameInfo.game_name;
                            gameProfile.GameGenreInternal = gameProfile.GameInfo.game_genre;
                        }
                        else
                        {
                            gameProfile.GameNameInternal = Path.GetFileNameWithoutExtension(file) + " (Metadata Missing)";
                        }
                        lock (userprofileList)
                        {
                            userprofileList.Add(gameProfile);
                        }
                    }
                    else
                    {
                        other.FileName = isThereOther;
                        other.ProfileName = Path.GetFileNameWithoutExtension(file);
                        other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                        other.GameInfo = JoystickHelper.DeSerializeMetadata(file);
                        if (other.GameInfo != null)
                        {
                            if (other.GameInfo.icon_name != "")
                            {
                                other.IconName = "Icons/" + other.GameInfo.icon_name;
                            }
                            other.GameNameInternal = other.GameInfo.game_name;
                            other.GameGenreInternal = other.GameInfo.game_genre;
                        }
                        else
                        {
                            other.GameNameInternal = Path.GetFileNameWithoutExtension(file) + " (Metadata Missing)";
                        }
                        lock (userprofileList)
                        {
                            userprofileList.Add(other);
                        }
                        return;
                    }
                }
            });
            UserProfiles = userprofileList
                .Where(IsVisibleOnThisPlatform)
                .OrderBy(x => x.GameNameInternal)
                .ToList();

            // Controls live in InputBindings/<profile>.json (single source of
            // truth); when a JSON exists it replaces whatever bindings the XML
            // carried. Profiles without a JSON keep XML bindings (migration).
            foreach (var profile in UserProfiles)
                TeknoParrotUi.Common.InputListening.ProfileStorage.BindingsStore.Apply(profile);
            foreach (var profile in GameProfiles)
                TeknoParrotUi.Common.InputListening.ProfileStorage.BindingsStore.Apply(profile);
        }

        /// <summary>
        /// On Linux only profiles confirmed working (LinuxOk in the profile XML)
        /// are listed. Set TP_LINUX_SHOW_ALL=1 to list everything (development).
        /// Windows always shows all profiles.
        /// </summary>
        private static bool IsVisibleOnThisPlatform(GameProfile profile)
        {
            return true;
            if (!System.OperatingSystem.IsLinux())
                return true;
            if (System.Environment.GetEnvironmentVariable("TP_LINUX_SHOW_ALL") == "1")
                return true;
            return profile.LinuxOk;
        }

        static GameProfileLoader()
        {
            LoadProfiles(false);
        }
    }
}