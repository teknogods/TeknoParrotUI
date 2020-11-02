using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

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
                            JoystickHelper.SerializeGameProfile(gameProfile);
                            profileList.Add(gameProfile);
                            continue;
                        }
                    }
                    gameProfile.FileName = file;
                    gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                    gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(file);
                    profileList.Add(gameProfile);
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
