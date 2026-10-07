using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TeknoParrotUi.Avalonia;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Avalonia.Views;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Online;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

internal static class Program
{
    private static int checks;
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); checks++; }

    [STAThread]
    private static int Main(string[] args)
    {
        var original = Environment.CurrentDirectory;
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "tpui-port-ui-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(temporary);
        try
        {
            var root = Path.GetFullPath(args[0]);
            foreach (var folder in new[] { "GameProfiles", "Metadata" })
            {
                Directory.CreateDirectory(Path.Combine(temporary, folder));
                foreach (var file in Directory.GetFiles(Path.Combine(root, "TeknoParrotUi.Common", folder)))
                    File.Copy(file, Path.Combine(temporary, folder, Path.GetFileName(file)));
            }
            Environment.CurrentDirectory = temporary;
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            NewsHistoryChecks.Run();
            var articles = new[]
            {
                new NewsArticle(new Uri("https://www.patreon.com/TeknoParrotTeam/posts/newer"), DateTimeOffset.UtcNow),
                new NewsArticle(new Uri("https://www.patreon.com/TeknoParrotTeam/posts/older"), DateTimeOffset.UtcNow.AddDays(-1))
            };
            var news = new AnnouncementView(articles, true);
            var picker = news.FindControl<ComboBox>("ArticlePicker");
            var older = news.FindControl<Button>("OlderArticle");
            var newer = news.FindControl<Button>("NewerArticle");
            Require(picker.SelectedIndex == 0 && !newer.IsEnabled && older.IsEnabled, "Newest article navigation state");
            older.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(picker.SelectedIndex == 1 && newer.IsEnabled && !older.IsEnabled, "Older article navigation state");
            newer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(picker.SelectedIndex == 0, "Newer article navigation");

            var profile = JoystickHelper.DeSerializeGameProfile("GameProfiles/cps_sf2.xml", false);
            profile.ProfileName = "cps_sf2";
            profile.WineRunnerPath = "/fixture/wine";
            ArcadeGameRevisions.Populate(profile);
            var settings = new GameSettingsView();
            settings.LoadProfile(profile);
            var revision = settings.FindControl<StackPanel>("FieldsPanel").GetLogicalDescendants().OfType<ComboBox>().First(combo =>
                combo.ItemsSource is IEnumerable<DynamicDropdownOption> options && options.Any(option => option.Value == "cps_sf2"));
            var options = (IEnumerable<DynamicDropdownOption>)revision.ItemsSource;
            var selected = options.First(option => option.Value != "cps_sf2");
            revision.SelectedItem = selected;
            Require(profile.ConfigValues.First(field => field.FieldName == ArcadeGameRevisions.SettingName).FieldValue == "cps_sf2",
                "Changing settings mutated the library owner before Save");
            var privateMethods = BindingFlags.Instance | BindingFlags.NonPublic;
            Require((bool)typeof(GameSettingsView).GetMethod("HasUnsavedChanges", privateMethods).Invoke(settings, null),
                "Revision change was not marked unsaved");
            typeof(GameSettingsView).GetMethod("SaveProfile", privateMethods).Invoke(settings, null);
            var saved = JoystickHelper.DeSerializeGameProfile("UserProfiles/cps_sf2.xml", true);
            Require(saved != null && saved.ConfigValues.First(field => field.FieldName == ArcadeGameRevisions.SettingName).FieldValue == selected.Value,
                "Revision save did not keep the selected revision");
            Require(saved.WineRunnerPath == "/fixture/wine", "Revision change lost the 2.0 Wine runner setting");

            var renamed = new GameProfile { ProfileName = "cybrcomm", EmulatorType = EmulatorType.TeknoS22,
                FileName = "UserProfiles/cybrcomm.xml",
                JoystickButtons = new List<JoystickButtons> { new() { ButtonName = "Analog X", InputMapping = InputMapping.Analog0,
                    XInputButton = new XInputButton { IsLeftThumbX = true }, BindNameXi = "Fixture stick" } } };
            BindingsStore.Save(renamed);
            renamed.JoystickButtons[0].ButtonName = "Left Stick X";
            renamed.JoystickButtons[0].XInputButton = null;
            BindingsStore.Apply(renamed);
            Require(renamed.JoystickButtons[0].XInputButton?.IsLeftThumbX == true, "Namco control rename lost JSON binding");

            Require(InitialDUnifiedMode.IsMatchmakingProfile(new GameProfile { ProfileName = "ID8", OnlineIdType = OnlineIdType.InitialD }),
                "Initial D matchmaking policy");
            Require(!InitialDUnifiedMode.IsMatchmakingProfile(new GameProfile { ProfileName = "ID4ExpElf2", OnlineIdType = OnlineIdType.InitialD }),
                "Initial D export incorrectly uses matchmaking");
            var party = InitialDUnifiedMode.GeneratePartyCode();
            Require(InitialDUnifiedMode.NormalizePartyCode(InitialDUnifiedMode.FormatPartyCode(party)) == party, "Friends code round trip");
            var running = new GameRunningView();
            var onlineProfile = new GameProfile { ProfileName = "ID8", OnlineIdType = OnlineIdType.InitialD };
            typeof(GameRunningView).GetField("_profile", privateMethods).SetValue(running, onlineProfile);
            typeof(GameRunningView).GetMethod("StartOnlineStatus", privateMethods).Invoke(running, new object[] { false });
            typeof(GameRunningView).GetMethod("StopOnlineStatus", privateMethods).Invoke(running, null);
            InitialDUnifiedMode.PrepareLaunch(onlineProfile, party);
            typeof(GameRunningView).GetMethod("StopOnlineStatus", privateMethods).Invoke(running, null);
            Require(InitialDUnifiedMode.CurrentPartyCode(onlineProfile) == party, "Previous view cleanup consumed the next friends code");
            InitialDUnifiedMode.EndLaunch(onlineProfile);
            Require(InitialDUnifiedMode.ParsePing(200, "stat=1&maint=1&party=0") is { Maintenance: true, Party: false },
                "Initial D maintenance policy");
            Require(InitialDOnlineHelper.NormalizePcbId("not a cabinet") == null && !InitialDOnlineHelper.IsValidSecret("short"),
                "Invalid online credentials accepted");
            var fields = new HashSet<string>(typeof(TroubleshootingReport).GetField("FilteredGameConfigValues",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as string[], StringComparer.OrdinalIgnoreCase);
            Require(fields.Contains("OnlineSecret") && fields.Contains("CabinetSerial"), "Online credentials missing from report redaction");
            TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Reset();
            TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Add(2, 120, -30);
            TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Add(2, 9, -2);
            uint previous = 0;
            Require(TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Delta(2, false, ref previous) == 129,
                "Third-player trackball lost unpublished movement");
            Require(TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Delta(2, false, ref previous) == 0,
                "Trackball repeated consumed movement");
            Require(unchecked((int)TeknoParrotUi.Common.InputListening.Mouse.TrackballMotion.Cumulative(2, true)) == -32,
                "Trackball cumulative axis direction");
            Require(HighScoreUrlResolver.Resolve("daytona", "en")?.AbsoluteUri.EndsWith("/DaytonaUSA") == true &&
                HighScoreUrlResolver.Resolve("swtrilgy", "en")?.AbsoluteUri.EndsWith("/StarWarsTrilogy") == true,
                "Model 2/3 high score mappings missing");
            foreach (var type in new[] { EmulatorType.TeknoHDrive, EmulatorType.TeknoMagic, EmulatorType.TeknoModel3,
                EmulatorType.TeknoMVS, EmulatorType.TeknoCPS, EmulatorType.TeknoSS32 })
                Require(TeknoParrotUi.Common.Updater.UpdaterComponent.BuildDefaultComponents("fixture-ui.exe").Any(component => component.name == type.ToString()),
                    "Missing updater component " + type);
            Console.WriteLine("PASS: " + checks + " Avalonia settings, news navigation, online policy, redaction and updater integration checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            Environment.CurrentDirectory = original;
            var expected = Path.GetFullPath(Path.GetTempPath()) + "tpui-port-ui-";
            if (temporary.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(temporary, true);
        }
    }
}
