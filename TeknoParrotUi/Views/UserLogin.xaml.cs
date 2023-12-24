using CefSharp;
using CefSharp.Wpf;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for UserLogin.xaml
    /// </summary>
    public partial class UserLogin
    {
        public bool IsActive = false;
        private TPO2Callback _tPO2Callback;

        public UserLogin()

        {
            InitializeComponent();

            _tPO2Callback = new TPO2Callback();
            //Browser.RegisterAsyncJsObject("callbackObj", _tPO2Callback);
            Browser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;
            Browser.JavascriptObjectRepository.Register("callbackObj", _tPO2Callback, isAsync: true);
        }

        public void RefreshBrowserToStart()
        {
            Browser.Address = "https://localhost:44339/Home/Chat";
        }

        private void UserLogin_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                RefreshBrowserToStart();
                IsActive = true;
            }
            else
            {
                IsActive = false;
            }
        }

        private void InitCEF()
        {
            var settings = new CefSettings();

            //// Increase the log severity so CEF outputs detailed information, useful for debugging
            //settings.LogSeverity = LogSeverity.Verbose;
            //// By default CEF uses an in memory cache, to save cached data e.g. to persist cookies you need to specify a cache path
            //// NOTE: The executing user must have sufficient privileges to write to this folder.
            //settings.CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CefSharp\\Cache");
            settings.CachePath = Path.Combine(Directory.GetCurrentDirectory(), "libs\\CefSharp\\Cache");
            //settings.RootCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CefSharp\\Cache");
            settings.BrowserSubprocessPath =
                Path.Combine(Directory.GetCurrentDirectory(), "libs\\CefSharp\\CefSharp.BrowserSubprocess.exe");
            settings.LocalesDirPath = Path.Combine(Directory.GetCurrentDirectory(), "libs\\CefSharp\\locales");
            settings.ResourcesDirPath = Path.Combine(Directory.GetCurrentDirectory(), "libs\\CefSharp\\");
            //settings.CefCommandLineArgs.Add("disable-gpu", "1");
            settings.LogFile = Path.Combine(Directory.GetCurrentDirectory(), "libs\\CefSharp\\debug.log");
            //settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");

            //settings.CefCommandLineArgs.Add("disable-gpu-vsync", "1");

            //settings.CefCommandLineArgs.Add("disable-software-rasterizer", "1");
            //settings.DisableGpuAcceleration();
            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        }
    }

    public class TPO2Callback
    {
        bool isLaunched = false;
        public static Process LauncherProcess;
        public void showMessage(string msg)
        {
            MessageBox.Show(msg);
        }

        public void startGame(string uniqueRoomName, string realRoomName, string gameId, string playerId, string playerName)
        {
            if(isLaunched)
            {
                MessageBox.Show("Game is already running.");
                return;
            }

            //MessageBox.Show("Unique: " + uniqueRoomName + "\nReal: " + realRoomName);
            var profileName = gameId + ".xml";
            var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName}  --tponline")
            {
                UseShellExecute = false
            };

            info.EnvironmentVariables.Add("TP_TPONLINE2", $"{uniqueRoomName}|{playerId}|{playerName}");

            LauncherProcess = Process.Start(info);
            //isLaunched = true;
        }
    }
}
