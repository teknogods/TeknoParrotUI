using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TeknoParrotUi.Common;
using Microsoft.Win32;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library : UserControl
    {
        //Defining variables that need to be accessed by all methods
        public UserControls.JoystickControl joystick = new UserControls.JoystickControl();
        List<GameProfile> gameNames = new List<GameProfile>();
        UserControls.GameSettingsControl gameSettings = new UserControls.GameSettingsControl();

        public Library()
        {
            InitializeComponent();
            BitmapImage imageBitmap = new BitmapImage(new Uri("pack://application:,,,/TeknoParrotUi;component/Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Absolute));
            
            image1.Source = imageBitmap;

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatreon = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatreon)
                    textBlockPatron.Text = "Yes";
            }
        }

        /// <summary>
        /// When the selection in the listbox is changed, this is run. It loads in the currently selected game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var modifyItem = (ListBoxItem)((ListBox)sender).SelectedItem;
            var profile = gameNames[gameList.SelectedIndex];
            var icon = profile.IconName;
            BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            image1.Source = imageBitmap;
            gameInfoText.Text = gameNames[gameList.SelectedIndex].Description;
            gameSettings.LoadNewSettings(profile, modifyItem);
            joystick.LoadNewSettings(profile, modifyItem, MainWindow._parrotData);
            if (!profile.HasSeparateTestMode)
            {
                ChkTestMenu.IsChecked = false;
                ChkTestMenu.IsEnabled = false;
            }
            else
            {
                ChkTestMenu.IsEnabled = true;
                ChkTestMenu.ToolTip = "Enable or disable test mode.";
            }
        }

        /// <summary>
        /// This updates the listbox when called
        /// </summary>
        private void listUpdate()
        {
            gameList.Items.Clear();
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };
                gameNames.Add(gameProfile);
                gameList.Items.Add(item);

                if (MainWindow._parrotData.SaveLastPlayed && gameProfile.GameName == MainWindow._parrotData.LastPlayed)
                {
                    gameList.SelectedItem = item;
                }
                else
                {
                    gameList.SelectedIndex = 0;
                }
            }
            if (gameList.Items.Count == 0)
            {
                if (MessageBox.Show("Looks like you have no games set up. Do you want to add one now?", "No games found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new AddGame();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }

        }

        /// <summary>
        /// This executes the code when the library usercontrol is loaded. ATM all it does is load the data and update the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().LoadParrotData();
            listUpdate(); 
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameLocation">Game executable location.</param>
        /// <param name="gameProfile">Input profile.</param>
        /// <param name="testMenuString">Command to run test menu.</param>
        /// <param name="isSinglePlayer">Init only first player controller.</param>
        /// <param name="testMenuIsExe">If uses separate exe.</param>
        /// <param name="exeName">Test menu exe name.</param>
        private void ValidateAndRun(GameProfile gameProfile, string testMenuString, string exeName = "")
        {
            if (!ValidateGameRun(gameProfile))
                return;

            var testMenu = ChkTestMenu.IsChecked;

            var gameRunning = new TeknoParrotUi.Views.GameRunning(gameProfile, testMenu, MainWindow._parrotData, testMenuString, gameProfile.TestMenuIsExecutable, exeName);
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameRunning;
        }

        static List<string> RequiredFiles = new List<string>
        {
            "OpenParrot.dll",
            "OpenParrot64.dll",
            "TeknoParrot.dll",
            "TeknoParrot64.dll",
            "OpenParrotLoader.exe",
            "OpenParrotLoader64.exe",
            "ParrotLoader.exe",
            "ParrotLoader64.exe",
            "BudgieLoader.exe"
        };

        /// <summary>
        /// This validates that the game can be run, checking for stuff like other emulators and incorrect files
        /// </summary>
        /// <param name="gameProfile"></param>
        /// <returns></returns>
        private bool ValidateGameRun(GameProfile gameProfile)
        {
            if (!File.Exists(gameProfile.GamePath))
            {
                MessageBox.Show($"Cannot find game exe at: {gameProfile.GamePath}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            foreach (var file in RequiredFiles)
            {
                if (!File.Exists(file))
                {
                    MessageBox.Show($"Cannot find {file}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            if (EmuBlacklist.CheckForBlacklist(Directory.GetFiles(Path.GetDirectoryName(gameProfile.GamePath))))
            {
                var errorMsg =
                    "Hold it right there!" + Environment.NewLine + "it seems you have other emulator already in use." + Environment.NewLine + "Please remove the following files from the game directory:" + Environment.NewLine;
                foreach (var fileName in EmuBlacklist.BlacklistedList)
                {
                    errorMsg += fileName + Environment.NewLine;
                }
                MessageBox.Show(errorMsg, "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (File.Exists(System.IO.Path.Combine(gameProfile.GamePath, "iDmacDrv32.dll")))
            {
                var description = FileVersionInfo.GetVersionInfo("iDmacDrv32.dll");
                if (description.FileDescription != "PCI-Express iDMAC Driver Library (DLL)")
                {
                    return (MessageBox.Show("You seem to be using an unofficial iDmacDrv32.dll file! This game may crash or be unstable. Continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
                }
            }

            return true;
        }

        /// <summary>
        /// This button opens the game settings window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameSettings;
        }

        /// <summary>
        /// This button opens the controller settings option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            joystick.Listen();
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = joystick;
        }

        /// <summary>
        /// This button actually launches the game selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            if (MainWindow._parrotData.SaveLastPlayed)
            {
                MainWindow._parrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize(MainWindow._parrotData);
            }

            var testMenuExe = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuParameter : "";

            var testStr = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuExtraParameters : gameProfile.TestMenuParameter;

            ValidateAndRun(gameProfile, testStr, testMenuExe);
        }

        /// <summary>
        /// This starts the MD5 verifier that checks whether a game is a clean dump
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new VerifyGame(gameNames[gameList.SelectedIndex].GamePath, gameNames[gameList.SelectedIndex].ValidMd5);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            Process.Start("https://wiki.teknoparrot.com/");
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < gameList.Items.Count; i++)
                {
                    if (!File.Exists(gameNames[i].IconName))
                    {
                        DownloadWindow update = new Views.DownloadWindow("https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/" + gameNames[i].IconName, gameNames[i].IconName, false);
                        update.ShowDialog();
                    }
                }
            }
            catch
            {
                
            }
        }
    }
}
