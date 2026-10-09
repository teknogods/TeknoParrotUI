using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

internal static class Contracts
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string[] Args(string text)
    {
        var pointer = CommandLineToArgvW("test.exe " + text, out var count);
        try { return Enumerable.Range(1, count - 1).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size))).ToArray(); }
        finally { LocalFree(pointer); }
    }
    private static byte[] Page()
    {
        var result = new byte[64];
        for (var attempt = 0; attempt < 100; ++attempt)
        {
            var before = JvsHelper.StateView.ReadUInt32(8);
            Thread.MemoryBarrier();
            JvsHelper.StateView.ReadArray(0, result, 0, 64);
            Thread.MemoryBarrier();
            if ((before & 1) == 0 && before == JvsHelper.StateView.ReadUInt32(8)) return result;
        }
        throw new Exception("No coherent TTZIN publication");
    }
    private static void CheckPublication()
    {
        // Exercise the publication boundary without concurrently changing source
        // controller bindings: an accepted page must contain one complete payload.
        var publish = (Action<byte[]>)Delegate.CreateDelegate(typeof(Action<byte[]>),
            typeof(TeknoTZeroPipe).GetMethod("Publish", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        var first = Enumerable.Range(0, 64).Select(i => (byte)(i ^ 0x35)).ToArray();
        var second = Enumerable.Range(0, 64).Select(i => (byte)(i ^ 0xca)).ToArray();
        foreach (var seed in new[] { 0u, 1u, uint.MaxValue - 1, uint.MaxValue })
        {
            JvsHelper.StateView.Write(8, seed);
            publish(first);
            Check(JvsHelper.StateView.ReadUInt32(8) == unchecked((seed & ~1u) + 2u), "Publication recovery or sequence wrap");
        }
        using (var started = new ManualResetEventSlim())
        using (var observed = new ManualResetEventSlim())
        {
            var done = 0;
            Exception writerError = null;
            var writer = new Thread(() =>
            {
                try
                {
                    publish(first); started.Set();
                    Check(observed.Wait(5000), "Publication reader did not start");
                    for (var i = 0; i < 50000; ++i) publish((i & 1) == 0 ? second : first);
                }
                catch (Exception error) { writerError = error; }
                finally { Volatile.Write(ref done, 1); started.Set(); }
            }) { IsBackground = true };
            writer.Start();
            var accepted = 0;
            try
            {
                Check(started.Wait(5000), "Publication writer did not start");
                var page = new byte[64];
                while (Volatile.Read(ref done) == 0)
                {
                    var before = JvsHelper.StateView.ReadUInt32(8);
                    if ((before & 1) != 0) continue;
                    Thread.MemoryBarrier();
                    JvsHelper.StateView.ReadArray(0, page, 0, 64);
                    Thread.MemoryBarrier();
                    if (before != JvsHelper.StateView.ReadUInt32(8)) continue;
                    var expected = page[0] == first[0] ? first : second;
                    for (var i = 0; i < 64; ++i)
                        if (i < 8 || i >= 12) Check(page[i] == expected[i], "Accepted a partially published input page");
                    ++accepted; observed.Set();
                }
            }
            finally
            {
                observed.Set();
                Check(writer.Join(10000), "Publication writer did not finish");
            }
            if (writerError != null) throw writerError;
            Check(accepted > 0, "No complete publications observed");
        }
        Console.WriteLine("Passed managed publication recovery, sequence wrap and 50,000 concurrent updates.");
    }
    private static void CheckOnlineRevisions(string repo)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var handoff = Environment.GetEnvironmentVariable("TP_TPONLINE2");
        var fixture = Path.GetFullPath(Path.Combine(repo, "bin", "typezero-contracts", "online-" + Guid.NewGuid()));
        Directory.CreateDirectory(Path.Combine(fixture, "GameProfiles"));
        Directory.CreateDirectory(Path.Combine(fixture, "UserProfiles"));
        Directory.CreateDirectory(Path.Combine(fixture, "Metadata"));
        try
        {
            foreach (var id in new[] { "batlgear_tz", "batlgr2_tz", "pwrshovl_tz", "raizpin_tz" })
            {
                File.Copy(Path.Combine(repo, "TeknoParrotUi.Common", "GameProfiles", id + ".xml"), Path.Combine(fixture, "GameProfiles", id + ".xml"));
                File.Copy(Path.Combine(repo, "TeknoParrotUi.Common", "Metadata", id + ".json"), Path.Combine(fixture, "Metadata", id + ".json"));
            }
            Directory.SetCurrentDirectory(fixture);
            Environment.SetEnvironmentVariable("TP_TPONLINE2", "revision test|0|Player one|2");
            foreach (var id in new[] { "batlgear_tz", "batlgr2_tz", "pwrshovl_tz", "raizpin_tz" })
            {
                var saved = OnlineGameRevisionProfiles.Load(id);
                saved.GamePath = "C:\\ROM library\\" + saved.ExecutableName;
                saved.ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue = "batlgr2a";
                saved.ConfigValues.Single(x => x.FieldName == "Internal Resolution").FieldValue = "8";
                saved.JoystickButtons[0].ButtonName = "Saved controller binding";
                saved.HasTpoSupport = false; // Existing user profiles predate the online flag.
                if (id == "batlgr2_tz") saved.ConfigValues.Single(x => x.FieldName == "XInput Rumble").FieldValue = "1";
                else saved.ConfigValues.RemoveAll(x => x.FieldName == "Game Version");
                using (var output = File.Create(Path.Combine("UserProfiles", id + ".xml")))
                    new XmlSerializer(typeof(GameProfile)).Serialize(output, saved);
            }
            foreach (var id in new[] { "batlgear_tz", "batlgr2_tz", "batlgr2a_tz", "pwrshovl_tz", "raizpin_tz", "pwrshovl_personal_tz", "raizpin_personal_tz" })
            {
                var parent = id == "batlgr2a_tz" ? "batlgr2_tz" : id.Replace("_personal_tz", "_tz");
                var userPath = Path.Combine("UserProfiles", parent + ".xml");
                var before = File.ReadAllBytes(userPath);
                var loaded = OnlineGameRevisionProfiles.Load(id);
                var set = id.Replace("_personal_tz", "_tz");
                set = set.Substring(0, set.Length - 3);
                Check(loaded.ProfileName == parent && loaded.HasTpoSupport && loaded.EmulatorType == EmulatorType.TeknoTZero,
                    "Online room did not resolve its parent profile");
                Check(loaded.ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue == set,
                    "Offline revision leaked into online session");
                Check(loaded.JoystickButtons[0].ButtonName == "Saved controller binding" && loaded.GamePath.StartsWith("C:\\ROM library\\") &&
                    !string.IsNullOrEmpty(loaded.GameInfo.game_name), "Saved controls/media or metadata lost");
                var launch = Args(TeknoTZeroLauncher.Build(loaded, null, null).Arguments);
                Check(launch.Last() == set && launch.Contains("--link.mode=tponline") &&
                    launch.Contains("--video.vulkan.internal_scale=8"), "Exact online revision/settings not passed to emulator");
                if (id == "batlgr2a_tz") Check(launch.Contains("--force_feedback.backend=none"), "Unsupported conversion rumble retained");
                var personal = id.Contains("_personal_tz");
                Check(launch.Contains("--video.player_view=" + (personal ? "personal" : "shared")), "Room view mode lost");
                if (personal)
                {
                    Check(launch.Contains("--video.widescreen=16:9") && launch.Contains("--video.openxr.enabled=false") &&
                        launch.Contains("--video.stereo.sbs=false"), "Personal room projection contract lost");
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", "revision test|1|Player two|2");
                    var joining = Args(TeknoTZeroLauncher.Build(loaded, null, null).Arguments);
                    Check(joining.Contains("--link.node=1") && joining.Contains("--video.player_view=personal"),
                        "Joining player lost assigned personal view");
                    Environment.SetEnvironmentVariable("TP_TPONLINE2", "revision test|0|Player one|2");
                }
                loaded.ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue = "changed";
                Check(File.ReadAllBytes(userPath).SequenceEqual(before) &&
                    OnlineGameRevisionProfiles.Load(id).ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue == set,
                    "Online launch changed the saved offline profile or leaked between sessions");
            }
            // A saved v2.04 wheel setup must also launch the older online revision,
            // without disabling force feedback in the user's offline profile.
            var wheelProfile = OnlineGameRevisionProfiles.Load("batlgr2_tz");
            wheelProfile.ConfigValues.Single(x => x.FieldName == "XInput Rumble").FieldValue = "0";
            wheelProfile.ConfigValues.Single(x => x.FieldName == "Wheel Force Feedback").FieldValue = "1";
            var wheelPath = Path.Combine("UserProfiles", "batlgr2_tz.xml");
            using (var output = File.Create(wheelPath)) new XmlSerializer(typeof(GameProfile)).Serialize(output, wheelProfile);
            var savedWheel = File.ReadAllBytes(wheelPath);
            foreach (var id in new[] { "batlgr2_tz", "batlgr2a_tz" })
            {
                var wheelArgs = Args(TeknoTZeroLauncher.Build(OnlineGameRevisionProfiles.Load(id), null, null).Arguments);
                Check(wheelArgs.Contains("--force_feedback.backend=" + (id == "batlgr2_tz" ? "dinput" : "none")),
                    "Online revision did not preserve or suppress the saved wheel correctly");
                Check(File.ReadAllBytes(wheelPath).SequenceEqual(savedWheel), "Online force-feedback override changed saved settings");
            }
            foreach (var id in new[] { "landhigh_tz", "dendego3_tz", "styphp_tz", "raizpinj_tz", "unknown_tz", "../batlgr2_tz", "batlgear_personal_tz", "styphp_personal_tz" })
            {
                var rejected = false;
                try { OnlineGameRevisionProfiles.Load(id); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Unsupported Type Zero online room accepted: " + id);
            }
            Console.WriteLine("Passed five exact Type Zero revisions and two personal view modes, saved settings isolation and exclusions.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TP_TPONLINE2", handoff);
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(fixture, true);
        }
    }
    public static int Main(string[] argv)
    {
        try
        {
            CheckOnlineRevisions(Path.GetFullPath(argv[0]));
            Check(Args(TeknoTZeroLauncher.Quote("C:\\a b\\")).Single() == "C:\\a b\\", "Trailing slash quote");
            Check(Args(TeknoTZeroLauncher.Quote("a\"b\\c")).Single() == "a\"b\\c", "Embedded quote");
            // Replace the helper's accessor before any publication. Never write the live cabinet page.
            JvsHelper.StateView.Dispose(); JvsHelper.StateSection.Dispose();
            JvsHelper.StateSection = MemoryMappedFile.CreateNew("TTZIN_UI_TEST_" + Guid.NewGuid(), 64);
            JvsHelper.StateView = JvsHelper.StateSection.CreateViewAccessor();
            CheckPublication();
            var count = 0;
            foreach (var file in Directory.GetFiles(Path.Combine(argv[0], "TeknoParrotUi.Common", "GameProfiles"), "*_tz.xml"))
            {
                GameProfile profile;
                using (var input = File.OpenRead(file)) profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(input);
                profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                profile.GamePath = "C:\\ROM library\\" + profile.ExecutableName;
                if (profile.ProfileName == "pwrshovl_tz")
                {
                    // These names select the existing four-stick keyboard dispatcher.
                    for (var player = 1; player <= 4; ++player)
                        foreach (var axis in new[] { "X", "Y" })
                            Check(profile.JoystickButtons.Any(b => b.ButtonName == $"Player {player} Joystick {axis}"), "Shovel axis not recognized by input listener");
                }
                var start = TeknoTZeroLauncher.Build(profile, null, null);
                var args = Args(start.Arguments);
                Check(args.Last() == profile.ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue &&
                    args.Take(args.Length - 1).All(x => x.StartsWith("--") && x.Contains("=")),
                    "Launch must contain only settings parameters and a game identifier");
                Check(profile.ConfigValues.Single(x => x.FieldName == "Volume").FieldStep == 1, "Volume UI can generate unsupported fractional values");
                Check(args.Contains("--audio.volume_percent=100"), "Default volume routing");
                Check(args.Contains("--video.stereo.sbs=false"), "Default stereo routing");
                Check(args.Contains("--video.widescreen=off"), "Default widescreen routing");
                Check(args.Contains("--video.vulkan.scene_cache=false") && args.Contains("--video.vulkan.presentation_pacing=false") &&
                    args.Contains("--machine.geometry_threads=0"), "Rendering defaults changed");
                var sceneReuse = profile.ConfigValues.Single(x => x.FieldName == "Reuse Unchanged Scenes");
                var completedPacing = profile.ConfigValues.Single(x => x.FieldName == "Pace Completed Frames");
                var geometry = profile.ConfigValues.Single(x => x.FieldName == "Geometry Threads");
                sceneReuse.FieldValue = completedPacing.FieldValue = "1"; geometry.FieldValue = "8";
                var renderingArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                Check(renderingArgs.Contains("--video.vulkan.scene_cache=true") && renderingArgs.Contains("--video.vulkan.presentation_pacing=true") &&
                    renderingArgs.Contains("--machine.geometry_threads=8"), "Rendering options lost");
                foreach (var invalid in new[] { "-1", "65", "8.5", "NaN" })
                {
                    geometry.FieldValue = invalid; var rejectedThreads = false;
                    try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedThreads = true; }
                    Check(rejectedThreads, "Invalid geometry thread count accepted");
                }
                sceneReuse.FieldValue = completedPacing.FieldValue = "0"; geometry.FieldValue = "0";
                if (profile.ProfileName == "batlgr2_tz")
                {
                    Check(args.Contains("--force_feedback.backend=none") && args.Contains("--machine.force_feedback_enabled=false"), "Rumble default must be off");
                    var rumble = profile.ConfigValues.Single(x => x.FieldName == "XInput Rumble");
                    var slot = profile.ConfigValues.Single(x => x.FieldName == "Rumble Controller");
                    var gain = profile.ConfigValues.Single(x => x.FieldName == "Rumble Gain");
                    Check(gain.FieldStep == 1, "Rumble slider generates unsupported fractional values");
                    rumble.FieldValue = "1";
                    foreach (var controller in new[] { "auto", "0", "1", "2", "3" })
                    {
                        Check(slot.FieldOptions.Contains(controller), "Missing rumble controller option");
                        slot.FieldValue = controller;
                        var rumbleArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                        Check(rumbleArgs.Contains("--force_feedback.backend=xinput") && rumbleArgs.Contains("--machine.force_feedback_enabled=true") &&
                            rumbleArgs.Contains("--force_feedback.device=" + controller) && rumbleArgs.Contains("--force_feedback.gain_percent=25") &&
                            rumbleArgs.Contains("--video.frame_limit_hz=60") && rumbleArgs.Contains("--sound.load_check_fast_forward=false"), "Rumble launch routing");
                    }
                    foreach (var invalid in new[] { new[] { "Rumble Controller", "4" }, new[] { "Rumble Controller", "-1" },
                        new[] { "Rumble Gain", "101" }, new[] { "Rumble Gain", "-1" }, new[] { "Game Version", "batlgr2a" } })
                    {
                        var setting = profile.ConfigValues.Single(x => x.FieldName == invalid[0]);
                        var original = setting.FieldValue; setting.FieldValue = invalid[1];
                        bool rejectedRumble = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedRumble = true; }
                        Check(rejectedRumble, "Invalid rumble setting accepted: " + invalid[0]);
                        setting.FieldValue = original;
                    }
                    rumble.FieldValue = "0"; slot.FieldValue = "auto";
                }
                Check(args.Contains("--video.expand_viewport=true"), "Default scanout-margin routing");
                Check(args.Contains("--video.secondary_display=true"), "Default secondary display routing");
                Check(args.Contains("--link.mode=off"), "UDP must be opt-in");
                if (profile.ProfileName == "pwrshovl_tz" || profile.ProfileName == "raizpin_tz")
                {
                    void SetLocal(string name, string value) => profile.ConfigValues.Single(x => x.FieldName == name).FieldValue = value;
                    SetLocal("Local TPO", "1"); SetLocal("Local TPO Server", "192.168.1.20");
                    foreach (var player in new[] { "0", "1" })
                        foreach (var view in new[] { "Full Split Screen", "Own Widescreen View (16:9)" })
                        {
                            SetLocal("Local TPO Player", player); SetLocal("Local TPO View", view);
                            var localArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                            Check(localArgs.Contains("--link.mode=tponline") && localArgs.Contains("--link.node=" + player) &&
                                localArgs.Contains("--link.tponline.server=192.168.1.20") && localArgs.Contains("--link.tponline.lobby=TypeZero-local") &&
                                localArgs.Contains("--video.player_view=" + (view.StartsWith("Own") ? "personal" : "shared")), "Local TPO room/view/seat lost");
                            if (view.StartsWith("Own")) Check(localArgs.Contains("--video.widescreen=16:9"), "Local personal view did not force 16:9");
                        }
                    foreach (var invalid in new[] { new[] { "Local TPO Player", "2" }, new[] { "Local TPO Port", "65536" },
                        new[] { "Local TPO Room", "bad|room" }, new[] { "Local TPO Server", "bad host" }, new[] { "Local TPO View", "bad view" } })
                    {
                        var field = profile.ConfigValues.Single(x => x.FieldName == invalid[0]); var old = field.FieldValue;
                        field.FieldValue = invalid[1]; var rejectedLocal = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedLocal = true; }
                        Check(rejectedLocal, "Invalid local TPO setting accepted"); field.FieldValue = old;
                    }
                    var oldHandoff = Environment.GetEnvironmentVariable("TP_TPONLINE2");
                    try
                    {
                        SetLocal("Local TPO Player", "99"); SetLocal("Local TPO View", "bad view");
                        Environment.SetEnvironmentVariable("TP_TPONLINE2", "service room|0|Online player|2");
                        var serviceArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                        Check(serviceArgs.Contains("--link.node=0") && serviceArgs.Contains("--link.tponline.server=164.132.246.154") &&
                            serviceArgs.Contains("--video.player_view=shared"), "Local settings leaked into service room");
                    }
                    finally { Environment.SetEnvironmentVariable("TP_TPONLINE2", oldHandoff); }
                    SetLocal("Local TPO", "0"); SetLocal("Local TPO Player", "0"); SetLocal("Local TPO View", "Full Split Screen");
                    Check(profile.ConfigValues.Single(x => x.FieldName == "Widescreen").FieldValue == "off", "Local room changed offline aspect preference");
                }
                var saveDirectory = profile.ConfigValues.Single(x => x.FieldName == "Save Directory");
                saveDirectory.FieldValue = "C:\\Cabinet saves\\first";
                Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--paths.nvram=C:\\Cabinet saves\\first"), "Save root quoting");
                saveDirectory.FieldValue = "";
                var stateSlot = profile.ConfigValues.Single(x => x.FieldName == "State Slot");
                var stateDirectory = profile.ConfigValues.Single(x => x.FieldName == "State Directory");
                stateSlot.FieldValue = "9"; stateDirectory.FieldValue = "C:\\State files\\first";
                var stateArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                Check(stateArgs.Contains("--state.slot=9") && stateArgs.Contains("--paths.state=C:\\State files\\first"), "State slot and directory routing");
                stateSlot.FieldValue = "10";
                var rejectedState = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedState = true; }
                Check(rejectedState, "Invalid state slot accepted");
                stateSlot.FieldValue = "0"; stateDirectory.FieldValue = "";
                if (profile.ProfileName == "batlgear_tz" || profile.ProfileName == "batlgr2_tz")
                {
                    var link = profile.ConfigValues.Single(x => x.FieldName == "UDP Cabinet Link");
                    var node = profile.ConfigValues.Single(x => x.FieldName == "UDP Node");
                    link.FieldValue = "1";
                    var firstCabinet = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(firstCabinet.Contains("--link.mode=udp") && firstCabinet.Contains("--link.node=0") &&
                        firstCabinet.Contains("--link.port=19792") && firstCabinet.Contains("--link.peer_port=19793") &&
                        firstCabinet.Contains("--sound.load_check_fast_forward=false") && firstCabinet.Contains("--video.frame_limit_hz=60"), "UDP endpoint and pacing routing");
                    node.FieldValue = "1";
                    var secondCabinet = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(firstCabinet.Single(x => x.StartsWith("--paths.nvram=")).EndsWith("\\link-0") &&
                        secondCabinet.Single(x => x.StartsWith("--paths.nvram=")).EndsWith("\\link-1"), "Linked cabinet saves overlap");
                    foreach (var invalid in new[] { new[] { "UDP Node", "2" }, new[] { "UDP Peer Address", "0.0.0.0" },
                        new[] { "UDP Bind Address", "::1" }, new[] { "UDP Local Port", "0" }, new[] { "UDP Peer Port", "65536" },
                        new[] { "UDP Peer Port", "19792" } })
                    {
                        var setting = profile.ConfigValues.Single(x => x.FieldName == invalid[0]);
                        var previous = setting.FieldValue; setting.FieldValue = invalid[1];
                        bool rejectedLink = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedLink = true; }
                        Check(rejectedLink, "Invalid UDP setting accepted: " + invalid[0]); setting.FieldValue = previous;
                    }
                    var oldHandoff = Environment.GetEnvironmentVariable("TP_TPONLINE2");
                    var endpoints = profile.ConfigValues.Single(x => x.FieldName == "UDP Endpoints");
                    var localPort = profile.ConfigValues.Single(x => x.FieldName == "UDP Local Port");
                    endpoints.FieldValue = "127.0.0.1:19792,127.0.0.1:19793,127.0.0.1:19794,127.0.0.1:19795";
                    node.FieldValue = "3"; localPort.FieldValue = "19795";
                    var fourthCabinet = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(fourthCabinet.Contains("--link.node=3") && fourthCabinet.Contains("--link.endpoints=" + endpoints.FieldValue) &&
                        fourthCabinet.Single(x => x.StartsWith("--paths.nvram=")).EndsWith("\\link-3"), "Four-cabinet UDP group routing and isolated saves");
                    link.FieldValue = "0";
                    var disabledGroup = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(disabledGroup.Contains("--link.mode=off") && disabledGroup.Contains("--link.endpoints=") &&
                        disabledGroup.Contains("--link.node=0"), "Disabled group must override a saved node 3 from earlier settings");
                    link.FieldValue = "1";
                    foreach (var invalid in new[] { "127.0.0.1:19792,", "127.0.0.1:19795,127.0.0.1:19795", "0.0.0.0:19792,127.0.0.1:19793",
                        "127.0.0.1:19792,127.0.0.1:19793,127.0.0.1:19794,224.0.0.1:19795" })
                    {
                        var saved = endpoints.FieldValue; endpoints.FieldValue = invalid;
                        var rejectedGroup = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedGroup = true; }
                        Check(rejectedGroup, "Invalid UDP group accepted"); endpoints.FieldValue = saved;
                    }
                    try
                    {
                        Environment.SetEnvironmentVariable("TP_TPONLINE2", "local test|1|Player two|2");
                        var online = TeknoTZeroLauncher.Build(profile, null, null);
                        var onlineArgs = Args(online.Arguments);
                        Check(onlineArgs.Contains("--link.mode=tponline") && !onlineArgs.Contains("--link.mode=udp") &&
                            onlineArgs.Contains("--link.node=1") && onlineArgs.Contains("--video.frame_limit_hz=60") &&
                            onlineArgs.Contains("--sound.load_check_fast_forward=false") &&
                            onlineArgs.Single(x => x.StartsWith("--paths.nvram=")).EndsWith("\\link-1") &&
                            online.EnvironmentVariables["TP_TPONLINE2"] == "local test|1|Player two|2", "TPOnline UDP handoff overrides direct endpoints");
                        foreach (var invalid in new[] { "bad", "q|0|p|3", "q|2|p|2", "q|-1|p|2", "q|0||2", "q|0|p|2|extra" })
                        {
                            Environment.SetEnvironmentVariable("TP_TPONLINE2", invalid);
                            bool rejectedOnline = false;
                            try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedOnline = true; }
                            Check(rejectedOnline, "Invalid TPOnline handoff accepted");
                        }
                    }
                    finally { Environment.SetEnvironmentVariable("TP_TPONLINE2", oldHandoff); }
                    node.FieldValue = "0"; link.FieldValue = "0"; endpoints.FieldValue = ""; localPort.FieldValue = "19792";
                }
                if (profile.ProfileName == "landhigh_tz")
                {
                    var lcd = profile.ConfigValues.Single(x => x.FieldName == "Secondary LCD");
                    lcd.FieldValue = "0";
                    Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.secondary_display=false"), "Secondary display override");
                    lcd.FieldValue = "1";
                }
                var margins = profile.ConfigValues.Single(x => x.FieldName == "Fill Scanout Margins");
                margins.FieldValue = "0";
                Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.expand_viewport=false"), "Scanout-margin override");
                margins.FieldValue = "1";
                var wide = profile.ConfigValues.Single(x => x.FieldName == "Widescreen");
                foreach (var mode in new[] { "off", "16:9", "16:10", "21:9", "32:9" })
                {
                    Check(wide.FieldOptions.Contains(mode), "Missing widescreen UI option");
                    wide.FieldValue = mode;
                    Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.widescreen=" + mode), "Widescreen routing");
                }
                wide.FieldValue = "invalid";
                bool badWide = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { badWide = true; }
                Check(badWide, "Invalid widescreen accepted"); wide.FieldValue = "off";
                if (profile.ProfileName == "batlgr2_tz")
                {
                    var wheel = profile.ConfigValues.Single(x => x.FieldName == "Wheel Force Feedback");
                    var rumble = profile.ConfigValues.Single(x => x.FieldName == "XInput Rumble");
                    var version = profile.ConfigValues.Single(x => x.FieldName == "Game Version");
                    var savedVersion = version.FieldValue;
                    version.FieldValue = "batlgr2"; rumble.FieldValue = "0"; wheel.FieldValue = "1";
                    profile.ConfigValues.Single(x => x.FieldName == "Wheel Spring Mode").FieldValue = "constant";
                    profile.ConfigValues.Single(x => x.FieldName == "FFB Spring Gain").FieldValue = "37";
                    var wheelArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(wheelArgs.Contains("--force_feedback.backend=dinput") && wheelArgs.Contains("--force_feedback.spring_mode=constant") &&
                        wheelArgs.Contains("--force_feedback.spring_gain_percent=37"), "Wheel settings routing");
                    rumble.FieldValue = "1";
                    bool conflict = false;
                    try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { conflict = true; }
                    Check(conflict, "Simultaneous wheel/rumble enabled");
                    rumble.FieldValue = "0";
                    foreach (var invalid in new[] { new[] { "Wheel FFB Device", "garbage" }, new[] { "Wheel FFB Gain", "101" },
                        new[] { "FFB Spring Gain", "-1" }, new[] { "Wheel Spring Mode", "bad" }, new[] { "FFB Vibration Unit", "0" },
                        new[] { "Game Version", "batlgr2a" } })
                    {
                        var field = profile.ConfigValues.Single(x => x.FieldName == invalid[0]);
                        var previous = field.FieldValue; field.FieldValue = invalid[1];
                        bool rejectedWheel = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedWheel = true; }
                        Check(rejectedWheel, "Invalid wheel settings accepted: " + invalid[0]); field.FieldValue = previous;
                    }
                    wheel.FieldValue = "0"; version.FieldValue = savedVersion;
                }
                Check(!profile.ConfigValues.Any(x => x.FieldName == "Renderer"), "Obsolete renderer selector");
                Check(args.Contains("--video.backend=vulkan"), "Vulkan renderer required");
                Check(!args.Any(x => x.StartsWith("--video.software.")), "Software settings emitted");
                var crtMode = profile.ConfigValues.Single(x => x.FieldName == "CRT Mode");
                foreach (var mode in new[] { "custom", "lottes", "lottes-downsample" })
                {
                    Check(crtMode.FieldOptions.Contains(mode), "Missing CRT mode");
                    crtMode.FieldValue = mode;
                    Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.crt_mode=" + mode), "CRT mode routing");
                }
                crtMode.FieldValue = "invalid";
                bool badCrt = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { badCrt = true; }
                Check(badCrt, "Invalid CRT mode accepted"); crtMode.FieldValue = "custom";
                var resolution = profile.ConfigValues.Single(x => x.FieldName == "Internal Resolution");
                for (var scale = 1; scale <= 8; ++scale)
                {
                    resolution.FieldValue = scale.ToString();
                    Check(resolution.FieldOptions.Contains(scale.ToString()), "Missing scale UI option");
                    Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.vulkan.internal_scale=" + scale), "Scale routing");
                }
                resolution.FieldValue = "9";
                bool badScale = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { badScale = true; }
                Check(badScale, "Invalid scale accepted"); resolution.FieldValue = "2";
                profile.ConfigValues.Single(x => x.FieldName == "Side-by-Side 3D").FieldValue = "1";
                Check(args.Contains("--video.stereo.auto_convergence=false"), "Automatic convergence defaults off");
                profile.ConfigValues.Single(x => x.FieldName == "Auto 3D Depth").FieldValue = "1";
                Check(Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.stereo.sbs=true") && Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments).Contains("--video.stereo.auto_convergence=true"), "Desktop stereo setting ignored");
                Check(args.Contains("--video.stereo.depth_percent=0") && args.Contains("--video.stereo.near_fraction=0.5"), "Relative depth defaults changed");
                var depthPercent = profile.ConfigValues.Single(x => x.FieldName == "3D Depth Percent");
                var nearLimit = profile.ConfigValues.Single(x => x.FieldName == "3D Near Limit");
                depthPercent.FieldValue = "1.8"; nearLimit.FieldValue = "0.25";
                var depthArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                Check(depthArgs.Contains("--video.stereo.depth_percent=1.8") && depthArgs.Contains("--video.stereo.near_fraction=0.25"), "Relative depth values lost");
                foreach (var field in new[] { depthPercent, nearLimit })
                {
                    foreach (var invalid in new[] { "NaN", "Infinity", "-0.1", "10.1", "abc" })
                    {
                        var saved = field.FieldValue; field.FieldValue = invalid; var rejectedDepth = false;
                        try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejectedDepth = true; }
                        Check(rejectedDepth, "Invalid relative depth accepted"); field.FieldValue = saved;
                    }
                }
                depthPercent.FieldValue = "0"; nearLimit.FieldValue = "0.5";
                Check(args.Contains("--paths.rom=C:\\ROM library"), "ROM directory quoting");
                Check(args.Contains("--input.keyboard.provider=none"), "Native controls remain enabled");
                Check(start.RedirectStandardError && !start.UseShellExecute, "Launcher diagnostics");
                var volume = profile.ConfigValues.Single(x => x.FieldName == "Volume");
                volume.FieldValue = "101";
                bool badVolume = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { badVolume = true; }
                Check(badVolume, "Out-of-range volume accepted");
                volume.FieldValue = "100";
                profile.ConfigValues.Single(x => x.FieldName == "Enable Cabinet Outputs").FieldValue = "1";
                profile.ConfigValues.Single(x => x.FieldName == "Outputs").FieldValue = "Network Outputs";
                profile.CabinetOutputSettings = new CabinetOutputSettings { ListenerPort = 8123, LineEnding = "LF" };
                var network = TeknoTZeroLauncher.Build(profile, null, null);
                CabinetOutputSettings.Apply(profile, network);
                Check(Args(network.Arguments).Contains("--services.output.backend=cabinet") &&
                    Args(network.Arguments).Contains("--services.output.route=network") &&
                    network.EnvironmentVariables["TP_OUTPUT_PORT"] == "8123" &&
                    network.EnvironmentVariables["TP_OUTPUT_LINE_ENDING"] == "LF", "Cabinet settings not propagated");
                foreach (var revision in profile.ConfigValues.Single(x => x.FieldName == "Game Version").FieldOptions)
                {
                    profile.ConfigValues.Single(x => x.FieldName == "Game Version").FieldValue = revision;
                    var revisionArgs = Args(TeknoTZeroLauncher.Build(profile, null, null).Arguments);
                    Check(revisionArgs.Last() == revision, "Revision routing");
                    if (argv.Length > 1)
                    {
                        Directory.CreateDirectory(argv[1]);
                        File.WriteAllLines(Path.Combine(argv[1], revision + ".args"), revisionArgs,
                            new System.Text.UTF8Encoding(false));
                    }
                    ++count;
                }
                InputCode.GameProfile = profile;
                InputCode.PlayerDigitalButtons = Enumerable.Range(0, 4).Select(_ => new PlayerButtons()).ToArray();
                var pipe = new TeknoTZeroPipe();
                pipe.Start();
                var first = Page();
                Check(System.Text.Encoding.ASCII.GetString(first, 0, 5) == "TTZIN" && first[5] == 1 && first[6] == 1, "Header");
                var driving = profile.ProfileName.StartsWith("batl") || profile.ProfileName.StartsWith("styphp");
                if (driving) Check(first[20] == 128 && first[21] == 0 && first[22] == 0, "Driving neutral");
                if (profile.ProfileName.StartsWith("pwrshovl")) Check(first.Skip(20).Take(8).All(x => x == 128), "Shovel neutral");
                if (profile.ProfileName.StartsWith("landhigh"))
                {
                    var slots = new[] { 0, 1, 2, 4, 5 };
                    Check(first.Skip(20).Take(3).All(x => x == 128) && first.Skip(23).Take(5).All(x => x == 0), "Flight neutral or unused slots");
                    Check(slots.All(x => profile.JoystickButtons.Any(b => b.InputMapping.ToString() == "Analog" + (x * 2))) &&
                        !profile.JoystickButtons.Any(b => b.InputMapping.ToString() == "Analog6"), "Flight ADC mapping");
                    Check(new[] { "Yoke Roll", "Yoke Pitch", "Rudder Pedals" }.All(name =>
                        profile.JoystickButtons.Single(b => b.ButtonName == name).AnalogType.ToString() == "AnalogJoystick"), "Flight centered axis type");
                    for (var i = 0; i < 8; ++i) InputCode.AnalogBytes[i * 2] = (byte)(i * 31);
                    InputCode.PlayerDigitalButtons[0].Button6 = true;
                    pipe.Transmit();
                    var flight = Page();
                    Check(slots.All(x => flight[20 + x] == x * 31) && flight[23] == 0 && flight[26] == 0 && flight[27] == 0,
                        "Flight sparse channel routing");
                    Check((flight[28] & 4) != 0, "Flight lever sync routing");
                    InputCode.PlayerDigitalButtons[0].Button6 = false;
                }
                if (profile.ProfileName.StartsWith("raizpin"))
                {
                    var slots = new[] { 0, 1, 2, 4, 5, 6 };
                    Check(slots.All(x => first[20 + x] == 128) && first[23] == 0 && first[27] == 0, "Racket neutral or unused slots");
                    Check(slots.All(x => profile.JoystickButtons.Any(b => b.InputMapping.ToString() == "Analog" + (x * 2))), "Missing racket axis binding");
                    for (var i = 0; i < 8; ++i) InputCode.AnalogBytes[i * 2] = (byte)(i * 31);
                    InputCode.PlayerDigitalButtons[0].Start = true;
                    InputCode.PlayerDigitalButtons[1].Button1 = true;
                    pipe.Transmit();
                    var rackets = Page();
                    Check(slots.All(x => rackets[20 + x] == x * 31) && rackets[23] == 0 && rackets[27] == 0, "Racket channel routing");
                    Check(rackets[13] == 0x80 && rackets[14] == 0x40, "Racket Start/Select players");
                    InputCode.PlayerDigitalButtons[0].Start = false;
                    InputCode.PlayerDigitalButtons[1].Button1 = false;
                }
                InputCode.PlayerDigitalButtons[0].Coin = true;
                InputCode.PlayerDigitalButtons[0].Test = true;
                InputCode.PlayerDigitalButtons[0].ExtensionButton2 = true;
                pipe.Transmit();
                var next = Page();
                Check(next[32] == 1 && next[12] == 128 && (next[28] & 16) != 0, "Digital controls");
                Check(BitConverter.ToUInt32(first, 8) != BitConverter.ToUInt32(next, 8), "Heartbeat");
                if (profile.ProfileName.StartsWith("styphp"))
                {
                    InputCode.AnalogBytes[0] = 0; pipe.Transmit(); Check(Page()[20] == 255, "Stunt left direction");
                    InputCode.AnalogBytes[0] = 255; pipe.Transmit(); Check(Page()[20] == 0, "Stunt right direction");
                }
                if (profile.ProfileName.StartsWith("dendego3"))
                {
                    InputCode.PlayerDigitalButtons[0].Button6 = true; pipe.Transmit();
                    Check((Page()[28] & 7) == 4 && (Page()[13] & 0x70) == 0, "Exclusive notch");
                    InputCode.PlayerDigitalButtons[0].Button6 = false; pipe.Transmit();
                    Check((Page()[28] & 7) == 4, "Notch latch");
                }
                pipe.Stop(); Thread.Sleep(30);
                Check(Page()[6] == 0, "Stop did not revoke lease");
                for (var restart = 0; restart < 5; ++restart) { pipe.Start(); pipe.Stop(); }
                var stopped = BitConverter.ToUInt32(Page(), 8);
                Thread.Sleep(40);
                Check(Page()[6] == 0 && BitConverter.ToUInt32(Page(), 8) == stopped, "Stopped publisher continued after restart");
                var gameVersion = profile.ConfigValues.Single(x => x.FieldName == "Game Version");
                gameVersion.FieldValue = "../raizpinj";
                bool rejected = false;
                try { TeknoTZeroLauncher.Build(profile, null, null); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid revision accepted");
            }
            Check(count == 9, "Expected nine permitted revisions");
            Console.WriteLine("Passed TTZIN writer, launcher quoting and seven profiles / nine revisions.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
