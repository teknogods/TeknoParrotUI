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
using System.IO.Compression;
using System.Net;

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

        public void UpdatePatronText()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                    textBlockPatron.Text = "Yes";
            }
        }

        public Library(ContentControl contentControl)
        {
            InitializeComponent();
            BitmapImage imageBitmap = new BitmapImage(new Uri(
                "pack://application:,,,/TeknoParrotUi;component/Resources/teknoparrot_by_pooterman-db9erxd.png",
                UriKind.Absolute));

            gameIcon.Source = imageBitmap;

            UpdatePatronText();

            _contentControl = contentControl;
            Joystick =  new JoystickControl(contentControl, this);
        }

        static BitmapImage defaultIcon = new BitmapImage(new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));

        static BitmapImage LoadImage(string filename)
        {
            //https://stackoverflow.com/a/13265190
            BitmapImage iconimage = new BitmapImage();

            using (var file = File.OpenRead(filename))
            {
                iconimage.BeginInit();
                iconimage.CacheOption = BitmapCacheOption.OnLoad;
                iconimage.StreamSource = file;
                iconimage.EndInit();
            }

            return iconimage;
        }

        private static bool DownloadFile(string urlAddress, string filePath)
        {
            if (File.Exists(filePath)) return true;
            Debug.WriteLine($"Downloading {filePath} from {urlAddress}");
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(urlAddress);
                request.Timeout = 5000;
                request.Proxy = null;

                using (var response = request.GetResponse().GetResponseStream())
                using (var file = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    response.CopyTo(file);
                    return true;
                }
            }
            catch (WebException wx)
            {
                var error = wx.Response as HttpWebResponse;
                if (error.StatusCode == HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"File at {urlAddress} is missing!");
                    //
                }
                // ignore
            }
            return false;
        }

        public static void UpdateIcon(string iconName, ref Image gameIcon)
        {
            var iconPath = Path.Combine("Icons", iconName);
            bool success = Lazydata.ParrotData.DownloadIcons ? DownloadFile(
                    "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" +
                    iconName, iconPath) : true;

            if (success && File.Exists(iconPath))
            {
                try
                {
                    gameIcon.Source = LoadImage(iconPath);
                }
                catch
                {
                    //delete icon since it's probably corrupted, then load default icon
                    if (File.Exists(iconPath)) File.Delete(iconPath);
                    gameIcon.Source = defaultIcon;
                }
            }
            else
            {
                gameIcon.Source = defaultIcon;
            }
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
            UpdateIcon(profile.IconName.Split('/')[1], ref gameIcon);

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
            gameInfoText.Text = $"Emulator: {selectedGame.EmulatorType}\n{(selectedGame.GameInfo == null ? "No Game Information Available" : selectedGame.GameInfo.ToString())}";
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
        public static bool ValidateAndRun(GameProfile gameProfile, out string loaderExe, out string loaderDll, bool emuOnly = false)
        {
            if (emuOnly)
            {
                loaderDll = string.Empty;
                loaderExe = string.Empty;
                return true;
            }

            loaderExe = gameProfile.Is64Bit ? ".\\OpenParrotx64\\OpenParrotLoader64.exe" : ".\\OpenParrotWin32\\OpenParrotLoader.exe";
            loaderDll = string.Empty;

            switch (gameProfile.EmulatorType)
            {
                case EmulatorType.Lindbergh:
                    loaderExe = ".\\TeknoParrot\\BudgieLoader.exe";
                    break;
                case EmulatorType.N2:
                    loaderExe = ".\\N2\\BudgieLoader.exe";
                    break;
                case EmulatorType.OpenParrot:
                    loaderDll = (gameProfile.Is64Bit ? ".\\OpenParrotx64\\OpenParrot64" : ".\\OpenParrotWin32\\OpenParrot");
                    break;
                case EmulatorType.OpenParrotKonami:
                    loaderExe = ".\\OpenParrotWin32\\OpenParrotKonamiLoader.exe";
                    break;
                default:
                    loaderDll = (gameProfile.Is64Bit ? ".\\TeknoParrot\\TeknoParrot64" : ".\\TeknoParrot\\TeknoParrot");
                    break;
            }

            if (!File.Exists(loaderExe))
            {
                MessageBox.Show($"Cannot find {loaderExe}!\nPlease re-extract TeknoParrot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (loaderDll != string.Empty && !File.Exists(loaderDll + ".dll"))
            {
                MessageBox.Show($"Cannot find {loaderDll}.dll!\nPlease re-extract TeknoParrot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrEmpty(gameProfile.GamePath))
            {
                MessageBox.Show($"Game location not set! Please set it in Game Settings.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!File.Exists(gameProfile.GamePath))
            {
                MessageBox.Show($"Cannot find game exe at: {gameProfile.GamePath}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
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

            if (!CheckiDMAC(gameProfile.GamePath, false) ||
                !CheckiDMAC(gameProfile.GamePath, true))
                return false;

            if (gameProfile.RequiresAdmin)
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    if (!admin)
                    {
                        if (!(MessageBox.Show(
                            $"Seems like you are not running TeknoParrotUI as Administrator! The game {gameProfile.GameName} requires the UI to be running as Administrator to function properly. Continue?",
                            "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)) return false;
                    }
                }
            }

            return true;
        }

        private static bool CheckiDMAC(string gamepath, bool x64)
        {
            var iDmacDrv = $"iDmacDrv{(x64 ? "64" : "32")}.dll";
            var iDmacDrvPath = Path.Combine(Path.GetDirectoryName(gamepath), iDmacDrv);
            var iDmacDrvStubPath = Path.Combine($"OpenParrot{(x64 ? "x64" : "Win32")}", iDmacDrv);

            if (!File.Exists(iDmacDrvPath)) return true;

            var description = FileVersionInfo.GetVersionInfo(iDmacDrvPath);

            if (description != null)
            {
                if (description.FileDescription == "OpenParrot" || description.FileDescription == "PCI-Express iDMAC Driver Library (DLL)")
                {
                    Debug.Write($"{iDmacDrv} passed checks");
                    return true;
                }

                // if the stub doesn't exist (updated TPUI but not OpenParrot?), just show the old messagebox
                if (!File.Exists(iDmacDrvStubPath))
                {
                    Debug.WriteLine($"{iDmacDrv} stub missing! {iDmacDrvStubPath}");
                    return (MessageBox.Show(
                            $"You seem to be using an unofficial {iDmacDrv} file! The game may crash or be unstable. Continue?",
                            "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
                }

                Debug.WriteLine($"Unofficial {iDmacDrv} found, copying {iDmacDrvStubPath} to {iDmacDrvPath}");

                // move old iDmacDrv file so people don't complain
                File.Move(iDmacDrvPath, iDmacDrvPath + ".bak");

                // copy stub dll
                File.Copy(iDmacDrvStubPath, iDmacDrvPath);

                return true;
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

            if (ValidateAndRun(gameProfile, out var loader, out var dll))
            {
                var testMenu = ChkTestMenu.IsChecked;

                var gameRunning = new GameRunning(gameProfile, loader, dll, testMenu, false, false, this);
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameRunning;
            }
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
            if (MessageBox.Show("This will download every missing icon for TeknoParrot. The file is around 50 megabytes. Are you sure you want to continue?", "Warning",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var icons = new DownloadWindow("https://github.com/teknogods/TeknoParrotUIThumbnails/archive/master.zip", "TeknoParrot Icons", true);
                    icons.Closed += (x, x2) =>
                    {
                        if (icons.data == null)
                            return;
                        using (var memoryStream = new MemoryStream(icons.data))
                        using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                        {
                            foreach (var entry in zip.Entries)
                            {
                                //remove TeknoParrotUIThumbnails-master/
                                var name = entry.FullName.Substring(entry.FullName.IndexOf('/') + 1);
                                if (string.IsNullOrEmpty(name)) continue;

                                if (File.Exists(name))
                                {
                                    Debug.WriteLine($"Skipping already existing icon {name}");
                                    continue;
                                }

                                // skip readme and folder entries
                                if (name == "README.md" || name.EndsWith("/"))
                                    continue;

                                Debug.WriteLine($"Extracting {name}");

                                try
                                {
                                    using (var entryStream = entry.Open())
                                    using (var dll = File.Create(name))
                                    {
                                        entryStream.CopyTo(dll);
                                    }
                                }
                                catch
                                {
                                    // ignore..?
                                }
                            }
                        }
                    };
                    icons.Show();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}