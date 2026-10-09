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
            string Setting(string name, string fallback = "") =>
                profile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue ?? fallback;
            bool Enabled(string name, bool fallback = false)
            {
                var value = Setting(name, fallback ? "1" : "0");
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
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
            var chd = Setting("CHD Directory").Trim();
            var scale = Setting("Internal Resolution", "2");
            if (!int.TryParse(scale, out var scaleValue) || scaleValue < 1 || scaleValue > 8)
                throw new ArgumentException("Internal Resolution must be between 1 and 8.");
            var args = new List<string>();
            void Add(string key, string value) => args.Add(Quote("--" + key + "=" + value));
            void Flag(string key, bool value) => Add(key, value ? "true" : "false");
            Add("paths.rom", romRoot);
            Add("paths.chd", chd.Length == 0 ? romRoot : Resolve(chd));
            if (profile.ConfigValues?.Any(x => x.FieldName == "XInput Rumble") == true)
            {
                var rumble = Enabled("XInput Rumble");
                var wheel = Enabled("Wheel Force Feedback");
                if (rumble && wheel) throw new ArgumentException("Choose wheel force feedback or XInput rumble.");
                Add("force_feedback.backend", wheel ? "dinput" : rumble ? "xinput" : "none");
                Flag("machine.force_feedback_enabled", rumble || wheel);
                if (rumble || wheel)
                {
                    if (set != "batlgr2")
                        throw new ArgumentException("Force feedback requires Battle Gear 2 v2.04 and HANDLE TYPE AFSS in the game operator menu.");
                    var device = wheel ? Setting("Wheel FFB Device", "auto").Trim() : Setting("Rumble Controller", "auto");
                    if (wheel ? device != "auto" && !Guid.TryParse(device, out _) : !new[] { "auto", "0", "1", "2", "3" }.Contains(device))
                        throw new ArgumentException("Choose a wheel GUID/auto, or an XInput slot 0 to 3/auto.");
                    var gainName = wheel ? "Wheel FFB Gain" : "Rumble Gain";
                    if (!int.TryParse(Setting(gainName, "25"), out var gain) || gain < 0 || gain > 100)
                        throw new ArgumentException("Force feedback gain must be between 0 and 100 percent.");
                    Add("force_feedback.device", device);
                    Add("force_feedback.gain_percent", gain.ToString(CultureInfo.InvariantCulture));
                    Flag("force_feedback.invert", wheel && Enabled("Invert Wheel Torque"));
                    var spring = wheel ? Setting("Wheel Spring Mode", "spring") : "spring";
                    if (spring != "spring" && spring != "constant") throw new ArgumentException("Unsupported spring mode.");
                    Add("force_feedback.spring_mode", spring);
                    foreach (var effect in new[] { "Constant", "Damper", "Spring", "Friction", "Vibration" })
                    {
                        if (!int.TryParse(Setting("FFB " + effect + " Gain", "100"), out var effectGain) || effectGain < 0 || effectGain > 100)
                            throw new ArgumentException("Effect gains must be between 0 and 100 percent.");
                        Add("force_feedback." + effect.ToLowerInvariant() + "_gain_percent", effectGain.ToString(CultureInfo.InvariantCulture));
                    }
                    if (!int.TryParse(Setting("FFB Vibration Unit", "1"), out var period) || period < 1 || period > 20)
                        throw new ArgumentException("FFB Vibration Unit must be between 1 and 20 milliseconds.");
                    Add("force_feedback.period_unit_ms", period.ToString(CultureInfo.InvariantCulture));
                    Add("video.frame_limit_hz", "60"); Flag("sound.load_check_fast_forward", false);
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
            var outputs = Enabled("Enable Cabinet Outputs");
            Flag("machine.outputs_enabled", outputs);
            Add("services.output.backend", outputs ? "cabinet" : "none");
            Add("services.output.route", CabinetOutputSettings.GetRoute(profile) == "Network Outputs" ? "network" : "windows");
            Add("services.output.bind_address", Setting("Output Bind Address", "127.0.0.1"));
            args.Add(Quote(set));
            log?.Invoke($"TeknoTZero: {set}, ROM root={romRoot}");
            return new ProcessStartInfo(Path.Combine(workDir, "TeknoTZero.exe"), string.Join(" ", args))
            {
                WorkingDirectory = workDir, UseShellExecute = false, RedirectStandardError = true
            };
        }
    }
}
