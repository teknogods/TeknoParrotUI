using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Auth;
using TeknoParrotUi.Common.Online;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputProfiles.Helpers;

internal static class LatestPortChecks
{
    private static int checks;
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); checks++; }
    internal static async Task RunAsync(string temporary)
    {
        using (var callback = new TcpOAuthCallback())
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var waiting = callback.WaitAsync("fixture-state", cancellation.Token);
            using var idle = new TcpClient();
            await idle.ConnectAsync(IPAddress.Loopback, callback.RedirectUri.Port);
            var other = await Request(callback.RedirectUri.Port, "/favicon.ico");
            Check(other.StartsWith("HTTP/1.1 404"), "OAuth ignores unrelated requests");
            var bad = await Request(callback.RedirectUri.Port, "/callback?state=wrong&code=bad");
            Check(bad.StartsWith("HTTP/1.1 400") && !waiting.IsCompleted, "OAuth state mismatch");
            var good = await Request(callback.RedirectUri.Port, "/callback?state=fixture-state&code=valid%2Bcode");
            Check(await waiting == "valid+code" && good.StartsWith("HTTP/1.1 200"), "TCP OAuth callback and idle connection isolation");
        }
        using (var callback = new TcpOAuthCallback())
        using (var cancellation = new CancellationTokenSource())
        {
            var waiting = callback.WaitAsync("cancel", cancellation.Token); cancellation.Cancel();
            try { await waiting; throw new Exception("OAuth cancellation ignored"); }
            catch (OperationCanceledException) { checks++; }
        }

        var profile = new GameProfile { OnlineIdType = OnlineIdType.Kizuna, OnlineIdFieldName = "OnlineID",
            ConfigValues = new() { new() { FieldName = "OnlineID", FieldValue = "old" }, new() { FieldName = "OnlineSecret", FieldValue = "old" } } };
        var secret = new string('A', 43);
        Check(KizunaOnlineHelper.AutoFill(profile, "AAKZABC12345678", secret), "Kizuna replaces stale account credentials");
        Check(KizunaOnlineHelper.ProfileHasCredential(profile), "Kizuna account pair validity");
        profile.OnlineIdType = OnlineIdType.MarioKartId;
        Check(MarioKartOnlineHelper.AutoFill(profile, "AAMKABC12345678", secret), "Mario Kart account auto-fill");
        Check(MarioKartOnlineHelper.ProfileHasCredential(profile), "Mario Kart account pair validity");
        profile.ConfigValues.RemoveAt(1);
        Check(!MarioKartOnlineHelper.AppliesTo(profile), "Legacy Mario Kart profile unaffected");

        string savedBody = null;
        using var http = new HttpClient(new Handler(async request =>
        {
            Check(request.Headers.Authorization?.Parameter == "fixture-token", "Avatar API bearer handoff");
            if (request.Method == HttpMethod.Put)
            {
                savedBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"saved\",\"efsf\":null,\"zeon\":null}") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"slots\":13,\"voices\":13,\"categories\":[]}") };
        }));
        var catalog = await KizunaAvatarApi.GetCatalogAsync("fixture-token", http);
        Check(catalog.Outcome == KizunaAvatarApi.Outcome.Ok && catalog.Catalog.Slots == 13, "Avatar catalog deserialization");
        var saved = await KizunaAvatarApi.SaveAsync("fixture-token", 1, new KizunaAvatar(), http);
        using (var body = JsonDocument.Parse(savedBody!))
            Check(saved.Outcome == KizunaAvatarApi.Outcome.Ok && body.RootElement.GetProperty("side").GetInt32() == 1 &&
                body.RootElement.GetProperty("avatar").GetProperty("items").GetArrayLength() == 13, "Avatar save wire contract");
        Check((await KizunaAvatarApi.GetAccountAsync("", http)).Outcome == KizunaAvatarApi.Outcome.Unauthorized, "Avatar requires login");

        var folder = Path.Combine(temporary, KizunaAvatarGameData.DataFolderName, "avatar"); Directory.CreateDirectory(folder);
        var program = new byte[KizunaAvatarGameData.ItemCount * 60];
        Encoding.ASCII.GetBytes("001_00001_03").CopyTo(program, 0); BitConverter.GetBytes(9).CopyTo(program, 16);
        var game = Path.Combine(temporary, "n_gun_station_rel_opt_es1"); File.WriteAllBytes(game, program);
        File.WriteAllBytes(Path.Combine(folder, "base_male.tga"), Tga(0, 0, 255, 255));
        File.WriteAllBytes(Path.Combine(folder, "001_00001_03.tga"), Tga(255, 0, 0, 128));
        var data = KizunaAvatarGameData.Load(game, out var error);
        Check(data != null && error == null, "Avatar reads game-owned item table");
        var pixels = data!.Compose(false, new[] { 1 });
        Check(pixels[0] == 128 && pixels[1] == 0 && pixels[2] == 127 && pixels[3] == 255, "Avatar layer ordering and premultiplied pixels");
        Check(KizunaAvatarGameData.ReadTga(new byte[5]) == null, "Malformed texture rejected");
        Check(data.GreetingWav(0, 0) == null, "Missing voice bank is optional");
        foreach (byte value in new byte[] { 0, 32, 128, 255 })
            Check(AnalogHelper.CalculateSWThrottleXinput(new XInputButton { IsRightTrigger = true },
                new State { Gamepad = new XiGamepad { RightTrigger = value } }, false) == value, "Single flight throttle range");
        var mapper = new InputListenerXInput();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var single = typeof(InputListenerXInput).GetField("SingleThrottleAxis", flags);
        var oldSingle = single.GetValue(null);
        try
        {
            single.SetValue(null, true);
            var value = typeof(InputListenerXInput).GetMethod("ModifyAnalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(mapper, new object[] { new JoystickButtons { ButtonName = "Throttle Brake", AnalogType = AnalogType.SWThrottle,
                    XInputButton = new XInputButton { IsRightTrigger = true } }, new State(), 0, 0 });
            Check(value == null, "Split brake binding ignored for a single-axis throttle");
        }
        finally { single.SetValue(null, oldSingle); }
        var namco = new GameProfile { ProfileName = "panicprkj", EmulatorType = EmulatorType.TeknoS23,
            ConfigValues = new() { new() { FieldName = "Game Revision", FieldValue = "panicprkj" } } };
        Check(NamcoGameRevisions.GetLanguageOptions(namco).Any(option => option.Value == "en"), "Namco revision language options");
        Check(TeknoParrotUi.Common.GameLaunch.NamcoCameraDeviceProbe.GetLaunchSelection("webcam:0123456789abcdef") == "webcam:0123456789abcdef" &&
            TeknoParrotUi.Common.GameLaunch.NamcoCameraDeviceProbe.GetLaunchSelection("invalid argument") == "off", "Namco camera selection validation");
        Console.WriteLine("PASS: " + checks + " TCP OAuth, account credential, avatar API and game-data checks.");
    }
    private static byte[] Tga(byte blue, byte green, byte red, byte alpha)
    {
        var bytes = new byte[18 + KizunaAvatarGameData.Width * KizunaAvatarGameData.Height * 4];
        bytes[2] = 2; bytes[16] = 32; bytes[17] = 0x20;
        BitConverter.GetBytes((ushort)KizunaAvatarGameData.Width).CopyTo(bytes, 12);
        BitConverter.GetBytes((ushort)KizunaAvatarGameData.Height).CopyTo(bytes, 14);
        for (var i = 18; i < bytes.Length; i += 4) { bytes[i] = blue; bytes[i + 1] = green; bytes[i + 2] = red; bytes[i + 3] = alpha; }
        return bytes;
    }
    private static async Task<string> Request(int port, string target)
    {
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("GET " + target + " HTTP/1.1\r\nHost: localhost\r\n\r\n"));
        using var reader = new StreamReader(stream); return await reader.ReadToEndAsync();
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request);
    }
}
