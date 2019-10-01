using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TeknoParrotUi.Common;


namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private GameProfile _profile;
        private bool _emuOnly, _test;
        private bool _profileLaunch;

        private void TerminateProcesses()
        {
            var currentId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("TeknoParrotUi"))
            {
                if (process.Id != currentId)
                {
                    process.Kill();
                }
            }
        }

        private bool HandleArgs(string[] args)
        {
            _test = args.Any(x => x == "--test");
            if (args.Any(x => x.StartsWith("--profile=")) && args.All(x => x != "--emuonly"))
            {
                // Run game + emu
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = false;
                _profileLaunch = true;
                if (string.IsNullOrWhiteSpace(_profile.GamePath))
                {
                    MessageBox.Show("You have not set game directory for this game!");
                    return false;
                }

                return true;
            }

            if (args.Any(x => x.StartsWith("--profile=")) && args.Any(x => x == "--emuonly"))
            {
                // Run emu only
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = true;
                return true;
            }

            return false;
        }

        private bool FetchProfile(string profile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profile))
                    return false;
                var a = profile.Substring(10, profile.Length - 10);
                if (string.IsNullOrWhiteSpace(a))
                    return false;
                var b = Path.Combine("GameProfiles\\", a);
                if (!File.Exists(b))
                    return false;
                if (File.Exists(Path.Combine("UserProfiles\\", a)))
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles\\", a), true);
                }
                else
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(b, false);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static PaletteHelper ph = new PaletteHelper();
        static SwatchesProvider sp = new SwatchesProvider();
        static string GetResourceString(string input)
        {
            return $"pack://application:,,,/{input}";
        }

        public static void LoadTheme(string colourname, bool darkmode)
        {
            // only change theme if patreon key exists.
            var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            if (tp != null && tp.GetValue("PatreonSerialKey") != null)
            {
                ph.SetLightDark(darkmode);
                Debug.WriteLine($"UI colour: {colourname} | Dark mode: {darkmode}");
                var colour = sp.Swatches.FirstOrDefault(a => a.Name == colourname);
                if (colour != null)
                {
                    ph.ReplacePrimaryColor(colour);
                }
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (SingleApplicationDetector.IsRunning())
            {
                if ((e.Args.Any(x => x.StartsWith("--profile=")) && e.Args.All(x => x != "--emuonly")) || (e.Args.Any(x => x.StartsWith("--profile=")) && e.Args.Any(x => x == "--emuonly")))
                {
                    
                }
                else
                {
                    if (MessageBox.Show(
                            "TeknoParrot UI seems to already be running, want me to close it?", "Error",
                            MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        TerminateProcesses();
                    }
                    else
                    {
                        Current.Shutdown(0);
                        return;
                    }
                }
            }

            if (File.Exists("DumbJVSManager.exe"))
            {
                MessageBox.Show(
                    "Seems you have extracted me to directory of old TeknoParrot, please extract me to a new directory instead!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(0);
                return;
            }

            // updater cleanup
            var bakfiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.bak", SearchOption.AllDirectories);
            foreach (var file in bakfiles)
            {
                try
                {
                    Debug.WriteLine($"Deleting old updater file {file}");
                    File.Delete(file);
                }
                catch
                {
                    // ignore..
                }
            }

            // old description file cleanup
            var olddescriptions = Directory.GetFiles("Descriptions", "*.xml");
            foreach (var file in olddescriptions)
            {
                try
                {
                    Debug.WriteLine($"Deleting old description file {file}");
                    File.Delete(file);
                }
                catch
                {
                    // ignore..
                }
            }

            JoystickHelper.DeSerialize();

            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString($"MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml"))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString("MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml"))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString($"MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.LightBlue.xaml"))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString("MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml"))
            });

            LoadTheme(Lazydata.ParrotData.UiColour, Lazydata.ParrotData.UiDarkMode);

            if (Lazydata.ParrotData.UiDisableHardwareAcceleration)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args) && Views.Library.ValidateAndRun(_profile, out var loader, out var dll, _emuOnly))
                {
                    var gamerunning = new Views.GameRunning(_profile, loader, dll, _test, _emuOnly, _profileLaunch);

                    // Args ok, let's do stuff
                    var window = new Window
                    {
                        Title = "GameRunning",
                        Content = gamerunning,
                        MaxWidth = 800,
                        MaxHeight = 800,
                    };

                    //             d:DesignHeight="800" d:DesignWidth="800" Loaded="GameRunning_OnLoaded" Unloaded="GameRunning_OnUnloaded">
                    window.Dispatcher.ShutdownStarted += (x, x2) => gamerunning.GameRunning_OnUnloaded(null, null);

                    window.Show();

                    return;
                }
            }
            DiscordRPC.StartOrShutdown();

            StartApp();
        }

        private void StartApp()
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();
        }
    }
}
