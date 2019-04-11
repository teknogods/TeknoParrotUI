using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using TeknoParrotUi.Common;


namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // Discord Rich Presence application ID
        private const string APP_ID = "508838453937438752";
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
                    _profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles\\", a));
                }
                else
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(b);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
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
                        Application.Current.Shutdown(0);
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

            JoystickHelper.DeSerialize();

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // disable Discord RPC if the DLL doesn't exist
            if (!File.Exists("discord-rpc.dll"))
            {
                Lazydata.ParrotData.UseDiscordRPC = false;
            }

            if (Lazydata.ParrotData.UseDiscordRPC)
            {
                DiscordRPC.Initialize(APP_ID, IntPtr.Zero, false, null);
            }

            if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args))
                {
                    // Args ok, let's do stuff
                    Window window = new Window
                    {
                        Title = "GameRunning",
                        Content = new TeknoParrotUi.Views.GameRunning(_profile, _test, _profile.TestMenuParameter,
                           _profile.TestMenuIsExecutable, _profile.TestMenuExtraParameters, _emuOnly, _profileLaunch),
                        MaxWidth = 800,
                        MaxHeight = 800
                    };
                    window.Show();
                    return;
                }
            }
            StartApp();
        }

        private void StartApp()
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();
        }
    }
}
