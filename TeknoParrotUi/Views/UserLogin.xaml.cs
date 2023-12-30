using CefSharp;
using CefSharp.Wpf;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
            Browser.Address = "https://Teknoparrot.com/Home/Chat";
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
            if(isLaunched)
            {
                MessageBox.Show("Game is already running.");
                return;
            }

            //MessageBox.Show("Unique: " + uniqueRoomName + "\nReal: " + realRoomName + "\nPlayercount: " + playerCount);
            var profileName = gameId + ".xml";
            var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName} --tponline")
            {
                UseShellExecute = false
            };

            info.EnvironmentVariables.Add("TP_TPONLINE2", $"{uniqueRoomName}|{playerId}|{playerName}|{playerCount}");

            LauncherProcess = Process.Start(info);
            isLaunched = true;
			while (!LauncherProcess.HasExited)
	            Thread.Sleep(10);
            isLaunched = false;
        }
    }
}
