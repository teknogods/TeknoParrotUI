using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views;
using Application = System.Windows.Application;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static TeknoParrotOnline TpOnline = new TeknoParrotOnline();
        private readonly About _about = new About();
        private readonly Library _library;
        private readonly Patreon _patron = new Patreon();
        private readonly AddGame _addGame;
        private bool _showingDialog;
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory("Icons");
            _library = new Library(contentControl);
            _addGame = new AddGame(contentControl, _library);
            contentControl.Content = _library;
            versionText.Text = GameVersion.CurrentVersion;
            Title = "TeknoParrot UI " + GameVersion.CurrentVersion;

            SaveCompleteSnackbar.VerticalAlignment = VerticalAlignment.Top;
            SaveCompleteSnackbar.HorizontalContentAlignment = HorizontalAlignment.Center;
            // 2 seconds
            SaveCompleteSnackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
        }

        //this is a WIP, not working yet
        public void redistCheck()
        {
            if (MessageBox.Show("It appears that this is your first time starting TeknoParrot, it is highly recommended that you install all the Visual C++ Runtimes for the highest compatibility with games. If you would like TeknoParrot to download and install them for you, click Yes, otherwise click No. If you're not sure if you have them all installed, click Yes.", "Missing redistributables", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                Debug.WriteLine("user chose no, not gonna download them");
            }
            else
            {
                Debug.WriteLine("user chose yes, AAAAAAAAAA");


            }
        }

        public void ShowMessage(string message)
        {
            SaveCompleteSnackbar.MessageQueue.Enqueue(message);
        }

        /// <summary>
        /// Loads the about screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnAbout(object sender, RoutedEventArgs e)
        {
            _about.UpdateVersions();
            contentControl.Content = _about;
        }

        /// <summary>
        /// Loads the library screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnLibrary(object sender, RoutedEventArgs e)
        {
            _library.UpdatePatronText();
            contentControl.Content = _library;
        }

        /// <summary>
        /// Shuts down the Discord integration then quits the program, terminating any threads that may still be running.
        /// </summary>
        public static void SafeExit()
        {
            if (Lazydata.ParrotData.UseDiscordRPC)
                DiscordRPC.Shutdown();

            Environment.Exit(0);
        }

        /// <summary>
        /// Loads the settings screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSettings(object sender, RoutedEventArgs e)
        {
            //_settingsWindow.ShowDialog();
            var settings = new UserControls.SettingsControl(contentControl, _library);
            contentControl.Content = settings;
        }

        StackPanel ConfirmExit()
        {
            var txt1 = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.IsPatreon() ? (Lazydata.ParrotData.UiDarkMode ? "#FFFFFF" : "#303030") : "#303030")),
                Margin = new Thickness(4),
                TextWrapping = TextWrapping.WrapWithOverflow,
                FontSize = 18,
                Text = Properties.Resources.MainAreYouSure
            };

            var dck = new DockPanel();
            dck.Children.Add(new Button()
            {
                Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.IsPatreon() ? (Lazydata.ParrotData.UiDarkMode ? "#FFFFFF" : "#303030") : "#303030")),
                Width = 115,
                Height = 30,
                Margin = new Thickness(5),
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = true,
                Content = Properties.Resources.Yes
            });
            dck.Children.Add(new Button()
            {
                Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.IsPatreon() ? (Lazydata.ParrotData.UiDarkMode ? "#FFFFFF" : "#303030") : "#303030")),
                Width = 115,
                Height = 30,
                Margin = new Thickness(5),
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = false,
                Content = Properties.Resources.No
            });

            var stk = new StackPanel
            {
                Width = 250,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.IsPatreon() ? (Lazydata.ParrotData.UiDarkMode ? "#303030" : "#FFFFFF") : "#FFFFFF"))
            };
            stk.Children.Add(txt1);
            stk.Children.Add(dck);
            return stk;
        }

        /// <summary>
        /// If the window is being closed, prompts whether the user really wants to do that so it can safely shut down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            //If the user has elected to allow the close, simply let the closing event happen.
            if (_allowClose) return;

            //NB: Because we are making an async call we need to cancel the closing event
            e.Cancel = true;

            //we are already showing the dialog, ignore
            if (_showingDialog) return;

            if (Lazydata.ParrotData.ConfirmExit)
            {
                //Set flag indicating that the dialog is being shown
                _showingDialog = true;
                var result = await DialogHost.Show(ConfirmExit());
                _showingDialog = false;
                //The result returned will come form the button's CommandParameter.
                //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
                if (!(result is bool boolResult) || !boolResult) return;
            }

            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        /// <summary>
        /// Same as window_closed except on the quit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnQuit(object sender, RoutedEventArgs e)
        {
            //If the user has elected to allow the close, simply let the closing event happen.
            if (_allowClose || _showingDialog) return;

            if (Lazydata.ParrotData.ConfirmExit)
            {
                //Set flag indicating that the dialog is being shown
                _showingDialog = true;
                var result = await DialogHost.Show(ConfirmExit());
                _showingDialog = false;
                //The result returned will come form the button's CommandParameter.
                //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
                if (!(result is bool boolResult) || !boolResult) return;
            }

            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        public class UpdaterComponent
        {
            // component name
            public string name { get; set; }
            // location of file to check version from, i.e TeknoParrot\TeknoParrot.dll
            public string location { get; set; }
            // repository name, if not set it will use name as the repo name
            public string reponame { get; set; }
            // if set, the changelog button will link to the commits page, if not it will link to the release directly
            public bool opensource { get; set; } = true;
            // if set, the updater will extract the files into this folder rather than the name folder
            public string folderOverride { get; set; }
            public string fullUrl { get { return "https://github.com/teknogods/" + (!string.IsNullOrEmpty(reponame) ? reponame : name) + "/"; } }
            // local version number
            public string _localVersion;
            public string localVersion
            {
                get
                {
                    if (_localVersion == null)
                    {
                        if (File.Exists(location))
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(location);
                            var pv = fvi.ProductVersion;
                            _localVersion = (fvi != null && pv != null) ? pv : "unknown";
                        }
                        else
                        {
                            _localVersion = Properties.Resources.UpdaterNotInstalled;
                        }
                    }
                    return _localVersion;
                }
            }
        }

        public static List<UpdaterComponent> components = new List<UpdaterComponent>()
        {
            new UpdaterComponent
            {
                name = "TeknoParrotUI",
                location = Assembly.GetExecutingAssembly().Location
            },
            new UpdaterComponent
            {
                name = "OpenParrotWin32",
                location = Path.Combine("OpenParrotWin32", "OpenParrot.dll"),
                reponame = "OpenParrot"
            },
            new UpdaterComponent
            {
                name = "OpenParrotx64",
                location = Path.Combine("OpenParrotx64", "OpenParrot64.dll"),
                reponame = "OpenParrot"
            },
            new UpdaterComponent
            {
                name = "OpenSegaAPI",
                location = Path.Combine("TeknoParrot", "Opensegaapi.dll"),
                folderOverride = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "TeknoParrot",
                location = Path.Combine("TeknoParrot", "TeknoParrot.dll"),
                opensource = false
            },
            new UpdaterComponent
            {
                name = "TeknoParrotN2",
                location = Path.Combine("N2", "TeknoParrot.dll"),
                reponame = "TeknoParrot",
                opensource = false,
                folderOverride = "N2"
            },
        };

        async Task<GithubRelease> GetGithubRelease(UpdaterComponent component)
        {
            using (var client = new HttpClient())
            {
#if DEBUG
                //https://github.com/settings/applications/new GET ONE HERE
                //MAKE SURE YOU DO NOT COMMIT THIS TOKEN IF YOU ADD IT! ONLY USE FOR DEVELOPMENT THEN REMOVE!
                //(bypasses retarded rate limit)            
                string secret = string.Empty; //?client_id=CLIENT_ID_HERE&client_secret=CLIENT_SECRET_HERE"
#else
                string secret = string.Empty;
#endif
                //Github's API requires a user agent header, it'll 403 without it
                client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrot");
                var reponame = !string.IsNullOrEmpty(component.reponame) ? component.reponame : component.name;
                var url = $"https://api.github.com/repos/TeknoGods/{reponame}/releases/tags/{component.name}{secret}";
                Debug.WriteLine($"Updater url for {component.name}: {url}");
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var release = await response.Content.ReadAsAsync<GithubRelease>();
                    return release;
                }
                return null;
            }
        }

        public int GetVersionNumber(string version)
        {
            var split = version.Split('.');
            if (split.Length != 4 || string.IsNullOrEmpty(split[3]) || !int.TryParse(split[3], out var ver))
            {
                Debug.WriteLine($"{version} is formatted incorrectly!");
                return 0;
            }
            return ver;
        }

        private async void CheckGithub(UpdaterComponent component)
        {
            try
            {
                var githubRelease = await GetGithubRelease(component);
                if (githubRelease != null)
                {
                    var localVersionString = component.localVersion;
                    var onlineVersionString = githubRelease.name;
                    // fix for weird things like OpenParrotx64_1.0.0.30
                    if (onlineVersionString.Contains(component.name))
                    {
                        onlineVersionString = onlineVersionString.Split('_')[1];
                    }

                    bool needsUpdate = false;
                    // component not installed.
                    if (localVersionString == Properties.Resources.UpdaterNotInstalled)
                    {
                        needsUpdate = true;
                    }
                    else
                    {
                        switch (localVersionString)
                        {
                            // version number is weird / unable to be formatted
                            case "unknown":
                                Debug.WriteLine($"{component.name} version is weird! local: {localVersionString} | online: {onlineVersionString}");
                                needsUpdate = localVersionString != onlineVersionString;
                                break;
                            default:
                                int localNumber = GetVersionNumber(localVersionString);
                                int onlineNumber = GetVersionNumber(onlineVersionString);

                                needsUpdate = localNumber < onlineNumber;
                                break;
                        }
                    }

                    Debug.WriteLine($"{component.name} - local: {localVersionString} | online: {onlineVersionString} | needs update? {needsUpdate}");

                    if (needsUpdate)
                    {
                        new GitHubUpdates(component, githubRelease, localVersionString, onlineVersionString).Show();
                    }
                }
                else
                {
                    Debug.WriteLine($"release is null? component: {component.name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// When the window is loaded, the update checker is run and DiscordRPC is set
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //CHECK IF I LEFT DEBUG SET WRONG!!
#if !DEBUG
            if (Lazydata.ParrotData.CheckForUpdates)
            {
                components.ForEach(component => CheckGithub(component));
            }
#endif

            if (Lazydata.ParrotData.UseDiscordRPC)
                DiscordRPC.UpdatePresence(new DiscordRPC.RichPresence
                {
                    details = "Main Menu",
                    largeImageKey = "teknoparrot",
                });
        }

        /// <summary>
        /// Loads the AddGame screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnAddGame(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _addGame;
        }

        /// <summary>
        /// Loads the patreon screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnPatreon(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _patron;
        }

        private void BtnTPOnline(object sender, RoutedEventArgs e)
        {
            contentControl.Content = TpOnline;
        }

        private void ColorZone_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}