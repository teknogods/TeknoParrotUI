using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Helpers;
using Formatting = Newtonsoft.Json.Formatting;

namespace TeknoParrotUi.Common
{
    public class JoystickHelper
    {
        private static readonly JsonSerializer jsonSerializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };
        /// <summary>
        /// Serializes Lazydata.ParrotData to a ParrotData.xml file.
        /// </summary>
        public static void Serialize()
        {
            using (var writer = new StreamWriter("ParrotData.json"))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonSerializer.Serialize(jsonWriter, Lazydata.ParrotData);
            }
        }

        /// <summary>
        /// Deserializes ParrotData.xml to Lazydata.ParrotData.
        /// </summary>
        /// <returns>Read SettingsData class.</returns>
        public static async Task DeSerialize()
        {
            try
            {
                // Try to read the JSON file first
                if (File.Exists("ParrotData.json"))
                {
                    using (var reader = File.OpenText("ParrotData.json"))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        Lazydata.ParrotData = jsonSerializer.Deserialize<ParrotData>(jsonReader);
                    }
                    return;
                }

                // No config file found, create new
                //await MessageBoxHelper.InfoOK(Properties.Resources.FirstRun);
                Lazydata.ParrotData = new ParrotData();
                Serialize();
            }
            catch (FileNotFoundException)
            {
                await MessageBoxHelper.InfoOK(Properties.Resources.FirstRun);
                Lazydata.ParrotData = new ParrotData();
                Serialize();
            }
            catch (Exception e)
            {
                await MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.ErrorCantLoadParrotData, e.ToString()));
                Lazydata.ParrotData = new ParrotData();
            }
        }

        /// <summary>
        /// Serializes GameProfile class to a GameProfile.xml file.
        /// </summary>
        /// <param name="profile"></param>
        public static void SerializeGameProfile(GameProfile profile, string filename = "")
        {
            var filePath = filename == ""
                ? Path.Combine("UserProfilesJSON", Path.GetFileNameWithoutExtension(profile.FileName) + ".json")
                : Path.ChangeExtension(filename, ".json");

            using (var writer = new StreamWriter(filePath))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonSerializer.Serialize(jsonWriter, profile);
            }
        }

        /// <summary>
        /// Deserializes GameProfile.xml to the class.
        /// </summary>
        /// <returns>Read Gameprofile class.</returns>
        public static GameSetup DeSerializeGameSetup(string fileName)
        {
            try
            {
                string jsonFileName = Path.ChangeExtension(fileName, ".json");

                // Try JSON first
                if (File.Exists(jsonFileName))
                {
                    using (var reader = File.OpenText(jsonFileName))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        return jsonSerializer.Deserialize<GameSetup>(jsonReader);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deserializing GameSetup from {fileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserializes GameProfile.xml to the class.
        /// </summary>
        /// <returns>Read Gameprofile class.</returns>
        public static GameProfile DeSerializeGameProfile(string fileName, bool userProfile)
        {
            try
            {
                string jsonFileName = Path.ChangeExtension(fileName, ".json");
                GameProfile profile = null;

                // Try JSON first if it exists
                if (File.Exists(jsonFileName))
                {
                    using (var reader = File.OpenText(jsonFileName))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        profile = jsonSerializer.Deserialize<GameProfile>(jsonReader);
                    }
                }
                if (profile != null)
                {
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
                }
                else
                {
                    profile = new GameProfile();
                    profile.ProfileName = fileName;
                }

                // Add filename to profile
                profile.FileName = fileName;

                return profile;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception e)
            {
#if DEBUG
                if (MessageBoxHelper.ErrorYesNo(string.Format(Properties.Resources.ErrorCantLoadProfile, fileName) + "\n\nDebug info:\n" + e.Message).Result)
#else
                if (MessageBoxHelper.ErrorYesNo(string.Format(Properties.Resources.ErrorCantLoadProfile, fileName)).Result)
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

            try
            {
                using (var file = File.OpenText(metadataPath))
                using (var reader = new JsonTextReader(file))
                {
                    return jsonSerializer.Deserialize<Metadata>(reader);
                }
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine($"Metadata file {metadataPath} missing!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading Metadata file {metadataPath}: {ex.Message}");
            }

            return null;
        }
    }
}
