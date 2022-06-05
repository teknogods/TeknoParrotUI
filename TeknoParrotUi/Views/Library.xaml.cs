﻿using System;
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
using TeknoParrotUi.Helpers;
using ControlzEx.Standard;
using Linearstar.Windows.RawInput;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library
    {
        //Defining variables that need to be accessed by all methods
        public JoystickControl Joystick;
        public readonly List<GameProfile> _gameNames = new List<GameProfile>();
        readonly GameSettingsControl _gameSettings = new GameSettingsControl();
        private ContentControl _contentControl;
        public bool listRefreshNeeded = false;

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

        public static BitmapImage defaultIcon = new BitmapImage(new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));

        static BitmapSource LoadImage(string filename)
        {
            //There's a weird issue on Windows 8.1 that causes a memory leak
            //this code has issues!
            var file = new FileStream(Path.GetFullPath(filename), FileMode.Open, FileAccess.Read, FileShare.Read);
            PngBitmapDecoder decoder = new PngBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
            BitmapSource bs = decoder.Frames[0];

            return bs;
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
                if (error != null && error.StatusCode == HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"File at {urlAddress} is missing!");
                }
                // ignore
            }
            catch (Exception e)
            {
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
                ChkTestMenu.ToolTip = Properties.Resources.LibraryToggleTestMode;
            }
            var selectedGame = _gameNames[gameList.SelectedIndex];
            gameInfoText.Text = $"{Properties.Resources.LibraryEmulator}: {selectedGame.EmulatorType} ({(selectedGame.Is64Bit ? "x64" : "x86")})\n{(selectedGame.GameInfo == null ? Properties.Resources.LibraryNoInfo : selectedGame.GameInfo.ToString())}";
        }

        private void resetLibrary()
        {
            gameIcon.Source = defaultIcon;
            _gameSettings.InitializeComponent();
            Joystick.InitializeComponent();
            gameInfoText.Text = "";
        }

        /// <summary>
        /// This updates the listbox when called
        /// </summary>
        public void ListUpdate(string selectGame = null)
        {
            GameProfileLoader.LoadProfiles(true);

            // Clear list
            _gameNames.Clear();
            gameList.Items.Clear();

            // Populate list
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                // third-party emulators
                var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                // check the existing user profiles
                var existing = GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.GameName == gameProfile.GameName) != null;

                var item = new ListBoxItem
                {
                    Content = gameProfile.GameName +
                                (gameProfile.Patreon ? " (Patreon)" : "") +
                                (thirdparty ? $" (Third-Party - {gameProfile.EmulatorType})" : ""),
                    Tag = gameProfile
                };

                _gameNames.Add(gameProfile);
                gameList.Items.Add(item);
            }

            // Handle focus
            if (selectGame != null)
            {
                for (int i = 0; i < gameList.Items.Count; i++)
                {
                    if (_gameNames[i].GameName == selectGame)
                        gameList.SelectedIndex = i;
                }
            }
            else if (Lazydata.ParrotData.SaveLastPlayed)
            {
                for (int i = 0; i < gameList.Items.Count; i++)
                {
                    if (_gameNames[i].GameName == Lazydata.ParrotData.LastPlayed)
                        gameList.SelectedIndex = i;
                }
            }
            else
            {
                if (gameList.Items.Count > 0)
                    gameList.SelectedIndex = 0;
            }

            gameList.Focus();

            // No games?
            if (gameList.Items.Count == 0)
            {
                if (MessageBoxHelper.InfoYesNo(Properties.Resources.LibraryNoGames))
                    Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new AddGame(_contentControl, this);
            }

            if (listRefreshNeeded && gameList.Items.Count == 0)
            {
                resetLibrary();
            }

            listRefreshNeeded = false;
        }

        /// <summary>
        /// This executes the code when the library usercontrol is loaded. ATM all it does is load the data and update the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0 || listRefreshNeeded)
                ListUpdate();

            if (Application.Current.Windows.OfType<MainWindow>().Single()._updaterComplete)
            {
                Application.Current.Windows.OfType<MainWindow>().Single().updates = new List<GitHubUpdates>();
                Application.Current.Windows.OfType<MainWindow>().Single().checkForUpdates(true);
                Application.Current.Windows.OfType<MainWindow>().Single()._updaterComplete = false;
            }
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameProfile">Input profile.</param>
        public static bool ValidateAndRun(GameProfile gameProfile, out string loaderExe, out string loaderDll, bool emuOnly, Library library, bool _test)
        {
            loaderDll = string.Empty;
            loaderExe = string.Empty;

            bool is64Bit = _test ? gameProfile.TestExecIs64Bit : gameProfile.Is64Bit;

            // don't attempt to run 64 bit game on non-64 bit OS
            if (is64Bit && !App.Is64Bit())
            {
                MessageBoxHelper.ErrorOK(Properties.Resources.Library64bit);
                return false;
            }

            if (emuOnly)
            {
                return true;
            }

            loaderExe = is64Bit ? ".\\OpenParrotx64\\OpenParrotLoader64.exe" : ".\\OpenParrotWin32\\OpenParrotLoader.exe";
            loaderDll = string.Empty;

            switch (gameProfile.EmulatorType)
            {
                case EmulatorType.Lindbergh:
                    loaderExe = ".\\TeknoParrot\\BudgieLoader.exe";
                    break;
                case EmulatorType.N2:
                    loaderExe = ".\\N2\\BudgieLoader.exe";
                    break;
                case EmulatorType.ElfLdr2:
                    loaderExe = ".\\ElfLdr2\\BudgieLoader.exe";
                    break;
                case EmulatorType.OpenParrot:
                    loaderDll = (is64Bit ? ".\\OpenParrotx64\\OpenParrot64" : ".\\OpenParrotWin32\\OpenParrot");
                    break;
                case EmulatorType.OpenParrotKonami:
                    loaderExe = ".\\OpenParrotWin32\\OpenParrotKonamiLoader.exe";
                    break;
                case EmulatorType.SegaTools:
                    File.Copy(".\\SegaTools\\aimeio.dll", Path.GetDirectoryName(gameProfile.GamePath) + "\\aimeio.dll", true);
                    File.Copy(".\\SegaTools\\idzhook.dll", Path.GetDirectoryName(gameProfile.GamePath) + "\\idzhook.dll", true);
                    File.Copy(".\\SegaTools\\idzio.dll", Path.GetDirectoryName(gameProfile.GamePath) + "\\idzio.dll", true);
                    File.Copy(".\\SegaTools\\inject.exe", Path.GetDirectoryName(gameProfile.GamePath) + "\\inject.exe", true);
                    loaderExe = ".\\SegaTools\\inject.exe";
                    loaderDll = "idzhook";
                    break;
                default:
                    loaderDll = (is64Bit ? ".\\TeknoParrot\\TeknoParrot64" : ".\\TeknoParrot\\TeknoParrot");
                    break;
            }

            if (!File.Exists(loaderExe))
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindLoader, loaderExe));
                return false;
            }

            var dll_filename = loaderDll + ".dll";
            if (loaderDll != string.Empty && !File.Exists(dll_filename) && gameProfile.EmulationProfile != EmulationProfile.SegaToolsIDZ)
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindLoader, dll_filename));
                return false;
            }

            if (string.IsNullOrEmpty(gameProfile.GamePath))
            {
                MessageBoxHelper.ErrorOK(Properties.Resources.LibraryGameLocationNotSet);
                return false;
            }

            if (!File.Exists(gameProfile.GamePath))
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, gameProfile.GamePath));
                return false;
            }

            // Check second exe
            if (gameProfile.HasTwoExecutables)
            {
                if (string.IsNullOrEmpty(gameProfile.GamePath2))
                {
                    MessageBoxHelper.ErrorOK(Properties.Resources.LibraryGameLocation2NotSet);
                    return false;
                }

                if (!File.Exists(gameProfile.GamePath2))
                {
                    MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, gameProfile.GamePath));
                    return false;
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.FastIo || gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
            {
                if (!CheckiDMAC(gameProfile.GamePath, gameProfile.Is64Bit))
                    return false;
            }

            if (gameProfile.RequiresAdmin)
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    if (!admin)
                    {
                        if (!MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryNeedsAdmin, gameProfile.GameName)))
                            return false;
                    }
                }
            }

            EmuBlacklist bl = new EmuBlacklist(gameProfile.GamePath);
            EmuBlacklist bl2 = new EmuBlacklist(gameProfile.GamePath2);

            if (bl.FoundProblem || bl2.FoundProblem)
            {
                string err = "It seems you have another emulator already in use.\nThis will most likely cause problems.";

                if (bl.FilesToRemove.Count > 0 || bl2.FilesToRemove.Count > 0)
                {
                    err += "\n\nRemove the following files:\n";
                    err += String.Join("\n", bl.FilesToRemove);
                    err += String.Join("\n", bl2.FilesToRemove);
                }

                if (bl.FilesToClean.Count > 0 || bl2.FilesToClean.Count > 0)
                {
                    err += "\n\nReplace the following patched files by the originals:\n";
                    err += String.Join("\n", bl.FilesToClean);
                    err += String.Join("\n", bl2.FilesToClean);
                }

                err += "\n\nContinue?";

                if (!MessageBoxHelper.ErrorYesNo(err))
                    return false;
            }

            if (gameProfile.InvalidFiles != null)
            {
                string[] filesToDelete = gameProfile.InvalidFiles.Split(',');
                List<string> filesThatExist = new List<string>();

                foreach (var file in filesToDelete)
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(gameProfile.GamePath), file)))
                    {
                        filesThatExist.Add(file);
                    }
                }

                if (filesThatExist.Count > 0)
                {
                    var errorMsg = Properties.Resources.LibraryInvalidFiles;
                    foreach (var fileName in filesThatExist)
                    {
                        errorMsg += fileName + Environment.NewLine;
                    }
                    errorMsg += Properties.Resources.LibraryInvalidFilesContinue;

                    if (!MessageBoxHelper.WarningYesNo(errorMsg))
                    {
                        return false;
                    }
                }
            }

            // Check raw input profile
            if (gameProfile.ConfigValues.Any(x => x.FieldName == "Input API" && x.FieldValue == "RawInput"))
            {
                bool fixedSomething = false;
                var _joystickControlRawInput = new JoystickControlRawInput();

                foreach (var t in gameProfile.JoystickButtons)
                {
                    // Binded key without device path
                    if (!string.IsNullOrWhiteSpace(t.BindNameRi) && string.IsNullOrWhiteSpace(t.RawInputButton.DevicePath))
                    {
                        Debug.WriteLine("Keybind without path: button: {0} bind: {1}", t.ButtonName, t.BindNameRi);

                        // Handle special binds first
                        if (t.BindNameRi == "Windows Mouse Cursor")
                        {
                            t.RawInputButton.DevicePath = "Windows Mouse Cursor";
                            fixedSomething = true;
                        }
                        else if (t.BindNameRi == "None")
                        {
                            t.RawInputButton.DevicePath = "None";
                            fixedSomething = true;
                        }
                        else if (t.BindNameRi.ToLower().StartsWith("unknown device"))
                        {
                            t.RawInputButton.DevicePath = "null";
                            fixedSomething = true;
                        }
                        else
                        {
                            // Find device
                            RawInputDevice device = null;

                            if (t.RawInputButton.DeviceType == RawDeviceType.Mouse)
                                device = _joystickControlRawInput.GetMouseDeviceByBindName(t.BindNameRi);
                            else if (t.RawInputButton.DeviceType == RawDeviceType.Keyboard)
                                device = _joystickControlRawInput.GetKeyboardDeviceByBindName(t.BindNameRi);

                            if (device != null)
                            {
                                Debug.WriteLine("Device found: " + device.DevicePath);
                                t.RawInputButton.DevicePath = device.DevicePath;
                                fixedSomething = true;
                            }
                            else
                            {
                                Debug.WriteLine("Could not find device!");
                            }
                        }
                    }
                }

                // Save profile and reload library
                if (fixedSomething)
                {
                    JoystickHelper.SerializeGameProfile(gameProfile);
                    library.ListUpdate(gameProfile.GameName);
                }
            }

            return true;
        }

        private static bool CheckiDMAC(string gamepath, bool x64)
        {
            var iDmacDrv = $"iDmacDrv{(x64 ? "64" : "32")}.dll";
            var iDmacDrvPath = Path.Combine(Path.GetDirectoryName(gamepath), iDmacDrv);
            var iDmacDrvBackupPath = iDmacDrvPath + ".bak";
            var iDmacDrvStubPath = Path.Combine($"OpenParrot{(x64 ? "x64" : "Win32")}", iDmacDrv);

            // if the stub doesn't exist (updated TPUI but not OpenParrot?), just show the old messagebox
            if (!File.Exists(iDmacDrvStubPath))
            {
                Debug.WriteLine($"{iDmacDrv} stub missing from {iDmacDrvStubPath}!");
                return MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryBadiDMAC, iDmacDrv));
            }

            if (!File.Exists(iDmacDrvPath))
            {
                Debug.WriteLine($"{iDmacDrv} missing, copying {iDmacDrvStubPath} to {iDmacDrvPath}");

                File.Copy(iDmacDrvStubPath, iDmacDrvPath);
                return true;
            }

            var description = FileVersionInfo.GetVersionInfo(iDmacDrvPath);

            if (description != null)
            {
                if (description.FileDescription == "OpenParrot" || description.FileDescription == "PCI-Express iDMAC Driver Library (DLL)")
                {
                    Debug.Write($"{iDmacDrv} passed checks");
                    return true;
                }

                Debug.WriteLine($"Unofficial {iDmacDrv} found, copying {iDmacDrvStubPath} to {iDmacDrvPath}");

                // delete old backup
                if (File.Exists(iDmacDrvBackupPath))
                    File.Delete(iDmacDrvBackupPath);

                // move old iDmacDrv file so people don't complain
                File.Move(iDmacDrvPath, iDmacDrvBackupPath);

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
            if (gameList.Items.Count == 0)
                return;

            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _gameSettings;
        }

        /// <summary>
        /// This button opens the controller settings option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnControllerSettings(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;
            Joystick = new JoystickControl(_contentControl, this);
            Joystick.LoadNewSettings(_gameNames[gameList.SelectedIndex], (ListBoxItem)gameList.SelectedItem);
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

            var testMenu = ChkTestMenu.IsChecked;

            if (ValidateAndRun(gameProfile, out var loader, out var dll, false, this, testMenu))
            {
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
            if (gameList.Items.Count == 0)
                return;

            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (!File.Exists(selectedGame.ValidMd5))
            {
                MessageBoxHelper.InfoOK(Properties.Resources.LibraryNoHashes);
            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content =
                    new VerifyGame(selectedGame.GamePath, selectedGame.ValidMd5);
            }
        }

        private void BtnMoreInfo(object sender, RoutedEventArgs e)
        {
            string path = string.Empty;

            if (gameList.Items.Count != 0)
            {
                var selectedGame = _gameNames[gameList.SelectedIndex];

                // open game compatibility page
                if (selectedGame != null)
                {
                    path = "compatibility/" + Path.GetFileNameWithoutExtension(selectedGame.FileName) + ".htm";
                }
            }

            var url = "https://teknogods.github.io/" + path;
            Debug.WriteLine($"opening {url}");
            Process.Start(url);
        }

        private void BtnDownloadMissingIcons(object sender, RoutedEventArgs e)
        {
            if (MessageBoxHelper.WarningYesNo(Properties.Resources.LibraryDownloadAllIcons))
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