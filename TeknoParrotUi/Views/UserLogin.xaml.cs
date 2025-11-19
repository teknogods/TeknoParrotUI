using CefSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using TeknoParrotUi.Properties;
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
        private bool _isDisposed = false;

        public UserLogin()
        {
            InitializeComponent();

            _tPO2Callback = new TPO2Callback();

            // Subscribe to the GameProcessExited event
            _tPO2Callback.GameProcessExited += OnGameProcessExited;

            Browser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;
            Browser.JavascriptObjectRepository.Register("callbackObj", _tPO2Callback, isAsync: false, options: BindingOptions.DefaultBinder);
            Browser.MenuHandler = new CustomMenuHandler();
            
            // Subscribe to the Unloaded event to properly clean up
            this.Unloaded += UserLogin_OnUnloaded;
        }

        private void UserLogin_OnUnloaded(object sender, RoutedEventArgs e)
        {
            CleanupBrowser();
        }

        private void CleanupBrowser()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Unsubscribe from events
                if (_tPO2Callback != null)
                {
                    _tPO2Callback.GameProcessExited -= OnGameProcessExited;
                }

                // Dispose the browser control
                if (Browser != null)
                {
                    Browser.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - we're cleaning up
                System.Diagnostics.Debug.WriteLine($"Error during browser cleanup: {ex.Message}");
            }
        }

        private void OnGameProcessExited()
        {
            if (_isDisposed) return;
            
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Execute JavaScript to notify the website
                    Browser?.ExecuteScriptAsync("onGameProcessExited();");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error executing script: {ex.Message}");
                }
            });
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
            if (_isDisposed) return;
            
            try
            {
                //Browser.Address = "https://localhost:44339/Home/Chat";
                Browser.Address = "https://teknoparrot.com:3333/Home/Chat";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing browser: {ex.Message}");
            }
        }

        private void UserLogin_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isDisposed) return;
            
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
                try
                {
                    Browser?.Reload();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading browser: {ex.Message}");
                }
                IsActive = false;
            }
        }
    }

    public class TPO2Callback
    {
        public event Action GameProcessExited;
        private bool isLaunched = false;
        public static Process LauncherProcess;
        private string uniqueRoomName;
        
        public void showMessage(string msg)
        {
            MessageBox.Show(msg);
        }

        public void startGame(string uniqueRoomName, string realRoomName, string gameId, string playerId, string playerName, string playerCount)
        {
            if (LauncherProcess != null && !LauncherProcess.HasExited)
            {
                MessageBox.Show(Resources.UserLoginGameAlreadyRunning);
                return;
            }

            this.uniqueRoomName = uniqueRoomName; // Store unique room name for later use

            var profileName = gameId + ".xml";
            var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName} --tponline")
            {
                UseShellExecute = false
            };

            info.EnvironmentVariables.Add("TP_TPONLINE2", $"{uniqueRoomName}|{playerId}|{playerName}|{playerCount}");

            LauncherProcess = Process.Start(info);

            // Enable raising events
            LauncherProcess.EnableRaisingEvents = true;

            // Subscribe to the Exited event
            LauncherProcess.Exited += LauncherProcess_Exited;

            isLaunched = true;
        }

        private void LauncherProcess_Exited(object sender, EventArgs e)
        {
            isLaunched = false;
            LauncherProcess = null;

            // Notify the UI
            OnGameProcessExited();
        }

        protected void OnGameProcessExited()
        {
            GameProcessExited?.Invoke();
        }
    }
}
