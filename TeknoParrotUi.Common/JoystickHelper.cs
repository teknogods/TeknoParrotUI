using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public class JoystickHelper
    {
        /// <summary>
        /// Serializes Lazydata.ParrotData to a ParrotData.xml file.
        /// </summary>
        public static void Serialize()
        {
            var serializer = new XmlSerializer(typeof(ParrotData));
            using (var writer = XmlWriter.Create("ParrotData.xml"))
            {
                serializer.Serialize(writer, Lazydata.ParrotData);
            }
        }

        /// <summary>
        /// Deserializes ParrotData.xml to Lazydata.ParrotData.
        /// </summary>
        /// <returns>Read SettingsData class.</returns>
        public static void DeSerialize()
        {
            if (!File.Exists("ParrotData.xml"))
            {
                MessageBox.Show("Seems like this is first time you are running me, please set emulation settings.", "Hello World");
                Lazydata.ParrotData = new ParrotData();
                Serialize();
                return;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ParrotData));
                using (var reader = XmlReader.Create("ParrotData.xml"))
                {
                    Lazydata.ParrotData = (ParrotData)serializer.Deserialize(reader);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"Exception happened during loading ParrotData.xml! Generate new one by saving!{Environment.NewLine}{Environment.NewLine}{e}", "Error");
                Lazydata.ParrotData = new ParrotData();
            }
        }

        /// <summary>
        /// Serializes GameProfile class to a GameProfile.xml file.
        /// </summary>
        /// <param name="profile"></param>
        public static void SerializeGameProfile(GameProfile profile, string filename = "")
        {
            var serializer = new XmlSerializer(profile.GetType());
            using (var writer = XmlWriter.Create(filename == "" ? Path.Combine("UserProfiles", Path.GetFileName(profile.FileName)) : filename, new XmlWriterSettings { Indent = true }))
            {
                serializer.Serialize(writer, profile);
            }
        }

        /// <summary>
        /// Deserializes GameProfile.xml to the class.
        /// </summary>
        /// <returns>Read Gameprofile class.</returns>
        public static GameProfile DeSerializeGameProfile(string fileName, bool userProfile)
        {
            if (!File.Exists(fileName)) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(GameProfile));
                GameProfile profile;
                using (var reader = XmlReader.Create(fileName))
                {
                    profile = (GameProfile)serializer.Deserialize(reader);
                }
                // migrate stuff in case names get changed, only for UserProfiles
                if (userProfile)
                {
                    if (profile.EmulationProfile == EmulationProfile.FNFDrift)
                    {
                        profile.EmulationProfile = EmulationProfile.RawThrillsFNF;
                        SerializeGameProfile(profile, fileName);
                    }
                }
                return profile;
            }
            catch (Exception e)
            {
                if (MessageBox.Show($"Error loading {fileName}, would you like me to delete it?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                {
                    File.Delete(fileName);
                }
                return null;
            }
        }

        public static Description DeSerializeDescription(string fileName)
        {
            var descriptionfile = Path.Combine("Descriptions", Path.GetFileName(fileName));
            if (!File.Exists(descriptionfile))
            {
                Debug.WriteLine($"Description file {descriptionfile} missing!");
                return Description.NO_DATA;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(Description));
                using (var reader = XmlReader.Create(descriptionfile))
                {
                    var desc = (Description)serializer.Deserialize(reader);
                    return desc;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading description file {descriptionfile}!");
            }

            return Description.NO_DATA;
        }
    }
}
