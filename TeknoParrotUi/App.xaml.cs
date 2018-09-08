using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using TeknoParrotUi.Common;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private GameProfile _profile;
        private bool _emuOnly;
        private bool _test;

        private void TerminateProcesses()
        {
            var proc = Process.GetProcessesByName("TeknoParrotUi");
            var p = Process.GetCurrentProcess();
            foreach (var process in proc)
            {
                if (process.Id != p.Id)
                {
                    process.Kill();
                }
            }
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            if (SingleApplicationDetector.IsRunning())
            {
                if (MessageBox.Show(
                        "Detected already running TeknoParrot Ui, want me to close it for you?", "Error",
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
            if (File.Exists("DumbJVSManager.exe"))
            {
                MessageBox.Show(
                    "Seems you have extracted me to directory of old TeknoParrot, please extract me to a new directory instead!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(0);
                return;
            }
            var parrotData = JoystickHelper.DeSerialize();
            if (parrotData == null)
            {
                StartApp();
                return;
            }
                if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args))
                {
                    // Args ok, let's do stuff
                    if (_emuOnly)
                    {
                        TeknoParrotUi.Views.GameRunning g = new TeknoParrotUi.Views.GameRunning(_profile, _test, parrotData, _profile.TestMenuParameter,
                            _profile.TestMenuIsExecutable, _profile.TestMenuExtraParameters, true);
                        g.Show();
                        return;
                    }
                    else
                    {
                        TeknoParrotUi.Views.GameRunning g = new TeknoParrotUi.Views.GameRunning(_profile, _test, parrotData, _profile.TestMenuParameter,
                            _profile.TestMenuIsExecutable, _profile.TestMenuExtraParameters, false);
                        g.Show();
                        return;
                    }
                }
            }
            StartApp();
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
                var a = profile.Substring(10, profile.Length-10);
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

        private void StartApp()
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();
        }
    }
}