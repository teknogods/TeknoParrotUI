using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;

// Manual integration check. Use a freshly staged UI root with isolated seed saves.
internal static class LiveLaunch
{
    static long Frame(int port)
    {
        var request = WebRequest.CreateHttp($"http://127.0.0.1:{port}/health");
        request.Timeout = 2000;
        using (var response = request.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream()))
            return long.Parse(Regex.Match(reader.ReadToEnd(), "\"frame\":([0-9]+)").Groups[1].Value);
    }

    public static int Main(string[] args)
    {
        Process process = null;
        TeknoTZeroPipe pipe = null;
        try
        {
            if (args.Length < 4 || args.Length > 5) throw new ArgumentException("Expected UI source, staged UI root, ROM root, CHD root, optional game");
            var game = args.Length == 5 ? args[4] : "batlgear";
            if (game != "batlgear" && game != "raizpin" && game != "landhigh" && game != "landhigha" && game != "dendego3")
                throw new ArgumentException("Unsupported live input scenario");
            var raizin = game == "raizpin";
            var train = game == "dendego3";
            var flight = game == "landhigh" || game == "landhigha";
            var profileSet = flight ? "landhigh" : game;
            var source = Path.GetFullPath(args[0]);
            var root = Path.GetFullPath(args[1]);
            var rom = Path.GetFullPath(args[2]);
            var chd = Path.GetFullPath(args[3]);
            var output = Path.Combine(root, "live-result");
            if (Directory.Exists(output)) throw new IOException("Live result must be a new directory");
            Directory.CreateDirectory(output);
            Directory.SetCurrentDirectory(root);
            GameProfile profile;
            using (var file = File.OpenRead(Path.Combine(source, "TeknoParrotUi.Common", "GameProfiles", profileSet + "_tz.xml")))
                profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(file);
            profile.ProfileName = profileSet + "_tz";
            profile.GamePath = Path.Combine(rom, profileSet + ".zip");
            void Setting(string name, string value) => profile.ConfigValues.Single(x => x.FieldName == name).FieldValue = value;
            Setting("CHD Directory", chd);
            Setting("Game Version", game);
            Setting("DisplayMode", "Windowed");
            Setting("VSync", "0");
            Setting("Mute Audio", "1");
            Setting("Internal Resolution", raizin || flight ? "1" : "3");
            Setting("Widescreen", raizin || flight ? "off" : "16:9");
            Setting("Fill Scanout Margins", "1");
            var start = TeknoTZeroLauncher.Build(profile, null, Console.WriteLine);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var options = new Dictionary<string, string>
            {
                ["benchmark.enabled"] = "true", ["benchmark.paced"] = "false",
                ["benchmark.warmup_frames"] = "9000", ["benchmark.measured_frames"] = "600",
                ["benchmark.persistence_safe"] = "true", ["sound.load_check_fast_forward"] = "false",
                ["benchmark.summary_file"] = Path.Combine(output, "benchmark.log"),
                ["video.output_width"] = "1280", ["video.output_height"] = "720",
                ["services.http.enabled"] = "true", ["services.http.port"] = port.ToString(),
                ["services.http.bind_address"] = "127.0.0.1",
                ["input.record_file"] = Path.Combine(output, "inputs.ttzi"),
                ["video.vulkan.validation_hash"] = "true", ["video.vulkan.validation_frame"] = "9000",
                ["video.vulkan.validation_capture_count"] = "2", ["video.vulkan.validation_capture_interval"] = "300",
                ["video.vulkan.validation_capture_file"] = Path.Combine(output, "frame.bmp")
            };
            if (raizin || flight)
            {
                options["benchmark.warmup_frames"] = "13200";
                options["benchmark.measured_frames"] = "60";
                options["benchmark.segment_frames"] = "60";
                options["video.output_width"] = "640";
                options["video.output_height"] = "480";
                options["video.vulkan.validation_frame"] = "10000";
                options["video.vulkan.validation_capture_count"] = "4";
                options["video.vulkan.validation_capture_interval"] = "1000";
                options["video.vulkan.validation_dump_frame"] = "false";
            }
            if (train)
            {
                options["diagnostics.enabled"] = "true";
                options["diagnostics.input"] = "true";
                options["diagnostics.log_file"] = Path.Combine(output, "diagnostics.log");
                options["diagnostics.max_lines"] = "100000";
                foreach (var category in new[] { "performance", "ppc", "tlcs", "tlcs_trace", "tlcs_bus",
                    "mailbox", "hdd", "ata", "audio", "storage", "services", "video", "renderer" })
                    options["diagnostics." + category] = "false";
            }
            foreach (var option in options) start.Arguments += " " + TeknoTZeroLauncher.Quote("--" + option.Key + "=" + option.Value);
            start.CreateNoWindow = true;
            File.WriteAllText(Path.Combine(output, "launch.txt"), start.FileName + "\n" + start.Arguments);
            // Refuse a changing shared page rather than take another title's inputs.
            var before = new byte[64]; var after = new byte[64];
            JvsHelper.StateView.ReadArray(0, before, 0, 64);
            Thread.Sleep(100);
            JvsHelper.StateView.ReadArray(0, after, 0, 64);
            if (!before.SequenceEqual(after)) throw new InvalidOperationException("Another input writer is active");
            InputCode.GameProfile = profile;
            InputCode.PlayerDigitalButtons = Enumerable.Range(0, 4).Select(_ => new PlayerButtons()).ToArray();
            pipe = new TeknoTZeroPipe();
            pipe.Start();
            var events = new List<(long frame, Action action)>();
            var buttons = InputCode.PlayerDigitalButtons[0];
            if (raizin)
            {
                var second = InputCode.PlayerDigitalButtons[1];
                events.Add((8000, () => buttons.Test = true)); events.Add((8030, () => buttons.Test = false));
                events.Add((9000, () => buttons.Button1 = true)); events.Add((9030, () => buttons.Button1 = false));
                events.Add((9600, () => buttons.Start = true)); events.Add((9630, () => buttons.Start = false));
                events.Add((10600, () => {
                    var slots = new[] { 0, 1, 2, 4, 5, 6 };
                    var values = new byte[] { 0, 255, 128, 255, 128, 0 };
                    for (var i = 0; i < slots.Length; ++i) InputCode.AnalogBytes[slots[i] * 2] = values[i];
                    buttons.Start = true; second.Button1 = true;
                }));
                events.Add((11600, () => {
                    var slots = new[] { 0, 1, 2, 4, 5, 6 };
                    var values = new byte[] { 32, 64, 96, 160, 192, 224 };
                    for (var i = 0; i < slots.Length; ++i) InputCode.AnalogBytes[slots[i] * 2] = values[i];
                    buttons.Start = false; second.Button1 = false;
                    buttons.Button1 = true; second.Start = true;
                }));
                events.Add((12600, () => {
                    foreach (var slot in new[] { 0, 1, 2, 4, 5, 6 }) InputCode.AnalogBytes[slot * 2] = 128;
                    buttons.Button1 = false; second.Start = false;
                }));
            }
            else if (train)
            {
                // Deliberately hold each Coin for 1.5 guest seconds. The
                // emulator must turn each press into one finite switch pulse.
                events.Add((4000, () => buttons.Coin = true)); events.Add((4090, () => buttons.Coin = false));
                events.Add((4100, () => buttons.Coin = true)); events.Add((4190, () => buttons.Coin = false));
                foreach (var frame in new[] { 4500, 5000, 5500 })
                {
                    events.Add((frame, () => buttons.Start = true));
                    events.Add((frame + 30, () => buttons.Start = false));
                }
                events.Add((6000, () => buttons.Button6 = true));
            }
            else if (flight)
            {
                events.Add((6000, () => buttons.Test = true)); events.Add((6030, () => buttons.Test = false));
                events.Add((6400, () => buttons.Button2 = true)); events.Add((6430, () => buttons.Button2 = false));
                events.Add((6600, () => buttons.Start = true)); events.Add((6630, () => buttons.Start = false));
                events.Add((10600, () => {
                    var slots = new[] { 0, 1, 2, 4, 5 };
                    var values = new byte[] { 0, 255, 128, 64, 192 };
                    for (var i = 0; i < slots.Length; ++i) InputCode.AnalogBytes[slots[i] * 2] = values[i];
                    buttons.Button6 = true;
                }));
                events.Add((11600, () => {
                    var slots = new[] { 0, 1, 2, 4, 5 };
                    var values = new byte[] { 32, 64, 96, 255, 0 };
                    for (var i = 0; i < slots.Length; ++i) InputCode.AnalogBytes[slots[i] * 2] = values[i];
                    buttons.Button6 = false;
                }));
                events.Add((12600, () => {
                    for (var slot = 0; slot < 6; ++slot) InputCode.AnalogBytes[slot * 2] = (byte)(slot < 3 ? 128 : 0);
                }));
            }
            else
            {
            events.Add((4000, () => buttons.Coin = true)); events.Add((4030, () => buttons.Coin = false));
            events.Add((4100, () => buttons.Coin = true)); events.Add((4130, () => buttons.Coin = false));
            events.Add((4500, () => buttons.Start = true)); events.Add((4530, () => buttons.Start = false));
            foreach (var frame in new[] { 4800, 5200, 5600, 6000 })
            {
                events.Add((frame, () => InputCode.AnalogBytes[2] = 255));
                events.Add((frame + 60, () => InputCode.AnalogBytes[2] = 0));
            }
            events.Add((6500, () => InputCode.AnalogBytes[2] = 255));
            }
            process = Process.Start(start);
            var errors = process.StandardError.ReadToEndAsync();
            var deadline = DateTime.UtcNow.AddMinutes(5);
            var next = 0;
            while (!process.HasExited)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Live launch timed out");
                try
                {
                    var frame = Frame(port);
                    while (next < events.Count && events[next].frame <= frame) events[next++].action();
                    pipe.Transmit();
                }
                catch (WebException) { }
                Thread.Sleep(5);
            }
            File.WriteAllText(Path.Combine(output, "stderr.txt"), errors.Result);
            if (process.ExitCode != 0 || next != events.Count) throw new Exception("Incomplete live input run");
            var recorded = File.ReadAllText(Path.Combine(output, "inputs.ttzi"));
            var requiredEvents = raizin
                ? new[] { "button 1 start 1", "button 1 action_1 1", "button 2 start 1", "button 2 action_1 1",
                    "axis 1 racket_g 0", "axis 1 stick_x 1023", "axis 1 stick_y 384",
                    "axis 2 racket_g 1023", "axis 2 stick_x 770", "axis 2 stick_y 0" }
                : flight ? new[] { "button 1 action_6 1", "button 1 action_6 0", "axis 1 stick_x 0",
                    "axis 1 stick_y 1023", "axis 1 pedal_1 384", "axis 1 pedal_2 257", "axis 1 pedal_3 770" }
                : train ? new[] { "button 1 coin 1", "button 1 coin 0", "button 1 start 1", "button 1 action_6 1" }
                : new[] { "button 1 coin 1", "button 1 start 1", "axis 1 accelerator 1023" };
            foreach (var required in requiredEvents)
                if (!recorded.Contains(required)) throw new Exception("Input did not reach the emulator: " + required);
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: production launcher and TTZIN writer reached emulator input recording. Inspect captures separately.\n");
            Console.WriteLine("PASS: live launcher/input contract; captures require visual review.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            pipe?.Stop();
            if (process != null && !process.HasExited) { process.Kill(); process.WaitForExit(); }
            process?.Dispose();
        }
    }
}
