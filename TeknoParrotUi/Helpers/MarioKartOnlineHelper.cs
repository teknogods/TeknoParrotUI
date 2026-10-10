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
    /// Mario Kart Arcade GP DX Online in TeknoParrotUI: the account's own Mario Kart PCB ID (AAMK-...) and secret, a pair
    /// apart from Initial D's and Senjou no Kizuna's. Like Kizuna's (KizunaOnlineHelper, sharing no state or code with it),
    /// the pair comes with the account:
    /// <list type="bullet">
    /// <item>teknoparrot.com issues it on first use and returns it in api/User/Profile (MarioKartPcbId / MarioKartSecret); it
    /// works on any PC the account uses: the Mario Kart server keeps the newest sign-in, so the PC that starts the game last
    /// plays with it;</item>
    /// <item>TPUI keeps it in ParrotData (MarioKartOnlineId / MarioKartOnlineSecret), shows it on the Account page and fills
    /// it into the [Network] OnlineID / OnlineSecret fields of the profiles whose OnlineIdType is MarioKartId and that have
    /// both fields (MKDX118), which the ini writer puts into teknoparrot.ini like every other field. The two field names
    /// are fixed: OnlineIdFieldName of these profiles names the player's Mario Kart ID field (PlayerId), which the generic
    /// auto-fill (JoystickHelper.AutoFillOnlineId, AddGame) keeps filling from ParrotData.MarioKartId. No button: the
    /// Account page and a library start (<see cref="BeforeLaunchAsync"/>) fetch it.</item>
    /// </list>
    /// Unlike Senjou no Kizuna, Mario Kart plays without it (no supporter ranks then): a start never waits for a dialog,
    /// and nothing here touches a profile whose OnlineIdType is not MarioKartId.
    /// </summary>
    public static class MarioKartOnlineHelper
    {
        public const string IdFieldName = "OnlineID";
        public const string SecretFieldName = "OnlineSecret";

        /// <summary>The website's PCB ID prefix of Mario Kart Online IDs (AAMK-XYZnnnnCCCC).</summary>
        public const string PcbIdPrefix = "AAMK";

#if DEBUG && USE_LOCALHOST
        public const string DefaultProfileUrl = "https://localhost:44339/api/User/Profile";
#else
        public const string DefaultProfileUrl = "https://teknoparrot.com/api/User/Profile";
#endif

        /// <summary>The account's profile API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ProfileUrl { get; set; } = DefaultProfileUrl;

        // -------------------------------------------------------------------------------------------------------------
        // Credential form (the same rules as KizunaOnlineHelper / InitialDOnlineHelper)
        // -------------------------------------------------------------------------------------------------------------

        private const string B64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// The canonical PCB ID ("AAMK-XYZnnnnCCCC", upper case, dash at index 4) or null when the value is not 15 keychip
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

        /// <summary>The account's Mario Kart pair as TPUI last got it, or null when ParrotData holds no well-formed pair.</summary>
        public static (string PcbId, string Secret)? LocalCredential()
        {
            var data = Lazydata.ParrotData;
            var id = NormalizePcbId(data?.MarioKartOnlineId);
            var secret = data?.MarioKartOnlineSecret?.Trim();
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
        /// A Mario Kart profile that takes the credential: OnlineIdType MarioKartId with both an OnlineID and an
        /// OnlineSecret field. Older Mario Kart profiles (only PlayerId) are left alone.
        /// </summary>
        public static bool AppliesTo(GameProfile profile) =>
            profile != null && profile.OnlineIdType == OnlineIdType.MarioKartId &&
            Field(profile, IdFieldName) != null && Field(profile, SecretFieldName) != null;

        /// <summary>A well-formed Mario Kart pair is in the profile's OnlineID / OnlineSecret (what the DLL reads).</summary>
        public static bool ProfileHasCredential(GameProfile profile) =>
            AppliesTo(profile) &&
            NormalizePcbId(Field(profile, IdFieldName).FieldValue) != null &&
            IsValidSecret(Field(profile, SecretFieldName).FieldValue);

        /// <summary>
        /// The Mario Kart case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame, a library start), next to the
        /// PlayerId fill: the account's pair goes into the profile, replacing an older one (a regenerated pair, or another
        /// account's), as for Kizuna. Returns true when a value changed.
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
            AutoFill(profile, Lazydata.ParrotData?.MarioKartOnlineId, Lazydata.ParrotData?.MarioKartOnlineSecret);

        /// <summary>Sets OnlineID / OnlineSecret of one Mario Kart profile. Returns true when a value changed.</summary>
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
                Debug.WriteLine($"MarioKartOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the pair into every installed Mario Kart profile that takes it (UserProfiles, and the loaded copies the
        /// library launches from). Returns the number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.MarioKartId).ToList()
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
                        Debug.WriteLine($"MarioKartOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetProfileCredential(inMemory, pcbId, secret);
            }
            return changed;
        }

        /// <summary>
        /// The account's pair as api/User/Profile returned it: kept in ParrotData and filled into every Mario Kart profile.
        /// Nothing happens for a missing or malformed pair (Mario Kart online switched off, a ban), so the last good pair
        /// stays. Returns true when the pair is in place.
        /// </summary>
        public static bool StoreFromAccount(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            secret = secret.Trim();
            var data = Lazydata.ParrotData;
            if (data.MarioKartOnlineId != id || data.MarioKartOnlineSecret != secret)
            {
                data.MarioKartOnlineId = id;
                data.MarioKartOnlineSecret = secret;
                JoystickHelper.Serialize();
            }
            WriteToUserProfiles(id, secret);
            return true;
        }

        /// <summary>Logout: this PC forgets the account's pair (the profiles keep theirs, as for the other games' IDs).</summary>
        public static void ForgetAccount()
        {
            Lazydata.ParrotData.MarioKartOnlineId = "";
            Lazydata.ParrotData.MarioKartOnlineSecret = "";
        }

        // A short timeout: a start waits for this only when the profile has no pair yet, and the game plays without one.
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        private sealed class ProfileIds
        {
            public string MarioKartPcbId { get; set; }
            public string MarioKartSecret { get; set; }
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
                        return ids != null && StoreFromAccount(ids.MarioKartPcbId, ids.MarioKartSecret);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is IOException)
            {
                Debug.WriteLine($"MarioKartOnline: profile request failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Before a library start of a Mario Kart profile: the account's pair (ParrotData) goes into the profile for this
        /// start; when there is none yet and this PC is logged in, it is fetched from api/User/Profile first. Never a dialog
        /// and never a refusal: without a pair the game still plays online, only without supporter ranks. A start with a
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
