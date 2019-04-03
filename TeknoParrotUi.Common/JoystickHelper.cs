using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public class JoystickHelper
    {
        public static bool firstTime = false;
        /// <summary>
        /// Serializes Lazydata.ParrotData to a ParrotData.xml file.
        /// </summary>
        /// <param name="joystick"></param>
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
                firstTime = true;
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
        public static void SerializeGameProfile(GameProfile profile)
        {
            var serializer = new XmlSerializer(profile.GetType());
            using (var writer = XmlWriter.Create(Path.Combine("UserProfiles\\", Path.GetFileName(profile.FileName)), new XmlWriterSettings { Indent = true }))
            {
                serializer.Serialize(writer, profile);
            }
        }

        /// <summary>
        /// Deserializes GameProfile.xml to the class.
        /// </summary>
        /// <returns>Read Gameprofile class.</returns>
        public static GameProfile DeSerializeGameProfile(string fileName)
        {
            var serializer = new XmlSerializer(typeof(GameProfile));
            using (var reader = XmlReader.Create(fileName))
            {
                var joystick = (GameProfile)serializer.Deserialize(reader);
                return joystick;
            }
        }

        public static Description DeSerializeDescription(string fileName)
        {
            var serializer = new XmlSerializer(typeof(Description));
            using (var reader = XmlReader.Create("Descriptions\\" + Path.GetFileName(fileName)))
            {
                var desc = (Description)serializer.Deserialize(reader);
                return desc;
            }
        }

        /// <summary>
        /// Serializes Description class to a Description.xml file.
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="fileName"></param>
        public static void SerializeGameProfile(Description desc, string fileName)
        {
            var serializer = new XmlSerializer(desc.GetType());
            using (var writer = XmlWriter.Create(Path.Combine("Descriptions\\", Path.GetFileName(fileName)), new XmlWriterSettings { Indent = true }))
            {
                serializer.Serialize(writer, desc);
            }
        }
    }
}
