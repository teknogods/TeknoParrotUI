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
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Initial D Online in TeknoParrotUI (InitialDServer docs/IDENTITY_AND_BADGES.md sections 3.2, 3.3.2 and 4.4; the
    /// website contract is TeknoParrotDotCom/INITIALD_ONLINE_API.md):
    /// <list type="bullet">
    /// <item>the machine credential (PCB ID + secret) of this PC, kept in plain text in ParrotData.xml and copied into the
    /// [Network] OnlineID / OnlineSecret fields of the Initial D profiles (OnlineIdType.InitialD), which the ini writer
    /// puts into teknoparrot.ini like every other field;</item>
    /// <item>the TPUI-facing website API over the existing OAuth bearer (Machine / Credential / Provision / Regenerate /
    /// Revoke / Consent), with the fresh-login step-up (prompt=login) the secret reads need;</item>
    /// <item>the DLL's status file (&lt;game folder&gt;\TeknoParrot\idonline_status.json) for the exit toast and the
    /// Account page.</item>
    /// </list>
    /// Nothing here runs at game launch, nothing passes a rank to a game, and nothing here is used by any profile whose
    /// OnlineIdType is not InitialD. The rank shown in TPUI comes from api/User/Profile and is display only.
    /// </summary>
    public static class InitialDOnlineHelper
    {
        public const string IdFieldName = "OnlineID";
        public const string SecretFieldName = "OnlineSecret";
        public const string StatusFileName = "idonline_status.json";
        public const string ProfilePageUrl = "https://teknoparrot.com/OnlineProfile/InitialD";

#if DEBUG && USE_LOCALHOST
        public const string DefaultApiBase = "https://localhost:44339/api/InitialD/";
#else
        public const string DefaultApiBase = "https://teknoparrot.com/api/InitialD/";
#endif

        /// <summary>Root of the TPUI-facing API. Only tests change it; it is never read from a file or the environment.</summary>
        public static string ApiBase { get; set; } = DefaultApiBase;

        // -------------------------------------------------------------------------------------------------------------
        // Credential form (the same rules as the DLL's IDOnlineAuth: NormalizePcbId / Base64UrlDecode32)
        // -------------------------------------------------------------------------------------------------------------

        private const string B64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// The canonical PCB ID ("AALG-XYZnnnnCCCC", upper case, dash at index 4) or null when the value is not 15 keychip
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

        /// <summary>The credential stored for this PC, or null when ParrotData holds no well-formed pair.</summary>
        public static (string PcbId, string Secret)? LocalCredential()
        {
            var data = Lazydata.ParrotData;
            var id = NormalizePcbId(data?.InitialDOnlineId);
            var secret = data?.InitialDOnlineSecret?.Trim();
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

        /// <summary>
        /// The InitialD case of the auto-fill (JoystickHelper.AutoFillOnlineId, AddGame): the pair is filled only into an
        /// empty OnlineID (the existing "fill when empty" rule; the secret goes with it), or the secret alone when OnlineID
        /// already holds this PC's PCB ID and OnlineSecret is empty. A profile with another PCB ID is left alone.
        /// </summary>
        public static bool AutoFill(GameProfile profile, string pcbId, string secret)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.InitialD)
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

        /// <summary>AutoFill with the pair stored in ParrotData.</summary>
        public static bool AutoFill(GameProfile profile) =>
            AutoFill(profile, Lazydata.ParrotData?.InitialDOnlineId, Lazydata.ParrotData?.InitialDOnlineSecret);

        /// <summary>
        /// Sets OnlineID / OnlineSecret of one profile. <paramref name="onlyIfPcbId"/> (optional) limits the change to a
        /// profile whose OnlineID is that PCB ID (Remove). Returns true when a value changed.
        /// </summary>
        public static bool SetProfileCredential(GameProfile profile, string pcbId, string secret, string onlyIfPcbId = null)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.InitialD)
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
        internal static GameProfile ReadProfileQuietly(string file)
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
                Debug.WriteLine($"InitialDOnline: cannot read {file}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the pair into every installed Initial D profile (UserProfiles, and the loaded copies the library launches
        /// from). "" clears. With <paramref name="onlyIfPcbId"/> only profiles holding that PCB ID change. Returns the
        /// number of profile files changed. Other games' profiles are never opened.
        /// </summary>
        public static int WriteToUserProfiles(string pcbId, string secret, string onlyIfPcbId = null)
        {
            int changed = 0;
            var loaded = GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.InitialD).ToList()
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
                        Debug.WriteLine($"InitialDOnline: cannot write {file}: {ex.Message}");
                    }
                }
                SetProfileCredential(inMemory, pcbId, secret, onlyIfPcbId);
            }
            return changed;
        }

        /// <summary>Stores a new pair for this PC (Register, Use on this PC, Regenerate): ParrotData and every Initial D profile.</summary>
        public static int StoreCredential(string pcbId, string secret)
        {
            var id = NormalizePcbId(pcbId) ?? throw new ArgumentException("malformed PCB ID", nameof(pcbId));
            if (!IsValidSecret(secret))
                throw new ArgumentException("malformed secret", nameof(secret));
            Lazydata.ParrotData.InitialDOnlineId = id;
            Lazydata.ParrotData.InitialDOnlineSecret = secret.Trim();
            JoystickHelper.Serialize();
            return WriteToUserProfiles(id, secret.Trim());
        }

        /// <summary>Forgets a removed pair: ParrotData (if it holds it) and the profiles that hold it.</summary>
        public static int ClearCredential(string pcbId)
        {
            if (SamePcbId(Lazydata.ParrotData.InitialDOnlineId, pcbId) || string.IsNullOrWhiteSpace(Lazydata.ParrotData.InitialDOnlineId))
            {
                Lazydata.ParrotData.InitialDOnlineId = "";
                Lazydata.ParrotData.InitialDOnlineSecret = "";
                JoystickHelper.Serialize();
            }
            return WriteToUserProfiles("", "", pcbId);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Ranks (display only; the games get the rank from InitialDServer, never from TPUI)
        // -------------------------------------------------------------------------------------------------------------

        public sealed class RankStyle
        {
            public int Tier { get; set; }
            public string Name { get; set; }
            public string Insignia { get; set; }
            /// <summary>RRGGBB; null = the default text colour (PEASANT).</summary>
            public string Color { get; set; }
            /// <summary>RRGGBB of the second gradient stop for the two top ranks, else null.</summary>
            public string Color2 { get; set; }
            public bool Stars => Tier >= 3;
            public bool Motion => Tier >= 4;
            /// <summary>ID7: the King aura (owner decision 2026-09-30: on for 5-STAR GENERAL and PRESIDENT).</summary>
            public bool Id7KingAura => Tier >= 4;
            public string Display => string.IsNullOrEmpty(Insignia) ? Name : Tier == 5 ? Insignia : Insignia + " " + Name;
        }

        /// <summary>The ladder (spec 2.2). There is nothing above PRESIDENT.</summary>
        public static readonly IReadOnlyList<RankStyle> Ladder = new[]
        {
            new RankStyle { Tier = 0, Name = "PEASANT", Insignia = "", Color = null },
            new RankStyle { Tier = 1, Name = "SERGEANT", Insignia = "◇", Color = "D08A48" },
            new RankStyle { Tier = 2, Name = "COLONEL", Insignia = "◆", Color = "D8E0F0" },
            new RankStyle { Tier = 3, Name = "GENERAL", Insignia = "★★★★", Color = "FFD040" },
            new RankStyle { Tier = 4, Name = "5-STAR GENERAL", Insignia = "★★★★★", Color = "FF5A00", Color2 = "FFE680" },
            new RankStyle { Tier = 5, Name = "PRESIDENT", Insignia = "【PRESIDENT】", Color = "FFE070", Color2 = "FFFFFF" },
        };

        /// <summary>The style of a tier 0..5, or null for anything else (unknown = no rank shown).</summary>
        public static RankStyle Rank(int? tier) => tier.HasValue && tier.Value >= 0 && tier.Value < Ladder.Count ? Ladder[tier.Value] : null;

        // -------------------------------------------------------------------------------------------------------------
        // status.json written by TeknoParrot.dll (TP_CLIENT.md section 15.4)
        // -------------------------------------------------------------------------------------------------------------

        public sealed class OnlineStatus
        {
            public string Title { get; set; } = "";
            public string State { get; set; } = "";
            public string Code { get; set; } = "";
            public int Retry { get; set; }
            public string Message { get; set; } = "";
            public string PcbId { get; set; } = "";
            public string Server { get; set; } = "";
            public string Realm { get; set; } = "";
            public string Credential { get; set; } = "";
            public long Time { get; set; }
            public DateTime WrittenUtc { get; set; }
            public string FilePath { get; set; } = "";

            // Unified network mode (UNIFIED_MODE.md 2.4): written in every outcome. Empty with a DLL from before it.
            /// <summary>online / lan / offline / legacy.</summary>
            public string Mode { get; set; } = "";
            /// <summary>The outcome's reason (online, dns, no_credential, forced_no_server, legacy_pair_config, ...).</summary>
            public string Reason { get; set; } = "";
            /// <summary>set / unset: a friends code is active (never the code itself).</summary>
            public string Party { get; set; } = "";
            public string LanGroup { get; set; } = "";
            public string LanSeat { get; set; } = "";
            public string LanPartner { get; set; } = "";
            public int LanPeers { get; set; }
            public string LanWarning { get; set; } = "";
            /// <summary>The background re-probe of a LAN / OFFLINE boot found the server again.</summary>
            public bool OnlineBack { get; set; }
            /// <summary>FrameLock phase 1 (FRAMELOCK.md 3.5 / 5.5): the lock state (e.g. engaged / blocked) and its notice.</summary>
            public string FrameLockState { get; set; } = "";
            public string FrameLockText { get; set; } = "";
            /// <summary>A server notice for this boot (the TPAUTH replies' msg=, e.g. FRAMELOCK.md 5.5): shown as is.</summary>
            public string Notice { get; set; } = "";

            public bool IsUnified => !string.IsNullOrEmpty(Mode);

            /// <summary>
            /// States that need no toast: a session, a clean end, a boot that ended before a result, or a server without
            /// TPAUTH. In the unified mode also an offline start the user chose (the checkbox, NetworkMode=Legacy, a
            /// classic LAN pair).
            /// </summary>
            public bool IsProblem
            {
                get
                {
                    if (IsUnified && Mode != "online")
                        return !(Reason == "forced_no_server" || Reason == "legacy_forced" || Reason == "legacy_pair_config");
                    return !(State == "online" || State == "ended" || State == "connecting" || State == "server_legacy" || State == "");
                }
            }
        }

        /// <summary>
        /// Where the DLL of this profile writes its status file: &lt;game working directory&gt;\TeknoParrot. That is the game's
        /// folder (ElfLdr2 is started there; the RingEdge loader starts the game there); TPUI's own folder is checked too in
        /// case a loader keeps TPUI's working directory. The title inside the file must match the profile.
        /// </summary>
        public static IEnumerable<string> StatusFileCandidates(GameProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile?.GamePath))
                yield break;
            string game = null, own = null;
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(profile.GamePath));
                if (!string.IsNullOrEmpty(dir))
                    game = Path.Combine(dir, "TeknoParrot", StatusFileName);
                own = Path.Combine(Directory.GetCurrentDirectory(), "TeknoParrot", StatusFileName);
            }
            catch
            {
                // an unusable path: no candidates from it
            }
            if (game != null)
                yield return game;
            if (own != null && !string.Equals(own, game, StringComparison.OrdinalIgnoreCase))
                yield return own;
        }

        /// <summary>Parses one status file (at most 16 KB); null when it is missing, unreadable or not version 1.</summary>
        public static OnlineStatus ReadStatusFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 16 * 1024)
                    return null;
                string text;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    text = reader.ReadToEnd();
                using (var doc = JsonDocument.Parse(text))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || Int(root, "version") != 1)
                        return null;
                    var status = new OnlineStatus
                    {
                        Title = Str(root, "title"),
                        State = Str(root, "state"),
                        Code = Str(root, "code"),
                        Retry = (int)Math.Max(0, Math.Min(int.MaxValue, Int(root, "retry"))),
                        Message = Str(root, "message"),
                        PcbId = Str(root, "pcbId"),
                        Server = Str(root, "server"),
                        Realm = Str(root, "realm"),
                        Credential = Str(root, "credential"),
                        Time = Int(root, "time"),
                        WrittenUtc = info.LastWriteTimeUtc,
                        FilePath = path,
                        Mode = Str(root, "mode").ToLowerInvariant(),
                        Reason = Str(root, "reason"),
                        Party = Str(root, "party"),
                        OnlineBack = Bool(root, "online_back"),
                        Notice = Str(root, "notice"),
                    };
                    if (root.TryGetProperty("lan", out var lan) && lan.ValueKind == JsonValueKind.Object)
                    {
                        status.LanGroup = Str(lan, "group");
                        status.LanSeat = Str(lan, "seat");
                        status.LanPartner = Str(lan, "partner");
                        status.LanPeers = (int)Math.Max(0, Math.Min(64, Int(lan, "peers")));
                        status.LanWarning = Str(lan, "warning");
                    }
                    if (root.TryGetProperty("framelock", out var fl))
                    {
                        if (fl.ValueKind == JsonValueKind.Object)
                        {
                            status.FrameLockState = Str(fl, "state");
                            status.FrameLockText = Str(fl, "message");
                        }
                        else if (fl.ValueKind == JsonValueKind.String)
                            status.FrameLockState = fl.GetString() ?? "";
                    }
                    return status;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: cannot read {path}: {ex.Message}");
                return null;
            }
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static long Int(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

        private static bool Bool(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) &&
            (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) && n != 0));

        private static bool TitleMatches(GameProfile profile, OnlineStatus status) =>
            string.IsNullOrEmpty(profile.ProfileName) || string.IsNullOrEmpty(status.Title) ||
            profile.ProfileName.StartsWith(status.Title, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The newest status of this profile's game; with <paramref name="notBeforeUtc"/> only one written at or after that
        /// time (the file time and the JSON time must both be new enough, so a stale file never shows).
        /// </summary>
        public static OnlineStatus ReadStatus(GameProfile profile, DateTime? notBeforeUtc = null)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.InitialD)
                return null;
            OnlineStatus best = null;
            foreach (var path in StatusFileCandidates(profile))
            {
                var s = ReadStatusFile(path);
                if (s == null || !TitleMatches(profile, s))
                    continue;
                if (notBeforeUtc.HasValue)
                {
                    var limit = notBeforeUtc.Value;
                    var limitUnix = (long)(limit - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    if (s.WrittenUtc < limit || (s.Time != 0 && s.Time < limitUnix))
                        continue;
                }
                if (best == null || s.WrittenUtc > best.WrittenUtc)
                    best = s;
            }
            return best;
        }

        /// <summary>The newest status of all installed Initial D games (the Account page).</summary>
        public static OnlineStatus ReadNewestStatus()
        {
            OnlineStatus best = null;
            foreach (var profile in GameProfileLoader.UserProfiles?.Where(p => p != null && p.OnlineIdType == OnlineIdType.InitialD)
                                    ?? Enumerable.Empty<GameProfile>())
            {
                var s = ReadStatus(profile);
                if (s != null && (best == null || s.WrittenUtc > best.WrittenUtc))
                    best = s;
            }
            return best;
        }

        /// <summary>A translated one-line explanation of a status (the server's public message appended as is).</summary>
        public static string Describe(OnlineStatus s)
        {
            if (s == null)
                return R.InitialDOnlineNoStatus;
            if (s.IsUnified)
                return UnifiedText(s);
            return DescribeState(s);
        }

        /// <summary>The TPAUTH state of a status, as a lower-case fragment ("this account is banned.").</summary>
        private static string DescribeState(OnlineStatus s)
        {
            string text;
            switch (s.State)
            {
                case "online": text = R.InitialDOnlineStateOnline; break;
                case "connecting": text = R.InitialDOnlineStateConnecting; break;
                case "ended": text = R.InitialDOnlineStateEnded; break;
                case "no_credential": text = R.InitialDOnlineStateNoCredential; break;
                case "bad_credential": text = R.InitialDOnlineStateBadCredential; break;
                case "realm_mismatch": text = R.InitialDOnlineStateRealmMismatch; break;
                case "server_legacy": text = R.InitialDOnlineStateServerLegacy; break;
                case "unreachable": text = R.InitialDOnlineStateUnreachable; break;
                case "bad_reply": text = R.InitialDOnlineStateBadReply; break;
                case "denied": text = R.InitialDOnlineStateDenied; break;
                case "banned": text = R.InitialDOnlineStateBanned; break;
                case "revoked": text = R.InitialDOnlineStateRevoked; break;
                case "suspended": text = R.InitialDOnlineStateSuspended; break;
                case "superseded": text = R.InitialDOnlineStateSuperseded; break;
                case "version": text = R.InitialDOnlineStateVersion; break;
                case "busy": text = R.InitialDOnlineStateBusy; break;
                case "unavailable": text = R.InitialDOnlineStateUnavailable; break;
                default: text = string.Format(R.InitialDOnlineStateOther, Clip(s.State, 32)); break;
            }
            // "message" is the server's public text only for a server code; for bad_credential the DLL names the bad
            // key (OnlineID / OnlineSecret, or the credential file); the other local states carry none.
            var message = Clip(s.Message, 200);
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!string.IsNullOrEmpty(s.Code))
                    text += " " + string.Format(R.InitialDOnlineServerSays, message);
                else if (s.State == "bad_credential")
                    text += " (" + message + ")";
            }
            return text;
        }

        private static readonly HashSet<string> AccountReasons =
            new HashSet<string>(new[] { "banned", "suspended", "revoked", "denied", "superseded", "bad_credential", "realm_mismatch" }, StringComparer.Ordinal);

        /// <summary>
        /// The unified mode's one-line text (UNIFIED_MODE.md 2.4): was this start online, and if not, why. A LEGACY start
        /// says what its underlying reason says ("legacy (dns)" = dns), never "compatibility mode".
        /// </summary>
        public static string UnifiedText(OnlineStatus s)
        {
            if (s == null)
                return R.InitialDOnlineNoStatus;
            var reason = (s.Reason ?? "").Trim();
            var m = System.Text.RegularExpressions.Regex.Match(reason, @"^legacy\s*\(\s*([a-z_]+)\s*\)$");
            if (m.Success)
                reason = m.Groups[1].Value;
            if (s.Mode == "online" || (reason == "online" && s.Mode != "lan" && s.Mode != "offline"))
            {
                // online boot; a TPAUTH answer that ended the session later (banned, superseded, ...) still explains itself
                if (!string.IsNullOrEmpty(s.State) && AccountReasons.Contains(s.State))
                    return string.Format(R.InitialDUnifiedOfflineBecause, DescribeState(s));
                return R.InitialDUnifiedOnline;
            }
            switch (reason)
            {
                case "no_adapter": return R.InitialDUnifiedNoAdapter;
                case "no_gateway": return R.InitialDUnifiedNoGateway;
                case "dns":
                case "unreachable":
                case "refused_port":
                case "not_initiald": return R.InitialDUnifiedUnreachable;
                case "maintenance": return R.InitialDUnifiedMaintenance;
                case "rollout": return R.InitialDUnifiedRollout;
                case "version": return R.InitialDUnifiedVersion;
                case "no_credential": return R.InitialDUnifiedNoCredential;
                case "unavailable": return R.InitialDUnifiedUnavailable;
                case "vpn_adapter": return R.InitialDUnifiedVpnAdapter;
                case "forced_no_server": return R.InitialDUnifiedForcedNoServer;
                case "legacy_forced": return R.InitialDUnifiedLegacyForced;
                case "legacy_pair_config": return R.InitialDUnifiedLegacyPair;
            }
            if (AccountReasons.Contains(reason))
            {
                // the existing TPAUTH texts, with the server's public message (the state may be empty in a LAN status)
                var copy = new OnlineStatus { State = string.IsNullOrEmpty(s.State) ? reason : s.State, Code = s.Code, Message = s.Message };
                return string.Format(R.InitialDUnifiedOfflineBecause, DescribeState(copy));
            }
            return string.Format(R.InitialDUnifiedOfflineOther, Clip(reason.Length > 0 ? reason : s.Mode, 32));
        }

        /// <summary>What the GameRunning view shows: a headline and the detail lines.</summary>
        public sealed class LiveStatus
        {
            public string Headline { get; set; } = "";
            public List<string> Details { get; } = new List<string>();
            /// <summary>The DLL confirmed the friends code (party=set).</summary>
            public bool PartyConfirmed { get; set; }
        }

        /// <summary>
        /// The live status of a running Initial D game (UNIFIED_MODE.md 2.4 / 2.7, FRAMELOCK.md 7.4): the outcome text,
        /// the LAN line, "Friends code active", "Online is back", the frame-lock state and its notice.
        /// <paramref name="effectiveMode"/> is the NetworkMode the launch got, <paramref name="sinceLaunch"/> the time
        /// since the start (before the first status file).
        /// </summary>
        public static LiveStatus BuildLiveStatus(OnlineStatus s, string effectiveMode, TimeSpan sinceLaunch)
        {
            var live = new LiveStatus();
            if (s == null)
            {
                if (effectiveMode == InitialDUnifiedMode.ModeNoServer)
                    live.Headline = R.InitialDUnifiedForcedNoServer;
                else if (effectiveMode == InitialDUnifiedMode.ModeLegacy)
                    live.Headline = R.InitialDUnifiedLegacyForced;
                else
                    live.Headline = sinceLaunch < TimeSpan.FromSeconds(20) ? R.InitialDUnifiedChecking : R.InitialDUnifiedNoStatus;
                return live;
            }
            live.Headline = Describe(s);
            if (!s.IsUnified)
                return live;
            var linked = !string.IsNullOrWhiteSpace(s.LanPartner) && !string.IsNullOrWhiteSpace(s.LanSeat);
            if (linked)
                live.Details.Add(string.Format(R.InitialDUnifiedLinked, Clip(s.LanPartner, 40), Clip(s.LanSeat, 4)));
            // P2b: the agent saw other PCs but this game is not paired with one of them
            else if (s.LanPeers == 1)
                live.Details.Add(R.InitialDUnifiedLanPeerOne);
            else if (s.LanPeers > 1)
                live.Details.Add(string.Format(R.InitialDUnifiedLanPeerMany, s.LanPeers));
            if (!string.IsNullOrWhiteSpace(s.LanGroup))
                live.Details.Add(string.Format(R.InitialDUnifiedLanGroup, Clip(s.LanGroup, 32)));
            if (!string.IsNullOrWhiteSpace(s.LanWarning))
                live.Details.Add(string.Format(R.InitialDUnifiedLanWarning, Clip(s.LanWarning, 200)));
            if (string.Equals(s.Party, "set", StringComparison.OrdinalIgnoreCase))
            {
                live.PartyConfirmed = true;
                live.Details.Add(R.InitialDUnifiedPartyActive);
            }
            if (s.OnlineBack && s.Mode != "online")
                live.Details.Add(R.InitialDUnifiedOnlineBack);
            var fl = (s.FrameLockState ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(s.FrameLockText))
                live.Details.Add(Clip(s.FrameLockText, 200));
            else if (fl == "engaged" || fl == "on" || fl == "locked")
                live.Details.Add(R.InitialDUnifiedFrameLockOn);
            else if (fl.Length > 0 && fl != "off")
                live.Details.Add(string.Format(R.InitialDUnifiedFrameLockOther, Clip(fl, 32)));
            if (!string.IsNullOrWhiteSpace(s.Notice))
                live.Details.Add(Clip(s.Notice, 200));
            if (s.Mode == "online" && !string.IsNullOrWhiteSpace(s.Message) && string.IsNullOrEmpty(s.Code) && s.State != "bad_credential")
                live.Details.Add(Clip(s.Message, 200));
            return live;
        }

        private static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var clean = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max) + "...";
        }

        /// <summary>The exit toast text for a boot that started at <paramref name="launchUtc"/>, or null when there is nothing to say.</summary>
        public static string ExitToastText(GameProfile profile, DateTime launchUtc)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.InitialD)
                return null;
            var s = ReadStatus(profile, launchUtc.AddSeconds(-2));
            if (s == null || !s.IsProblem)
                return null;
            return string.Format(R.InitialDOnlineToast, string.IsNullOrEmpty(s.Title) ? profile.ProfileName : s.Title, Describe(s));
        }

        // -------------------------------------------------------------------------------------------------------------
        // Website API (JWT bearer; INITIALD_ONLINE_API.md "TPUI-facing routes")
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

        /// <summary>Spec 2.7; Visibility is public / own / off, the rest optional (null = unchanged).</summary>
        public sealed class ConsentChoice
        {
            public string Visibility { get; set; }
            public bool? Attract { get; set; }
            public bool? AnnounceRankups { get; set; }
            public bool? KeepOwnAura { get; set; }
            public bool? AllowDirect { get; set; }
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

            public Task<ApiResult<CredentialInfo>> ProvisionAsync(string label, ConsentChoice consent) =>
                SendAsync(() => Post("Provision", new { label = Label(label), kind = "personal", consent }), ParseCredential);

            public Task<ApiResult<CredentialInfo>> RegenerateAsync(string pcbId) =>
                SendAsync(() => Post("Regenerate", new { pcbId }), ParseCredential);

            public Task<ApiResult<bool>> RevokeAsync(string pcbId) =>
                SendAsync(() => Post("Revoke", new { pcbId }), _ => true);

            public Task<ApiResult<bool>> SaveConsentAsync(ConsentChoice consent) =>
                SendAsync(() => Post("Consent", consent), _ => true);

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
                        Debug.WriteLine($"InitialDOnline: token: {ex.Message}");
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
                        Debug.WriteLine($"InitialDOnline: request failed: {ex.Message}");
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
                                Debug.WriteLine($"InitialDOnline: fresh login: {ex.Message}");
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
                            // {"error":"no_machine"} = the account has no PCB ID; a 404 without it = the feature is switched off.
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
                        return Str(doc.RootElement, "error");
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
                        return (int)Math.Max(0, Math.Min(int.MaxValue, Int(doc.RootElement, "retryAfterSeconds")));
                }
                catch (JsonException)
                {
                    return 0;
                }
            }
        }

        /// <summary>A translated message for a failed API call.</summary>
        public static string DescribeError<T>(ApiResult<T> result)
        {
            switch (result.Error)
            {
                case ApiError.NotLoggedIn: return R.InitialDOnlineErrorNotLoggedIn;
                case ApiError.FreshLoginRequired:
                case ApiError.FreshLoginFailed: return R.InitialDOnlineErrorFreshLogin;
                case ApiError.NoMachine: return R.InitialDOnlineNotRegistered;
                case ApiError.FeatureOff: return R.InitialDOnlineErrorFeatureOff;
                case ApiError.Banned: return R.InitialDOnlineErrorBanned;
                case ApiError.RateLimited: return R.InitialDOnlineErrorRateLimited;
                case ApiError.Unavailable:
                case ApiError.Unreachable: return R.InitialDOnlineErrorUnavailable;
                default:
                    return string.Format(R.InitialDOnlineErrorGeneric,
                        string.IsNullOrEmpty(result.ErrorCode) ? result.Status.ToString(CultureInfo.InvariantCulture) : result.ErrorCode);
            }
        }
    }
}
