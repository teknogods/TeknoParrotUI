using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    // Hello
    public partial class App
    {
        private GameProfile _profile;
        private bool _emuOnly, _test, _tpOnline, _startMin;
        private bool _profileLaunch;

        public static bool Is64Bit()
        {
            // for testing
            //return false;
            return Environment.Is64BitOperatingSystem;
        }

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
            if (args.Contains("--tponline"))
            {
                _tpOnline = true;
            }

            if (args.Contains("--startMinimized"))
            {
                _startMin = true;
            }
            if (args.Any(x => x.StartsWith("--profile=")) && args.All(x => x != "--emuonly"))
            {
                // Run game + emu
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = false;
                _profileLaunch = true;
                if (string.IsNullOrWhiteSpace(_profile.GamePath))
                {
                    MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorGamePathNotSet);
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

        public static void LoadTheme(string colourname, bool darkmode, bool holiday)
        {
            // if user isn't patreon, use defaults
            if (!IsPatreon())
            {
                colourname = "lightblue";

                if (holiday)
                {
                    var now = DateTime.Now;

                    if (now.Month == 10 && now.Day == 31)
                    {
                        // halloween - orange title
                        colourname = "orange";
                    }

                    if (now.Month == 12 && now.Day == 25)
                    {
                        // christmas - red title
                        colourname = "red";
                    }
                }
            }

            Debug.WriteLine($"UI colour: {colourname} | Dark mode: {darkmode}");

            ph.SetLightDark(darkmode);
            var colour = sp.Swatches.FirstOrDefault(a => a.Name == colourname);
            if (colour != null)
            {
                ph.ReplacePrimaryColor(colour);
            }
        }

        public static bool IsPatreon()
        {
            var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            return (tp != null && tp.GetValue("PatreonSerialKey") != null);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // This fixes the paths when the ui is started through the command line in a different folder
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((_, ex) =>
            {
                // give us the exception in english
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
                var exceptiontext = (ex.ExceptionObject as Exception).ToString();
                MessageBoxHelper.ErrorOK($"TeknoParrotUI ran into an exception!\nPlease send exception.txt to the #teknoparrothelp channel on Discord or create a Github issue!\n{exceptiontext}");
                File.WriteAllText("exception.txt", exceptiontext);
                Environment.Exit(1);
            });
            // Localization testing without changing system language.
            // Language code list: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/70feba9f-294e-491e-b6eb-56532684c37f
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("fr-FR");

            //this'll sort dumb stupid tp online gay shit
            HandleArgs(e.Args);
            JoystickHelper.DeSerialize();
            if (!_tpOnline)
            {
                if (Process.GetProcessesByName("TeknoParrotUi").Where((p) => p.Id != Process.GetCurrentProcess().Id)
                    .Count() > 0)
                {
                    if (MessageBoxHelper.ErrorYesNo(TeknoParrotUi.Properties.Resources.ErrorAlreadyRunning))
                    {
                        TerminateProcesses();
                    }
                    else
                    {
                        Current.Shutdown(0);
                        return;
                    }
                }

                if (!Lazydata.ParrotData.HideVanguardWarning)
                {
                    if (Process.GetProcessesByName("vgc").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0 || Process.GetProcessesByName("vgtray").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0)
                    {
                        MessageBoxHelper.WarningOK(TeknoParrotUi.Properties.Resources.VanguardDetected);
                    }
                }
            }

            if (File.Exists("DumbJVSManager.exe"))
            {
                MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorOldTeknoParrotDirectory);
                Current.Shutdown(0);
                return;
            }

            // updater cleanup
            try
            {
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
            } catch
            {
                // do nothing honestly
            }

            // old description file cleanup
            try
            {
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
            }
            catch
            {
                // ignore
            }


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

            LoadTheme(Lazydata.ParrotData.UiColour, Lazydata.ParrotData.UiDarkMode, Lazydata.ParrotData.UiHolidayThemes);

            if (Lazydata.ParrotData.UiDisableHardwareAcceleration)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args) && Views.Library.ValidateAndRun(_profile, out var loader, out var dll, _emuOnly, null, _test))
                {
                    var gamerunning = new Views.GameRunning(_profile, loader, dll, _test, _emuOnly, _profileLaunch);
                    // Args ok, let's do stuff
                    var window = new Window
                    {
                        //fuck you nezarn no more resizing smh /s
                        Title = "GameRunning",
                        Content = gamerunning,
                        MaxWidth = 800,
                        MinWidth = 800,
                        MaxHeight = 800,
                        MinHeight = 800,
                    };
                    if (_startMin)
                    {
                        window.WindowState = WindowState.Minimized;
                    }

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
