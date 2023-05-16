using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using TeknoParrotUi.Helpers;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Common
{
    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; }
        public static List<GameProfile> UserProfiles { get; set; }

        public static void LoadProfiles(bool onlyUserProfiles)
        {
            if (!Directory.Exists("GameProfiles"))
            {
                // Let's not exit here, just in case there's a strange edge case where this is possible.
                MessageBoxHelper.WarningOK("Your TeknoParrot installation seems to be missing the GameProfiles folder. Please re-install!", onlyOnce: true);
                GameProfiles = new List<GameProfile>();
                UserProfiles = new List<GameProfile>();
                // MainWindow.SafeExit();
                return;
            }

            var origProfiles = Directory.GetFiles("GameProfiles\\", "*.xml");
            Directory.CreateDirectory("UserProfiles");
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml");

            // check if the icon folder even exists and has icons in it
            var iconsExists = Directory.Exists("Icons") && Directory.GetFiles("Icons").Count() > 0;

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
                            other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                            other.GameInfo = JoystickHelper.DeSerializeDescription(file);
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
                            gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                            gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(file);
                            gameProfile.GamePath = other.GamePath;
                            gameProfile.GamePath2 = other.GamePath2;
                            JoystickHelper.SerializeGameProfile(gameProfile);
                            profileList.Add(gameProfile);
                            continue;
                        }
                    }
                    gameProfile.FileName = file;
                    gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                    gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(file);
                    profileList.Add(gameProfile);

                    // only log if we at least have one icon
                    if (iconsExists)
                    {
                        if (!File.Exists(gameProfile.IconName))
                        {
                            Debug.WriteLine($"{gameProfile.FileName} icon is missing! - {gameProfile.IconName}");
                        }
                    }

                }

                GameProfiles = profileList.OrderBy(x => x.GameName).ToList();
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
                        gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                        gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(file);
                        userprofileList.Add(gameProfile);
                        continue;
                    }
                    else
                    {
                        other.FileName = isThereOther;
                        other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                        other.GameInfo = JoystickHelper.DeSerializeDescription(file);
                        userprofileList.Add(other);
                        continue;
                    }
                }
            }
            UserProfiles = userprofileList.OrderBy(x => x.GameName).ToList();
        }

        static GameProfileLoader()
        {
            LoadProfiles(false);
        }
    }
}
