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
            if (!Directory.Exists("UserProfiles"))
            {
                Directory.CreateDirectory("UserProfiles");
            }
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml");

            List<GameProfile> profileList = new List<GameProfile>();
            List<GameProfile> userprofileList = new List<GameProfile>();

            if (!onlyUserProfiles)
            {
                foreach (var file in origProfiles)
                {
                    var gameProfile = JoystickHelper.DeSerializeGameProfile(file);
                    var isThereOther = userProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                    if (!string.IsNullOrWhiteSpace(isThereOther))
                    {
                        var other = JoystickHelper.DeSerializeGameProfile(isThereOther);
                        if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                        {
                            other.FileName = isThereOther;
                            other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                            profileList.Add(other);
                            continue;
                        }
                        else
                        {
                            //woah automapper
                            Debug.WriteLine("gameprofile " + gameProfile.GameProfileRevision + " userprofile " + other.GameProfileRevision);

                            for (int i = 0; i < other.JoystickButtons.Count; i++)
                            {
                                var button = gameProfile.JoystickButtons.FirstOrDefault(x =>
                                    x.ButtonName == other.JoystickButtons[i].ButtonName);
                                if (button != null)
                                {
                                    button.DirectInputButton = other.JoystickButtons[i].DirectInputButton;
                                    button.XInputButton = other.JoystickButtons[i].XInputButton;
                                    button.InputMapping = other.JoystickButtons[i].InputMapping;
                                    button.AnalogType = other.JoystickButtons[i].AnalogType;
                                    button.BindNameDi = other.JoystickButtons[i].BindNameDi;
                                    button.BindNameXi = other.JoystickButtons[i].BindNameXi;
                                    button.BindName = other.JoystickButtons[i].BindName;
                                }
                            }

                            //for (int i = 0; i < gameProfile.JoystickButtons.Count; i++)
                            //{
                            //    gameProfile.JoystickButtons[i].DirectInputButton = other.JoystickButtons[i].DirectInputButton;
                            //    gameProfile.JoystickButtons[i].XInputButton = other.JoystickButtons[i].XInputButton;
                            //    gameProfile.JoystickButtons[i].InputMapping = other.JoystickButtons[i].InputMapping;
                            //    gameProfile.JoystickButtons[i].AnalogType = other.JoystickButtons[i].AnalogType;
                            //    gameProfile.JoystickButtons[i].BindNameDi = other.JoystickButtons[i].BindNameDi;
                            //    gameProfile.JoystickButtons[i].BindNameXi = other.JoystickButtons[i].BindNameXi;
                            //    gameProfile.JoystickButtons[i].BindName = other.JoystickButtons[i].BindName;
                            //}

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
                            gameProfile.GamePath = other.GamePath;
                            JoystickHelper.SerializeGameProfile(gameProfile);
                            profileList.Add(gameProfile);
                            continue;
                        }
                    }
                    gameProfile.FileName = file;
                    gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                    profileList.Add(gameProfile);
                }

                GameProfiles = profileList.OrderBy(x => x.GameName).ToList();
            }

            foreach (var file in userProfiles)
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file);
                var isThereOther = origProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                if (!string.IsNullOrWhiteSpace(isThereOther))
                {
                    var other = JoystickHelper.DeSerializeGameProfile(isThereOther);
                    if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                    {
                        gameProfile.FileName = file;
                        gameProfile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
                        userprofileList.Add(gameProfile);
                        continue;
                    }
                    else
                    {
                        other.FileName = isThereOther;
                        other.IconName = "Icons/" + Path.GetFileNameWithoutExtension(file) + ".png";
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
