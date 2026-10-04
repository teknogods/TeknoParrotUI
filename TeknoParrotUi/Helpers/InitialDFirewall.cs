using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Initial D: the inbound firewall rules a linked LAN launch needs (InitialDServer docs/UNIFIED_MODE.md 3.8,
    /// LAN_AGENT.md 8; phase P2b). The in-store link has no relay fallback, and a Defender prompt would open behind the
    /// full-screen game, so TeknoParrotUI offers the rules once, before the first launch that could link.
    /// <list type="bullet">
    /// <item>UDP 13390 (the LAN agent) for TeknoParrotUI itself and for the game executable;</item>
    /// <item>the title's own inbound UDP ports (the IDSR game port, the in-store VS control and race ports, live cast);</item>
    /// <item>profile <c>private</c> only - <c>private,public</c> only when the user set a LAN group or LAN peers;</item>
    /// <item>one elevated step (UAC) that the user starts by pressing the button; nothing is added otherwise.</item>
    /// </list>
    /// </summary>
    public static class InitialDFirewall
    {
        public const string RulePrefix = "TeknoParrot Initial D LAN";

        /// <summary>One inbound rule: a name, the program it belongs to, and its UDP ports.</summary>
        public sealed class Rule
        {
            public string Name { get; set; } = "";
            public string Program { get; set; } = "";
            public IReadOnlyList<int> Ports { get; set; } = new int[0];
            public bool Public { get; set; }

            public string PortList => string.Join(",", Ports.Select(p => p.ToString()));
            public string Profiles => Public ? "private,public" : "private";
        }

        /// <summary>
        /// The title's own inbound UDP ports (UNIFIED_MODE.md 3.8; TP_CLIENT.md 13.3 / 14.2 for the per-title IDSR and
        /// store ports). Empty for a profile that is not one of the five matchmaking titles.
        /// </summary>
        /// <summary>
        /// TPLR, the LAN realm discovery port of UNIFIED_MODE.md 3.9 (P5). The game only ever SENDS a query from it
        /// (and only with the hidden [Network] LanRealm=1), but the answer comes back from another PC's server, so the
        /// inbound rule has to name it. An operator who moved the port with [LanRealm] port / [Network] LanRealmPort
        /// adds that one by hand.
        /// </summary>
        public const int LanRealmPort = 13391;

        public static IReadOnlyList<int> GamePorts(GameProfile profile)
        {
            switch (InitialDUnifiedMode.ProfileKey(profile))
            {
                // Lindbergh: IDSR game port, in-store VS control, the in-store race pair, live cast, the LAN realm
                // discovery answer (UNIFIED_MODE.md 3.9, P5: the game's query socket takes 13391 when it is free, so
                // a bundled InitialDServer on another PC of the LAN can answer it)
                case "ID4JapElf2":
                case "ID5Elf2":
                    return new[] { 13346, 12222, 13349, 13350, 19999, LanRealmPort };
                // RingEdge: the IDSR game port of the title, VS control, the TAG partner port, the race pair, live cast
                case "ID6":
                    return new[] { 13456, 12222, 12223, 13349, 31349, 32349, 19999, LanRealmPort };
                case "ID7":
                    return new[] { 32457, 12222, 12223, 13349, 31349, 32349, 19999, LanRealmPort };
                case "ID8":
                    return new[] { 51457, 12222, 12223, 13349, 31349, 32349, 19999, LanRealmPort };
                default:
                    return new int[0];
            }
        }

        /// <summary>
        /// The rules for one title: the agent port for TeknoParrotUI and for the game, and the game's own ports.
        /// <paramref name="uiPath"/> defaults to this process's executable.
        /// </summary>
        public static List<Rule> Rules(GameProfile profile, string uiPath = null)
        {
            var rules = new List<Rule>();
            var ports = GamePorts(profile);
            if (ports.Count == 0)
                return rules;
            var allowPublic = InitialDLanPresence.LanGroup(profile).Length != 0 ||
                              InitialDLanPresence.LanPeers(profile).Count != 0;
            var ui = string.IsNullOrEmpty(uiPath) ? ThisProgram() : uiPath;
            var game = GameExecutable(profile);
            var title = (profile?.GameNameInternal ?? InitialDUnifiedMode.ProfileKey(profile) ?? "").Replace("\"", "");
            if (!string.IsNullOrEmpty(ui))
                rules.Add(new Rule
                {
                    Name = RulePrefix + " (TeknoParrotUI)",
                    Program = ui,
                    Ports = new[] { InitialDLanPresence.Port },
                    Public = allowPublic,
                });
            if (!string.IsNullOrEmpty(game))
                rules.Add(new Rule
                {
                    Name = RulePrefix + " (" + title + ")",
                    Program = game,
                    Ports = new[] { InitialDLanPresence.Port }.Concat(ports).ToArray(),
                    Public = allowPublic,
                });
            return rules;
        }

        /// <summary>The netsh lines of a rule set: the old rule of that name goes first, so a re-run is clean.</summary>
        public static List<string> NetshCommands(IEnumerable<Rule> rules)
        {
            var cmds = new List<string>();
            foreach (var r in rules ?? Enumerable.Empty<Rule>())
            {
                cmds.Add($"netsh advfirewall firewall delete rule name=\"{r.Name}\" dir=in");
                cmds.Add($"netsh advfirewall firewall add rule name=\"{r.Name}\" dir=in action=allow protocol=UDP " +
                         $"localport={r.PortList} program=\"{r.Program}\" profile={r.Profiles} enable=yes");
            }
            return cmds;
        }

        /// <summary>What the dialog lists: one line per rule.</summary>
        public static List<string> Describe(IEnumerable<Rule> rules) =>
            (rules ?? Enumerable.Empty<Rule>())
            .Select(r => $"UDP {r.PortList}  {Path.GetFileName(r.Program)}  ({r.Profiles})").ToList();

        /// <summary>
        /// True when this launch should be offered the rules: a matchmaking title whose rules were never offered. The
        /// answer is kept in ParrotData, so the question is asked once (the user can repeat it from the dialog's note).
        /// </summary>
        public static bool ShouldOffer(GameProfile profile) =>
            InitialDUnifiedMode.IsMatchmakingProfile(profile) && GamePorts(profile).Count > 0 &&
            !Lazydata.ParrotData.InitialDLanFirewallAsked;

        public static void MarkOffered()
        {
            Lazydata.ParrotData.InitialDLanFirewallAsked = true;
            try
            {
                JoystickHelper.Serialize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDFirewall: cannot save the answer: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs the netsh lines in one elevated step (UAC). True = the step ran and every line succeeded. Never called
        /// unless the user pressed the button; the tests replace it through <see cref="Seams.Run"/>.
        /// </summary>
        public static bool Apply(IEnumerable<Rule> rules)
        {
            var cmds = NetshCommands(rules);
            if (cmds.Count == 0)
                return false;
            if (Seams.Run != null)
                return Seams.Run(cmds);
            string script = null;
            try
            {
                script = Path.Combine(Path.GetTempPath(), "tp_initiald_firewall_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".cmd");
                var sb = new StringBuilder("@echo off\r\n");
                foreach (var c in cmds)
                    sb.Append(c).Append("\r\n");
                sb.Append("exit /b 0\r\n");
                File.WriteAllText(script, sb.ToString(), Encoding.Default);
                var psi = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                        return false;
                    p.WaitForExit(60000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                // the user said no to UAC, or netsh is not available
                Debug.WriteLine($"InitialDFirewall: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (script != null && File.Exists(script))
                        File.Delete(script);
                }
                catch (Exception)
                {
                    // the temp file stays: harmless
                }
            }
        }

        private static string ThisProgram()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// The executable Windows sees for this profile: the loader that owns the game's sockets. The same choice
        /// Library.ValidateAndRun makes (loaderExe), so the rule matches the process that binds the ports.
        /// </summary>
        public static string GameExecutable(GameProfile profile)
        {
            if (profile == null)
                return "";
            string rel;
            var x64 = profile.Is64Bit;
            switch (profile.EmulatorType)
            {
                case EmulatorType.Lindbergh:
                    rel = @".\TeknoParrot\BudgieLoader.exe";
                    break;
                case EmulatorType.N2:
                    rel = @".\N2\BudgieLoader.exe";
                    break;
                case EmulatorType.ElfLdr2:
                    rel = x64 ? @".\ElfLdr2\x64\BudgieLoader_x64.exe" : @".\ElfLdr2\BudgieLoader.exe";
                    break;
                case EmulatorType.OpenParrotKonami:
                    rel = @".\OpenParrotWin32\OpenParrotKonamiLoader.exe";
                    break;
                default:
                    rel = x64 ? @".\OpenParrotx64\OpenParrotLoader64.exe" : @".\OpenParrotWin32\OpenParrotLoader.exe";
                    break;
            }
            try
            {
                return Path.GetFullPath(rel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDFirewall: loader path: {ex.Message}");
                return rel;
            }
        }

        /// <summary>Test seam, never set by TeknoParrotUI itself.</summary>
        internal static class Seams
        {
            internal static Func<List<string>, bool> Run { get; set; }
        }
    }
}
