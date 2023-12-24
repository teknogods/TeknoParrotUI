using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
