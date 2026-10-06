using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// The unified network mode of the Initial D titles in TeknoParrotUI, phase P1 (InitialDServer docs/UNIFIED_MODE.md
    /// 2.4, 2.6, 2.7, 4.2, 5.3 and 8 "P1"; FRAMELOCK.md 2.6 / 7.4):
    /// <list type="bullet">
    /// <item>which profiles are the five matchmaking titles (ID4 JP, ID5, ID6 Double Ace, ID7, ID8; never ID4 EXP or the
    /// ID6 1.2 build) and on which of them TeknoParrot Online is retired (owner decision 2026-10-02: TPO stays only on
    /// titles without matchmaking);</item>
    /// <item>the online server of a profile (the official server: the profiles have no server field, and Release DLLs
    /// ignore <c>[Network] OnlineServer</c>) and the server's constant <c>/tp/v1/ping</c> reply (maintenance, friends
    /// codes);</item>
    /// <item>automatic server checks are always enabled for the five matchmaking titles;</item>
    /// <item>the per-launch friends code (<c>TP_PARTY</c>; generated, typed, never saved);</item>
    /// <item>the profile migrations: the first move of a user profile to the unified fields (SINGLE becomes AUTO), the
    /// P3 removal of the classic LAN
    /// pair fields (a profile left on the removed "Old LAN pair" view goes back to Basic) and the one-time copy of the
    /// old-loader profiles ID5.xml / ID4Jap.xml into their ElfLoader 2 profiles, whose library entries are then
    /// hidden.</item>
    /// </list>
    /// Nothing here touches a profile whose OnlineIdType is not InitialD, except the TPO checks by profile name.
    /// </summary>
    public static class InitialDUnifiedMode
    {
        // -------------------------------------------------------------------------------------------------------------
        // Titles
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The five matchmaking profiles (file names without .xml).</summary>
        public static readonly IReadOnlyList<string> MatchmakingProfiles = new[] { "ID4JapElf2", "ID5Elf2", "ID6", "ID7", "ID8" };

        /// <summary>Profiles whose TeknoParrot Online entry is retired (ID6 only for the Double Ace build, see IsTpoRetired).</summary>
        private static readonly HashSet<string> TpoRetiredProfiles =
            new HashSet<string>(new[] { "ID4Jap", "ID4JapElf2", "ID5", "ID5Elf2", "ID7", "ID8" }, StringComparer.OrdinalIgnoreCase);

        public const string ViewField = "Show settings";
        public const string ViewBasic = "Basic";
        public const string ViewAdvanced = "Advanced";
        /// <summary>Removed at P3 (the classic LAN pair fields are gone); a user profile left on it goes back to Basic.</summary>
        public const string ViewOldLanPair = "Old LAN pair";
        public const string ModeField = "NetworkMode";
        public const string ModeAuto = "Auto";
        public const string ModeNoServer = "NoServer";
        public const string ModeLegacy = "Legacy";
        public const string ServerField = "OnlineServer";
        public const string SeatField = "SeatNumber";
        public const string PartyEnvironmentVariable = "TP_PARTY";

        /// <summary>The profile's file name without .xml (ProfileName once the library loaded it).</summary>
        public static string ProfileKey(GameProfile profile)
        {
            if (profile == null)
                return "";
            if (!string.IsNullOrEmpty(profile.ProfileName))
                return profile.ProfileName;
            return string.IsNullOrEmpty(profile.FileName) ? "" : Path.GetFileNameWithoutExtension(profile.FileName);
        }

        private static bool IsMatchmakingKey(string key) =>
            MatchmakingProfiles.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// One of the five matchmaking titles (unified mode, friends codes, live status, no TPO). ID6.xml serves two
        /// builds: the 1.2 build (GameID::ID6Update) has no online path and keeps its old behaviour.
        /// </summary>
        public static bool IsMatchmakingProfile(GameProfile profile)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.InitialD)
                return false;
            var key = ProfileKey(profile);
            if (!IsMatchmakingKey(key))
                return false;
            return !string.Equals(key, "ID6", StringComparison.OrdinalIgnoreCase) || Id6BuildOf(profile) != Id6Build.Version12;
        }

        /// <summary>TeknoParrot Online is not offered for this profile (the title has matchmaking).</summary>
        public static bool IsTpoRetired(GameProfile profile)
        {
            var key = ProfileKey(profile);
            if (TpoRetiredProfiles.Contains(key))
                return true;
            return string.Equals(key, "ID6", StringComparison.OrdinalIgnoreCase) && Id6BuildOf(profile) != Id6Build.Version12;
        }

        /// <summary>The TPO lobby's game id (the profile name, with or without .xml): true when TPO is retired for it.</summary>
        public static bool IsTpoRetired(string gameId)
        {
            var key = (gameId ?? "").Trim();
            if (key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - 4);
            if (TpoRetiredProfiles.Contains(key))
                return true;
            if (!string.Equals(key, "ID6", StringComparison.OrdinalIgnoreCase))
                return false;
            var installed = GameProfileLoader.UserProfiles?.FirstOrDefault(p =>
                p != null && string.Equals(ProfileKey(p), "ID6", StringComparison.OrdinalIgnoreCase));
            return installed != null && IsTpoRetired(installed);
        }

        public enum Id6Build
        {
            Unknown,
            DoubleAce,
            Version12,
        }

        /// <summary>
        /// TeknoParrot.dll tells the two ID6 builds apart by the CRC-32 of the first 0x400 bytes of the game module
        /// (GameDetect.cpp, the first switch): 0x379FA53E = GameID::ID6 (Double Ace), 0x8CE99606 = GameID::ID6Update.
        /// </summary>
        public const uint Id6DoubleAceCrc = 0x379FA53E;
        public const uint Id6Version12Crc = 0x8CE99606;

        private static readonly object Id6CacheLock = new object();
        private static string _id6CachePath;
        private static DateTime _id6CacheTime;
        private static long _id6CacheLength;
        private static Id6Build _id6CacheBuild;

        public static Id6Build Id6BuildOf(GameProfile profile) => DetectId6Build(profile?.GamePath);

        public static Id6Build DetectId6Build(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return Id6Build.Unknown;
            try
            {
                var info = new FileInfo(exePath);
                if (!info.Exists || info.Length < 0x400)
                    return Id6Build.Unknown;
                lock (Id6CacheLock)
                {
                    if (string.Equals(_id6CachePath, info.FullName, StringComparison.OrdinalIgnoreCase) &&
                        _id6CacheTime == info.LastWriteTimeUtc && _id6CacheLength == info.Length)
                        return _id6CacheBuild;
                }
                var head = new byte[0x400];
                using (var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    int got = 0;
                    while (got < head.Length)
                    {
                        int n = stream.Read(head, got, head.Length - got);
                        if (n <= 0)
                            return Id6Build.Unknown;
                        got += n;
                    }
                }
                var crc = Crc32(head);
                var build = crc == Id6DoubleAceCrc ? Id6Build.DoubleAce : crc == Id6Version12Crc ? Id6Build.Version12 : Id6Build.Unknown;
                lock (Id6CacheLock)
                {
                    _id6CachePath = info.FullName;
                    _id6CacheTime = info.LastWriteTimeUtc;
                    _id6CacheLength = info.Length;
                    _id6CacheBuild = build;
                }
                return build;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDUnified: cannot read {exePath}: {ex.Message}");
                return Id6Build.Unknown;
            }
        }

        private static uint[] _crcTable;

        /// <summary>CRC-32 (IEEE, reflected, as Utility/Utils.cpp GetCRC32).</summary>
        public static uint Crc32(byte[] data)
        {
            var table = _crcTable;
            if (table == null)
            {
                table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[i] = c;
                }
                _crcTable = table;
            }
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
                crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
            return crc ^ 0xFFFFFFFF;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Server
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The official server's host names: a copy of IDOnlineAuth.cpp detail::kOfficialHosts (change both together,
        /// LAUNCH_CHECKLIST.md 0.1). The server has no DNS name yet, so its IPv4 address is first: the first entry is the
        /// server every launch uses, and the ping goes to it. The name is kept for a later A record.
        /// </summary>
        public static readonly IReadOnlyList<string> OfficialHosts = new[] { "164.132.246.154", "initiald.teknoparrot.com" };

        /// <summary>ALL.Net, news, TPAUTH and the ping share this port on every title (UNIFIED_MODE.md 2.2 step 5).</summary>
        public const int DefaultPort = 8080;

        public sealed class ServerInfo
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public bool Official { get; set; }
        }

        /// <summary>host or host:port (IPv4 / a name; no IPv6). False for an empty or malformed value.</summary>
        public static bool TryParseServer(string value, out string host, out int port)
        {
            host = null;
            port = DefaultPort;
            var t = (value ?? "").Trim();
            if (t.Length == 0 || t.Any(char.IsWhiteSpace) || t.IndexOf('/') >= 0)
                return false;
            int colon = t.LastIndexOf(':');
            if (colon >= 0)
            {
                if (t.IndexOf(':') != colon)
                    return false;
                if (!int.TryParse(t.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
                    port < 1 || port > 65535)
                    return false;
                t = t.Substring(0, colon);
            }
            if (t.Length == 0 || t.Length > 253)
                return false;
            host = t;
            return true;
        }

        public static bool IsOfficialHost(string host) =>
            !string.IsNullOrEmpty(host) && OfficialHosts.Any(h => string.Equals(h, host.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The server a launch of this profile talks to: the official server. The shipped profiles have no OnlineServer
        /// field; a value is only read when a developer adds one to a user profile by hand, for a Debug DLL (the only
        /// build that honours [Network] OnlineServer).
        /// </summary>
        public static ServerInfo ServerOf(GameProfile profile)
        {
            var value = Field(profile, ServerField)?.FieldValue;
            if (string.IsNullOrWhiteSpace(value))
                return new ServerInfo { Host = OfficialHosts[0], Port = DefaultPort, Official = true };
            if (!TryParseServer(value, out var host, out var port))
                return new ServerInfo { Host = value.Trim(), Port = DefaultPort, Official = false };
            return new ServerInfo { Host = host, Port = port, Official = IsOfficialHost(host) };
        }

        // -------------------------------------------------------------------------------------------------------------
        // NetworkMode and teknoparrot.ini
        // -------------------------------------------------------------------------------------------------------------

        private static FieldInformation Field(GameProfile profile, string name) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name && x.CategoryName == "Network");

        private static string CanonicalMode(string value)
        {
            var t = (value ?? "").Trim();
            if (t.Equals(ModeNoServer, StringComparison.OrdinalIgnoreCase))
                return ModeNoServer;
            if (t.Equals(ModeLegacy, StringComparison.OrdinalIgnoreCase))
                return ModeLegacy;
            return ModeAuto;
        }

        /// <summary>
        /// Matchmaking titles always use Auto, including profiles saved with an old opt-out or debug mode.
        /// Other profiles retain their original network mode.
        /// </summary>
        public static string EffectiveNetworkMode(GameProfile profile)
        {
            return IsMatchmakingProfile(profile) ? ModeAuto : CanonicalMode(Field(profile, ModeField)?.FieldValue);
        }

        /// <summary>
        /// ConfigurationWriter hook: the value written for one ini field. Only [Network] NetworkMode of a matchmaking
        /// profile is always Auto; every other field of every profile is returned as is.
        /// </summary>
        public static string IniValue(GameProfile profile, FieldInformation field, string value)
        {
            if (field == null || field.FieldName != ModeField || field.CategoryName != "Network" || !IsMatchmakingProfile(profile))
                return value;
            return ModeAuto;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Friends code (UNIFIED_MODE.md 4.2): per launch, never saved; the DLL hashes it (ph) before it leaves the PC
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Generated codes: 8 symbols of this 32-symbol alphabet (Crockford base32: no I, L, O, U), 40 bits.</summary>
        public const string PartyAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        public const int GeneratedPartyLength = 8;
        public const int MinPartyLength = 6;
        public const int MaxPartyLength = 16;

        public static string GeneratePartyCode()
        {
            var bytes = new byte[5];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            ulong bits = 0;
            foreach (var b in bytes)
                bits = (bits << 8) | b;
            var sb = new StringBuilder(GeneratedPartyLength);
            for (int i = GeneratedPartyLength - 1; i >= 0; i--)
                sb.Append(PartyAlphabet[(int)((bits >> (5 * i)) & 31)]);
            return sb.ToString();
        }

        /// <summary>
        /// The canonical form of a typed or generated code, which TP_PARTY carries: upper case, spaces / dashes /
        /// underscores / dots dropped, O read as 0 and I / L as 1 (so a misread generated code still matches). Null when
        /// it is not 6..16 letters and digits.
        /// </summary>
        public static string NormalizePartyCode(string typed)
        {
            if (typed == null)
                return null;
            var sb = new StringBuilder(typed.Length);
            foreach (var ch in typed.Trim())
            {
                if (ch == ' ' || ch == '-' || ch == '_' || ch == '.')
                    continue;
                var c = char.ToUpperInvariant(ch);
                if (c == 'O')
                    c = '0';
                else if (c == 'I' || c == 'L')
                    c = '1';
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
                    return null;
                sb.Append(c);
            }
            return sb.Length >= MinPartyLength && sb.Length <= MaxPartyLength ? sb.ToString() : null;
        }

        private static readonly HashSet<string> WeakCodes = new HashSet<string>(new[]
        {
            "123456", "1234567", "12345678", "123456789", "1234567890", "654321", "87654321", "123123", "112233",
            "121212", "696969", "TEST12", "TEST123", "TEST1234", "TESTER", "ABCDEF", "ABC123", "QWERTY", "QWERTZ",
            "AZERTY", "PASSWORD", "FRIENDS", "INITIALD", "TEKNOPARROT", "AKINA", "TAKUMI", "TOUGE", "HACHIROKU",
        }.Select(x => NormalizePartyCode(x)).Where(x => x != null), StringComparer.Ordinal);

        /// <summary>A typed code strangers could guess: a common word or number, one repeated symbol, or a run.</summary>
        public static bool IsWeakPartyCode(string normalized)
        {
            if (string.IsNullOrEmpty(normalized))
                return true;
            if (WeakCodes.Contains(normalized) || normalized.All(c => c == normalized[0]))
                return true;
            bool up = true, down = true;
            for (int i = 1; i < normalized.Length; i++)
            {
                up &= normalized[i] == normalized[i - 1] + 1;
                down &= normalized[i] == normalized[i - 1] - 1;
            }
            return up || down;
        }

        /// <summary>For display only: an 8-symbol code as XXXX-XXXX (the dash is dropped again by NormalizePartyCode).</summary>
        public static string FormatPartyCode(string code) =>
            code != null && code.Length == 8 ? code.Substring(0, 4) + "-" + code.Substring(4) : code ?? "";

        public static string InviteText(GameProfile profile, string code) =>
            string.Format(R.InitialDFriendsInvite, profile?.GameNameInternal ?? ProfileKey(profile), FormatPartyCode(code));

        private sealed class LaunchCode
        {
            public string Key;
            public string Code;
            public DateTime PreparedUtc;
        }

        private static readonly object LaunchLock = new object();
        private static LaunchCode _pending;
        private static LaunchCode _active;

        /// <summary>
        /// Called by every library launch of a game (Play / Play with friends): the friends code for this one launch, or
        /// null. A code is held in memory only and is consumed by the next start of the same profile within 5 minutes.
        /// </summary>
        public static void PrepareLaunch(GameProfile profile, string partyCode)
        {
            lock (LaunchLock)
            {
                _active = null;
                _pending = string.IsNullOrEmpty(partyCode) || profile == null
                    ? null
                    : new LaunchCode { Key = ProfileKey(profile), Code = partyCode, PreparedUtc = DateTime.UtcNow };
            }
        }

        /// <summary>GameProcessManager: the TP_PARTY value for the process it starts now (null = none; also removes an inherited value).</summary>
        public static string TakePartyCodeForProcess(GameProfile profile)
        {
            lock (LaunchLock)
            {
                var p = _pending;
                _pending = null;
                if (p == null || !string.Equals(p.Key, ProfileKey(profile), StringComparison.OrdinalIgnoreCase) ||
                    DateTime.UtcNow - p.PreparedUtc > TimeSpan.FromMinutes(5))
                {
                    _active = null;
                    return null;
                }
                _active = p;
                return p.Code;
            }
        }

        /// <summary>The friends code of the running (or starting) launch of this profile, for the GameRunning view.</summary>
        public static string CurrentPartyCode(GameProfile profile)
        {
            lock (LaunchLock)
            {
                var p = _active ?? _pending;
                return p != null && string.Equals(p.Key, ProfileKey(profile), StringComparison.OrdinalIgnoreCase) ? p.Code : null;
            }
        }

        /// <summary>The launch ended: forget its code.</summary>
        public static void EndLaunch(GameProfile profile)
        {
            lock (LaunchLock)
            {
                if (_active != null && string.Equals(_active.Key, ProfileKey(profile), StringComparison.OrdinalIgnoreCase))
                    _active = null;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // GET /tp/v1/ping (UNIFIED_MODE.md 2.6): a constant reply per server, no credential
        // -------------------------------------------------------------------------------------------------------------

        public sealed class PingInfo
        {
            /// <summary>An InitialDServer answered (stat=1, stat=0&amp;busy=1, or 404 = a server without the endpoint).</summary>
            public bool Reachable { get; set; }
            public bool Busy { get; set; }
            public bool NoEndpoint { get; set; }
            public string Realm { get; set; } = "";
            public string Auth { get; set; } = "";
            public bool Maintenance { get; set; }
            public int Admit { get; set; } = 100;
            public int Lanbox { get; set; }
            public string MinDll { get; set; } = "";
            /// <summary>Friends codes on (null = the reply did not say).</summary>
            public bool? Party { get; set; }
            public bool? Tag { get; set; }
        }

        /// <summary>Parses a ping reply; null = not an InitialDServer (a captive portal, another service on the port).</summary>
        public static PingInfo ParsePing(int status, string body)
        {
            if (status == 404)
                return new PingInfo { Reachable = true, NoEndpoint = true };
            if (status != 200 || body == null)
                return null;
            var t = body.Trim();
            if (!t.StartsWith("stat=", StringComparison.Ordinal) || t.Length > 4096)
                return null;
            var kv = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in t.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && !kv.ContainsKey(part.Substring(0, eq)))
                    kv[part.Substring(0, eq)] = Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' '));
            }
            string Get(string k) => kv.TryGetValue(k, out var v) ? v : null;
            bool? Flag(string k) => Get(k) == "1" ? true : Get(k) == "0" ? (bool?)false : null;
            int Num(string k, int dflt, int min, int max) =>
                int.TryParse(Get(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Max(min, Math.Min(max, n)) : dflt;
            var stat = Get("stat");
            if (stat == "0")
                return Get("busy") == "1" ? new PingInfo { Reachable = true, Busy = true } : null;
            if (stat != "1")
                return null;
            return new PingInfo
            {
                Reachable = true,
                Realm = Get("realm") ?? "",
                Auth = Get("auth") ?? "",
                Maintenance = Flag("maint") == true,
                Admit = Num("admit", 100, 0, 100),
                Lanbox = Num("lanbox", 0, 0, 100),
                MinDll = Get("min_dll") ?? "",
                Party = Flag("party"),
                Tag = Flag("tag"),
            };
        }

        /// <summary>The ping with a hard time limit; null when the server does not answer in time or is not an InitialDServer.</summary>
        public static async Task<PingInfo> PingAsync(ServerInfo server, TimeSpan timeout, HttpMessageHandler handler = null)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.Host))
                return null;
            try
            {
                var url = new UriBuilder("http", server.Host, server.Port, "/tp/v1/ping").Uri;
                using (var cts = new CancellationTokenSource(timeout))
                using (var http = handler == null ? new HttpClient() : new HttpClient(handler, false))
                {
                    http.Timeout = timeout + TimeSpan.FromSeconds(1);
                    var send = http.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token);
                    // DNS lookups ignore the token on .NET Framework: never wait longer than the budget.
                    var done = await Task.WhenAny(send, Task.Delay(timeout)).ConfigureAwait(true);
                    if (done != send)
                    {
                        cts.Cancel();
                        _ = send.ContinueWith(x => x.Exception, TaskContinuationOptions.OnlyOnFaulted);
                        return null;
                    }
                    using (var response = await send.ConfigureAwait(true))
                    {
                        var body = response.Content == null ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        return ParsePing((int)response.StatusCode, body);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDUnified: ping {server.Host}:{server.Port}: {ex.Message}");
                return null;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Migrations (UNIFIED_MODE.md 5.3)
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// GameProfileLoader's revision merge, called before the user's values are copied into the new base profile:
        /// returns the step to run after the copy, or null for every profile that is not a matchmaking title.
        /// <para>P1, once per profile, on the first revision with the unified fields (the old user profile has no
        /// NetworkMode yet):</para>
        /// <list type="bullet">
        /// <item>the shipped SeatNumber=SINGLE becomes AUTO (a hand-picked A1..D2 stays).</item>
        /// <item>The old OnlineServer / AllNetPort values are not carried over: the profiles have no server field, and
        /// a Release DLL always uses the official server.</item>
        /// </list>
        /// <para>P3 (UNIFIED_MODE.md 5.1 / 5.3), on every revision merge: the classic LAN pair fields are gone, so a
        /// profile that still holds a removed "Show settings" value ("Old LAN pair") is put back on a view the base
        /// profile offers - without it the settings page would show a dropdown value it has no option for. The EEPROM
        /// network record (Dhcp / Ip / Mask / Gateway / Dns1 / Dns2) stays and is carried over by the normal field
        /// copy: the DLL rebuilds the Lindbergh EEPROM eth0 record from it on every boot (5.3, fp1 correctness
        /// review F1).</para>
        /// </summary>
        public static Action PrepareRevisionMigration(GameProfile newBase, GameProfile oldUser)
        {
            if (newBase == null || oldUser == null || newBase.OnlineIdType != OnlineIdType.InitialD || !IsMatchmakingKey(ProfileKey(newBase)))
                return null;
            if (Field(newBase, ModeField) == null)
                return null;
            // P1 runs only on the move to the unified fields; the P3 view fix runs on every merge.
            var toUnified = Field(oldUser, ModeField) == null;

            var oldSeat = Field(oldUser, SeatField)?.FieldValue;
            var baseView = Field(newBase, ViewField)?.FieldValue;

            return () =>
            {
                if (toUnified)
                {
                    var seat = Field(newBase, SeatField);
                    var seatOld = (oldSeat ?? "").Trim();
                    if (seat?.FieldOptions != null && seat.FieldOptions.Contains("AUTO") &&
                        (seatOld.Length == 0 || seatOld.Equals("SINGLE", StringComparison.OrdinalIgnoreCase) || seatOld == "0"))
                        seat.FieldValue = "AUTO";
                }

                var view = Field(newBase, ViewField);
                if (view?.FieldOptions != null && !string.IsNullOrEmpty(view.FieldValue) &&
                    !view.FieldOptions.Contains(view.FieldValue))
                    view.FieldValue = view.FieldOptions.Contains(baseView ?? "") ? baseView : ViewBasic;
            };
        }

        /// <summary>
        /// P3 (UNIFIED_MODE.md 5.1): the old-loader library entries ID5.xml / ID4Jap.xml are hidden once their
        /// ElfLoader 2 successor is installed - the migration has copied the settings over
        /// (MigrateOldLoaderProfiles) and the old tile has had its release with the "moved to ..." note. A user whose
        /// successor profile is not installed keeps the old entry, so nothing disappears without a replacement.
        /// Returns the number of entries hidden.
        /// </summary>
        public static int HideReplacedOldLoaderProfiles(List<GameProfile> userProfiles)
        {
            if (userProfiles == null || userProfiles.Count == 0)
                return 0;
            var installed = new HashSet<string>(userProfiles.Select(ProfileKey), StringComparer.OrdinalIgnoreCase);
            var hidden = 0;
            foreach (var pair in OldLoaderProfiles)
            {
                if (!installed.Contains(pair.Key) || !installed.Contains(pair.Value))
                    continue;
                hidden += userProfiles.RemoveAll(p =>
                    string.Equals(ProfileKey(p), pair.Key, StringComparison.OrdinalIgnoreCase));
                Debug.WriteLine($"InitialDUnified: {pair.Key} hidden ({pair.Value} is installed)");
            }
            return hidden;
        }

        /// <summary>The old-loader profiles and their ElfLoader 2 successors.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> OldLoaderProfiles = new[]
        {
            new KeyValuePair<string, string>("ID5", "ID5Elf2"),
            new KeyValuePair<string, string>("ID4Jap", "ID4JapElf2"),
        };

        /// <summary>The ElfLoader 2 successor of an old-loader profile, or null.</summary>
        public static string SuccessorOf(GameProfile profile)
        {
            var key = ProfileKey(profile);
            return OldLoaderProfiles.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        /// <summary>
        /// One-time copy of an installed old-loader profile (UserProfiles\ID5.xml / ID4Jap.xml) into its ElfLoader 2
        /// profile when that one is not installed yet: GamePath, the input bindings and every field both profiles share
        /// (the revision merge), plus this PC's Online ID. The old entry stays in the library (one release, "moved to").
        /// Done once per old profile (ParrotData.InitialDOldLoaderMigrated), so a deleted Elf2 profile is not created
        /// again. Returns the number of profiles created.
        /// </summary>
        public static int MigrateOldLoaderProfiles(string gameProfilesDir = "GameProfiles", string userProfilesDir = "UserProfiles")
        {
            var data = Lazydata.ParrotData;
            if (data == null)
                return 0;
            int created = 0;
            bool changed = false;
            try
            {
                var done = new HashSet<string>((data.InitialDOldLoaderMigrated ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
                foreach (var pair in OldLoaderProfiles)
                {
                    if (done.Contains(pair.Key))
                        continue;
                    var oldUser = Path.Combine(userProfilesDir, pair.Key + ".xml");
                    var newUser = Path.Combine(userProfilesDir, pair.Value + ".xml");
                    var newBase = Path.Combine(gameProfilesDir, pair.Value + ".xml");
                    if (!File.Exists(oldUser) || !File.Exists(newBase))
                        continue;
                    if (File.Exists(newUser))
                    {
                        done.Add(pair.Key);
                        changed = true;
                        continue;
                    }
                    var oldProfile = InitialDOnlineHelper.ReadProfileQuietly(oldUser);
                    var profile = InitialDOnlineHelper.ReadProfileQuietly(newBase);
                    if (oldProfile == null || profile == null)
                        continue;
                    GameProfileLoader.MergeUserSettings(profile, oldProfile);
                    InitialDOnlineHelper.AutoFill(profile);
                    profile.FileName = newUser;
                    JoystickHelper.SerializeGameProfile(profile, newUser);
                    done.Add(pair.Key);
                    changed = true;
                    created++;
                    Debug.WriteLine($"InitialDUnified: {pair.Key}.xml copied to {pair.Value}.xml");
                }
                if (changed)
                {
                    data.InitialDOldLoaderMigrated = string.Join(",", done.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                    JoystickHelper.Serialize();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDUnified: old-loader migration: {ex.Message}");
            }
            return created;
        }
    }
}
