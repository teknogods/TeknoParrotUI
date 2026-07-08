using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TeknoParrotUi.Avalonia.Views;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? Array.Empty<string>();

            JoystickHelper.DeSerialize();

            // Apply the saved UI language (classic multilanguage support)
            Services.Loc.ApplyCulture(Lazydata.ParrotData.Language);

            // TPO lobby CLI mode (--tponline --game=X --action=...) and deep links
            TPOConfig.ParseCliArgs(args);
            var tpoLink = args.FirstOrDefault(x => x.StartsWith("tponline://", StringComparison.OrdinalIgnoreCase));
            if (tpoLink != null)
                TPOConfig.ParseDeepLink(tpoLink);
            TPOConfig.RegisterProtocol();

            var profileArg = args.FirstOrDefault(x => x.StartsWith("--profile="));
            var profile = profileArg != null ? FetchProfile(profileArg) : null;

            if (profile != null)
            {
                // Direct game launch (TPO bridge spawn, shortcuts, frontends):
                // show only the game-running window; exit when the game stops so
                // the TPO browser receives onGameProcessExited.
                // --emuonly (developer mode) runs just the emulation layer — the
                // window stays open until closed manually.
                var test = args.Any(x => x == "--test");
                var emuOnly = args.Any(x => x == "--emuonly");
                var view = new GameRunningView();
                var window = new Window
                {
                    Title = "TeknoParrot — Game Running",
                    Width = 800,
                    Height = 800,
                    MinWidth = 640,
                    MinHeight = 480,
                    Content = view
                };
                if (args.Contains("--startMinimized"))
                    window.WindowState = WindowState.Minimized;
                view.BackRequested += () => window.Close();
                if (!emuOnly)
                    view.GameExited += _ => window.Close();
                window.Opened += (_, _) => view.StartGame(profile, test, emuOnly);
                desktop.MainWindow = window;
            }
            else
            {
                var main = new MainWindow();
                desktop.MainWindow = main;
                if (args.Contains("--tponline") || TPOConfig.IsConfigured)
                    main.NavigateToTpo();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Loads a game profile from a --profile=Name.xml argument — ported from the
    /// classic App.FetchProfile.
    /// </summary>
    private static GameProfile? FetchProfile(string profileArg)
    {
        try
        {
            var a = profileArg.Substring("--profile=".Length);
            if (string.IsNullOrWhiteSpace(a))
                return null;
            var b = Path.Combine("GameProfiles", a);
            if (!File.Exists(b))
                return null;

            GameProfile profile;
            if (File.Exists(Path.Combine("UserProfiles", a)))
                profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", a), true);
            else
                profile = JoystickHelper.DeSerializeGameProfile(b, false);

            if (profile == null)
                return null;

            profile.FileName = b;
            profile.ProfileName = Path.GetFileNameWithoutExtension(a);
            profile.IconName = "Icons/" + Path.GetFileNameWithoutExtension(a) + ".png";
            profile.GameInfo = JoystickHelper.DeSerializeMetadata(b);
            if (profile.GameInfo != null)
            {
                profile.GameNameInternal = profile.GameInfo.game_name;
                profile.GameGenreInternal = profile.GameInfo.game_genre;
                if (profile.GameInfo.icon_name != "")
                    profile.IconName = "Icons/" + profile.GameInfo.icon_name;
            }
            else
            {
                profile.GameNameInternal = Path.GetFileNameWithoutExtension(a) + " (Metadata Missing)";
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }
}
