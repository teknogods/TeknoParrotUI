using CefSharp;
using System.Diagnostics;
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
            Browser.JavascriptObjectRepository.Register("callbackObj", _tPO2Callback, isAsync: false, options: BindingOptions.DefaultBinder);
            Browser.MenuHandler = new CustomMenuHandler();
        }

        public class CustomMenuHandler : CefSharp.IContextMenuHandler
        {
            public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
            {
                model.Clear();
            }

            public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                return false;
            }

            public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
            {

            }

            public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
            {
                return false;
            }
        }

        public void RefreshBrowserToStart()
        {
            //Browser.Address = "https://localhost:44339/Home/Chat";
            Browser.Address = "https://teknoparrot.com:3333/Home/Chat";
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
                RefreshBrowserToStart();
                // force a reload because otherwise it keeps a ghost lobby up that we can't rejoin.
                // not sure why it happens
                Browser.Reload();
                IsActive = false;
            }
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

        public void startGame(string uniqueRoomName, string realRoomName, string gameId, string playerId, string playerName, string playerCount)
        {
            if (LauncherProcess != null && LauncherProcess.HasExited)
            {
                isLaunched = false;
                LauncherProcess = null;
            }

            if (isLaunched)
            {
                MessageBox.Show("Game is already running.");
                return;
            }

            var profileName = gameId + ".xml";
            var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName} --tponline")
            {
                UseShellExecute = false
            };

            info.EnvironmentVariables.Add("TP_TPONLINE2", $"{uniqueRoomName}|{playerId}|{playerName}|{playerCount}");

            LauncherProcess = Process.Start(info);
            isLaunched = true;
        }
    }
}
