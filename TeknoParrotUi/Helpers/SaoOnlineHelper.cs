using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Sword Art Online Arcade online in TeknoParrotUI. A player is their access code and nothing else: teknoparrot.com issues
    /// one per account (api/User/Profile SaoAccessCode), TPUI keeps it in ParrotData, shows it on the Account page and fills it
    /// into the [Card] AccessCode field of the profiles whose OnlineIdType is SaoAccessCode, which the ini writer puts into
    /// teknoparrot.ini like every other field. Without a login the player types the code into that field; a code typed by hand
    /// is never replaced. When the account gets a new code (the website's "Get a new access code"), the profiles that still
    /// hold the account's old one get the new one. Nothing here touches a profile whose OnlineIdType is not SaoAccessCode.
    /// </summary>
    public static class SaoOnlineHelper
    {
        public const string AccessCodeField = "AccessCode";
        public const int CodeLength = 20;

        /// <summary>The code without spaces and dashes (20 digits), or null when it can't be one.</summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var digits = new StringBuilder(CodeLength);
            foreach (var c in value.Trim())
            {
                if (c == ' ' || c == '-')
                    continue;
                if (c < '0' || c > '9' || digits.Length == CodeLength)
                    return null;
                digits.Append(c);
            }
            return digits.Length == CodeLength ? digits.ToString() : null;
        }

        private static FieldInformation CodeField(GameProfile profile) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == (string.IsNullOrEmpty(profile.OnlineIdFieldName) ? AccessCodeField : profile.OnlineIdFieldName));

        /// <summary>
        /// The SaoAccessCode case of the online ID auto-fill (JoystickHelper.AutoFillOnlineId, AddGame): the account's code into
        /// an empty access code field. True when the field changed.
        /// </summary>
        public static bool AutoFill(GameProfile profile)
        {
            var code = Normalize(Lazydata.ParrotData.SaoAccessCode);
            var field = CodeField(profile);
            if (profile?.OnlineIdType != OnlineIdType.SaoAccessCode || field == null || code == null || !string.IsNullOrWhiteSpace(field.FieldValue))
                return false;
            field.FieldValue = code;
            return true;
        }

        /// <summary>
        /// The account's code as api/User/Profile returned it: kept in ParrotData, and put into every SAO profile whose field
        /// is empty or still holds the account's previous code. Nothing happens without a code (SAO online switched off on the
        /// website), so the last one stays.
        /// </summary>
        public static void StoreFromAccount(string accessCode)
        {
            var code = Normalize(accessCode);
            if (code == null)
                return;
            var data = Lazydata.ParrotData;
            var previous = Normalize(data.SaoAccessCode);
            if (previous != code)
            {
                data.SaoAccessCode = code;
                JoystickHelper.Serialize();
            }
            WriteToUserProfiles(code, previous);
        }

        /// <summary>Logout: this PC forgets the account's code (the profiles keep theirs, as for the other games' IDs).</summary>
        public static void ForgetAccount()
        {
            Lazydata.ParrotData.SaoAccessCode = "";
        }

        private static bool SetCode(GameProfile profile, string code, string previous)
        {
            var field = profile?.OnlineIdType == OnlineIdType.SaoAccessCode ? CodeField(profile) : null;
            if (field == null)
                return false;
            var current = Normalize(field.FieldValue);
            if (!string.IsNullOrWhiteSpace(field.FieldValue) && (current == null || current != previous))
                return false;
            if (field.FieldValue == code)
                return false;
            field.FieldValue = code;
            return true;
        }

        private static int WriteToUserProfiles(string code, string previous)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.SaoAccessCode).ToList()
                         ?? new List<GameProfile>();
            foreach (var inMemory in loaded)
            {
                if (string.IsNullOrEmpty(inMemory.FileName))
                    continue;
                var file = Path.Combine("UserProfiles", Path.GetFileName(inMemory.FileName));
                var onDisk = File.Exists(file) ? ReadProfileQuietly(file) : null;
                if (onDisk != null && SetCode(onDisk, code, previous))
                {
                    try
                    {
                        JoystickHelper.SerializeGameProfile(onDisk);
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SaoOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetCode(inMemory, code, previous);
            }
            return changed;
        }

        private static XmlSerializer _profileSerializer;
        private static XmlSerializer ProfileSerializer => _profileSerializer ??= new XmlSerializer(typeof(GameProfile));

        /// <summary>Reads a user profile without JoystickHelper's "delete the broken file?" prompt; null on any error.</summary>
        private static GameProfile ReadProfileQuietly(string file)
        {
            try
            {
                using (var reader = XmlReader.Create(file, new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    var profile = (GameProfile)ProfileSerializer.Deserialize(reader);
                    profile.FileName = file;
                    return profile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaoOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }
    }
}
