using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Common.Online
{
    /// <summary>
    /// Initial D LAN presence (InitialDServer docs/LAN_AGENT.md "TPLA v1", UNIFIED_MODE.md 2.7 "LAN presence",
    /// 3.3 / 3.4, phase P2b). While one of the five matchmaking titles is selected in the library, TeknoParrotUI
    /// beacons on the agent port so the other TeknoParrot PCs of the LAN see it, shows them ("Bob's PC is ready for
    /// Initial D 7"), asks the one-time link question, and starts both games together.
    /// <para>
    /// TeknoParrotUI is the <b>presence</b> role (<c>role</c> 0): it sends BEACON / BYE / START and nothing else, and it
    /// <b>releases the port before a game starts</b>, so the game's own agent gets the exclusive bind. The install id is
    /// made once, kept in ParrotData and handed to the game in <c>TP_LAN_INSTALL</c>, so a PC is one install to its
    /// peers whether its game or its launcher is beaconing.
    /// </para>
    /// Nothing here contacts the Internet, and no beacon carries a cabinet serial, a credential or a friends code.
    /// </summary>
    public static class InitialDLanPresence
    {
        public const int Port = 13390;
        public const string InstallEnvironmentVariable = "TP_LAN_INSTALL";
        public const string ConsentEnvironmentVariable = "TP_LAN_CONSENT";
        public const string UiEnvironmentVariable = "TP_LANUI";
        public const string GroupField = "LanGroup";
        public const string PeersField = "LanPeers";
        public const string NameField = "LanName";

        public const byte TypeBeacon = 1;
        public const byte TypeBye = 7;
        public const byte TypeStart = 8;
        public const int BeaconBody = 88;
        public const int ByeBody = 24;
        public const int StartBody = 32;
        public const int NameBytes = 22;

        public const byte FlagLinkAlways = 1;
        public const byte FlagLinkOff = 2;
        public const byte FlagReady = 4;

        /// <summary>A peer is gone after this much silence (LAN_AGENT.md 1).</summary>
        public static readonly TimeSpan PeerTtl = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan BeaconInterval = TimeSpan.FromSeconds(1);
        public const int MaxPeers = 64;
        public const int MaxDatagramsPerSecondPerSource = 20;
        public const int MaxDatagram = 4096;

        private static readonly byte[] Magic = { (byte)'T', (byte)'P', (byte)'L', (byte)'A' };

        // -------------------------------------------------------------------------------------------------------------
        // The wire (LAN_AGENT.md 2-5; tools/tpla_vectors.py is the reference implementation)
        // -------------------------------------------------------------------------------------------------------------

        public enum MessageType { Beacon, Bye, Start, Other }

        public sealed class Message
        {
            public MessageType Type { get; set; }
            public string Title { get; set; } = "";
            public string Version { get; set; } = "";
            public string InstallId { get; set; } = "";
            public string BootId { get; set; } = "";
            public ulong BootMs { get; set; }
            public ulong GroupHash { get; set; }
            public IPAddress Ip { get; set; } = IPAddress.Any;
            public int Prefix { get; set; }
            public int Role { get; set; }
            public int Outcome { get; set; }
            public int Seat { get; set; }
            public int SeatClaim { get; set; }
            public byte Flags { get; set; }
            public string Name { get; set; } = "";

            public bool LinkAlways => (Flags & FlagLinkAlways) != 0;
            public bool LinkOff => (Flags & FlagLinkOff) != 0;
            public bool Ready => (Flags & FlagReady) != 0;
        }

        /// <summary>u64: the first 8 bytes of SHA-256("TPLAG1|" + LanGroup), big-endian. Blank or empty = 0.</summary>
        public static ulong GroupHash(string group)
        {
            var g = (group ?? "").Trim();
            if (g.Length == 0)
                return 0;
            using (var sha = SHA256.Create())
            {
                var d = sha.ComputeHash(Encoding.UTF8.GetBytes("TPLAG1|" + g));
                ulong v = 0;
                for (int i = 0; i < 8; i++)
                    v = (v << 8) | d[i];
                return v;
            }
        }

        /// <summary>UTF-8, cut on a character boundary at <paramref name="width"/> bytes, NUL-padded to it.</summary>
        public static byte[] FixedText(string s, int width)
        {
            var all = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(all.Length, width);
            // a cut inside a character walks back to where the character starts (all[n] is then a continuation byte)
            while (n > 0 && n < all.Length && (all[n] & 0xC0) == 0x80)
                n--;
            var outb = new byte[width];
            Buffer.BlockCopy(all, 0, outb, 0, n);
            return outb;
        }

        /// <summary>A received name: up to the first NUL, control and format characters out, trimmed.</summary>
        public static string CleanName(byte[] raw, int off, int len)
        {
            int n = 0;
            while (n < len && raw[off + n] != 0)
                n++;
            var s = Encoding.UTF8.GetString(raw, off, n);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format ||
                    cat == UnicodeCategory.OtherNotAssigned || cat == UnicodeCategory.Surrogate ||
                    cat == UnicodeCategory.PrivateUse || cat == UnicodeCategory.LineSeparator ||
                    cat == UnicodeCategory.ParagraphSeparator)
                    continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private static byte[] Header(byte type, int bodyLength)
        {
            var h = new byte[8];
            Buffer.BlockCopy(Magic, 0, h, 0, 4);
            h[4] = 1;
            h[5] = type;
            h[6] = (byte)(bodyLength >> 8);
            h[7] = (byte)bodyLength;
            return h;
        }

        private static void PutU64(byte[] b, int off, ulong v)
        {
            for (int i = 0; i < 8; i++)
                b[off + i] = (byte)(v >> (56 - 8 * i));
        }

        private static ulong GetU64(byte[] b, int off)
        {
            ulong v = 0;
            for (int i = 0; i < 8; i++)
                v = (v << 8) | b[off + i];
            return v;
        }

        public static byte[] EncodeBeacon(string title, string version, byte[] installId, byte[] bootId, ulong bootMs,
            ulong groupHash, IPAddress ip, int prefix, int role, int outcome, int seat, int seatClaim, byte flags,
            string name)
        {
            if (installId == null || installId.Length != 16 || bootId == null || bootId.Length != 8)
                throw new ArgumentException("installId is 16 bytes, bootId is 8");
            var d = new byte[8 + BeaconBody];
            Buffer.BlockCopy(Header(TypeBeacon, BeaconBody), 0, d, 0, 8);
            Buffer.BlockCopy(FixedText(title, 8), 0, d, 8, 8);
            Buffer.BlockCopy(FixedText(version, 8), 0, d, 16, 8);
            Buffer.BlockCopy(installId, 0, d, 24, 16);
            Buffer.BlockCopy(bootId, 0, d, 40, 8);
            PutU64(d, 48, bootMs);
            PutU64(d, 56, groupHash);
            Buffer.BlockCopy((ip ?? IPAddress.Any).GetAddressBytes(), 0, d, 64, 4);
            d[68] = (byte)prefix;
            d[69] = (byte)role;
            d[70] = (byte)outcome;
            d[71] = (byte)seat;
            d[72] = (byte)seatClaim;
            d[73] = flags;
            Buffer.BlockCopy(FixedText(name, NameBytes), 0, d, 74, NameBytes);
            return d;
        }

        public static byte[] EncodeBye(byte[] installId, byte[] bootId)
        {
            var d = new byte[8 + ByeBody];
            Buffer.BlockCopy(Header(TypeBye, ByeBody), 0, d, 0, 8);
            Buffer.BlockCopy(installId, 0, d, 8, 16);
            Buffer.BlockCopy(bootId, 0, d, 24, 8);
            return d;
        }

        public static byte[] EncodeStart(string title, byte[] installId, byte[] bootId)
        {
            var d = new byte[8 + StartBody];
            Buffer.BlockCopy(Header(TypeStart, StartBody), 0, d, 0, 8);
            Buffer.BlockCopy(FixedText(title, 8), 0, d, 8, 8);
            Buffer.BlockCopy(installId, 0, d, 16, 16);
            Buffer.BlockCopy(bootId, 0, d, 32, 8);
            return d;
        }

        /// <summary>A TPLA v1 message, or null when any form rule of LAN_AGENT.md 6 step 1 fails.</summary>
        public static Message Decode(byte[] d, int length)
        {
            if (d == null || length < 8 || length > MaxDatagram || length > d.Length)
                return null;
            if (d[0] != Magic[0] || d[1] != Magic[1] || d[2] != Magic[2] || d[3] != Magic[3] || d[4] != 1)
                return null;
            int blen = (d[6] << 8) | d[7];
            if (length != 8 + blen)
                return null;
            switch (d[5])
            {
                case TypeBeacon:
                    if (blen != BeaconBody)
                        return null;
                    return new Message
                    {
                        Type = MessageType.Beacon,
                        Title = CleanName(d, 8, 8),
                        Version = CleanName(d, 16, 8),
                        InstallId = Hex(d, 24, 16),
                        BootId = Hex(d, 40, 8),
                        BootMs = GetU64(d, 48),
                        GroupHash = GetU64(d, 56),
                        Ip = new IPAddress(new[] { d[64], d[65], d[66], d[67] }),
                        Prefix = d[68],
                        Role = d[69],
                        Outcome = d[70],
                        Seat = d[71],
                        SeatClaim = d[72],
                        Flags = d[73],
                        Name = CleanName(d, 74, NameBytes),
                    };
                case TypeBye:
                    if (blen != ByeBody)
                        return null;
                    return new Message { Type = MessageType.Bye, InstallId = Hex(d, 8, 16), BootId = Hex(d, 24, 8) };
                case TypeStart:
                    if (blen != StartBody)
                        return null;
                    return new Message
                    {
                        Type = MessageType.Start,
                        Title = CleanName(d, 8, 8),
                        InstallId = Hex(d, 16, 16),
                        BootId = Hex(d, 32, 8),
                    };
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    return new Message { Type = MessageType.Other };   // game agent to game agent (LAN_AGENT.md 2)
                default:
                    return null;
            }
        }

        private static string Hex(byte[] d, int off, int len)
        {
            var sb = new StringBuilder(len * 2);
            for (int i = 0; i < len; i++)
                sb.Append(d[off + i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static byte[] Random(int n)
        {
            var b = new byte[n];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return null;
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                if (!byte.TryParse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b[i]))
                    return null;
            return b;
        }

        // -------------------------------------------------------------------------------------------------------------
        // This install: its id, its name, the title codes and the link consent
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The install id of this PC, 32 hex characters: made once and kept in ParrotData.</summary>
        public static string InstallId
        {
            get
            {
                var data = Lazydata.ParrotData;
                var kept = (data.InitialDLanInstallId ?? "").Trim().ToLowerInvariant();
                if (kept.Length == 32 && FromHex(kept) != null)
                    return kept;
                var made = Hex(Random(16), 0, 16);
                data.InitialDLanInstallId = made;
                try
                {
                    JoystickHelper.Serialize();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitialDLan: cannot save the install id: {ex.Message}");
                }
                return made;
            }
        }

        /// <summary>The title code of a matchmaking profile ("SBJJ1", "SBJJ", "SBUU", "SBYD", "SBZZ"), else "".</summary>
        public static string TitleCode(GameProfile profile)
        {
            switch (InitialDUnifiedMode.ProfileKey(profile))
            {
                case "ID4JapElf2": return "SBJJ1";
                case "ID5Elf2": return "SBJJ";
                case "ID6": return InitialDUnifiedMode.Id6BuildOf(profile) == InitialDUnifiedMode.Id6Build.Version12 ? "" : "SBUU";
                case "ID7": return "SBYD";
                case "ID8": return "SBZZ";
                default: return "";
            }
        }

        private static string FieldValue(GameProfile profile, string name) =>
            profile?.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? "";

        /// <summary>LanName, or the Windows computer name when it is empty.</summary>
        public static string DisplayName(GameProfile profile)
        {
            var n = FieldValue(profile, NameField).Trim();
            if (n.Length == 0)
            {
                try
                {
                    n = Environment.MachineName;
                }
                catch (Exception)
                {
                    n = "TeknoParrot";
                }
            }
            return n;
        }

        public static string LanGroup(GameProfile profile) => FieldValue(profile, GroupField).Trim();

        /// <summary>The LanPeers addresses of a profile (comma or space separated; bad entries dropped).</summary>
        public static List<IPAddress> LanPeers(GameProfile profile)
        {
            var list = new List<IPAddress>();
            foreach (var part in FieldValue(profile, PeersField).Split(new[] { ',', ';', ' ', '\t' },
                         StringSplitOptions.RemoveEmptyEntries))
                if (IPAddress.TryParse(part.Trim(), out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                    list.Add(ip);
            return list;
        }

        public enum Consent { Ask, Always, Never }

        /// <summary>
        /// The install ids the player answered "This time" for in this run of TeknoParrotUI. Nothing is saved for them
        /// (LAN_AGENT.md 7), but the game started afterwards still has to know, so they go into TP_LAN_CONSENT as
        /// <c>once</c> rows: the agent then links with that peer for this boot only.
        /// </summary>
        private static readonly HashSet<string> SessionConsent = new HashSet<string>();

        /// <summary>Remembers a "This time" answer for the game processes this run of TeknoParrotUI starts.</summary>
        public static void NoteConsentOnce(string installId)
        {
            var id = (installId ?? "").Trim().ToLowerInvariant();
            if (id.Length != 32)
                return;
            lock (SessionConsent)
            {
                if (SessionConsent.Count < 64)
                    SessionConsent.Add(id);
            }
        }

        /// <summary>
        /// GameProcessManager: the TP_LAN_CONSENT value for the process it starts now - the kept answers plus this
        /// run's "This time" ones, as "&lt;32 hex&gt;=always|never|once" rows. null when there is nothing to hand over.
        /// </summary>
        public static string ConsentRowsForProcess()
        {
            var rows = new List<string>();
            foreach (var entry in (Lazydata.ParrotData.InitialDLanConsent ?? "").Split(new[] { ',' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = entry.Split('=');
                if (kv.Length != 2)
                    continue;
                var id = kv[0].Trim().ToLowerInvariant();
                if (id.Length != 32)
                    continue;
                rows.Add(id + "=" + (kv[1].Trim().ToLowerInvariant() == "always" ? "always" : "never"));
            }
            lock (SessionConsent)
            {
                foreach (var id in SessionConsent)
                    rows.Add(id + "=once");
            }
            return rows.Count == 0 ? null : string.Join(",", rows);
        }

        /// <summary>The kept answer of the link question for a peer install id (LAN_AGENT.md 7).</summary>
        public static Consent ConsentOf(string installId)
        {
            var id = (installId ?? "").Trim().ToLowerInvariant();
            if (id.Length != 32)
                return Consent.Ask;
            foreach (var entry in (Lazydata.ParrotData.InitialDLanConsent ?? "").Split(new[] { ',' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = entry.Split('=');
                if (kv.Length == 2 && kv[0].Trim().ToLowerInvariant() == id)
                    return kv[1].Trim().ToLowerInvariant() == "always" ? Consent.Always : Consent.Never;
            }
            return Consent.Ask;
        }

        /// <summary>Keeps (or forgets, with Ask) the answer for a peer install id. At most 64 entries are kept.</summary>
        public static void SetConsent(string installId, Consent consent)
        {
            var id = (installId ?? "").Trim().ToLowerInvariant();
            if (id.Length != 32)
                return;
            var kept = new List<string>();
            foreach (var entry in (Lazydata.ParrotData.InitialDLanConsent ?? "").Split(new[] { ',' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = entry.Split('=');
                if (kv.Length == 2 && kv[0].Trim().ToLowerInvariant() != id)
                    kept.Add(kv[0].Trim().ToLowerInvariant() + "=" + (kv[1].Trim().ToLowerInvariant() == "always" ? "always" : "never"));
            }
            if (consent != Consent.Ask)
                kept.Add(id + "=" + (consent == Consent.Always ? "always" : "never"));
            if (kept.Count > 64)
                kept.RemoveRange(0, kept.Count - 64);
            Lazydata.ParrotData.InitialDLanConsent = string.Join(",", kept);
            try
            {
                JoystickHelper.Serialize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: cannot save the link consent: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // The network this PC is on
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The adapter the DLL's probe will bind to: the one named in TP_ETH (Elfldr2NetworkAdapterName) when it has an
        /// IPv4 address, else the first up adapter with an IPv4 default gateway, else the first up private address.
        /// </summary>
        public static bool SelectedAdapter(out IPAddress ip, out int prefix)
        {
            ip = null;
            prefix = 0;
            var wanted = (Lazydata.ParrotData.Elfldr2NetworkAdapterName ?? "").Trim();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();
                var ordered = new List<NetworkInterface>();
                if (wanted.Length > 0)
                    ordered.AddRange(nics.Where(n => n.Name == wanted || n.Description == wanted));
                ordered.AddRange(nics.Where(n => !ordered.Contains(n) &&
                                                 n.GetIPProperties().GatewayAddresses.Any(g =>
                                                     g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                                     !g.Address.Equals(IPAddress.Any))));
                ordered.AddRange(nics.Where(n => !ordered.Contains(n)));
                foreach (var n in ordered)
                {
                    foreach (var a in n.GetIPProperties().UnicastAddresses)
                    {
                        if (a.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;
                        ip = a.Address;
                        prefix = PrefixOf(a);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: adapters: {ex.Message}");
            }
            return false;
        }

        private static int PrefixOf(UnicastIPAddressInformation a)
        {
            try
            {
                if (a.IPv4Mask == null)
                    return 0;
                int bits = 0;
                foreach (var b in a.IPv4Mask.GetAddressBytes())
                    for (int i = 7; i >= 0; i--)
                        if ((b & (1 << i)) != 0)
                            bits++;
                return bits;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>True when the two addresses share the /<paramref name="prefix"/> network of LAN_AGENT.md 6 step 2.</summary>
        public static bool SameSubnet(IPAddress a, IPAddress b, int prefix)
        {
            if (a == null || b == null || prefix <= 0 || prefix > 32)
                return false;
            var x = a.GetAddressBytes();
            var y = b.GetAddressBytes();
            if (x.Length != 4 || y.Length != 4)
                return false;
            uint ux = (uint)((x[0] << 24) | (x[1] << 16) | (x[2] << 8) | x[3]);
            uint uy = (uint)((y[0] << 24) | (y[1] << 16) | (y[2] << 8) | y[3]);
            uint mask = prefix == 32 ? uint.MaxValue : ~(uint.MaxValue >> prefix);
            return (ux & mask) == (uy & mask);
        }

        /// <summary>
        /// Windows' own category of the connected networks (Network List Manager): true only when every connected
        /// network is Private or Domain. Unknown (no COM, no network) = false, which means "ask for a LAN group".
        /// </summary>
        public static bool NetworkIsPrivate()
        {
            try
            {
                var t = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
                if (t == null)
                    return false;
                var mgr = Activator.CreateInstance(t);
                // NLM_ENUM_NETWORK_CONNECTED = 1
                var nets = t.InvokeMember("GetNetworks", System.Reflection.BindingFlags.InvokeMethod, null, mgr,
                    new object[] { 1 });
                bool any = false;
                foreach (var n in (System.Collections.IEnumerable)nets)
                {
                    any = true;
                    var cat = n.GetType().InvokeMember("GetCategory", System.Reflection.BindingFlags.InvokeMethod, null, n,
                        null);
                    // NLM_NETWORK_CATEGORY: 0 public, 1 private, 2 domain authenticated
                    var v = Convert.ToInt32(cat, CultureInfo.InvariantCulture);
                    if (v != 1 && v != 2)
                        return false;
                }
                return any;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: network category: {ex.Message}");
                return false;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // The live presence
        // -------------------------------------------------------------------------------------------------------------

        public sealed class Peer
        {
            public string InstallId { get; set; } = "";
            public string BootId { get; set; } = "";
            public string Name { get; set; } = "";
            public IPAddress Address { get; set; }
            public int Role { get; set; }
            public bool Ready { get; set; }
            public bool LinkAlways { get; set; }
            public bool LinkOff { get; set; }
            public DateTime SeenUtc { get; set; }
            public Consent Consent { get; set; }
        }

        /// <summary>
        /// One beaconing session: it owns the socket and the peer table. Start it while an Initial D title is selected,
        /// and dispose it before the game starts (LAN_AGENT.md 1: the game's agent needs the exclusive bind).
        /// </summary>
        public sealed class Session : IDisposable
        {
            private readonly object _lock = new object();
            private readonly Dictionary<string, Peer> _peers = new Dictionary<string, Peer>(StringComparer.Ordinal);
            private readonly Dictionary<string, int[]> _rate = new Dictionary<string, int[]>(StringComparer.Ordinal);
            private readonly byte[] _install;
            private readonly byte[] _boot = Random(8);
            private readonly ulong _bootMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            private readonly List<IPEndPoint> _targets = new List<IPEndPoint>();
            private readonly List<IPAddress> _unicast;
            private Socket _socket;
            private Timer _timer;
            private byte[] _beacon;
            private int _rateSecond;
            private bool _stopped;

            public string Title { get; }
            public int PortUsed { get; }
            /// <summary>This install's id as it goes on the wire (32 hex).</summary>
            public string InstallIdHex => Hex(_install, 0, 16);
            public string Group { get; }
            public ulong GroupHashValue { get; }
            public IPAddress LocalIp { get; private set; } = IPAddress.Any;
            public int Prefix { get; }
            public bool LinkingOff { get; }
            /// <summary>Why linking is off ("" = it is on).</summary>
            public string LinkingOffReason { get; } = "";
            /// <summary>Set when the port was taken (a game of this PC is beaconing): presence is then off.</summary>
            public string StartError { get; private set; } = "";

            /// <summary>A peer appeared, changed or went away.</summary>
            public event Action PeersChanged;
            /// <summary>A consented peer pressed "Start together" (LAN_AGENT.md 5).</summary>
            public event Action<Peer> StartRequested;
            /// <summary>A peer asks to link and has no kept answer yet.</summary>
            public event Action<Peer> LinkAsked;

            public Session(GameProfile profile) : this(profile, Port)
            {
            }

            /// <param name="port">the agent port; another one only for the tests (two sessions on one PC)</param>
            /// <param name="bindIp">
            /// the address to bind and announce; null = the adapter the DLL's probe will use. The tests give each
            /// instance its own loopback address, the way the DLL's TP_BIND_IP does (UNIFIED_MODE.md 3.8).
            /// </param>
            /// <param name="prefix">the prefix length of <paramref name="bindIp"/></param>
            public Session(GameProfile profile, int port, IPAddress bindIp = null, int prefix = 0)
            {
                PortUsed = port;
                Title = TitleCode(profile);
                Group = LanGroup(profile);
                GroupHashValue = GroupHash(Group);
                _unicast = LanPeers(profile);
                _install = FromHex(InstallId) ?? Random(16);
                if (bindIp != null)
                {
                    LocalIp = bindIp;
                    Prefix = prefix;
                }
                else
                {
                    SelectedAdapter(out var ip, out int auto);
                    LocalIp = ip ?? IPAddress.Any;
                    Prefix = auto;
                }
                if (!NetworkIsPrivate() && Group.Length == 0 && _unicast.Count == 0)
                {
                    LinkingOff = true;
                    LinkingOffReason = "public_network";
                }
                _beacon = BuildBeacon(DisplayName(profile));
            }

            private byte[] BuildBeacon(string name)
            {
                byte flags = FlagReady;
                if (LinkingOff)
                    flags |= FlagLinkOff;
                if (Group.Length != 0)
                    flags |= FlagLinkAlways;   // a shared LanGroup is consent (UNIFIED_MODE.md 3.3)
                return EncodeBeacon(Title, "", _install, _boot, _bootMs, GroupHashValue, LocalIp, Prefix, 0, 0, 0, 0,
                    flags, name);
            }

            /// <summary>Binds the port and starts beaconing. False = the port was not free (StartError says so).</summary>
            public bool Start()
            {
                if (string.IsNullOrEmpty(Title))
                    return false;
                try
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    _socket.ExclusiveAddressUse = true;
                    _socket.EnableBroadcast = true;
                    _socket.Bind(new IPEndPoint(LocalIp.Equals(IPAddress.Any) ? IPAddress.Any : LocalIp, PortUsed));
                    try
                    {
                        // SIO_UDP_CONNRESET off: an ICMP port-unreachable from a target with no listener must not fail
                        // the next receive on a broadcast socket
                        _socket.IOControl(unchecked((int)0x9800000C), new byte[] { 0, 0, 0, 0 }, null);
                    }
                    catch (Exception)
                    {
                        // not supported on this stack: the receive loop re-arms itself instead
                    }
                }
                catch (Exception ex)
                {
                    StartError = ex.Message;
                    Close();
                    return false;
                }
                _targets.Add(new IPEndPoint(IPAddress.Broadcast, PortUsed));
                if (Prefix > 0 && Prefix < 32 && !LocalIp.Equals(IPAddress.Any))
                {
                    var b = LocalIp.GetAddressBytes();
                    uint u = (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
                    uint mask = ~(uint.MaxValue >> Prefix);
                    uint dir = (u & mask) | ~mask;
                    _targets.Add(new IPEndPoint(new IPAddress(new[]
                    {
                        (byte)(dir >> 24), (byte)(dir >> 16), (byte)(dir >> 8), (byte)dir,
                    }), PortUsed));
                }
                foreach (var p in _unicast)
                    _targets.Add(new IPEndPoint(p, PortUsed));
                BeginReceive();
                _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, BeaconInterval);
                return true;
            }

            private void Tick()
            {
                try
                {
                    SendAll(_beacon);
                    bool changed = false;
                    var now = DateTime.UtcNow;
                    lock (_lock)
                    {
                        foreach (var key in _peers.Where(p => now - p.Value.SeenUtc > PeerTtl).Select(p => p.Key).ToList())
                        {
                            _peers.Remove(key);
                            changed = true;
                        }
                    }
                    if (changed)
                        PeersChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitialDLan: beacon: {ex.Message}");
                }
            }

            private void SendAll(byte[] data)
            {
                if (_socket == null)
                    return;
                foreach (var t in _targets)
                {
                    try
                    {
                        _socket.SendTo(data, t);
                    }
                    catch (Exception)
                    {
                        // a broadcast an adapter refuses is not an error worth a line every second
                    }
                }
            }

            /// <summary>"Start together": the START message to these peers (then the caller starts its own game).</summary>
            public void SendStart(IEnumerable<Peer> peers)
            {
                var msg = EncodeStart(Title, _install, _boot);
                foreach (var p in peers ?? Enumerable.Empty<Peer>())
                {
                    if (p?.Address == null)
                        continue;
                    try
                    {
                        _socket?.SendTo(msg, new IPEndPoint(p.Address, PortUsed));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"InitialDLan: start to {p.Address}: {ex.Message}");
                    }
                }
            }

            public List<Peer> Peers()
            {
                var now = DateTime.UtcNow;
                lock (_lock)
                    return _peers.Values.Where(p => now - p.SeenUtc <= PeerTtl).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
            }

            private void BeginReceive()
            {
                var buf = new byte[MaxDatagram];
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    _socket.BeginReceiveFrom(buf, 0, buf.Length, SocketFlags.None, ref from, ar =>
                    {
                        int n;
                        EndPoint peer = new IPEndPoint(IPAddress.Any, 0);
                        try
                        {
                            n = _socket.EndReceiveFrom(ar, ref peer);
                        }
                        catch (Exception)
                        {
                            if (!_stopped)
                                BeginReceive();   // a reset from a target with no listener: keep listening
                            return;
                        }
                        try
                        {
                            Handle(buf, n, (peer as IPEndPoint)?.Address);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"InitialDLan: receive: {ex.Message}");
                        }
                        if (!_stopped)
                            BeginReceive();
                    }, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitialDLan: receive start: {ex.Message}");
                }
            }

            /// <summary>One datagram, after the rules of LAN_AGENT.md 6. Public so the tests can drive it.</summary>
            public void Handle(byte[] data, int length, IPAddress from)
            {
                if (from == null || !RateOk(from))
                    return;
                var m = Decode(data, length);
                if (m == null || m.Type == MessageType.Other)
                    return;
                if (!TrustedSource(from))
                    return;
                Peer peer = null;
                bool changed = false;
                if (m.Type == MessageType.Beacon)
                {
                    if (m.InstallId == Hex(_install, 0, 16) || m.Title != Title || m.GroupHash != GroupHashValue)
                        return;
                    lock (_lock)
                    {
                        if (!_peers.TryGetValue(m.InstallId, out peer))
                        {
                            if (_peers.Count >= MaxPeers)
                                return;
                            peer = new Peer { InstallId = m.InstallId };
                            _peers[m.InstallId] = peer;
                            changed = true;
                        }
                        if (peer.Name != m.Name || peer.Ready != m.Ready || !Equals(peer.Address, from) ||
                            peer.Role != m.Role || peer.LinkOff != m.LinkOff)
                            changed = true;
                        peer.BootId = m.BootId;
                        peer.Name = m.Name.Length > 0 ? m.Name : peer.Name;
                        peer.Address = from;
                        peer.Role = m.Role;
                        peer.Ready = m.Ready;
                        peer.LinkAlways = m.LinkAlways;
                        peer.LinkOff = m.LinkOff;
                        peer.SeenUtc = DateTime.UtcNow;
                        peer.Consent = ConsentOf(m.InstallId);
                    }
                    if (changed)
                        PeersChanged?.Invoke();
                    return;
                }
                if (m.Type == MessageType.Bye)
                {
                    lock (_lock)
                        changed = _peers.Remove(m.InstallId);
                    if (changed)
                        PeersChanged?.Invoke();
                    return;
                }
                // START: only from a live peer of this title, and only when the link is consented (LAN_AGENT.md 5)
                if (m.Title != Title)
                    return;
                lock (_lock)
                {
                    if (!_peers.TryGetValue(m.InstallId, out peer) || peer.BootId != m.BootId ||
                        DateTime.UtcNow - peer.SeenUtc > PeerTtl || peer.Role != 0)
                        return;
                    peer.Consent = ConsentOf(m.InstallId);
                }
                if (peer.Consent == Consent.Never)
                    return;
                if (peer.Consent == Consent.Always || (Group.Length != 0 && peer.LinkAlways))
                    StartRequested?.Invoke(peer);
                else
                    LinkAsked?.Invoke(peer);
            }

            private bool RateOk(IPAddress from)
            {
                var key = from.ToString();
                int second = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond);
                lock (_lock)
                {
                    if (second != _rateSecond)
                    {
                        _rateSecond = second;
                        _rate.Clear();
                    }
                    if (!_rate.TryGetValue(key, out var n))
                    {
                        if (_rate.Count >= 256)
                            return false;
                        n = new int[1];
                        _rate[key] = n;
                    }
                    return ++n[0] <= MaxDatagramsPerSecondPerSource;
                }
            }

            /// <summary>LAN_AGENT.md 6 step 2: our own subnet, 169.254/16 on this adapter, or a LanPeers address.</summary>
            public bool TrustedSource(IPAddress from)
            {
                if (from == null || from.AddressFamily != AddressFamily.InterNetwork)
                    return false;
                if (_unicast.Any(p => p.Equals(from)))
                    return true;
                var b = from.GetAddressBytes();
                if (b[0] == 169 && b[1] == 254)
                    return true;
                return SameSubnet(LocalIp, from, Prefix);
            }

            public void Dispose()
            {
                if (_stopped)
                    return;
                _stopped = true;
                try
                {
                    _timer?.Dispose();
                }
                catch (Exception)
                {
                    // nothing to do
                }
                _timer = null;
                if (_socket != null)
                    SendAll(EncodeBye(_install, _boot));
                Close();
            }

            private void Close()
            {
                try
                {
                    _socket?.Close();
                }
                catch (Exception)
                {
                    // nothing to do
                }
                _socket = null;
            }
        }
    }
}
