using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public class JoystickHelper
    {
        /// <summary>
        /// UI hooks so this library stays free of UI framework dependencies.
        /// The frontend (WPF, future Avalonia, headless) wires these at startup.
        /// Defaults are no-ops / safe fallbacks.
        /// </summary>
        public static Action OnFirstRun = () => { };
        public static Action<string> OnParrotDataLoadError = _ => { };
        /// <summary>Asks the user whether a corrupt profile should be deleted. Params: file name, extra detail.</summary>
        public static Func<string, string, bool> ConfirmCorruptProfileDeletion = (_, _) => false;

        private static readonly XmlSerializer gameProfileSerializer = new XmlSerializer(typeof(GameProfile));
        private static readonly XmlSerializer gameSetupSerializer = new XmlSerializer(typeof(GameSetup));
        private static readonly XmlReaderSettings readerSettings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            ValidationType = ValidationType.None
        };
        private static readonly JsonSerializer jsonSerializer = new JsonSerializer();
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
            try
            {
                var serializer = new XmlSerializer(typeof(ParrotData));
                using (var reader = XmlReader.Create("ParrotData.xml"))
                {
                    Lazydata.ParrotData = (ParrotData)serializer.Deserialize(reader);
                }
            }
            catch (FileNotFoundException)
            {
                OnFirstRun();
                // Fullscreen scaling (Gamescope AutomaticFit) is EXPERIMENTAL -
                // its central scaling assumption has been corrected but not
                // yet validated against a real TeknoParrot game's full
                // loader/JVS pipeline, multiple GPU vendors, or lightgun/
                // pointer input (see LinuxFullscreenScalingMode docs). Both
                // genuinely new installs AND existing installs whose settings
                // file simply lacks the element stay at the null default here
                // -> resolved conservatively as Disabled everywhere. Users may
                // still explicitly opt in via the Linux Setup page, and
                // TP_GAMESCOPE=1 still forces it for testing. Revisit this
                // default only in a separate, clearly justified commit once
                // real-game/lightgun validation succeeds.
                Lazydata.ParrotData = new ParrotData();
                Serialize();
            }
            catch (Exception e)
            {
                OnParrotDataLoadError(e.ToString());
                Lazydata.ParrotData = new ParrotData();
            }
        }

        /// <summary>
        /// Serializes GameProfile class to a GameProfile.xml file.
        /// </summary>
        /// <param name="profile"></param>
        public static void SerializeGameProfile(GameProfile profile, string filename = "")
        {
            using (var writer = XmlWriter.Create(filename == "" ? Path.Combine("UserProfiles", Path.GetFileName(profile.FileName)) : filename, new XmlWriterSettings { Indent = true }))
            {
                gameProfileSerializer.Serialize(writer, profile);
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

                GameSetup profile;

                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    profile = (GameSetup)gameSetupSerializer.Deserialize(reader);
                }
#if !DEBUG
                if (profile.DevOnly)
                {
                    Debug.WriteLine($"Skipping loading dev profile {fileName}");
                    return null;
                }
#endif

                return profile;
            }
            catch (Exception)
            {
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
                GameProfile profile;

                using (XmlReader reader = XmlReader.Create(fileName, readerSettings))
                {
                    profile = (GameProfile)gameProfileSerializer.Deserialize(reader);
                }
#if !DEBUG
                if (profile.DevOnly)
                {
                    Debug.WriteLine($"Skipping loading dev profile {fileName}");
                    return null;
                }
#endif

                if (profile.Is64Bit && !Environment.Is64BitOperatingSystem)
                {
                    Debug.WriteLine($"Skipping loading profile (64 bit profile on 32 bit OS) {fileName}");
                    return null;
                }

                // Add filename to profile
                profile.FileName = fileName;

                return profile;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception _e)
            {
#if DEBUG
                if (ConfirmCorruptProfileDeletion(fileName, "\n\nDebug info:\n" + _e.InnerException.Message))
#else
                if (ConfirmCorruptProfileDeletion(fileName, string.Empty))
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
                byte[] bytes = File.ReadAllBytes(metadataPath);
                return Utf8Json.JsonSerializer.Deserialize<Metadata>(bytes);
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

        /// <summary>
        /// Auto-fills online ID fields in game profiles if they're empty and matching IDs exist in user account
        /// </summary>
        public static bool AutoFillOnlineId(GameProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.OnlineIdFieldName) || profile.OnlineIdType == OnlineIdType.None)
                return false;

            var configField = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == profile.OnlineIdFieldName);
            if (configField == null || (!string.IsNullOrEmpty(configField.FieldValue) && configField.FieldValue != "1234567890"))
                return false;

            bool changed = false;
            switch (profile.OnlineIdType)
            {
                case OnlineIdType.SegaId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.SegaId))
                    {
                        configField.FieldValue = Lazydata.ParrotData.SegaId;
                        changed = true;
                    }
                    break;
                case OnlineIdType.NamcoId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.NamcoId))
                    {
                        configField.FieldValue = Lazydata.ParrotData.NamcoId;
                        changed = true;
                    }
                    break;
                case OnlineIdType.HighscoreSerial:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.ScoreSubmissionID))
                    {
                        configField.FieldValue = Lazydata.ParrotData.ScoreSubmissionID;
                        changed = true;
                    }
                    break;
                case OnlineIdType.MarioKartId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.MarioKartId))
                    {
                        configField.FieldValue = Lazydata.ParrotData.MarioKartId;
                        changed = true;
                    }
                    break;
            }

            return changed;
        }
    }
}
