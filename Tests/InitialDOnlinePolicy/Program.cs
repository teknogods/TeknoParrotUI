using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;
using Newtonsoft.Json;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

internal static class Program
{
    private static int assertions;
    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static GameProfile Profile(string key, string mode, string gamePath = null)
    {
        return new GameProfile { ProfileName = key, OnlineIdType = OnlineIdType.InitialD, GamePath = gamePath,
            ConfigValues = new List<FieldInformation> { new FieldInformation {
                CategoryName = "Network", FieldName = InitialDUnifiedMode.ModeField, FieldValue = mode } } };
    }

    private static byte[] Header(uint target)
    {
        var bytes = new byte[0x400];
        var baseline = InitialDUnifiedMode.Crc32(bytes);
        var basis = new uint[32];
        var masks = new uint[32];
        for (int bit = 0; bit < 32; bit++)
        {
            bytes[1020 + bit / 8] ^= (byte)(1 << (bit % 8));
            var value = InitialDUnifiedMode.Crc32(bytes) ^ baseline;
            bytes[1020 + bit / 8] ^= (byte)(1 << (bit % 8));
            var mask = 1u << bit;
            for (int n = 31; n >= 0; n--)
            {
                if ((value & (1u << n)) == 0) continue;
                if (basis[n] == 0) { basis[n] = value; masks[n] = mask; break; }
                value ^= basis[n]; mask ^= masks[n];
            }
        }
        var difference = target ^ baseline;
        uint patch = 0;
        for (int n = 31; n >= 0; n--)
        {
            if ((difference & (1u << n)) == 0) continue;
            Require(basis[n] != 0, "Synthetic CRC basis missing");
            difference ^= basis[n]; patch ^= masks[n];
        }
        Require(difference == 0, "Synthetic CRC target is not reachable");
        for (int bit = 0; bit < 32; bit++)
            if ((patch & (1u << bit)) != 0) bytes[1020 + bit / 8] ^= (byte)(1 << (bit % 8));
        Require(InitialDUnifiedMode.Crc32(bytes) == target, "Synthetic title header mismatch");
        return bytes;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        yield return node;
        var panel = node as Panel;
        var content = node as ContentControl;
        var decorator = node as Decorator;
        var children = panel != null ? panel.Children.Cast<DependencyObject>() :
            content?.Content is DependencyObject ? new[] { (DependencyObject)content.Content } :
            decorator?.Child != null ? new[] { decorator.Child } : Enumerable.Empty<DependencyObject>();
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var folder = Path.GetFullPath(Path.Combine(tempRoot, "tpui-initiald-policy-" + Guid.NewGuid().ToString("N")));
        Require(folder.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase), "Temporary path outside intended root");
        Directory.CreateDirectory(folder);
        try
        {
            Lazydata.ParrotData = JsonConvert.DeserializeObject<ParrotData>("{\"InitialDConnectToServer\":false}");
            Require(typeof(ParrotData).GetProperty("InitialDConnectToServer") == null, "Retired preference still exists");
            Require(!JsonConvert.SerializeObject(Lazydata.ParrotData).Contains("InitialDConnectToServer"), "Retired preference persisted");
            foreach (var key in InitialDUnifiedMode.MatchmakingProfiles)
            {
                foreach (var mode in new[] { "Auto", "NoServer", "Legacy", " noserver ", "unknown", "" })
                {
                    var profile = Profile(key, mode);
                    Require(InitialDUnifiedMode.EffectiveNetworkMode(profile) == "Auto", key + " honored an old off mode");
                    Require(InitialDUnifiedMode.IniValue(profile, profile.ConfigValues[0], mode) == "Auto", key + " wrote an off mode to ini");
                    var unrelated = new FieldInformation { CategoryName = "Network", FieldName = "Chat" };
                    Require(InitialDUnifiedMode.IniValue(profile, unrelated, "0") == "0", "Unrelated field changed");
                }
                using (var stream = File.OpenRead(Path.Combine(args[0], "TeknoParrotUi.Common", "GameProfiles", key + ".xml")))
                {
                    var real = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(stream);
                    real.ProfileName = key;
                    Require(InitialDUnifiedMode.IsMatchmakingProfile(real), "Real XML no longer routes to the dedicated server");
                    Require(InitialDUnifiedMode.EffectiveNetworkMode(real) == "Auto", "Real XML did not select Auto");
                }
            }
            var legacyPath = Path.Combine(folder, "legacy.exe");
            var aaPath = Path.Combine(folder, "double-ace.exe");
            File.WriteAllBytes(legacyPath, Header(InitialDUnifiedMode.Id6Version12Crc));
            File.WriteAllBytes(aaPath, Header(InitialDUnifiedMode.Id6DoubleAceCrc));
            var legacy = Profile("ID6", "NoServer", legacyPath);
            Require(!InitialDUnifiedMode.IsMatchmakingProfile(legacy), "ID6 1.2 incorrectly uses InitialDServer");
            Require(!InitialDUnifiedMode.IsTpoRetired(legacy), "ID6 1.2 lost TPO");
            Require(InitialDUnifiedMode.EffectiveNetworkMode(legacy) == "NoServer", "ID6 1.2 mode changed");
            Require(InitialDUnifiedMode.IniValue(legacy, legacy.ConfigValues[0], "Legacy") == "Legacy", "ID6 1.2 ini changed");
            var aa = Profile("ID6", "Legacy", aaPath);
            Require(InitialDUnifiedMode.IsTpoRetired(aa), "ID6 Double Ace still offers TPO");
            Require(InitialDUnifiedMode.EffectiveNetworkMode(aa) == "Auto", "ID6 Double Ace did not select Auto");
            foreach (var key in new[] { "ID4Exp", "ID4ExpElf2", "Unrelated" })
            {
                var profile = Profile(key, "NoServer");
                Require(!InitialDUnifiedMode.IsMatchmakingProfile(profile), "Non-matchmaking profile affected");
                Require(InitialDUnifiedMode.EffectiveNetworkMode(profile) == "NoServer", "Legacy mode changed");
                Require(InitialDUnifiedMode.IniValue(profile, profile.ConfigValues[0], "Legacy") == "Legacy", "Legacy ini changed");
            }
            var account = File.ReadAllText(Path.Combine(args[0], "TeknoParrotUi", "Views", "AccountPage.xaml"));
            Require(!account.Contains("InitialDConnectCheckBox") && !account.Contains("InitialDConnectCard"), "Account toggle still present");
            var flow = typeof(InitialDUnifiedMode).Assembly.GetType("TeknoParrotUi.Views.InitialDLaunchFlow", true);
            var build = flow.GetMethod("BuildFirstRunNotice", BindingFlags.Static | BindingFlags.NonPublic);
            var handle = build.Invoke(null, new object[] { null });
            var window = (Window)handle.GetType().GetProperty("Window").GetValue(handle);
            Require(!Descendants(window).OfType<CheckBox>().Any(), "First-run opt-out still present");
            var acknowledge = (Func<bool>)handle.GetType().GetProperty("Result").GetValue(handle);
            Require(acknowledge(), "First-run acknowledgement failed");
            window.Close();
            Console.WriteLine("PASS: " + assertions + " Initial D automatic server, saved-off migration, real profile, legacy-version and dialog assertions. No server, account or game was contacted.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally { Directory.Delete(folder, true); }
    }
}
