using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TeknoParrotUi.Common;
using TeknoParrotUi.ViewModels;
using MaterialDesignThemes.Wpf;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
    public static ParrotData _parrotData;
    UserControls.JoystickControl joystick = new UserControls.JoystickControl();
        public static Views.TeknoParrotOnline tpOnline = new Views.TeknoParrotOnline();
    
    public MainWindow()
        {
            InitializeComponent();
            LoadParrotData();
            this.contentControl.Content = new Views.Library();
            versionText.Text = GameVersion.CurrentVersion;
            this.Title = "TeknoParrot UI " + GameVersion.CurrentVersion;
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
                    _parrotData = new ParrotData();
                    Lazydata.ParrotData = _parrotData;
                    JoystickHelper.Serialize(_parrotData);
                    return;
                }
                _parrotData = JoystickHelper.DeSerialize();
                if (_parrotData == null)
                {
                    _parrotData = new ParrotData();
                    Lazydata.ParrotData = _parrotData;
                    JoystickHelper.Serialize(_parrotData);
                }
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
            this.contentControl.Content = new Views.About();
        }

        /// <summary>
        /// Loads the library screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.contentControl.Content = new Views.Library();
        }

        private void BtnSettings(object sender, RoutedEventArgs e)
        {
            //_settingsWindow.ShowDialog();
            LoadParrotData();
            //SettingsControl.LoadStuff(_parrotData);
            //EmulatorSettings.IsOpen = true;
        }

        /// <summary>
        /// Shuts down the Discord integration then quits the program, terminating any threads that may still be running.
        /// </summary>
        public static void SafeExit()
        { 
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
            joystick.StopListening();
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
            UserControls.SettingsControl settings = new UserControls.SettingsControl();
            settings.LoadStuff(_parrotData);
            this.contentControl.Content = settings;
        }

        private bool _ShowingDialog;
        private bool _AllowClose;

        /// <summary>
        /// If the window is being closed, prompts whether the user really wants to do that so it can safely shut down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            //If the user has elected to allow the close, simply let the closing event happen.
            if (_AllowClose) return;

            //NB: Because we are making an async call we need to cancel the closing event
            e.Cancel = true;

            //we are already showing the dialog, ignore
            if (_ShowingDialog) return;

            TextBlock txt1 = new TextBlock();
            txt1.HorizontalAlignment = HorizontalAlignment.Center;
            txt1.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF53B3B"));
            txt1.Margin = new Thickness(4);
            txt1.TextWrapping = TextWrapping.WrapWithOverflow;
            txt1.FontSize = 18;
            txt1.Text = "Are you sure?";

            Button btn1 = new Button();
            Style style = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
            btn1.Style = style;
            btn1.Width = 115;
            btn1.Height = 30;
            btn1.Margin = new Thickness(5);
            btn1.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
            btn1.CommandParameter = true;
            btn1.Content = "Yes";

            Button btn2 = new Button();
            Style style2 = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
            btn2.Style = style2;
            btn2.Width = 115;
            btn2.Height = 30;
            btn2.Margin = new Thickness(5);
            btn2.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
            btn2.CommandParameter = false;
            btn2.Content = "No";


            DockPanel dck = new DockPanel();
            dck.Children.Add(btn1);
            dck.Children.Add(btn2);

            StackPanel stk = new StackPanel();
            stk.Width = 250;
            stk.Children.Add(txt1);
            stk.Children.Add(dck);

            //Set flag indicating that the dialog is being shown
            _ShowingDialog = true;
            object result = await MaterialDesignThemes.Wpf.DialogHost.Show(stk);
            _ShowingDialog = false;
            //The result returned will come form the button's CommandParameter.
            //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
            if (result is bool boolResult && boolResult)
            {
                _AllowClose = true;
                joystick.StopListening();
                SafeExit();
            }
        }

        /// <summary>
        /// Same as window_closed except on the quit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //If the user has elected to allow the close, simply let the closing event happen.
            if (_AllowClose) return;

            //NB: Because we are making an async call we need to cancel the closing event
            

            //we are already showing the dialog, ignore
            if (_ShowingDialog) return;

            TextBlock txt1 = new TextBlock();
            txt1.HorizontalAlignment = HorizontalAlignment.Center;
            txt1.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF53B3B"));
            txt1.Margin = new Thickness(4);
            txt1.TextWrapping = TextWrapping.WrapWithOverflow;
            txt1.FontSize = 18;
            txt1.Text = "Are you sure?";

            Button btn1 = new Button();
            Style style = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
            btn1.Style = style;
            btn1.Width = 115;
            btn1.Height = 30;
            btn1.Margin = new Thickness(5);
            btn1.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
            btn1.CommandParameter = true;
            btn1.Content = "Yes";

            Button btn2 = new Button();
            Style style2 = Application.Current.FindResource("MaterialDesignFlatButton") as Style;
            btn2.Style = style2;
            btn2.Width = 115;
            btn2.Height = 30;
            btn2.Margin = new Thickness(5);
            btn2.Command = MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand;
            btn2.CommandParameter = false;
            btn2.Content = "No";


            DockPanel dck = new DockPanel();
            dck.Children.Add(btn1);
            dck.Children.Add(btn2);

            StackPanel stk = new StackPanel();
            stk.Width = 250;
            stk.Children.Add(txt1);
            stk.Children.Add(dck);

            //Set flag indicating that the dialog is being shown
            _ShowingDialog = true;
            object result = await MaterialDesignThemes.Wpf.DialogHost.Show(stk);
            _ShowingDialog = false;
            //The result returned will come form the button's CommandParameter.
            //If the user clicked "Yes" set the _AllowClose flag, and re-trigger the window Close.
            if (result is bool boolResult && boolResult)
            {
                _AllowClose = true;
                joystick.StopListening();
                SafeExit();
            }
        }

        /// <summary>
        /// When the window is loaded, the update checker is run and DiscordRPC is set
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                    if (UpdateChecker.CheckForUpdate(GameVersion.CurrentVersion, contents))
                    {
                        if (MessageBox.Show(
                                $"There is a new version available: {contents} (currently using {GameVersion.CurrentVersion}). Would you like to download it?",
                                "New update!", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            Thread.CurrentThread.IsBackground = false;
                            //Process.Start("https://teknoparrot.com");
                            Application.Current.Dispatcher.Invoke((Action)delegate {
                                Views.DownloadWindow update = new Views.DownloadWindow();
                                update.ShowDialog();
                            });

                        }                        
                    }
                }
                catch (Exception)
                {
                    // Ignored
                }
            }).Start();

            if (_parrotData.UseDiscordRPC)
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
            Views.AddGame addGame = new Views.AddGame();
            
            this.contentControl.Content = addGame;
        }

        /// <summary>
        /// Loads the patreon screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {

                Views.Patreon patreon = new Views.Patreon();

                this.contentControl.Content = patreon;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            this.contentControl.Content = tpOnline;
        }
    }
}
