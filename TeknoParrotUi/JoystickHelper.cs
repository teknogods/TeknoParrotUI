using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Helpers;

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
                MessageBoxHelper.InfoOK(Properties.Resources.FirstRun);
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
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.ErrorCantLoadParrotData, e.ToString()));
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
            if (!File.Exists(fileName))
                return null;

            try
            {
                var serializer = new XmlSerializer(typeof(GameProfile));
                GameProfile profile;

                using (var reader = XmlReader.Create(fileName))
                {
                    profile = (GameProfile)serializer.Deserialize(reader);
                }
#if !DEBUG
                if (profile.DevOnly)
                {
                    Debug.WriteLine($"Skipping loading dev profile {fileName}");
                    return null;
                }
#endif

                if (profile.Is64Bit && !App.Is64Bit())
                {
                    Debug.WriteLine($"Skipping loading profile (64 bit profile on 32 bit OS) {fileName}");
                    return null;
                }

                // Add filename to profile
                profile.FileName = fileName;

                return profile;
            }
            catch (Exception e)
            {
#if DEBUG
                if (MessageBoxHelper.ErrorYesNo(string.Format(Properties.Resources.ErrorCantLoadProfile, fileName) + "\n\nDebug info:\n" + e.InnerException.Message))
#else
                if (MessageBoxHelper.ErrorYesNo(string.Format(Properties.Resources.ErrorCantLoadProfile, fileName)))
#endif
                {
                    File.Delete(fileName);
                }
                return null;
            }
        }

        public static Metadata DeSerializeMetadata(string fileName)
        {
            var metadataPath = Path.Combine("Metadata", Path.GetFileNameWithoutExtension(fileName) + ".json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    return JsonConvert.DeserializeObject<Metadata>(File.ReadAllText(metadataPath));
                }
                catch
                {
                    Debug.WriteLine($"Error loading Metadata file {metadataPath}!");
                }
            }
            else
            {
                Debug.WriteLine($"Metadata file {metadataPath} missing!");
            }
            return null;
        }
    }
}
