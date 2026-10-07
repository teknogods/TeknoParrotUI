using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;


namespace TeknoParrotUi.Common.Online
{
    /// <summary>
    /// Senjou no Kizuna Online in TeknoParrotUI. The game (GundamPod, ElfLdr2) plays online only, and TeknoParrot's server
    /// takes only cabinets signed in (TPAUTH) with the account's own Senjou no Kizuna PCB ID (AAKZ-...) and secret, a pair
    /// apart from Initial D's (TeknoParrotDotCom Services/Kizuna, Controllers/KizunaApiController.cs). Kizuna's own twin of
    /// InitialDOnlineHelper, sharing no state or code with it:
    /// <list type="bullet">
    /// <item>the machine credential of this PC, kept in plain text in ParrotData (KizunaOnlineId / KizunaOnlineSecret) and
    /// copied into the [Network] OnlineID / OnlineSecret fields of the profiles whose OnlineIdType is Kizuna, which the ini
    /// writer puts into teknoparrot.ini like every other field;</item>
    /// <item>the website's TPUI-facing API under api/Kizuna/ (Machine / Credential / Provision / Regenerate / Revoke: the
    /// contract of api/InitialD/ without Consent, since Kizuna always shows the rank) over the existing OAuth bearer, with
    /// the fresh-login step-up (prompt=login) the secret reads need;</item>
    /// <item>the rank ladder the Account page shows (display only; the game gets the rank from the Kizuna server, never
    /// from TPUI). The game always plays on TeknoParrot's server, a fixed address in the DLL (GundamPODOnline.h
    /// kOfficialServer): no profile field names another.</item>
    /// </list>
    /// Nothing here runs at game launch except the library's check (KizunaLaunchFlow), and nothing here touches a profile
    /// whose OnlineIdType is not Kizuna.
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

#if DEBUG && USE_LOCALHOST
        public const string DefaultApiBase = "https://localhost:44339/api/Kizuna/";
#else
        public const string DefaultApiBase = "https://teknoparrot.com/api/Kizuna/";
#endif

        /// <summary>Root of the TPUI-facing API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ApiBase { get; set; } = DefaultApiBase;

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

        /// <summary>
        /// The Online ID and the secret in pasted text, as teknoparrot.com shows them: one value, or both from its
        /// OnlineID= / OnlineSecret= lines. Null for a value the text does not hold.
        /// </summary>
        public static (string PcbId, string Secret) ReadPasted(string text)
        {
            string id = null, secret = null;
            foreach (var word in System.Text.RegularExpressions.Regex.Split(text ?? "", "[^A-Za-z0-9_-]+"))
            {
                if (id == null && NormalizePcbId(word) is string pcbId)
                    id = pcbId;
                else if (secret == null && IsValidSecret(word))
                    secret = word;
            }
            return (id, secret);
        }

        public static bool SamePcbId(string a, string b)
        {
            var na = NormalizePcbId(a);
            return na != null && na == NormalizePcbId(b);
        }

        /// <summary>The Kizuna credential stored for this PC, or null when ParrotData holds no well-formed pair.</summary>
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

        /// <summary>This PC has a well-formed Kizuna pair in the profile's OnlineID / OnlineSecret (what the DLL reads).</summary>
        public static bool ProfileHasCredential(GameProfile profile)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return false;
            return NormalizePcbId(Field(profile, IdFieldOf(profile))?.FieldValue) != null &&
                   IsValidSecret(Field(profile, SecretFieldName)?.FieldValue);
        }

        /// <summary>
        /// The Kizuna case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame, the library's launch check): the pair
        /// is filled only into an empty OnlineID (the existing "fill when empty" rule; the secret goes with it), or the
        /// secret alone when OnlineID already holds this PC's PCB ID and OnlineSecret is empty. A profile with another PCB
        /// ID is left alone.
        /// </summary>
        public static bool AutoFill(GameProfile profile, string pcbId, string secret)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return false;
            var id = NormalizePcbId(pcbId);
            if (id == null || !IsValidSecret(secret))
                return false;
            var idField = Field(profile, IdFieldOf(profile));
            var secretField = Field(profile, SecretFieldName);
            if (idField == null || secretField == null)
                return false;
            if (string.IsNullOrWhiteSpace(idField.FieldValue))
            {
                idField.FieldValue = id;
                secretField.FieldValue = secret.Trim();
                return true;
            }
            if (SamePcbId(idField.FieldValue, id) && string.IsNullOrWhiteSpace(secretField.FieldValue))
            {
                secretField.FieldValue = secret.Trim();
                return true;
            }
            return false;
        }

        /// <summary>AutoFill with the Kizuna pair stored in ParrotData.</summary>
        public static bool AutoFill(GameProfile profile) =>
            AutoFill(profile, Lazydata.ParrotData?.KizunaOnlineId, Lazydata.ParrotData?.KizunaOnlineSecret);

        /// <summary>
        /// Sets OnlineID / OnlineSecret of one Kizuna profile. <paramref name="onlyIfPcbId"/> (optional) limits the change to
        /// a profile whose OnlineID is that PCB ID (Remove). Returns true when a value changed.
        /// </summary>
        public static bool SetProfileCredential(GameProfile profile, string pcbId, string secret, string onlyIfPcbId = null)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return false;
            var idField = Field(profile, IdFieldOf(profile));
            var secretField = Field(profile, SecretFieldName);
            if (idField == null || secretField == null)
                return false;
            if (onlyIfPcbId != null && !SamePcbId(idField.FieldValue, onlyIfPcbId))
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
        /// from). "" clears. With <paramref name="onlyIfPcbId"/> only profiles holding that PCB ID change. Returns the
        /// number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret, string onlyIfPcbId = null)
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
                if (onDisk != null && SetProfileCredential(onDisk, pcbId, secret, onlyIfPcbId))
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
                SetProfileCredential(inMemory, pcbId, secret, onlyIfPcbId);
            }
            return changed;
        }

        /// <summary>Stores a new pair for this PC (Register, Use on this PC, Regenerate): ParrotData and every Kizuna profile.</summary>
        public static int StoreCredential(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId) ?? throw new ArgumentException("malformed PCB ID", nameof(pcbId));
            if (!IsValidSecret(secret))
                throw new ArgumentException("malformed secret", nameof(secret));
            Lazydata.ParrotData.KizunaOnlineId = id;
            Lazydata.ParrotData.KizunaOnlineSecret = secret.Trim();
            JoystickHelper.Serialize();
            return WriteToUserProfiles(id, secret.Trim());
        }

        /// <summary>Forgets a removed pair: ParrotData (if it holds it) and the Kizuna profiles that hold it.</summary>
        public static int ClearCredential(string pcbId)
        {
            if (SamePcbId(Lazydata.ParrotData.KizunaOnlineId, pcbId) || string.IsNullOrWhiteSpace(Lazydata.ParrotData.KizunaOnlineId))
            {
                Lazydata.ParrotData.KizunaOnlineId = "";
                Lazydata.ParrotData.KizunaOnlineSecret = "";
                JoystickHelper.Serialize();
            }
            return WriteToUserProfiles("", "", pcbId);
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

        // -------------------------------------------------------------------------------------------------------------
        // Website API (JWT bearer; api/Kizuna/, the contract of INITIALD_ONLINE_API.md "TPUI-facing routes" without Consent)
        // -------------------------------------------------------------------------------------------------------------

        public enum ApiError
        {
            None,
            NotLoggedIn,
            FreshLoginRequired,
            FreshLoginFailed,
            NoMachine,
            FeatureOff,
            Banned,
            RateLimited,
            Unavailable,
            BadAnswer,
            Unreachable,
            Other,
        }

        public sealed class ApiResult<T>
        {
            public bool Ok => Error == ApiError.None;
            public ApiError Error { get; set; }
            public int Status { get; set; }
            public string ErrorCode { get; set; } = "";
            public int RetryAfterSeconds { get; set; }
            public T Value { get; set; }
        }

        public sealed class CredentialInfo
        {
            public string PcbId { get; set; }
            public string Secret { get; set; }
            public string Status { get; set; }
        }

        public sealed class MachineInfo
        {
            public string PcbId { get; set; }
            public string Label { get; set; }
            public string Kind { get; set; }
            public string CreatedUtc { get; set; }
            public string LastSeenUtc { get; set; }
            public string MachineChangedUtc { get; set; }
            public string Status { get; set; }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// The API client. <c>getToken</c> returns a current access token or null (not logged in); <c>freshLogin</c> runs the
        /// browser login with prompt=login and returns true when a new token with auth_time is in place. A request answered
        /// 401 fresh_login_required is sent again once after a successful fresh login.
        /// </summary>
        public sealed class ApiClient
        {
            private readonly Func<Task<string>> _getToken;
            private readonly Func<Task<bool>> _freshLogin;
            private readonly HttpClient _http;

            public ApiClient(Func<Task<string>> getToken, Func<Task<bool>> freshLogin, HttpClient http = null)
            {
                _getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
                _freshLogin = freshLogin;
                _http = http ?? SharedHttp;
            }

            public Task<ApiResult<MachineInfo>> GetMachineAsync() =>
                SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url("Machine")), ParseJson<MachineInfo>);

            public Task<ApiResult<CredentialInfo>> GetCredentialAsync() =>
                SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url("Credential")), ParseCredential);

            /// <summary>A new pair for this account (or its existing one); Kizuna has no consent step.</summary>
            public Task<ApiResult<CredentialInfo>> ProvisionAsync(string label) =>
                SendAsync(() => Post("Provision", new { label = Label(label), kind = "personal" }), ParseCredential);

            public Task<ApiResult<CredentialInfo>> RegenerateAsync(string pcbId) =>
                SendAsync(() => Post("Regenerate", new { pcbId }), ParseCredential);

            public Task<ApiResult<bool>> RevokeAsync(string pcbId) =>
                SendAsync(() => Post("Revoke", new { pcbId }), _ => true);

            private static string Url(string action) => ApiBase.TrimEnd('/') + "/" + action;

            private static string Label(string label)
            {
                label = (label ?? "").Trim();
                return label.Length <= 64 ? label : label.Substring(0, 64);
            }

            private static HttpRequestMessage Post(string action, object body) =>
                new HttpRequestMessage(HttpMethod.Post, Url(action))
                {
                    Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"),
                };

            private static T ParseJson<T>(string body) where T : class =>
                string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, JsonOptions);

            private static CredentialInfo ParseCredential(string body)
            {
                var c = ParseJson<CredentialInfo>(body);
                // Never store a malformed pair: the DLL would reject it and the user would not know why.
                if (c == null || NormalizePcbId(c.PcbId) == null || !IsValidSecret(c.Secret))
                    return null;
                c.PcbId = NormalizePcbId(c.PcbId);
                c.Secret = c.Secret.Trim();
                return c;
            }

            private async Task<ApiResult<T>> SendAsync<T>(Func<HttpRequestMessage> build, Func<string, T> parse)
            {
                for (int attempt = 0; ; attempt++)
                {
                    string token;
                    try
                    {
                        token = await _getToken().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"KizunaOnline: token: {ex.Message}");
                        token = null;
                    }
                    if (string.IsNullOrEmpty(token))
                        return new ApiResult<T> { Error = ApiError.NotLoggedIn };

                    int status;
                    string body;
                    TimeSpan? retryAfterHeader;
                    try
                    {
                        using (var request = build())
                        {
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            using (var response = await _http.SendAsync(request).ConfigureAwait(true))
                            {
                                status = (int)response.StatusCode;
                                retryAfterHeader = response.Headers.RetryAfter?.Delta;
                                body = response.Content == null ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is WebException || ex is IOException)
                    {
                        Debug.WriteLine($"KizunaOnline: request failed: {ex.Message}");
                        return new ApiResult<T> { Error = ApiError.Unreachable };
                    }

                    var code = ErrorCodeOf(body);
                    var result = new ApiResult<T> { Status = status, ErrorCode = code };

                    if (status == 401 && code == "fresh_login_required")
                    {
                        if (attempt == 0 && _freshLogin != null)
                        {
                            bool fresh;
                            try
                            {
                                fresh = await _freshLogin().ConfigureAwait(true);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"KizunaOnline: fresh login: {ex.Message}");
                                fresh = false;
                            }
                            if (fresh)
                                continue;
                            result.Error = ApiError.FreshLoginFailed;
                            return result;
                        }
                        result.Error = ApiError.FreshLoginRequired;
                        return result;
                    }

                    if (status >= 200 && status < 300)
                    {
                        T value;
                        try
                        {
                            value = parse(body);
                        }
                        catch (JsonException)
                        {
                            value = default;
                        }
                        if (value == null)
                        {
                            result.Error = ApiError.BadAnswer;
                            return result;
                        }
                        result.Value = value;
                        return result;
                    }

                    result.RetryAfterSeconds = RetryAfterOf(retryAfterHeader, body);
                    switch (status)
                    {
                        case 401:
                            result.Error = ApiError.NotLoggedIn;
                            break;
                        case 403:
                            result.Error = code == "banned" ? ApiError.Banned : ApiError.Other;
                            break;
                        case 404:
                            // {"error":"no_machine"} = the account has no Kizuna PCB ID; a 404 without it = Kizuna is switched off.
                            result.Error = code == "no_machine" ? ApiError.NoMachine : ApiError.FeatureOff;
                            break;
                        case 429:
                            result.Error = ApiError.RateLimited;
                            break;
                        case 500:
                        case 502:
                        case 503:
                        case 504:
                            result.Error = ApiError.Unavailable;
                            break;
                        default:
                            result.Error = ApiError.Other;
                            break;
                    }
                    return result;
                }
            }

            private static string ErrorCodeOf(string body)
            {
                if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                    return "";
                try
                {
                    using (var doc = JsonDocument.Parse(body))
                        return doc.RootElement.ValueKind == JsonValueKind.Object &&
                               doc.RootElement.TryGetProperty("error", out var v) && v.ValueKind == JsonValueKind.String
                            ? v.GetString() ?? ""
                            : "";
                }
                catch (JsonException)
                {
                    return "";
                }
            }

            private static int RetryAfterOf(TimeSpan? delta, string body)
            {
                if (delta.HasValue)
                    return (int)Math.Ceiling(delta.Value.TotalSeconds);
                if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
                    return 0;
                try
                {
                    using (var doc = JsonDocument.Parse(body))
                        return doc.RootElement.ValueKind == JsonValueKind.Object &&
                               doc.RootElement.TryGetProperty("retryAfterSeconds", out var v) &&
                               v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)
                            ? (int)Math.Max(0, Math.Min(int.MaxValue, n))
                            : 0;
                }
                catch (JsonException)
                {
                    return 0;
                }
            }
        }

        /// <summary>A translated message for a failed API call (Kizuna's texts where they name the game).</summary>
        public static string DescribeError<T>(ApiResult<T> result)
        {
            switch (result.Error)
            {
                case ApiError.NotLoggedIn: return OnlineText.Get("InitialDOnlineErrorNotLoggedIn");
                case ApiError.FreshLoginRequired:
                case ApiError.FreshLoginFailed: return OnlineText.Get("InitialDOnlineErrorFreshLogin");
                case ApiError.NoMachine: return OnlineText.Get("KizunaOnlineNotRegistered");
                case ApiError.FeatureOff: return OnlineText.Get("KizunaOnlineErrorFeatureOff");
                case ApiError.Banned: return OnlineText.Get("KizunaOnlineErrorBanned");
                case ApiError.RateLimited: return OnlineText.Get("InitialDOnlineErrorRateLimited");
                case ApiError.Unavailable:
                case ApiError.Unreachable: return OnlineText.Get("InitialDOnlineErrorUnavailable");
                default:
                    return string.Format(OnlineText.Get("InitialDOnlineErrorGeneric"),
                        string.IsNullOrEmpty(result.ErrorCode) ? result.Status.ToString(CultureInfo.InvariantCulture) : result.ErrorCode);
            }
        }
    }
}
