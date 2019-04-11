using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using Microsoft.Win32;
using TeknoParrotUi.UserControls;
using System.Security.Principal;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library
    {
        //Defining variables that need to be accessed by all methods
        public JoystickControl Joystick;
        readonly List<GameProfile> _gameNames = new List<GameProfile>();
        readonly GameSettingsControl _gameSettings = new GameSettingsControl();
        private ContentControl _contentControl;
        private int _listIndex = 0;

        public Library(ContentControl contentControl)
        {
            InitializeComponent();
            BitmapImage imageBitmap = new BitmapImage(new Uri(
                "pack://application:,,,/TeknoParrotUi;component/Resources/teknoparrot_by_pooterman-db9erxd.png",
                UriKind.Absolute));

            image1.Source = imageBitmap;

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                    textBlockPatron.Text = "Yes";
            }

            _contentControl = contentControl;
            Joystick =  new JoystickControl(contentControl, this);
        }

        /// <summary>
        /// When the selection in the listbox is changed, this is run. It loads in the currently selected game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;
            var modifyItem = (ListBoxItem) ((ListBox) sender).SelectedItem;
            var profile = _gameNames[gameList.SelectedIndex];
            var icon = profile.IconName;
            var imageBitmap = new BitmapImage(File.Exists(icon)
                ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute)
                : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            image1.Source = imageBitmap;
            _gameSettings.LoadNewSettings(profile, modifyItem, _contentControl, this);
            Joystick.LoadNewSettings(profile, modifyItem);
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
            var selectedGame = _gameNames[gameList.SelectedIndex];
            gameInfoText.Text = $"Emulator: {selectedGame.EmulatorType}\n{selectedGame.GameInfo.SmallText}";
        }

        /// <summary>
        /// This updates the listbox when called
        /// </summary>
        public void ListUpdate(bool fromAddGame)
        {
            GameProfileLoader.LoadProfiles(true);
            gameList.SelectedIndex = _listIndex;
            _gameNames.Clear();
            gameList.Items.Clear();
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameName + (gameProfile.Patreon ? " (Patreon Only)" : string.Empty),
                    Tag = gameProfile
                };

                gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(gameProfile.FileName);

                _gameNames.Add(gameProfile);
                gameList.Items.Add(item);

                if (fromAddGame || (Lazydata.ParrotData.SaveLastPlayed && gameProfile.GameName == Lazydata.ParrotData.LastPlayed))
                {
                    gameList.SelectedItem = item;
                    gameList.Focus();
                }
                else
                {
                    gameList.SelectedIndex = _listIndex;
                    gameList.Focus();
                }
            }

            if (gameList.Items.Count != 0) return;
            if (MessageBox.Show("Looks like you have no games set up. Do you want to add one now?",
                    "No games found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new AddGame(_contentControl, this);
            }
        }

        /// <summary>
        /// This executes the code when the library usercontrol is loaded. ATM all it does is load the data and update the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ListUpdate(false);
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameProfile">Input profile.</param>
        /// <param name="testMenuString">Command to run test menu.</param>
        /// <param name="exeName">Test menu exe name.</param>
        private void ValidateAndRun(GameProfile gameProfile, string testMenuString, string exeName = "")
        {
            if (!ValidateGameRun(gameProfile))
                return;

            var testMenu = ChkTestMenu.IsChecked;

            var gameRunning = new GameRunning(gameProfile, testMenu, testMenuString,
                gameProfile.TestMenuIsExecutable, exeName, false, false, this);
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameRunning;
        }

        static readonly List<string> RequiredFiles = new List<string>
        {
            ".\\OpenParrotWin32\\OpenParrot.dll",
            ".\\OpenParrotx64\\OpenParrot64.dll",
            ".\\TeknoParrot\\TeknoParrot.dll",
            ".\\OpenParrotWin32\\OpenParrotLoader.exe",
            ".\\OpenParrotx64\\OpenParrotLoader64.exe",
            ".\\TeknoParrot\\BudgieLoader.exe"
        };

        private bool CheckiDMAC(string gamepath, string idmac)
        {
            var iDmacDrvPath = Path.Combine(Path.GetDirectoryName(gamepath), idmac);

            if (!File.Exists(iDmacDrvPath)) return true;

            var description = FileVersionInfo.GetVersionInfo(iDmacDrvPath);
            if (description != null && description.FileDescription != "PCI-Express iDMAC Driver Library (DLL)")
            {
                return (MessageBox.Show(
                            $"You seem to be using an unofficial {idmac} file! This game may crash or be unstable. Continue?",
                            "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
            }
            return true;
        }

        /// <summary>
        /// This validates that the game can be run, checking for stuff like other emulators, incorrect files and administrator privledges
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

            if (EmuBlacklist.CheckBlacklist(
                Directory.GetFiles(Path.GetDirectoryName(gameProfile.GamePath) ??
                                   throw new InvalidOperationException())))
            {
                var errorMsg =
                    $"Hold it right there!{Environment.NewLine}it seems you have other emulator already in use.{Environment.NewLine}Please remove the following files from the game directory:{Environment.NewLine}";
                foreach (var fileName in EmuBlacklist.Blacklist)
                {
                    errorMsg += fileName + Environment.NewLine;
                }

                MessageBox.Show(errorMsg, "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!CheckiDMAC(gameProfile.GamePath, "iDmacDrv32.dll") || 
                !CheckiDMAC(gameProfile.GamePath, "iDmacDrv64.dll"))
                return false;

            if (gameProfile.RequiresAdmin)
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    if (!admin)
                    {
                        return (MessageBox.Show(
                            $"Seems like you are not running TeknoParrotUI as Administrator! The game {gameProfile.GameName} requires the UI to be running as Administrator to function properly. Continue?",
                            "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// This button opens the game settings window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnGameSettings(object sender, RoutedEventArgs e)
        {
            _listIndex = gameList.SelectedIndex;
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _gameSettings;
        }

        /// <summary>
        /// This button opens the controller settings option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnControllerSettings(object sender, RoutedEventArgs e)
        {
            _listIndex = gameList.SelectedIndex;
            Joystick.Listen();
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = Joystick;
        }

        /// <summary>
        /// This button actually launches the game selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnLaunchGame(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile) ((ListBoxItem) gameList.SelectedItem).Tag;

            if (Lazydata.ParrotData.SaveLastPlayed)
            {
                Lazydata.ParrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize();
            }

            var testMenuExe = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuParameter : "";

            var testStr = gameProfile.TestMenuIsExecutable
                ? gameProfile.TestMenuExtraParameters
                : gameProfile.TestMenuParameter;

            ValidateAndRun(gameProfile, testStr, testMenuExe);
        }

        /// <summary>
        /// This starts the MD5 verifier that checks whether a game is a clean dump
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnVerifyGame(object sender, RoutedEventArgs e)
        {
            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (!File.Exists(selectedGame.ValidMd5))
            {
                MessageBox.Show(
                    "It appears that you are trying to verify a game that doesn't have a clean file hash list yet. ");
            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content =
                    new VerifyGame(selectedGame.GamePath, selectedGame.ValidMd5);
            }
        }

        private void BtnMoreInfo(object sender, RoutedEventArgs e)
        {
            Process.Start("https://wiki.teknoparrot.com/");
        }

        private void BtnDownloadMissingIcons(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < gameList.Items.Count; i++)
                {
                    if (File.Exists(_gameNames[i].IconName)) continue;
                    var update =
                        new DownloadWindow(
                            "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/" +
                            _gameNames[i].IconName, _gameNames[i].IconName);
                    update.ShowDialog();
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}