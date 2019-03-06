using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public class JoystickHelper
    {
        /// <summary>
        /// Serializes SettingsData class to a ParrotData.xml file.
        /// </summary>
        /// <param name="joystick"></param>
        public static void Serialize(ParrotData joystick)
        {
            Lazydata.ParrotData = joystick;
            var serializer = new XmlSerializer(joystick.GetType());
            using (var writer = XmlWriter.Create("ParrotData.xml"))
            {
                serializer.Serialize(writer, joystick);
            }
        }

        /// <summary>
        /// Deserializes ParrotData.xml to the class.
        /// </summary>
        /// <returns>Read SettingsData class.</returns>
        public static ParrotData DeSerialize()
        {
            var serializer = new XmlSerializer(typeof(ParrotData));
            if (!File.Exists("ParrotData.xml"))
                return null;
            using (var reader = XmlReader.Create("ParrotData.xml"))
            {
                var joystick = (ParrotData)serializer.Deserialize(reader);
                Lazydata.ParrotData = joystick;
                return joystick;
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
