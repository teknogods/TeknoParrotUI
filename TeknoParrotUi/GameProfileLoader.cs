using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Common
{
    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; }
        public static List<GameProfile> UserProfiles { get; set; }

        public static void LoadProfiles(bool onlyUserProfiles)
        {
            var origProfiles = Directory.GetFiles("GameProfiles\\", "*.xml");
            Directory.CreateDirectory("UserProfiles");
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml");

            List<GameProfile> profileList = new List<GameProfile>();
            List<GameProfile> userprofileList = new List<GameProfile>();

            if (!onlyUserProfiles)
            {
                foreach (var file in origProfiles)
                {
                    var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);

                    if (gameProfile == null)
                        continue;

                    var isThereOther = userProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                    if (!string.IsNullOrWhiteSpace(isThereOther))
                    {
                        var other = JoystickHelper.DeSerializeGameProfile(isThereOther, true);

                        if (other == null)
                            continue;

                        if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                        {
                            other.FileName = isThereOther;
                            other.ProfileName = Path.GetFileNameWithoutExtension(file);
                            other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
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

                            profileList.Add(other);
                            continue;
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
                            profileList.Add(gameProfile);
                            continue;
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

                    profileList.Add(gameProfile);

                    if (!File.Exists(gameProfile.IconName))
                    {
                        Debug.WriteLine($"{gameProfile.FileName} icon is missing! - {gameProfile.IconName}");
                    }

                }

                GameProfiles = profileList.OrderBy(x => x.GameNameInternal).ToList();
            }

            foreach (var file in userProfiles)
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (gameProfile == null) continue;
                var isThereOther = origProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                if (!string.IsNullOrWhiteSpace(isThereOther))
                {
                    var other = JoystickHelper.DeSerializeGameProfile(isThereOther, true);
                    if (other == null) continue;

                    if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                    {
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
                        userprofileList.Add(gameProfile);
                        continue;
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
                        userprofileList.Add(other);
                        continue;
                    }
                }
            }
            UserProfiles = userprofileList.OrderBy(x => x.GameNameInternal).ToList();
        }

        static GameProfileLoader()
        {
            LoadProfiles(false);
        }
    }
}
