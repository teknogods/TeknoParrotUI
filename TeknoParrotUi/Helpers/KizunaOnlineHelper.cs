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
    /// Senjou no Kizuna Online in TeknoParrotUI. The game (GundamPod, ElfLdr2) plays online only, and TeknoParrot's server
    /// takes only cabinets signed in (TPAUTH) with the account's own Senjou no Kizuna PCB ID (AAKZ-...) and secret, a pair
    /// apart from Initial D's. Like the other games' IDs (SEGA, Namco, Golden Tee), the pair comes with the account:
    /// <list type="bullet">
    /// <item>teknoparrot.com issues it on first use and returns it in api/User/Profile (KizunaPcbId / KizunaSecret); it works
    /// on any PC the account uses (one at a time);</item>
    /// <item>TPUI keeps it in ParrotData (KizunaOnlineId / KizunaOnlineSecret), shows it on the Account page and fills it
    /// into the [Network] OnlineID / OnlineSecret fields of the profiles whose OnlineIdType is Kizuna, which the ini writer
    /// puts into teknoparrot.ini like every other field. No button: the Account page, the setup wizard and a library start
    /// (KizunaLaunchFlow) fetch it;</item>
    /// <item>the rank ladder the Account page shows (display only; the game gets the rank from the Kizuna server, never from
    /// TPUI). The game always plays on TeknoParrot's server, a fixed address in the DLL (GundamPODOnline.h
    /// kOfficialServer): no profile field names another.</item>
    /// </list>
    /// Nothing here touches a profile whose OnlineIdType is not Kizuna.
    /// </summary>
    public static class KizunaOnlineHelper
    {
        public const string IdFieldName = "OnlineID";
        public const string SecretFieldName = "OnlineSecret";
        public const string ProfilePageUrl = "https://teknoparrot.com/OnlineProfile/Kizuna";

        /// <summary>
        /// The website's PCB ID prefixes (OnlineProfiles:Kizuna / InitialD:PcbIdPrefix): Kizuna's own, and Initial D's,
        /// the account's other pair, which this game's server does not take.
        /// </summary>
        public const string PcbIdPrefix = "AAKZ";
        public const string InitialDPcbIdPrefix = "AALG";

#if DEBUG
        public const string DefaultProfileUrl = "https://localhost:44339/api/User/Profile";
#else
        public const string DefaultProfileUrl = "https://teknoparrot.com/api/User/Profile";
#endif

        /// <summary>The account's profile API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ProfileUrl { get; set; } = DefaultProfileUrl;

        // -------------------------------------------------------------------------------------------------------------
        // Credential form (the same rules as the DLL's GundamOnlineAuth.h, a copy of IDOnlineAuth's)
        // -------------------------------------------------------------------------------------------------------------

        private const string B64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// The canonical PCB ID ("AAKZ-XYZnnnnCCCC", upper case, dash at index 4) or null when the value is not 15 keychip
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

        /// <summary>The account's Kizuna pair as TPUI last got it, or null when ParrotData holds no well-formed pair.</summary>
        public static (string PcbId, string Secret)? LocalCredential()
        {
            var data = Lazydata.ParrotData;
            var id = NormalizePcbId(data?.KizunaOnlineId);
            var secret = data?.KizunaOnlineSecret?.Trim();
            if (id == null || !IsValidSecret(secret))
                return null;
            return (id, secret);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Game profiles
        // -------------------------------------------------------------------------------------------------------------

        private static FieldInformation Field(GameProfile profile, string name) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name);

        private static string IdFieldOf(GameProfile profile) =>
            string.IsNullOrEmpty(profile.OnlineIdFieldName) ? IdFieldName : profile.OnlineIdFieldName;

        /// <summary>A well-formed Kizuna pair is in the profile's OnlineID / OnlineSecret (what the DLL reads).</summary>
        public static bool ProfileHasCredential(GameProfile profile)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return false;
            return NormalizePcbId(Field(profile, IdFieldOf(profile))?.FieldValue) != null &&
                   IsValidSecret(Field(profile, SecretFieldName)?.FieldValue);
        }

        /// <summary>
        /// The Kizuna case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame, a library start): the account's pair
        /// goes into the profile, replacing an older one (a regenerated pair, or another account's). Returns true when a
        /// value changed.
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
            AutoFill(profile, Lazydata.ParrotData?.KizunaOnlineId, Lazydata.ParrotData?.KizunaOnlineSecret);

        /// <summary>Sets OnlineID / OnlineSecret of one Kizuna profile. Returns true when a value changed.</summary>
        public static bool SetProfileCredential(GameProfile profile, string pcbId, string secret)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return false;
            var idField = Field(profile, IdFieldOf(profile));
            var secretField = Field(profile, SecretFieldName);
            if (idField == null || secretField == null)
                return false;
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
                Debug.WriteLine($"KizunaOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the pair into every installed Kizuna profile (UserProfiles, and the loaded copies the library launches
        /// from). Returns the number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.Kizuna).ToList()
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
                        Debug.WriteLine($"KizunaOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetProfileCredential(inMemory, pcbId, secret);
            }
            return changed;
        }

        /// <summary>
        /// The account's pair as api/User/Profile returned it: kept in ParrotData and filled into every Kizuna profile.
        /// Nothing happens for a missing or malformed pair (Kizuna online switched off, a ban), so the last good pair stays.
        /// Returns true when the pair is in place.
        /// </summary>
        public static bool StoreFromAccount(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            secret = secret.Trim();
            var data = Lazydata.ParrotData;
            if (data.KizunaOnlineId != id || data.KizunaOnlineSecret != secret)
            {
                data.KizunaOnlineId = id;
                data.KizunaOnlineSecret = secret;
                JoystickHelper.Serialize();
            }
            WriteToUserProfiles(id, secret);
            return true;
        }

        /// <summary>Logout: this PC forgets the account's pair (the profiles keep theirs, as for the other games' IDs).</summary>
        public static void ForgetAccount()
        {
            Lazydata.ParrotData.KizunaOnlineId = "";
            Lazydata.ParrotData.KizunaOnlineSecret = "";
        }

        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private sealed class ProfileIds
        {
            public string KizunaPcbId { get; set; }
            public string KizunaSecret { get; set; }
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
                        return ids != null && StoreFromAccount(ids.KizunaPcbId, ids.KizunaSecret);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is IOException)
            {
                Debug.WriteLine($"KizunaOnline: profile request failed: {ex.Message}");
                return false;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Ranks (display only; the pods get the rank from the Kizuna server, never from TPUI)
        // -------------------------------------------------------------------------------------------------------------

        public sealed class RankStyle
        {
            public int Tier { get; set; }
            public string Name { get; set; }
            /// <summary>The mark the Kizuna server puts in front of the arcade's name in the game ("" = none, PEASANT).</summary>
            public string Mark { get; set; }
            /// <summary>RRGGBB; null = the default text colour (PEASANT).</summary>
            public string Color { get; set; }
            /// <summary>RRGGBB of the second gradient stop for the two top ranks, else null.</summary>
            public string Color2 { get; set; }
            public string Display => string.IsNullOrEmpty(Mark) ? Name : Mark + " " + Name;
        }

        /// <summary>
        /// The account's TeknoParrot.com rank (the one Initial D shows too, api/User/Profile InitialDRank) with the marks the
        /// Kizuna server draws (kizuna_svr_csharp Services/RankMarks.cs) and the website's tier colours. Always shown in
        /// Kizuna: there is no visibility choice.
        /// </summary>
        public static readonly IReadOnlyList<RankStyle> Ladder = new[]
        {
            new RankStyle { Tier = 0, Name = "PEASANT", Mark = "", Color = null },
            new RankStyle { Tier = 1, Name = "SERGEANT", Mark = "◇", Color = "D08A48" },
            new RankStyle { Tier = 2, Name = "COLONEL", Mark = "◆", Color = "D8E0F0" },
            new RankStyle { Tier = 3, Name = "GENERAL", Mark = "★", Color = "FFD040" },
            new RankStyle { Tier = 4, Name = "5-STAR GENERAL", Mark = "★★", Color = "FF5A00", Color2 = "FFE680" },
            new RankStyle { Tier = 5, Name = "PRESIDENT", Mark = "◎", Color = "FFE070", Color2 = "FFFFFF" },
        };

        /// <summary>The style of a tier 0..5, or null for anything else (unknown = no rank shown).</summary>
        public static RankStyle Rank(int? tier) => tier.HasValue && tier.Value >= 0 && tier.Value < Ladder.Count ? Ladder[tier.Value] : null;
    }
}
