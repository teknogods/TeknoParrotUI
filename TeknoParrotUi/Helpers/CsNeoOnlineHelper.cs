using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Counter-Strike NEO Online in TeknoParrotUI (profile CSNEO, ElfLdr2): the account's own CS:NEO PCB ID (AACN-...) and
    /// secret, a pair apart from Initial D's, Senjou no Kizuna's, Mario Kart's and JoJo's. Like JoJo's (JojoOnlineHelper,
    /// sharing no state or code with it), the pair comes with the account:
    /// <list type="bullet">
    /// <item>teknoparrot.com issues it on first use and returns it in api/User/Profile (CsNeoPcbId / CsNeoSecret); it works
    /// on any PC the account uses;</item>
    /// <item>TPUI keeps it in ParrotData (CsNeoOnlineId / CsNeoOnlineSecret), shows it on the Account page and fills it into
    /// the [Network] OnlineID / OnlineSecret fields of the profiles whose OnlineIdType is CsNeo and that have both fields
    /// (CSNEO), which the ini writer puts into teknoparrot.ini like every other field: for this ElfLdr2 game the file next
    /// to hlds_amd, the folder the loader is started in. The two field names are fixed. No button: the Account page and a
    /// library start (<see cref="BeforeLaunchAsync"/>) fetch it.</item>
    /// <item>The player card is not the account's but this PC's: 26 hex digits made once (<see cref="LocalCardId"/>), kept
    /// in ParrotData (CsNeoCardId) and filled into [Network] Card ID while that holds no card (a library start also saves
    /// it in the user profile). The DLL appends the CRC-24 and holds the card on the reader when the Insert Card button
    /// is pressed.</item>
    /// <item>The game's command line depends on the same profile (<see cref="GameArguments"/>): offline it is started
    /// in the cabinet's local mode, without the store's database and match server.</item>
    /// </list>
    /// Like JoJo and unlike Senjou no Kizuna, the game runs without the pair (the offline mode and community servers): a
    /// start never waits for a dialog, and nothing here touches a profile whose OnlineIdType is not CsNeo.
    /// </summary>
    public static class CsNeoOnlineHelper
    {
        public const string IdFieldName = "OnlineID";
        public const string SecretFieldName = "OnlineSecret";
        public const string CardIdFieldName = "Card ID";
        public const string OnlineFieldName = "Online";
        public const string DeveloperConsoleFieldName = "Developer Console";

        // The website's PCB ID prefix of CS:NEO Online IDs (AACN) is the website's own and nothing here depends on it:
        // any well-formed keychip ID is taken, as for JoJo.

#if DEBUG && USE_LOCALHOST
        public const string DefaultProfileUrl = "https://localhost:44339/api/User/Profile";
#else
        public const string DefaultProfileUrl = "https://teknoparrot.com/api/User/Profile";
#endif

        /// <summary>The account's profile API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ProfileUrl { get; set; } = DefaultProfileUrl;

        // -------------------------------------------------------------------------------------------------------------
        // Credential form (the same rules as JojoOnlineHelper / MarioKartOnlineHelper / KizunaOnlineHelper)
        // -------------------------------------------------------------------------------------------------------------

        private const string B64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// The canonical PCB ID ("AACN-XYZnnnnCCCC", upper case, dash at index 4) or null when the value is not 15 keychip
        /// characters (7 of 0-9A-HJ-NP-Z, then 8 digits) with an optional dash at index 4. Surrounding whitespace is ignored.
        /// </summary>
        public static string NormalizePcbId(string value)
        {
            if (value == null)
                return null;
            var t = value.Trim(' ', '\t', '\r', '\n');
            var a = new StringBuilder(15);
            for (int i = 0; i < t.Length; i++)
            {
                var c = t[i];
                if (c > 0x7F)
                    return null;
                if (c == '-')
                {
                    if (i != 4)
                        return null;
                    continue;
                }
                a.Append(c >= 'a' && c <= 'z' ? (char)(c - 32) : c);
            }
            if (a.Length != 15)
                return null;
            for (int i = 0; i < 15; i++)
            {
                var c = a[i];
                bool ok = i < 7
                    ? (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z' && c != 'I' && c != 'O')
                    : c >= '0' && c <= '9';
                if (!ok)
                    return null;
            }
            var s = a.ToString();
            return s.Substring(0, 4) + "-" + s.Substring(4);
        }

        /// <summary>True for exactly 43 base64url characters without padding whose last character is canonical (32 bytes).</summary>
        public static bool IsValidSecret(string value)
        {
            if (value == null)
                return false;
            var t = value.Trim(' ', '\t', '\r', '\n');
            if (t.Length != 43)
                return false;
            for (int i = 0; i < 43; i++)
            {
                if (B64Url.IndexOf(t[i]) < 0)
                    return false;
            }
            return (B64Url.IndexOf(t[42]) & 3) == 0;
        }

        public static bool SamePcbId(string a, string b)
        {
            var na = NormalizePcbId(a);
            return na != null && na == NormalizePcbId(b);
        }

        /// <summary>The account's CS:NEO pair as TPUI last got it, or null when ParrotData holds no well-formed pair.</summary>
        public static (string PcbId, string Secret)? LocalCredential()
        {
            var data = Lazydata.ParrotData;
            var id = NormalizePcbId(data?.CsNeoOnlineId);
            var secret = data?.CsNeoOnlineSecret?.Trim();
            if (id == null || !IsValidSecret(secret))
                return null;
            return (id, secret);
        }

        // -------------------------------------------------------------------------------------------------------------
        // The player card (this PC's, not the account's)
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The hex digits of a card: the reader's 13 data bytes. The DLL appends the 3 bytes of the CRC-24.</summary>
        public const int CardIdLength = 26;

        /// <summary>
        /// The card as the DLL takes it (26 hex digits, upper case) or null when the value is not one. Surrounding
        /// whitespace is ignored.
        /// </summary>
        public static string NormalizeCardId(string value)
        {
            if (value == null)
                return null;
            var t = value.Trim(' ', '\t', '\r', '\n');
            if (t.Length != CardIdLength)
                return null;
            var a = new StringBuilder(CardIdLength);
            foreach (var c in t)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
                    a.Append(c);
                else if (c >= 'a' && c <= 'f')
                    a.Append((char)(c - 32));
                else
                    return null;
            }
            return a.ToString();
        }

        /// <summary>A new card: 13 random bytes as 26 upper-case hex digits, never all zero (the reader's "no card").</summary>
        public static string NewCardId()
        {
            var bytes = new byte[CardIdLength / 2];
            using (var rng = RandomNumberGenerator.Create())
            {
                do
                {
                    rng.GetBytes(bytes);
                } while (bytes.All(b => b == 0));
            }
            var a = new StringBuilder(CardIdLength);
            foreach (var b in bytes)
                a.Append(b.ToString("X2"));
            return a.ToString();
        }

        /// <summary>
        /// This PC's card: the one in ParrotData, made (and saved) on first use. It stays the same when the game is removed
        /// and added again, and a logout keeps it. Null only without ParrotData.
        /// </summary>
        public static string LocalCardId()
        {
            var data = Lazydata.ParrotData;
            if (data == null)
                return null;
            var card = NormalizeCardId(data.CsNeoCardId);
            if (card == null)
                card = NewCardId();
            if (data.CsNeoCardId != card)
            {
                data.CsNeoCardId = card;
                JoystickHelper.Serialize();
            }
            return card;
        }

        /// <summary>
        /// Puts this PC's card into [Network] Card ID of a CS:NEO profile that holds none (empty, or not 26 hex digits). A
        /// card the user entered (another PC's, to play as that player) stays, and becomes this PC's card when ParrotData
        /// has lost its own. Returns true when the field changed.
        /// </summary>
        public static bool FillCardId(GameProfile profile)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.CsNeo)
                return false;
            var field = Field(profile, CardIdFieldName);
            if (field == null)
                return false;
            var card = NormalizeCardId(field.FieldValue);
            if (card == null)
            {
                card = LocalCardId();
            }
            else if (Lazydata.ParrotData != null && NormalizeCardId(Lazydata.ParrotData.CsNeoCardId) == null)
            {
                Lazydata.ParrotData.CsNeoCardId = card;
                JoystickHelper.Serialize();
            }
            if (card == null || field.FieldValue == card)
                return false;
            field.FieldValue = card;
            return true;
        }

        /// <summary>
        /// Saves the card of a started profile in its file in UserProfiles when that file holds none, so the card is kept
        /// in two places (ParrotData and the profile). Only the Card ID of the file changes: what a start changed in the
        /// loaded profile is not written. Quiet on any error.
        /// </summary>
        private static void SaveCardId(GameProfile profile)
        {
            var card = NormalizeCardId(Field(profile, CardIdFieldName)?.FieldValue);
            if (card == null || string.IsNullOrEmpty(profile.FileName))
                return;
            var file = Path.Combine("UserProfiles", Path.GetFileName(profile.FileName));
            var onDisk = File.Exists(file) ? ReadProfileQuietly(file) : null;
            var field = onDisk == null || onDisk.OnlineIdType != OnlineIdType.CsNeo ? null : Field(onDisk, CardIdFieldName);
            if (field == null || NormalizeCardId(field.FieldValue) != null)
                return;
            field.FieldValue = card;
            try
            {
                JoystickHelper.SerializeGameProfile(onDisk);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CsNeoOnline: cannot write {file}: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Game profiles
        // -------------------------------------------------------------------------------------------------------------

        private static FieldInformation Field(GameProfile profile, string name) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name);

        private static bool IsOn(GameProfile profile, string name) => Field(profile, name)?.FieldValue == "1";

        /// <summary>
        /// A CS:NEO profile that takes the credential: OnlineIdType CsNeo with both an OnlineID and an OnlineSecret field.
        /// </summary>
        public static bool AppliesTo(GameProfile profile) =>
            profile != null && profile.OnlineIdType == OnlineIdType.CsNeo &&
            Field(profile, IdFieldName) != null && Field(profile, SecretFieldName) != null;

        /// <summary>A well-formed CS:NEO pair is in the profile's OnlineID / OnlineSecret (what the DLL reads).</summary>
        public static bool ProfileHasCredential(GameProfile profile) =>
            AppliesTo(profile) &&
            NormalizePcbId(Field(profile, IdFieldName).FieldValue) != null &&
            IsValidSecret(Field(profile, SecretFieldName).FieldValue);

        /// <summary>
        /// The game arguments of hlds_amd, the GoldSrc engine options GameProcessManager puts behind the game's path:
        /// always "-game czero" (the game directory; without it the engine stops on a missing texture); with [Network]
        /// Online off "-nodb -noms", the cabinet's local mode without the store's database and match server; with
        /// [General] Developer Console on "-devel -console +set developer 1".
        /// </summary>
        public static string GameArguments(GameProfile profile)
        {
            var arguments = "-game czero";
            if (!IsOn(profile, OnlineFieldName))
                arguments += " -nodb -noms";
            if (IsOn(profile, DeveloperConsoleFieldName))
                arguments += " -devel -console +set developer 1";
            return arguments;
        }

        /// <summary>
        /// The pair's part of the auto-fill: the account's pair goes into the profile, replacing an older one (a regenerated
        /// pair, or another account's), as for JoJo. Returns true when a value changed.
        /// </summary>
        public static bool AutoFill(GameProfile profile, string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            return SetProfileCredential(profile, id, secret.Trim());
        }

        /// <summary>
        /// The CS:NEO case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame, a library start): the account's pair
        /// stored in ParrotData, and this PC's card while the profile holds none. Returns true when a value changed.
        /// </summary>
        public static bool AutoFill(GameProfile profile) =>
            AutoFill(profile, Lazydata.ParrotData?.CsNeoOnlineId, Lazydata.ParrotData?.CsNeoOnlineSecret) | FillCardId(profile);

        /// <summary>Sets OnlineID / OnlineSecret of one CS:NEO profile. Returns true when a value changed.</summary>
        public static bool SetProfileCredential(GameProfile profile, string pcbId, string secret)
        {
            if (!AppliesTo(profile))
                return false;
            var idField = Field(profile, IdFieldName);
            var secretField = Field(profile, SecretFieldName);
            pcbId ??= "";
            secret ??= "";
            if (idField.FieldValue == pcbId && secretField.FieldValue == secret)
                return false;
            idField.FieldValue = pcbId;
            secretField.FieldValue = secret;
            return true;
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
                Debug.WriteLine($"CsNeoOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the pair into every installed CS:NEO profile that takes it (UserProfiles, and the loaded copies the
        /// library launches from). Returns the number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.CsNeo).ToList()
                         ?? new List<GameProfile>();
            foreach (var inMemory in loaded)
            {
                if (string.IsNullOrEmpty(inMemory.FileName))
                    continue;
                var file = Path.Combine("UserProfiles", Path.GetFileName(inMemory.FileName));
                var onDisk = File.Exists(file) ? ReadProfileQuietly(file) : null;
                if (onDisk != null && SetProfileCredential(onDisk, pcbId, secret))
                {
                    try
                    {
                        JoystickHelper.SerializeGameProfile(onDisk);
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CsNeoOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetProfileCredential(inMemory, pcbId, secret);
            }
            return changed;
        }

        /// <summary>
        /// The account's pair as api/User/Profile returned it: kept in ParrotData and filled into every CS:NEO profile.
        /// Nothing happens for a missing or malformed pair (CS:NEO online switched off, a ban, a website without the CS:NEO
        /// section yet), so the last good pair stays. Returns true when the pair is in place.
        /// </summary>
        public static bool StoreFromAccount(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            secret = secret.Trim();
            var data = Lazydata.ParrotData;
            if (data.CsNeoOnlineId != id || data.CsNeoOnlineSecret != secret)
            {
                data.CsNeoOnlineId = id;
                data.CsNeoOnlineSecret = secret;
                JoystickHelper.Serialize();
            }
            WriteToUserProfiles(id, secret);
            return true;
        }

        /// <summary>
        /// Logout: this PC forgets the account's pair (the profiles keep theirs, as for the other games' IDs). The card is
        /// this PC's and stays.
        /// </summary>
        public static void ForgetAccount()
        {
            Lazydata.ParrotData.CsNeoOnlineId = "";
            Lazydata.ParrotData.CsNeoOnlineSecret = "";
        }

        // A short timeout: a start waits for this only when the profile has no pair yet, and the game starts without one.
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        private sealed class ProfileIds
        {
            public string CsNeoPcbId { get; set; }
            public string CsNeoSecret { get; set; }
        }

        /// <summary>
        /// Gets the account's pair from api/User/Profile and stores it (StoreFromAccount). <paramref name="getToken"/> returns
        /// a current access token or null (not logged in). False without a login, without a pair, or when the website can't
        /// be reached.
        /// </summary>
        public static async Task<bool> FetchFromAccountAsync(Func<Task<string>> getToken, HttpClient http = null)
        {
            try
            {
                var token = await getToken().ConfigureAwait(true);
                if (string.IsNullOrEmpty(token))
                    return false;
                using (var request = new HttpRequestMessage(HttpMethod.Get, ProfileUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var response = await (http ?? SharedHttp).SendAsync(request).ConfigureAwait(true))
                    {
                        if (!response.IsSuccessStatusCode)
                            return false;
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                            return false;
                        var ids = JsonSerializer.Deserialize<ProfileIds>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return ids != null && StoreFromAccount(ids.CsNeoPcbId, ids.CsNeoSecret);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is IOException)
            {
                Debug.WriteLine($"CsNeoOnline: profile request failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Before a library start of a CS:NEO profile: this PC's card goes into the profile while it holds none (and into
        /// its file in UserProfiles), and the account's pair (ParrotData) goes into the profile for this start; when there
        /// is none yet and this PC is logged in, it is fetched from api/User/Profile first. Never a dialog and never a
        /// refusal: without a pair the game still starts (offline mode, community servers). The pair of a start with a
        /// credential file named by TP_ONLINE_CRED (test rigs) is left alone.
        /// </summary>
        public static async Task BeforeLaunchAsync(GameProfile profile, Func<Task<string>> getToken)
        {
            if (FillCardId(profile))
                SaveCardId(profile);
            if (!AppliesTo(profile) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_ONLINE_CRED")))
                return;
            AutoFill(profile, Lazydata.ParrotData?.CsNeoOnlineId, Lazydata.ParrotData?.CsNeoOnlineSecret);
            if (ProfileHasCredential(profile))
                return;
            if (await FetchFromAccountAsync(getToken).ConfigureAwait(true))
                AutoFill(profile, Lazydata.ParrotData?.CsNeoOnlineId, Lazydata.ParrotData?.CsNeoOnlineSecret);
        }
    }
}
