using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common
{
    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; }
        public static List<GameProfile> UserProfiles { get; set; }

        static GameProfileLoader()
        {
            var origProfiles = Directory.GetFiles("GameProfiles\\", "*.xml");
            if (!Directory.Exists("UserProfiles"))
            {
                Directory.CreateDirectory("UserProfiles");
            }
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml");

            List<GameProfile> profileList = new List<GameProfile>();
            List<GameProfile> userprofileList = new List<GameProfile>();

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
                        other.IconName = "\\" + Path.GetFileNameWithoutExtension(file) + ".png";
                        profileList.Add(other);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("gameprofile " + gameProfile.GameProfileRevision + " userprofile " + other.GameProfileRevision);
                        File.Delete(userProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file)));
                        File.Copy(file, "UserProfiles\\" + Path.GetFileName(file));
                    }
                }
                gameProfile.FileName = file;
                gameProfile.IconName = "Icons\\" + Path.GetFileNameWithoutExtension(file) + ".png";
                profileList.Add(gameProfile);
            }

            GameProfiles = profileList.OrderBy(x => x.GameName).ToList();


            foreach (var file in userProfiles)
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file);
                var isThereOther = origProfiles.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(file));
                if (!string.IsNullOrWhiteSpace(isThereOther))
                {
                    var other = JoystickHelper.DeSerializeGameProfile(isThereOther);
                    if (other.GameProfileRevision == gameProfile.GameProfileRevision)
                    {
                        other.FileName = isThereOther;
                        other.IconName = "Icons\\" + Path.GetFileNameWithoutExtension(file) + ".png";
                        userprofileList.Add(other);
                        continue;
                    }
                }
                gameProfile.FileName = file;
                gameProfile.IconName = "Icons\\" + Path.GetFileNameWithoutExtension(file) + ".png";
                userprofileList.Add(gameProfile);
            }
            UserProfiles = userprofileList.OrderBy(x => x.GameName).ToList();
        }
    }
}
