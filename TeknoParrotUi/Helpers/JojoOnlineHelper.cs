using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// JoJo Last Survivor Online in TeknoParrotUI: the account's own JoJo PCB ID and secret, a pair apart from Initial D's,
    /// Senjou no Kizuna's and Mario Kart's. Like Mario Kart's (MarioKartOnlineHelper, sharing no state or code with it),
    /// the pair comes with the account:
    /// <list type="bullet">
    /// <item>teknoparrot.com issues it on first use and returns it in api/User/Profile (JojoPcbId / JojoSecret); it works on
    /// any PC the account uses;</item>
    /// <item>TPUI keeps it in ParrotData (JojoOnlineId / JojoOnlineSecret), shows it on the Account page and fills it into
    /// the [Network] OnlineID / OnlineSecret fields of the profiles whose OnlineIdType is Jojo and that have both fields
    /// (JojoLastSurvivor), which the ini writer puts into teknoparrot.ini like every other field. The two field names are
    /// fixed. No button: the Account page and a library start (<see cref="BeforeLaunchAsync"/>) fetch it.</item>
    /// </list>
    /// Like Mario Kart and unlike Senjou no Kizuna, the game runs without it (the offline mode and community servers;
    /// TeknoParrot's own server does not let a cabinet without one play): a start never waits for a dialog, and nothing
    /// here touches a profile whose OnlineIdType is not Jojo.
    /// </summary>
    public static class JojoOnlineHelper
    {
        public const string IdFieldName = "OnlineID";
        public const string SecretFieldName = "OnlineSecret";

        // The website's PCB ID prefix of JoJo Online IDs is the website's own and nothing here depends on it: any
        // well-formed keychip ID is taken, as the DLL does (JojoTpAuth.h NormalizePcbId).

#if DEBUG && USE_LOCALHOST
        public const string DefaultProfileUrl = "https://localhost:44339/api/User/Profile";
#else
        public const string DefaultProfileUrl = "https://teknoparrot.com/api/User/Profile";
#endif

        /// <summary>The account's profile API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ProfileUrl { get; set; } = DefaultProfileUrl;

        // -------------------------------------------------------------------------------------------------------------
        // Credential form (the same rules as MarioKartOnlineHelper / KizunaOnlineHelper / InitialDOnlineHelper)
        // -------------------------------------------------------------------------------------------------------------

        private const string B64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// The canonical PCB ID ("AAxx-XYZnnnnCCCC", upper case, dash at index 4) or null when the value is not 15 keychip
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

        /// <summary>The account's JoJo pair as TPUI last got it, or null when ParrotData holds no well-formed pair.</summary>
        public static (string PcbId, string Secret)? LocalCredential()
        {
            var data = Lazydata.ParrotData;
            var id = NormalizePcbId(data?.JojoOnlineId);
            var secret = data?.JojoOnlineSecret?.Trim();
            if (id == null || !IsValidSecret(secret))
                return null;
            return (id, secret);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Game profiles
        // -------------------------------------------------------------------------------------------------------------

        private static FieldInformation Field(GameProfile profile, string name) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name);

        /// <summary>
        /// A JoJo profile that takes the credential: OnlineIdType Jojo with both an OnlineID and an OnlineSecret field.
        /// </summary>
        public static bool AppliesTo(GameProfile profile) =>
            profile != null && profile.OnlineIdType == OnlineIdType.Jojo &&
            Field(profile, IdFieldName) != null && Field(profile, SecretFieldName) != null;

        /// <summary>A well-formed JoJo pair is in the profile's OnlineID / OnlineSecret (what the DLL reads).</summary>
        public static bool ProfileHasCredential(GameProfile profile) =>
            AppliesTo(profile) &&
            NormalizePcbId(Field(profile, IdFieldName).FieldValue) != null &&
            IsValidSecret(Field(profile, SecretFieldName).FieldValue);

        /// <summary>
        /// The JoJo case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame, a library start): the account's pair
        /// goes into the profile, replacing an older one (a regenerated pair, or another account's), as for Mario Kart.
        /// Returns true when a value changed.
        /// </summary>
        public static bool AutoFill(GameProfile profile, string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            return SetProfileCredential(profile, id, secret.Trim());
        }

        /// <summary>AutoFill with the account's pair stored in ParrotData.</summary>
        public static bool AutoFill(GameProfile profile) =>
            AutoFill(profile, Lazydata.ParrotData?.JojoOnlineId, Lazydata.ParrotData?.JojoOnlineSecret);

        /// <summary>Sets OnlineID / OnlineSecret of one JoJo profile. Returns true when a value changed.</summary>
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
                Debug.WriteLine($"JojoOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the pair into every installed JoJo profile that takes it (UserProfiles, and the loaded copies the library
        /// launches from). Returns the number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.Jojo).ToList()
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
                        Debug.WriteLine($"JojoOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetProfileCredential(inMemory, pcbId, secret);
            }
            return changed;
        }

        /// <summary>
        /// The account's pair as api/User/Profile returned it: kept in ParrotData and filled into every JoJo profile.
        /// Nothing happens for a missing or malformed pair (JoJo online switched off, a ban, an older website), so the last
        /// good pair stays. Returns true when the pair is in place.
        /// </summary>
        public static bool StoreFromAccount(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            secret = secret.Trim();
            var data = Lazydata.ParrotData;
            if (data.JojoOnlineId != id || data.JojoOnlineSecret != secret)
            {
                data.JojoOnlineId = id;
                data.JojoOnlineSecret = secret;
                JoystickHelper.Serialize();
            }
            WriteToUserProfiles(id, secret);
            return true;
        }

        /// <summary>Logout: this PC forgets the account's pair (the profiles keep theirs, as for the other games' IDs).</summary>
        public static void ForgetAccount()
        {
            Lazydata.ParrotData.JojoOnlineId = "";
            Lazydata.ParrotData.JojoOnlineSecret = "";
        }

        // A short timeout: a start waits for this only when the profile has no pair yet, and the game starts without one.
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        private sealed class ProfileIds
        {
            public string JojoPcbId { get; set; }
            public string JojoSecret { get; set; }
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
                        return ids != null && StoreFromAccount(ids.JojoPcbId, ids.JojoSecret);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is IOException)
            {
                Debug.WriteLine($"JojoOnline: profile request failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Before a library start of a JoJo profile: the account's pair (ParrotData) goes into the profile for this start;
        /// when there is none yet and this PC is logged in, it is fetched from api/User/Profile first. Never a dialog and
        /// never a refusal: without a pair the game still starts (offline mode, community servers). A start with a
        /// credential file named by TP_ONLINE_CRED (test rigs) is left alone.
        /// </summary>
        public static async Task BeforeLaunchAsync(GameProfile profile, Func<Task<string>> getToken)
        {
            if (!AppliesTo(profile) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_ONLINE_CRED")))
                return;
            AutoFill(profile);
            if (ProfileHasCredential(profile))
                return;
            if (await FetchFromAccountAsync(getToken).ConfigureAwait(true))
                AutoFill(profile);
        }
    }
}
