using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class TeknoTZeroLauncher
    {
        private static readonly Dictionary<string, string[]> Sets = new Dictionary<string, string[]>
        {
            { "batlgear_tz", new[] { "batlgear" } },
            { "batlgr2_tz", new[] { "batlgr2", "batlgr2a" } },
            { "landhigh_tz", new[] { "landhigh", "landhigha" } },
            { "pwrshovl_tz", new[] { "pwrshovl" } },
            { "dendego3_tz", new[] { "dendego3" } },
            { "styphp_tz", new[] { "styphp" } },
            { "raizpin_tz", new[] { "raizpin" } }
        };

        // MAME disk names per revision (Game CHD), with the MAME set folder.
        private static readonly Dictionary<string, string[]> Disks = new Dictionary<string, string[]>
        {
            { "batlgear", new[] { "batlgear", "batlgear.chd" } },
            { "batlgr2", new[] { "batlgr2", "bg2_204j.chd" } },
            { "batlgr2a", new[] { "batlgr2", "bg2_201j.chd" } },
            { "landhigh", new[] { "landhigh", "landhigh.chd" } },
            { "landhigha", new[] { "landhigh", "landhigha.chd" } },
            { "pwrshovl", new[] { "pwrshovl", "pwrshovl.chd" } },
            { "dendego3", new[] { "dendego3", "ddg3.chd" } },
            { "styphp", new[] { "styphp", "styphp.chd" } },
            { "raizpin", new[] { "raizpin", "raizpin.chd" } }
        };

        internal static string DiskFile(string set) =>
            Disks.TryGetValue(set ?? "", out var disk) ? disk[1] : null;

        private static string SelectedRevision(GameProfile profile)
        {
            if (!Sets.TryGetValue(profile.ProfileName ?? "", out var revisions)) return null;
            var set = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == "Game Version")?.FieldValue ?? revisions[0];
            return revisions.Contains(set) ? set : revisions[0];
        }

        // Finds the selected revision's CHD next to the ROM: MAME layout
        // (<rom folder>/<set>/<disk>.chd) or the ROM folder itself.
        internal static string FindChd(GameProfile profile, string romPath = null)
        {
            var set = SelectedRevision(profile);
            if (set == null || !Disks.TryGetValue(set, out var disk)) return null;
            romPath = string.IsNullOrWhiteSpace(romPath) ? profile.GamePath : romPath;
            if (string.IsNullOrWhiteSpace(romPath)) return null;
            try
            {
                var full = Path.GetFullPath(Path.IsPathRooted(romPath) ? romPath : Path.Combine(Directory.GetCurrentDirectory(), romPath));
                var folder = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
                foreach (var candidate in new[] { Path.Combine(folder, disk[0], disk[1]), Path.Combine(folder, disk[1]) })
                    if (File.Exists(candidate)) return candidate;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
            }
            return null;
        }

        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var c in value ?? string.Empty)
            {
                if (c == '\\') { ++slashes; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        public static ProcessStartInfo Build(GameProfile profile, string gameLocation, Action<string> log)
        {
            bool HasSetting(string name) =>
                profile.ConfigValues?.Any(x => x.FieldName == name) == true;
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            int Percent(string name, int minimum = 0, int maximum = 100, int fallback = 100)
            {
                if (!int.TryParse(Setting(name, fallback.ToString(CultureInfo.InvariantCulture)),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                    value < minimum || value > maximum)
                    value = fallback;
                return value;
            }
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            if (!Sets.TryGetValue(profile.ProfileName ?? "", out var revisions))
                throw new ArgumentException("Unknown Type Zero game profile.");
            var set = Setting("Game Version", revisions[0]);
            if (!revisions.Contains(set)) throw new ArgumentException("Unsupported Type Zero game version.");
            var uiRoot = Directory.GetCurrentDirectory();
            var workDir = Path.Combine(uiRoot, "TeknoTZero");
            string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(uiRoot, path));
            var selected = string.IsNullOrWhiteSpace(gameLocation) ? profile.GamePath : gameLocation;
            if (string.IsNullOrWhiteSpace(selected))
                throw new ArgumentException("Select the game's ROM ZIP in Game Settings.");
            var rom = Resolve(selected);
            var romRoot = Directory.Exists(rom) ? rom : Path.GetDirectoryName(rom);
            // Game CHD (second game path), as Cart Fury. A CHD that belongs to
            // the other revision still works: the emulator then looks for the
            // selected revision's disk in the same folder.
            var chd = string.IsNullOrWhiteSpace(profile.GamePath2) ? FindChd(profile, rom) : Resolve(profile.GamePath2);
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var scaleValue) || scaleValue < 1 || scaleValue > 8)
                throw new ArgumentException("Internal Resolution must be between 1 and 8.");
            var args = new List<string>();
            void Add(string key, string value) => args.Add(Quote("--" + key + "=" + value));
            void Flag(string key, bool value) => Add(key, value ? "true" : "false");
            Add("paths.rom", romRoot);
            if (string.IsNullOrWhiteSpace(chd))
                Add("paths.chd", romRoot);
            else
            {
                if (!chd.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Select the game's .chd file as Game CHD.");
                Add("paths.disk", chd);
                Add("paths.chd", "");
            }
            // SDL3 force feedback through tzerohaptic device tokens, as TeknoModel3.
            var ffbDevice = HasSetting("Force Feedback Device")
                ? TeknoParrotUi.Helpers.TZeroFfbDeviceProbe.GetLaunchSelection(Setting("Force Feedback Device", "off"))
                : "off";
            // Power Shovel's second seat has its own device.
            var ffbDevice2 = HasSetting("Player 2 Force Feedback Device")
                ? TeknoParrotUi.Helpers.TZeroFfbDeviceProbe.GetLaunchSelection(Setting("Player 2 Force Feedback Device", "off"))
                : "off";
            if (!ffbDevice2.Equals("off", StringComparison.OrdinalIgnoreCase) && ffbDevice2 == ffbDevice)
                throw new ArgumentException("Choose a different force feedback device for each player.");
            Add("force_feedback.device", ffbDevice);
            Add("force_feedback.device2", ffbDevice2);
            if (!ffbDevice.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                !ffbDevice2.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                Add("force_feedback.gain", Number(Percent("Force Feedback Strength")));
                if (HasSetting("Enable Spring Effect"))
                {
                    var springMode = Setting("Force Feedback Spring Effect", "Spring");
                    Add("force_feedback.spring_mode",
                        springMode == "Spring using Constant Force" || springMode == "Constant Spring" ? "constant" : "spring");
                    Add("force_feedback.spring_gain", Number(
                        Enabled("Enable Spring Effect", true) ? Percent("Spring Effect Strength") : 0));
                }
                if (HasSetting("Enable Constant Effect"))
                    Add("force_feedback.constant_gain", Number(
                        Enabled("Enable Constant Effect", true) ? Percent("Constant Effect Strength") : 0));
                if (HasSetting("Enable Damper Effect"))
                    Add("force_feedback.damper_gain", Number(
                        Enabled("Enable Damper Effect", true) ? Percent("Damper Effect Strength") : 0));
                if (HasSetting("Enable Friction Effect"))
                    Add("force_feedback.friction_gain", Number(
                        Enabled("Enable Friction Effect", true) ? Percent("Friction Effect Strength", 0, 100, 30) : 0));
                if (HasSetting("Enable Sine Effect"))
                {
                    Add("force_feedback.vibration_gain", Number(
                        Enabled("Enable Sine Effect", true) ? Percent("Sine Effect Strength") : 0));
                    Add("force_feedback.sine_period_ms", Number(Percent("Sine Effect Period", 10, 200, 40)));
                }
            }
            var handoff = Environment.GetEnvironmentVariable("TP_TPONLINE2");
            var roomHandoff = !string.IsNullOrWhiteSpace(handoff);
            var localTpo = !roomHandoff && Enabled("Local TPO");
            var online = roomHandoff || localTpo;
            var onlineNode = 0;
            if (roomHandoff)
            {
                var fields = handoff.Split('|');
                bool ValidText(string value, int maximum) => value.Length != 0 &&
                    Encoding.UTF8.GetByteCount(value) <= maximum && !value.Any(char.IsControl);
                if (!new[] { "batlgear_tz", "batlgr2_tz", "pwrshovl_tz", "raizpin_tz" }.Contains(profile.ProfileName) ||
                    fields.Length != 4 || !ValidText(fields[0], 127) || !ValidText(fields[2], 63) ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out onlineNode) ||
                    onlineNode < 0 || onlineNode > 1 || fields[3] != "2")
                    throw new ArgumentException("Type Zero TPOnline requires a supported multiplayer game with exactly two players (IDs 0 and 1).");
                if (set == "pwrshovl" || set == "raizpin")
                    log?.Invoke("TPOnline: two-player mode is locked on. Use your Player 1 controls; the joining player is assigned to Player 2.");
            }
            if (localTpo)
            {
                if (set != "pwrshovl" && set != "raizpin")
                    throw new ArgumentException("Local TPO shared play requires Power Shovel or Raizin Ping Pong.");
                var server = Setting("Local TPO Server", "127.0.0.1").Trim();
                var lobby = Setting("Local TPO Room", "TypeZero-local").Trim();
                var name = Setting("Local TPO Player Name", "Player").Trim();
                bool ValidLocalText(string value, int max) => !string.IsNullOrEmpty(value) &&
                    Encoding.UTF8.GetByteCount(value) <= max && !value.Any(c => char.IsControl(c) || c == '|');
                if (!ValidLocalText(lobby, 127) || !ValidLocalText(name, 63) ||
                    (Uri.CheckHostName(server) != UriHostNameType.Dns && Uri.CheckHostName(server) != UriHostNameType.IPv4) ||
                    !int.TryParse(Setting("Local TPO Port", "7788"), NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port < 1 || port > 65535 ||
                    !int.TryParse(Setting("Local TPO Player", "0"), NumberStyles.None, CultureInfo.InvariantCulture, out onlineNode) || onlineNode < 0 || onlineNode > 1)
                    throw new ArgumentException("Local TPO requires a broker host/port, room name, player name and player 0 or 1.");
                Add("link.tponline.server", server); Add("link.tponline.port", port.ToString(CultureInfo.InvariantCulture));
                Add("link.tponline.lobby", lobby); Add("link.tponline.player_name", name);
                log?.Invoke("Local TPO: use Player 1 controls. Player 0 hosts the shared cabinet; player 1 joins as Player 2. Rollback is automatic.");
            }
            // Service rooms always use the production endpoint, even if a local
            // broker was previously selected in the Local TPO settings.
            if (roomHandoff) { Add("link.tponline.server", "164.132.246.154"); Add("link.tponline.port", "7788"); }
            var link = !online && Enabled("UDP Cabinet Link");
            Add("link.mode", online ? "tponline" : link ? "udp" : "off");
            if (!link) Add("link.endpoints", "");
            if (!link && !online) Add("link.node", "0");
            var saveRoot = Setting("Save Directory").Trim();
            if (online)
            {
                Add("link.node", onlineNode.ToString(CultureInfo.InvariantCulture));
                Add("video.frame_limit_hz", "60"); Flag("sound.load_check_fast_forward", false);
                if (saveRoot.Length == 0) saveRoot = Path.Combine(workDir, "nvram", "link-" + onlineNode);
            }
            if (link)
            {
                var endpoints = Setting("UDP Endpoints").Trim();
                var group = new List<string>();
                var groupPorts = new List<int>();
                if (endpoints.Length != 0)
                {
                    foreach (var entry in endpoints.Split(','))
                    {
                        var parts = entry.Trim().Split(':');
                        var octets = parts[0].Split('.');
                        if (parts.Length != 2 || octets.Length != 4 ||
                            octets.Any(x => !byte.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
                                (x.Length > 1 && x[0] == '0')) ||
                            !IPAddress.TryParse(parts[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
                            address.Equals(IPAddress.Any) || address.GetAddressBytes()[0] >= 224 ||
                            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port < 1 || port > 65535)
                            throw new ArgumentException("UDP Endpoints requires two to four unicast IPv4:port entries in cabinet order.");
                        var canonical = address + ":" + port.ToString(CultureInfo.InvariantCulture);
                        if (group.Contains(canonical)) throw new ArgumentException("UDP Endpoints contains a duplicate endpoint.");
                        group.Add(canonical); groupPorts.Add(port);
                    }
                    if (group.Count < 2 || group.Count > 4)
                        throw new ArgumentException("UDP Endpoints requires two to four cabinets.");
                }
                if (!int.TryParse(Setting("UDP Node", "0"), out var node) || node < 0 || node >= (group.Count == 0 ? 2 : group.Count))
                    throw new ArgumentException("UDP Node must select a configured cabinet. Nodes 2 and 3 require UDP Endpoints.");
                string Address(string setting, bool peer)
                {
                    var text = Setting(setting, "127.0.0.1").Trim();
                    if (!IPAddress.TryParse(text, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
                        (peer && address.Equals(IPAddress.Any)))
                        throw new ArgumentException(setting + " must be an IPv4 address" + (peer ? " other than 0.0.0.0." : "."));
                    return address.ToString();
                }
                string Port(string setting, string fallback)
                {
                    if (!int.TryParse(Setting(setting, fallback), out var port) || port < 1 || port > 65535)
                        throw new ArgumentException(setting + " must be between 1 and 65535.");
                    return port.ToString(CultureInfo.InvariantCulture);
                }
                var localAddress = Address("UDP Bind Address", false);
                var peerAddress = Address("UDP Peer Address", true);
                var localPort = Port("UDP Local Port", "19792");
                var peerPort = Port("UDP Peer Port", "19793");
                if (group.Count == 0 && localAddress == peerAddress && localPort == peerPort)
                    throw new ArgumentException("UDP local and peer endpoints must differ.");
                if (group.Count != 0 && localPort != groupPorts[node].ToString(CultureInfo.InvariantCulture))
                    throw new ArgumentException("UDP Local Port must match this node's entry in UDP Endpoints.");
                Add("link.endpoints", string.Join(",", group));
                Add("link.node", node.ToString(CultureInfo.InvariantCulture));
                Add("link.address", localAddress); Add("link.port", localPort);
                Add("link.peer_address", peerAddress); Add("link.peer_port", peerPort);
                Add("video.frame_limit_hz", "60"); Flag("sound.load_check_fast_forward", false);
                if (saveRoot.Length == 0) saveRoot = Path.Combine(workDir, "nvram", "link-" + node);
            }
            // Linked local instances must not share writable cabinet state.
            // The emulator appends the selected revision to this root.
            Add("paths.nvram", saveRoot.Length == 0 ? Path.Combine(workDir, "nvram") : Resolve(saveRoot));
            Add("paths.calibration", saveRoot.Length == 0 ? Path.Combine(workDir, "nvram") : Resolve(saveRoot));
            Add("paths.snapshot", Path.Combine(workDir, "snapshots"));
            var stateRoot = Setting("State Directory").Trim();
            Add("paths.state", stateRoot.Length == 0 ? Path.Combine(workDir, "state") : Resolve(stateRoot));
            if (!int.TryParse(Setting("State Slot", "0"), out var stateSlot) || stateSlot < 0 || stateSlot > 9)
                throw new ArgumentException("State Slot must be between 0 and 9.");
            Add("state.slot", stateSlot.ToString(CultureInfo.InvariantCulture));
            // Ignore saved renderer choices from pre-Vulkan-only profiles.
            Add("video.backend", "vulkan");
            Add("video.vulkan.internal_scale", scaleValue.ToString(CultureInfo.InvariantCulture));
            Flag("video.fullscreen", Setting("DisplayMode", "Fullscreen") == "Fullscreen");
            Flag("video.vsync", Enabled("VSync", true));
            Flag("video.vulkan.scene_cache", Enabled("Reuse Unchanged Scenes"));
            Flag("video.vulkan.presentation_pacing", Enabled("Pace Completed Frames"));
            if (!int.TryParse(Setting("Geometry Threads", "0"), out var geometryThreads) || geometryThreads < 0 || geometryThreads > 64)
                throw new ArgumentException("Geometry Threads must be 0 (automatic) or 1 to 64.");
            Add("machine.geometry_threads", geometryThreads.ToString(CultureInfo.InvariantCulture));
            Flag("video.antialiasing", Enabled("Antialiasing"));
            var filter = Setting("Presentation Filter", "linear");
            if (!new[] { "nearest", "linear", "bicubic", "lanczos", "ssaa" }.Contains(filter))
                throw new ArgumentException("Unsupported presentation filter.");
            Add("video.presentation_filter", filter);
            Flag("video.crt_shader", Enabled("CRT Shader"));
            var crtMode = Setting("CRT Mode", "custom");
            if (!new[] { "custom", "lottes", "lottes-downsample" }.Contains(crtMode))
                throw new ArgumentException("Unsupported CRT mode.");
            Add("video.crt_mode", crtMode);
            Flag("video.preserve_aspect_ratio", !Enabled("Stretch to Fullscreen"));
            var widescreen = Setting("Widescreen", "off");
            if (!new[] { "off", "16:9", "16:10", "21:9", "32:9" }.Contains(widescreen))
                throw new ArgumentException("Unsupported widescreen aspect ratio.");
            var playerView = Setting("Player View", "shared");
            if (localTpo)
            {
                var view = Setting("Local TPO View", "Full Split Screen");
                if (view != "Full Split Screen" && view != "Own Widescreen View (16:9)")
                    throw new ArgumentException("Unsupported Local TPO view.");
                playerView = view == "Full Split Screen" ? "shared" : "personal";
                if (playerView == "personal") widescreen = "16:9";
            }
            Add("video.widescreen", widescreen);
            if (playerView != "shared" && playerView != "personal")
                throw new ArgumentException("Player View must be shared or personal.");
            if (playerView == "personal" && (!online || (set != "pwrshovl" && set != "raizpin") ||
                widescreen == "off" || Enabled("Enable VR") || Enabled("Side-by-Side 3D")))
                throw new ArgumentException("Personal player view requires a Power Shovel or Raizin TPOnline room, widescreen and no stereo/VR.");
            Add("video.player_view", playerView);
            Flag("video.expand_viewport", Enabled("Fill Scanout Margins", true));
            Flag("video.show_fps", Enabled("Show FPS"));
            Flag("video.show_frame_number", false);
            Flag("video.secondary_display", Enabled("Secondary LCD", true));
            var bezel = Setting("Bezel Image").Trim();
            Flag("video.bezel_enabled", bezel.Length != 0);
            if (bezel.Length != 0) Add("video.bezel_path", Resolve(bezel));
            Flag("video.openxr.enabled", Enabled("Enable VR"));
            Flag("video.stereo.sbs", Enabled("Side-by-Side 3D"));
            Flag("video.stereo.auto_convergence", Enabled("Auto 3D Depth"));
            string StereoNumber(string name, string fallback, double maximum)
            {
                if (!double.TryParse(Setting(name, fallback), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > maximum)
                    throw new ArgumentException(name + " must be between 0 and " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
                return value.ToString(CultureInfo.InvariantCulture);
            }
            Add("video.stereo.depth_percent", StereoNumber("3D Depth Percent", "0", 10));
            Add("video.stereo.near_fraction", StereoNumber("3D Near Limit", "0.5", 1));
            Flag("video.openxr.controllers", Enabled("Use VR Controls", true));
            Add("audio.backend", Enabled("Mute Audio") ? "none" : "wasapi");
            if (!int.TryParse(Setting("Volume", "100"), out var volume) || volume < 0 || volume > 100)
                throw new ArgumentException("Volume must be between 0 and 100.");
            Add("audio.volume_percent", volume.ToString(CultureInfo.InvariantCulture));
            var pauseKey = Lazydata.ParrotData?.PauseGameKey;
            if (!string.IsNullOrWhiteSpace(pauseKey) && int.TryParse(pauseKey.Trim().Replace("0x", "").Replace("0X", ""),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pause) && pause >= 0 && pause <= 255)
                Add("host.pause_key", pause.ToString(CultureInfo.InvariantCulture));
            // Shared input owns all controls. Native bindings must not leak into a revoked lease.
            foreach (var device in new[] { "keyboard", "mouse", "lightgun", "joystick" })
                Add("input." + device + ".provider", "none");
            // Cabinet outputs are always published, as TeknoModel3. The route,
            // port and discovery settings arrive through TP_OUTPUT_* variables.
            Flag("machine.outputs_enabled", true);
            Add("services.output.backend", "cabinet");
            Add("services.output.route", CabinetOutputSettings.GetRoute(profile) == "Network Outputs" ? "network" : "windows");
            args.Add(Quote(set));
            log?.Invoke($"TeknoTZero: {set}, ROM root={romRoot}");
            return new ProcessStartInfo(Path.Combine(workDir, "TeknoTZero.exe"), string.Join(" ", args))
            {
                WorkingDirectory = workDir, UseShellExecute = false, RedirectStandardError = true
            };
        }
    }
}
