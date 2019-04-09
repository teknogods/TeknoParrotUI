using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
        }

        /// <summary>
        /// Loads the about screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnAbout(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _about;
        }

        /// <summary>
        /// Loads the library screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnLibrary(object sender, RoutedEventArgs e)
        {
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
                var txt1 = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF53B3B")),
                    Margin = new Thickness(4),
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    FontSize = 18,
                    Text = "Are you sure?"
                };

                var btn1 = new Button();
                var style = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
                btn1.Style = style;
                btn1.Width = 115;
                btn1.Height = 30;
                btn1.Margin = new Thickness(5);
                btn1.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
                btn1.CommandParameter = true;
                btn1.Content = "Yes";

                var btn2 = new Button();
                var style2 = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
                btn2.Style = style2;
                btn2.Width = 115;
                btn2.Height = 30;
                btn2.Margin = new Thickness(5);
                btn2.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
                btn2.CommandParameter = false;
                btn2.Content = "No";

                var dck = new DockPanel();
                dck.Children.Add(btn1);
                dck.Children.Add(btn2);

                var stk = new StackPanel { Width = 250 };
                stk.Children.Add(txt1);
                stk.Children.Add(dck);

                //Set flag indicating that the dialog is being shown
                _showingDialog = true;
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(stk);
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
            if (_allowClose) return;

            //NB: Because we are making an async call we need to cancel the closing event


            //we are already showing the dialog, ignore
            if (_showingDialog) return;

            if (Lazydata.ParrotData.ConfirmExit)
            {
                var txt1 = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF53B3B")),
                    Margin = new Thickness(4),
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    FontSize = 18,
                    Text = "Are you sure?"
                };

                var btn1 = new Button();
                var style = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
                btn1.Style = style;
                btn1.Width = 115;
                btn1.Height = 30;
                btn1.Margin = new Thickness(5);
                btn1.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
                btn1.CommandParameter = true;
                btn1.Content = "Yes";

                var btn2 = new Button();
                var style2 = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
                btn2.Style = style2;
                btn2.Width = 115;
                btn2.Height = 30;
                btn2.Margin = new Thickness(5);
                btn2.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
                btn2.CommandParameter = false;
                btn2.Content = "No";


                var dck = new DockPanel();
                dck.Children.Add(btn1);
                dck.Children.Add(btn2);

                var stk = new StackPanel
                {
                    Width = 250
                };
                stk.Children.Add(txt1);
                stk.Children.Add(dck);

                //Set flag indicating that the dialog is being shown
                _showingDialog = true;
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(stk);
                _showingDialog = false;
                //The result returned will come form the button's CommandParameter.
                //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
                if (!(result is bool boolResult) || !boolResult) return;
            }

            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        async Task<List<GithubRelease>> GetGithubReleases(string repo)
        {
            using (var client = new HttpClient())
            {
                //Github's API requires a user agent header, it'll 403 without it
                client.DefaultRequestHeaders.Add("User-Agent", "TeknoParrot");
                var url = $"https://api.github.com/repos/TeknoGods/{repo}/releases";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var releases = await response.Content.ReadAsAsync<List<GithubRelease>>();
                    return releases;
                }
                return null;
            }
        }

        private async void CheckGitHub(string componentToCheck)
        {
            try
            {
                if (componentToCheck == "TeknoParrotUI")
                {
                    var releases = await GetGithubReleases(componentToCheck);
                    var latest = releases[0];
                    int uiId = 0;
                    try
                    {
                        using (RegistryKey key =
                            Registry.CurrentUser.OpenSubKey("Software\\TeknoGods\\TeknoParrot"))
                        {
                            if (key != null)
                            {
                                uiId = (int) key.GetValue("TeknoParrotUI");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                    if (latest.id != uiId)
                    {
                        GitHubUpdates windowGitHubUpdates =
                            new GitHubUpdates(componentToCheck, latest);
                        windowGitHubUpdates.Show();
                    }
                }
                else if (componentToCheck == "OpenParrot")
                {
                    //check openparrot32 first
                    var releases = await GetGithubReleases(componentToCheck);
                    int x32id = 0;
                    int x64id = 0;
                    try
                    {
                        using (RegistryKey key =
                            Registry.CurrentUser.OpenSubKey("Software\\TeknoGods\\TeknoParrot"))
                        {
                            if (key != null)
                            {
                                x32id = (int) key.GetValue("OpenParrotWin32");
                                x64id = (int) key.GetValue("OpenParrotx64");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                    for (int i = 0; i < releases.Count; i++)
                    {
                        var latest = releases[i];
                        if (latest.tag_name == "OpenParrotWin32")
                        {
                            if (latest.id != x32id)
                            {
                                GitHubUpdates windowGitHubUpdates =
                                    new GitHubUpdates(componentToCheck + "Win32", latest);
                                windowGitHubUpdates.Show();
                            }
                            else break;
                        }
                    }

                    //checking openparrot64
                    for (int i = 0; i < releases.Count; i++)
                    {
                        var latest = releases[i];
                        if (latest.tag_name == "OpenParrotx64")
                        {
                            if (latest.id != x64id)
                            {
                                GitHubUpdates windowGitHubUpdates =
                                    new GitHubUpdates(componentToCheck + "x64", latest);
                                windowGitHubUpdates.Show();
                            }
                            else break;
                        }
                    }
                }
                else
                {
                    var releases = await GetGithubReleases(componentToCheck);
                    int segaApiId = 0;
                    try
                    {
                        using (RegistryKey key =
                            Registry.CurrentUser.OpenSubKey("Software\\TeknoGods\\TeknoParrot"))
                        {
                            if (key != null)
                            {
                                segaApiId = (int) key.GetValue("OpenSegaAPI");
                            }
                        }
                    }
                    catch
                    {

                    }

                    for (int i = 0; i < releases.Count; i++)
                    {
                        var latest = releases[i];
                        if (latest.id != segaApiId)
                        {
                            GitHubUpdates windowGitHubUpdates = new GitHubUpdates(componentToCheck, latest);
                            windowGitHubUpdates.Show();
                            break;
                        }
                        else
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }

        private async void InitUpdater()
        {
            var tpUi = await GetGithubReleases("TeknoParrotUI");
            var openParrot = await GetGithubReleases("OpenParrot");
            var openSegaApi = await GetGithubReleases("OpenSegaAPI");
            int op32Id = 0;
            int op64Id = 0;
            int tpUiId = 0;

            for (int i = 0; i < tpUi.Count; i++)
            {
                var latest = tpUi[i];
                if (latest.name == Assembly.GetExecutingAssembly().GetName().Version.ToString())
                {
                    tpUiId = latest.id;
                    break;
                }
            }

            for (int i = 0; i < openParrot.Count; i++)
            {
                var latest = openParrot[i];
                if (latest.tag_name == "OpenParrotWin32")
                {
                    op32Id = latest.id;
                    break;
                }
            }
            //checking openparrot64
            for (int i = 0; i < openParrot.Count; i++)
            {
                var latest = openParrot[i];
                if (latest.tag_name == "OpenParrotx64")
                {
                    op64Id = latest.id;
                    break;
                }
            }

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\TeknoGods\TeknoParrot", true);
            key.SetValue("TeknoParrotUI", tpUiId);
            key.SetValue("OpenSegaAPI", openSegaApi[0].id);
            key.SetValue("OpenParrotWin32", op32Id);
            key.SetValue("OpenParrotx64", op64Id);
            key.Close();
        }


        /// <summary>
        /// When the window is loaded, the update checker is run and DiscordRPC is set
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
#if !DEBUG
            if (JoystickHelper.firstTime)
            {
                InitUpdater();
            }
            else if (Lazydata.ParrotData.CheckForUpdates)
            {
                CheckGitHub("TeknoParrotUI");
                CheckGitHub("OpenParrot");
                CheckGitHub("OpenSegaAPI");
            }

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                try
                {
                    string contents;
                    using (var wc = new WebClient())
                        contents = wc.DownloadString("https://teknoparrot.com/api/version");
                    if (!TeknoParrotUi.Common.UpdateChecker.CheckForUpdate(GameVersion.CurrentVersion, contents)) return;
                    if (MessageBox.Show(
                            $"There is a new version available: {contents} (currently using {GameVersion.CurrentVersion}). Would you like to download it?",
                            "New update!", MessageBoxButton.YesNo, MessageBoxImage.Information) !=
                        MessageBoxResult.Yes) return;
                    Thread.CurrentThread.IsBackground = false;
                    Process.Start("https://teknoparrot.com");
                }
                catch (Exception)
                {
                    // Ignored
                }
            }).Start();
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
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}