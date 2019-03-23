using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static ParrotData ParrotData;
        public static TeknoParrotOnline TpOnline = new TeknoParrotOnline();
        private readonly About _about = new About();
        private readonly Library _library;
        private readonly Patreon _patron = new Patreon();
        private readonly AddGame _addGame = new AddGame();
        private bool _showingDialog;
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();
            LoadParrotData();
            IconCheck();
            _library = new Library(contentControl);
            contentControl.Content = _library;
            versionText.Text = GameVersion.CurrentVersion;
            Title = "TeknoParrot UI " + GameVersion.CurrentVersion;
        }

        public void IconCheck()
        {
            Directory.CreateDirectory("Icons");
        }

        /// <summary>
        /// Loads data from ParrotData.xml
        /// </summary>
        public void LoadParrotData()
        {
            try
            {
                if (!File.Exists("ParrotData.xml"))
                {
                    MessageBox.Show("Seems this is first time you are running me, please set emulation settings.",
                        "Hello World", MessageBoxButton.OK, MessageBoxImage.Information);
                    ParrotData = new ParrotData();
                    Lazydata.ParrotData = ParrotData;
                    JoystickHelper.Serialize(ParrotData);
                    return;
                }

                ParrotData = JoystickHelper.DeSerialize();
                if (ParrotData != null) return;
                ParrotData = new ParrotData();
                Lazydata.ParrotData = ParrotData;
                JoystickHelper.Serialize(ParrotData);
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"Exception happened during loading ParrotData.xml! Generate new one by saving!{Environment.NewLine}{Environment.NewLine}{e}",
                    "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads the about screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _about;
        }

        /// <summary>
        /// Loads the library screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _library;
        }

        private void BtnSettings(object sender, RoutedEventArgs e)
        {
            LoadParrotData();
        }

        /// <summary>
        /// Shuts down the Discord integration then quits the program, terminating any threads that may still be running.
        /// </summary>
        public static void SafeExit()
        {
            if (ParrotData.UseDiscordRPC && File.Exists("discord-rpc.dll"))
                DiscordRPC.Shutdown();
            Environment.Exit(0);
        }

        /// <summary>
        /// Terminates the joystick listener if it's still running then safely exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnQuit(object sender, RoutedEventArgs e)
        {
            _library.Joystick.StopListening();
            SafeExit();
        }


        /// <summary>
        /// Loads the settings screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //_settingsWindow.ShowDialog();
            LoadParrotData();
            var settings = new UserControls.SettingsControl();
            settings.LoadStuff(ParrotData);
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

            var txt1 = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#FFF53B3B")),
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

            var stk = new StackPanel {Width = 250};
            stk.Children.Add(txt1);
            stk.Children.Add(dck);

            //Set flag indicating that the dialog is being shown
            _showingDialog = true;
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(stk);
            _showingDialog = false;
            //The result returned will come form the button's CommandParameter.
            //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
            if (!(result is bool boolResult) || !boolResult) return;
            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        /// <summary>
        /// Same as window_closed except on the quit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //If the user has elected to allow the close, simply let the closing event happen.
            if (_allowClose) return;

            //NB: Because we are making an async call we need to cancel the closing event


            //we are already showing the dialog, ignore
            if (_showingDialog) return;

            var txt1 = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#FFF53B3B")),
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
            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        /// <summary>
        /// When the window is loaded, the update checker is run and DiscordRPC is set
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("Updater disabled because this is a debug build.");
#else
            new Thread(() =>
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Thread.CurrentThread.IsBackground = true;
                try
                {
                    string contents;
                    using (var wc = new WebClient())
                        contents = wc.DownloadString("https://teknoparrot.com/api/version");
                    if (!UpdateChecker.CheckForUpdate(GameVersion.CurrentVersion, contents)) return;
                    if (MessageBox.Show(
                            $"There is a new version available: {contents} (currently using {GameVersion.CurrentVersion}). Would you like to download it?",
                            "New update!", MessageBoxButton.YesNo, MessageBoxImage.Information) !=
                        MessageBoxResult.Yes) return;
                    Thread.CurrentThread.IsBackground = false;
                    Process.Start("https://teknoparrot.com");
                    //Application.Current.Dispatcher.Invoke((Action)delegate {
                    //    var update = new DownloadWindow("https://teknoparrot.com/files/TeknoParrot_" + contents + ".zip", Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip", true);
                    //    update.ShowDialog();
                    //});
                }
                catch (Exception)
                {
                    // Ignored
                }
            }).Start();
#endif

            if (ParrotData.UseDiscordRPC)
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
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _addGame;
        }

        /// <summary>
        /// Loads the patreon screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _patron;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            contentControl.Content = TpOnline;
        }

        private void ColorZone_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Button_Click_3(sender, e);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}