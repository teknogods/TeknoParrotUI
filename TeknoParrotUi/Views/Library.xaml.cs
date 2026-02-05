using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
using ControlzEx;
using Linearstar.Windows.RawInput;
using TeknoParrotUi.Properties;
using SharpDX.XInput;

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
        public static bool firstBoot = true;
        private string _searchText = string.Empty;
        private DispatcherTimer _searchDebounceTimer;
        private bool _isSearchUpdate = false;
        private string _savedSelection = null;
        private Window _highScoreWindow;

        public static BitmapImage defaultIcon = new BitmapImage(new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));

        public Library(ContentControl contentControl)
        {
            InitializeComponent();
            gameIcon.Source = defaultIcon;
            _contentControl = contentControl;
            Joystick = new JoystickControl(contentControl, this);
            InitializeGenreComboBox();
            InitializeSearchDebounceTimer();
        }

        private void InitializeSearchDebounceTimer()
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                _isSearchUpdate = true;
                ListUpdate();
                _isSearchUpdate = false;
            };
        }

        private void InitializeGenreComboBox()
        {
            var genreItems = TeknoParrotUi.Helpers.GenreTranslationHelper.GetGenreItems(false);
            GenreBox.ItemsSource = genreItems;
            GenreBox.SelectedIndex = 0;
        }

        static BitmapSource LoadImage(string filename)
        {
            using (var file = new FileStream(Path.GetFullPath(filename), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PngBitmapDecoder decoder = new PngBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource bs = decoder.Frames[0];
                bs.Freeze();
                return bs;
            }
        }

        private static bool DownloadFile(string urlAddress, string filePath)
        {
            if (File.Exists(filePath)) return true;
            Debug.WriteLine($"Downloading {filePath} from {urlAddress}");
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(urlAddress);
                request.CachePolicy = new System.Net.Cache.HttpRequestCachePolicy(System.Net.Cache.HttpRequestCacheLevel.NoCacheNoStore);
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
            catch (Exception)
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

            // Close high score window if it's open when selecting a different game
            if (_highScoreWindow != null && _highScoreWindow.IsLoaded)
            {
                _highScoreWindow.Close();
                _highScoreWindow = null;
            }

            var modifyItem = (ListBoxItem)((ListBox)sender).SelectedItem;
            var profile = _gameNames[gameList.SelectedIndex];
            UpdateIcon(profile.IconName.Split('/')[1], ref gameIcon);

            _gameSettings.LoadNewSettings(profile, modifyItem, _contentControl, this);
            Joystick.LoadNewSettings(profile, modifyItem);
            if (!profile.HasSeparateTestMode)
            {
                testMenuButton.IsEnabled = false;
                testMenuButton.ToolTip = "Test menu accessed ingame via buttons or not available";
            }
            else
            {
                testMenuButton.IsEnabled = true;
                testMenuButton.ToolTip = TeknoParrotUi.Properties.Resources.LibraryToggleTestMode;
            }
            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (selectedGame.OnlineProfileURL != "")
            {
                gameOnlineProfileButton.Visibility = Visibility.Visible;
            }
            else
            {
                gameOnlineProfileButton.Visibility = Visibility.Hidden;
            }

            // Check online titles and show button if required
            if (selectedGame.HasTpoSupport)
            {
                playOnlineButton.Visibility = Visibility.Visible;
            }
            else
            {
                playOnlineButton.Visibility = Visibility.Hidden;
            }

            if (selectedGame.HasTpoSupport && selectedGame.OnlineProfileURL == "")
            {
                Grid.SetRow(playOnlineButton, 5);
            }
            else
            {
                Grid.SetRow(playOnlineButton, 4);
            }

            if (selectedGame.IsTpoExclusive)
            {
                gameLaunchButton.IsEnabled = false;
            }
            else
            {
                gameLaunchButton.IsEnabled = true;
            }

            var basicInfo = $"{Properties.Resources.LibraryEmulator}: {selectedGame.EmulatorType} ({(selectedGame.Is64Bit ? "x64" : "x86")})\n";

            if (selectedGame.GameInfo != null)
            {
                basicInfo += selectedGame.GameInfo.ToString();
                gpuCompatibilityDisplay.SetGpuStatus(selectedGame.GameInfo.nvidia, selectedGame.GameInfo.amd, selectedGame.GameInfo.intel);
            }
            else
            {
                basicInfo += Properties.Resources.LibraryNoInfo;
                gpuCompatibilityDisplay.SetGpuStatus(GPUSTATUS.NO_INFO, GPUSTATUS.NO_INFO, GPUSTATUS.NO_INFO);
            }

            gameInfoText.Text = basicInfo;
            delGame.IsEnabled = true;

            if (!string.IsNullOrWhiteSpace(_searchText) && !_isSearchUpdate)
            {
                _savedSelection = selectedGame.GameNameInternal;
            }

            // Generate URL from ProfileName (hardcoded mapping)
            string highScoreUrl = GetHighScoreUrlForProfile(selectedGame.ProfileName);

            if (!string.IsNullOrEmpty(highScoreUrl) && IsValidHighScoreUrl(highScoreUrl))
            {
                highScoreButton.IsEnabled = true;
                highScoreButton.Visibility = Visibility.Visible;
                highScoreButton.ToolTip = "View High Scores";
            }
            else
            {
                highScoreButton.IsEnabled = false;
                highScoreButton.Visibility = Visibility.Collapsed;
                highScoreButton.ToolTip = null;
            }
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
            if (!firstBoot)
            {
                GameProfileLoader.LoadProfiles(true);
            }
            else
            {
                firstBoot = false;
            }

            _gameNames.Clear();

            if (gameList != null)
            {
                gameList.Items.Clear();

                string selectedInternalGenre = "All";
                if (GenreBox != null && GenreBox.SelectedItem != null)
                {
                    var genreItem = GenreBox.SelectedItem as TeknoParrotUi.Helpers.GenreItem;
                    selectedInternalGenre = genreItem?.InternalName ?? "All";
                }

                foreach (var gameProfile in GameProfileLoader.UserProfiles)
                {
                    var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                    // Use the translation helper to check if the game matches the selected genre
                    bool matchesGenre = TeknoParrotUi.Helpers.GenreTranslationHelper.DoesGameMatchGenre(selectedInternalGenre, gameProfile);

                    if (!matchesGenre)
                        continue;

                    // Filter by search text if present
                    if (!string.IsNullOrWhiteSpace(_searchText))
                    {
                        bool matchesSearch = gameProfile.GameNameInternal.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!matchesSearch)
                            continue;
                    }

                    var item = new ListBoxItem
                    {
                        Content = gameProfile.GameNameInternal +
                                    (gameProfile.Patreon ? TeknoParrotUi.Properties.Resources.LibrarySubscriptionSuffix : "") +
                                    (thirdparty ? string.Format(TeknoParrotUi.Properties.Resources.LibraryThirdPartySuffix, gameProfile.EmulatorType) : ""),
                        Tag = gameProfile
                    };

                    _gameNames.Add(gameProfile);
                    gameList.Items.Add(item);
                }

                // Rest of the method remains the same...
                if (selectGame != null)
                {
                    for (int i = 0; i < gameList.Items.Count; i++)
                    {
                        if (_gameNames[i].GameNameInternal == selectGame)
                            gameList.SelectedIndex = i;
                    }
                }
                else if (Lazydata.ParrotData.SaveLastPlayed)
                {
                    for (int i = 0; i < gameList.Items.Count; i++)
                    {
                        if (_gameNames[i].GameNameInternal == Lazydata.ParrotData.LastPlayed)
                            gameList.SelectedIndex = i;
                    }
                }
                else
                {
                    if (gameList.Items.Count > 0)
                        gameList.SelectedIndex = 0;
                }

                if (!_isSearchUpdate)
                {
                    gameList.Focus();
                }
                if (gameList.SelectedItem != null)
                {
                    try
                    {
                        gameList.ScrollIntoView(gameList.SelectedItem);
                    }
                    catch
                    {
                        // do nothing
                    }
                }

                if (gameList.Items.Count == 0 && GameProfileLoader.UserProfiles.Count == 0)
                {
                    if (MessageBoxHelper.InfoYesNo(Properties.Resources.LibraryNoGames))
                        Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new SetupWizard(_contentControl, this);
                }
            }

            if (gameList != null && listRefreshNeeded && gameList.Items.Count == 0)
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
                Application.Current.Windows.OfType<MainWindow>().Single().checkForUpdates(true, false);
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
                case EmulatorType.Dolphin:
                    loaderExe = ".\\CrediarDolphin\\Dolphin.exe";
                    break;
                case EmulatorType.Play:
                    loaderExe = ".\\Play\\Play.exe";
                    break;
                case EmulatorType.RPCS3:
                    loaderExe = ".\\RPCS3\\rpcs3.exe";
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
                if (gameProfile.ProfileName != "tatsuvscap")
                {
                    MessageBoxHelper.ErrorOK(Properties.Resources.LibraryGameLocationNotSet);
                    return false;
                }
            }

            if (!File.Exists(gameProfile.GamePath))
            {
                if (gameProfile.ProfileName != "tatsuvscap")
                {
                    MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, gameProfile.GamePath));
                    return false;
                }
            }

            if (gameProfile.ProfileName == "tatsuvscap")
            {
                if (!File.Exists(".\\CrediarDolphin\\User\\Wii\\title\\00000001\\00000002\\data\\RVA.txt"))
                {
                    MessageBoxHelper.ErrorOK(Properties.Resources.LibraryTatsuvscapDataNotFound);
                    return false;
                }
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

            if (gameProfile.EmulatorType == EmulatorType.Play)
            {
                var result = CheckPlay(gameProfile.GamePath, gameProfile.ProfileName);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, result));
                    return false;
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.FastIo || gameProfile.EmulationProfile == EmulationProfile.Theatrhythm || gameProfile.EmulationProfile == EmulationProfile.NxL2 || gameProfile.EmulationProfile == EmulationProfile.GunslingerStratos3)
            {
                if (!CheckiDMAC(gameProfile.GamePath, gameProfile.Is64Bit))
                    return false;
            }

            if (gameProfile.RequiresBepInEx)
            {
                if (!CheckBepinEx(gameProfile.GamePath, gameProfile.Is64Bit))
                {
                    {
                        return false;
                    }
                }
            }

            if (gameProfile.Requires4GBPatch)
            {
                if (!Helpers.PEPatcher.IsLargeAddressAware(gameProfile.GamePath))
                {
                    if (MessageBoxHelper.WarningYesNo(Properties.Resources.LibraryNeeds4GBPatch))
                    {
                        if (!Helpers.PEPatcher.ApplyLargeAddressAwarePatch(gameProfile.GamePath))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        MessageBoxHelper.InfoOK(Properties.Resources.LibraryLaunchCancelled4GBPatch);
                        return false;
                    }
                }
            }

            if (gameProfile.FileName.Contains("PullTheTrigger.xml"))
            {
                if (!CheckPTTDll(gameProfile.GamePath))
                {
                    return false;
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.IncredibleTechnologies)
            {
                var autoCreateDb = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Automatically create Database")?.FieldValue;
                if (autoCreateDb == "1")
                {
                    if (!CheckPostgresDatabase(gameProfile))
                    {
                        return false;
                    }
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.NxL2)
            {
                if (!CheckNxl2Core(gameProfile.GamePath))
                {
                    return false;
                }
            }

            if (gameProfile.EmulatorType == EmulatorType.RPCS3)
            {
                if (!CheckRpcs3(gameProfile.GamePath, gameProfile.ProfileName))
                {
                    return false;
                }
            }

            //For banapass support (ie don't do this if banapass support is unchecked.)
            if (gameProfile.GameNameInternal == "Wangan Midnight Maximum Tune 6" && gameProfile.ConfigValues.Find(x => x.FieldName == "Banapass Connection").FieldValue == "1")
            {
                if (!checkbngrw(gameProfile.GamePath))
                    return false;
            }

            if (gameProfile.RequiresAdmin)
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    if (!admin)
                    {
                        if (!MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryNeedsAdmin, gameProfile.GameNameInternal)))
                            return false;
                    }
                }
            }

            if (gameProfile.ProfileName != "tatsuvscap")
            {
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

                    err += "\n\nTry to start it anyway?";

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
                    library.ListUpdate(gameProfile.GameNameInternal);
                }
            }

            return true;
        }

        private static bool CheckRpcs3(string gamePath, string profileName)
        {
            var currentDir = Path.Combine(Directory.GetCurrentDirectory(), "RPCS3");
            var firmwareVersion = Path.Combine(currentDir, "dev_flash", "vsh", "etc", "version.txt");
            if (!File.Exists(firmwareVersion))
            {
                if (MessageBoxHelper.WarningYesNo("RPCS3 Firmware is not installed, want to install it now?"))
                {
                    OpenFileDialog ofd = new OpenFileDialog
                    {
                        Title = "Select PS3 Firmware .pup file",
                        Filter = "PS3 Firmware .pup|*.pup",
                        CheckFileExists = true,
                        CheckPathExists = true,
                        Multiselect = false
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        // CRC Check firmware here
                        var parameters = new List<string>();
                        parameters.Add($"--installfw \"{ofd.FileName}\"");
                        ProcessStartInfo info;
                        var rpcs3Parameters = string.Join(" ", parameters);
                        info = new ProcessStartInfo(@".\RPCS3\rpcs3.exe", rpcs3Parameters);
                        info.UseShellExecute = false;
                        info.WorkingDirectory = currentDir ?? throw new InvalidOperationException();
                        var cmdProcess = new Process
                        {
                            StartInfo = info
                        };
                        cmdProcess.Start();
                        cmdProcess.WaitForExit();

                        if (!File.Exists(firmwareVersion))
                        {
                            MessageBoxHelper.ErrorOK("Firmware installation failed, please try again...");
                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }

            var folderList = new List<string> { "dev_usb000", "dev_usb001", "dev_hdd0", "dev_hdd1" };
            foreach (string folderName in folderList)
            {
                var finalFolder = Path.Combine(currentDir, profileName, folderName);
                Directory.CreateDirectory(finalFolder);
            }

            var cachesFolder = Path.Combine(currentDir, profileName, "dev_hdd1", "caches");
            Directory.CreateDirectory(cachesFolder);

            var gameConfigs = new Dictionary<string, (string configName, byte seriesVersion, byte year, byte taikoArchiveVersion, byte[] hardwareId, byte hardwareRevision, byte configChecksum)>
            {
                { "ttt2u", ("ttt2u", 0, 0, 0, new byte[] { 0x43, 0x50 }, 0x24, 0x68) },
                { "ttt2", ("ttt2", 0, 0, 0, new byte[] { 0x43, 0x50 }, 0x24, 0x68) },
                { "Tekken6", ("Tekken6", 0, 0, 0, null, 0, 0) },
                { "Tekken6BR", ("Tekken6BR", 0, 0, 0, null, 0, 0) },
                { "RazingStorm", ("RazingStorm", 0, 0, 0, null, 0, 0) },
                { "DSPS", ("dsps", 0, 0, 0, null, 0, 0) },
                { "dbzenkai", ("dbzenkai", 0, 0, 0, null, 0, 0) },
                { "AKB48", ("akb48", 0, 0, 0, new byte[] { 0x43, 0x50 }, 0x54, 0x4E) },
                { "DarkEscape4D", ("darkescape4d", 0, 0, 0, new byte[] { 0x31, 0x4C }, 0xB0, 0xE9) },
                // Taiko Time
                { "taikogreen", ("taikogreen", 0x0B, 0x19, 0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikoblue", ("taikoblue", 0x0A, 0x19, 0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikoyellow", ("taikoyellow", 0x09, 0x19, 0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikored", ("taikored", 0x08, 0x19, 0x0A, new byte[] { 0x31, 0x4c }, 0xA7, 0x9B) },
                { "taikowhite", ("taikowhite", 0x07, 0x19, 0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikomurasaki", ("taikomurasaki", 0x06, 0x19,0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikokimidori", ("taikokimidori", 0x05, 0x13, 0x0A, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikomomoiro", ("taikomomoiro", 0x04, 0x13, 0x08, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikosorairo", ("taikosorairo", 0x03, 0x13, 0x08, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                { "taikokatsudon", ("taikokatsudon", 0x02, 0x11, 0x08, new byte[] { 0x43, 0x50 }, 0xA7, 0x9B) },
                // Wadaiko Master = Brazilian release of momoiro
                { "wadaikomaster", ("wadaikomaster", 0x04, 0x13, 0x0A, new byte[] { 0x83, 0x20 }, 0xA7, 0x9B) },
            };

            if (gameConfigs.TryGetValue(profileName, out var config))
            {
                if (!SetupGameConfig(currentDir, config.configName))
                    return false;

                if (profileName.StartsWith("taiko") && !SetupTaikoVersionFiles(currentDir, profileName, config.seriesVersion, config.year, config.taikoArchiveVersion))
                {

                    return false;
                }

                if (profileName.StartsWith("wadaiko") && !SetupTaikoVersionFiles(currentDir, profileName, config.seriesVersion, config.year, config.taikoArchiveVersion))
                {

                    return false;
                }

                if (config.hardwareId != null && !CreateBoardStorageFile(currentDir, profileName, config.hardwareId, config.hardwareRevision, config.configChecksum))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SetupGameConfig(string currentDir, string configName)
        {
            var configFile = Path.Combine(currentDir, "config", $"{configName}_vfs.yml");
            if (!File.Exists(configFile))
            {
                MessageBoxHelper.ErrorOK($"Cannot find {configName} Config, reinstall RPCS3 FORK!");
                return false;
            }
            File.Copy(configFile, Path.Combine(currentDir, "config", "vfs.yml"), true);
            return true;
        }

        private static bool SetupTaikoVersionFiles(string currentDir, string profileName, byte seriesVersion, byte year, byte archiveVersion)
        {
            var versionFile1 = Path.Combine(currentDir, profileName, "dev_usb000", "VERSIONUP", "DATA00000.BIN");
            var versionFile2 = Path.Combine(currentDir, profileName, "dev_usb001", "VERSIONUP", "DATA00000.BIN");

            var versionData = new List<byte>();

            versionData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x16 }); // size of signature string
            versionData.AddRange(System.Text.Encoding.ASCII.GetBytes("serialization::archive"));
            versionData.Add(0x00);
            versionData.Add(archiveVersion);
            versionData.AddRange(new byte[] { 0x04, 0x04, 0x04, 0x08 });
            versionData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
            versionData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            versionData.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            versionData.Add(seriesVersion);
            // Product version, 201903 for 2019 March for exmaple
            versionData.AddRange(new byte[] { 0x00, 0x20 });
            versionData.Add(year);
            versionData.Add(0x03); // Month

            byte[] versionBytes = versionData.ToArray();

            string[] versionFiles = { versionFile1, versionFile2 };

            foreach (string versionFile in versionFiles)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(versionFile));
                File.WriteAllBytes(versionFile, versionBytes);
            }

            return true;
        }

        private static bool CreateBoardStorageFile(string currentDir, string profileName, byte[] hardwareId, byte hardwareRevision, byte configChecksum)
        {
            var boardStoragePath = Path.Combine(currentDir, profileName, "dev_hdd1", "caches", "board_storage.bin");
            var boardStorageData = new List<byte>
            {
                0x01,
                0xFC
            };
            boardStorageData.AddRange(hardwareId);
            boardStorageData.Add(hardwareRevision);
            boardStorageData.Add(configChecksum);

            for (int i = 0; i < 10; i++)
            {
                boardStorageData.Add(0xFF);
            }

            byte[] boardStorageBytes = boardStorageData.ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(boardStoragePath));
            File.WriteAllBytes(boardStoragePath, boardStorageBytes);

            return true;
        }

        private static bool checkbngrw(string gamepath)
        {
            var bngrw = "bngrw.dll";
            var bngrwPath = Path.Combine(Path.GetDirectoryName(gamepath), bngrw);
            var bngrwBackupPath = bngrwPath + ".bak";
            var OpenParrotPassPath = Path.Combine($"OpenParrotx64", bngrw);
            // if the stub doesn't exist (updated TPUI but not OpenParrot?), just show the old messagebox
            if (!File.Exists(OpenParrotPassPath))
            {
                Debug.WriteLine($"{bngrw} stub missing from {OpenParrotPassPath}!");
                return MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryBadiDMAC, bngrw));
            }

            if (!File.Exists(bngrwPath))
            {
                Debug.WriteLine($"{bngrw} missing, copying {bngrwBackupPath} to {bngrwPath}");

                File.Copy(OpenParrotPassPath, bngrwPath);
                return true;
            }
            var description = FileVersionInfo.GetVersionInfo(bngrwPath);
            if (description != null)
            {
                if (description.FileDescription == "BngRw" && description.ProductName == "BanaPassRW Lib")
                {
                    Debug.WriteLine("Original bngrw found, overwriting.");
                    File.Move(bngrwPath, bngrwBackupPath);
                    File.Copy(OpenParrotPassPath, bngrwPath);
                }
                else if (description.ProductVersion != "1.0.0.2")
                {
                    Debug.WriteLine("Old openparrotpass found, overwriting.");
                    File.Delete(bngrwPath);
                    File.Copy(OpenParrotPassPath, bngrwPath);
                }
                else
                {
                    Debug.WriteLine("This should be the correct file.");
                }
            }

            return true;
        }

        private static bool CheckNxl2Core(string gamePath)
        {
            // Samurai Showdown
            if (File.Exists(Path.Combine(Path.GetDirectoryName(gamePath), "Onion-Win64-Shipping.exe")))
            {
                var mainDll = Path.Combine(Path.GetDirectoryName(gamePath), "../../Plugins/NxL2CorePlugin/NxL2Core.dll");
                var alternativeDll = Path.Combine(Path.GetDirectoryName(gamePath), "../../Plugins/NxL2CorePlugin/NxL2Core_2.dll");
                var bad = Path.Combine(Path.GetDirectoryName(gamePath), "../../Plugins/NxL2CorePlugin/NxL2Core_bad.dll");
                FileInfo dllInfo = new FileInfo(mainDll);
                long size = dllInfo.Length;
                if (size < 100000)
                {
                    if (File.Exists(alternativeDll))
                    {
                        System.IO.File.Move(mainDll, bad);
                        System.IO.File.Move(alternativeDll, mainDll);
                        return true;
                    }
                    else
                    {
                        MessageBox.Show(TeknoParrotUi.Properties.Resources.LibraryNxL2CoreTampered);
                        return false;
                    }
                }
            }
            else
            {
                var mainDll = Path.Combine(Path.GetDirectoryName(gamePath), "NxL2Core.dll");
                var alternativeDll = Path.Combine(Path.GetDirectoryName(gamePath), "NxL2Core_2.dll");
                var bad = Path.Combine(Path.GetDirectoryName(gamePath), "NxL2Core_bad.dll");
                FileInfo dllInfo = new FileInfo(mainDll);
                long size = dllInfo.Length;
                if (size < 100000)
                {
                    if (File.Exists(alternativeDll))
                    {
                        System.IO.File.Move(mainDll, bad);
                        System.IO.File.Move(alternativeDll, mainDll);
                        return true;
                    }
                    else
                    {
                        MessageBox.Show(TeknoParrotUi.Properties.Resources.LibraryNxL2CoreTampered);
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool CheckBepinEx(string gamePath, bool is64BitGame)
        {
            string dllPathBase = Path.Combine(Path.GetDirectoryName(gamePath), "winhttp.dll");
            string versionText = is64BitGame ? TeknoParrotUi.Properties.Resources.LibraryBepInEx64Bit : TeknoParrotUi.Properties.Resources.LibraryBepInEx32Bit;
            string messageBoxText = string.Format(TeknoParrotUi.Properties.Resources.LibraryBepInExRequired, versionText);
            string caption = TeknoParrotUi.Properties.Resources.LibraryBepInExRequiredCaption;
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result;
            if (!File.Exists(dllPathBase))
            {
                result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        _ = Process.Start("explorer.exe", "https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2");
                        break;
                    case MessageBoxResult.No:
                        break;
                }
                return false;
            }

            // Let's check that its the right architecture
            if (DllArchitectureChecker.IsDll64Bit(dllPathBase, out bool is64Bit))
            {
                if (is64Bit != is64BitGame)
                {
                    string currentVersionText = is64Bit ? TeknoParrotUi.Properties.Resources.LibraryBepInEx64Bit : TeknoParrotUi.Properties.Resources.LibraryBepInEx32Bit;
                    string requiredVersionText = is64BitGame ? TeknoParrotUi.Properties.Resources.LibraryBepInEx64Bit : TeknoParrotUi.Properties.Resources.LibraryBepInEx32Bit;
                    string messageBoxText2 = string.Format(TeknoParrotUi.Properties.Resources.LibraryBepInExIncompatible, currentVersionText, requiredVersionText);
                    MessageBoxResult result2;
                    result2 = MessageBox.Show(messageBoxText2, caption, button, icon, MessageBoxResult.Yes);
                    switch (result2)
                    {
                        case MessageBoxResult.Yes:
                            _ = Process.Start("explorer.exe", "https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2");
                            break;
                        case MessageBoxResult.No:
                            break;
                    }
                    return false;
                }
            }
            else
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.LibraryCouldNotCheckBitness);
                return false;
            }

            return true;
        }

        private static bool CheckPTTDll(string gamePath)
        {
            var dllPathBase = Path.Combine(Path.GetDirectoryName(gamePath), "WkWin32.dll");
            if (!File.Exists(dllPathBase))
            {
                var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(gamePath));
                var dllPathParent = Path.Combine(parentDir, "WkWin32.dll");
                if (!File.Exists(dllPathBase))
                {
                    MessageBox.Show(TeknoParrotUi.Properties.Resources.LibraryWkWin32Missing);
                    return false;
                }
                else
                {
                    try
                    {
                        File.Copy(dllPathParent, dllPathBase, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error copying DLL: {ex.Message}");
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool CheckPostgresDatabase(GameProfile gameProfile)
        {
            var postgresPath = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Path")?.FieldValue;
            var postgresAddress = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Address")?.FieldValue;
            var postgresPort = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Port")?.FieldValue;
            var postgresDbName = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "DbName")?.FieldValue;
            var postgresUser = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "User")?.FieldValue;
            var postgresPass = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Pass")?.FieldValue;

            Trace.WriteLine("[PostgreSQL Check] Starting database validation...");

            if (string.IsNullOrWhiteSpace(postgresPath) || string.IsNullOrWhiteSpace(postgresDbName))
            {
                Trace.WriteLine("[PostgreSQL Check] Configuration incomplete");
                MessageBoxHelper.ErrorOK("PostgreSQL configuration is incomplete. Please configure the Postgres settings in the game profile.");
                return false;
            }

            var psqlExePath = Path.Combine(postgresPath, "psql.exe");
            var pgRestoreExePath = Path.Combine(postgresPath, "pg_restore.exe");
            
            Trace.WriteLine($"[PostgreSQL Check] Looking for PostgreSQL tools at: {postgresPath}");
            
            if (!File.Exists(psqlExePath))
            {
                Trace.WriteLine("[PostgreSQL Check] psql.exe not found");
                MessageBoxHelper.ErrorOK($"PostgreSQL executable not found at: {psqlExePath}\n\nPlease verify the Postgres Path setting in the game profile.");
                return false;
            }

            if (!File.Exists(pgRestoreExePath))
            {
                Trace.WriteLine("[PostgreSQL Check] pg_restore.exe not found");
                MessageBoxHelper.ErrorOK($"pg_restore.exe not found at: {pgRestoreExePath}\n\nPlease verify the Postgres Path setting in the game profile.");
                return false;
            }

            try
            {
                // Use -w to prevent password prompts and set connection timeout
                var arguments = $"-h {postgresAddress} -p {postgresPort} -U {postgresUser} -d postgres --set=connect_timeout=3 -tAc \"SELECT 1 FROM pg_database WHERE datname='{postgresDbName}'\"";
                Trace.WriteLine($"[PostgreSQL Check] Prepared arguments: {arguments}");
                var processInfo = new ProcessStartInfo
                {
                    FileName = psqlExePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrWhiteSpace(postgresPass))
                {
                    processInfo.EnvironmentVariables["PGPASSWORD"] = postgresPass;
                    Trace.WriteLine("[PostgreSQL Check] Password environment variable set");
                } else {
                    processInfo.EnvironmentVariables["PGPASSWORD"] = "";
                    Trace.WriteLine("[PostgreSQL Check] Password environment variable set with empty value as no pw specified");
                }

                Trace.WriteLine($"[PostgreSQL Check] Executing: {psqlExePath} {arguments}");
                Trace.WriteLine($"[PostgreSQL Check] Connecting to: {postgresAddress}:{postgresPort}");
                Trace.WriteLine($"[PostgreSQL Check] Database: {postgresDbName}, User: {postgresUser}");

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        Trace.WriteLine("[PostgreSQL Check] Failed to start process");
                        MessageBoxHelper.ErrorOK("Failed to start PostgreSQL process.");
                        return false;
                    }

                    Trace.WriteLine("[PostgreSQL Check] Process started, waiting for exit...");
                    
                    if (!process.WaitForExit(5000))
                    {
                        Trace.WriteLine("[PostgreSQL Check] Process timed out after 5 seconds");
                        Trace.WriteLine("[PostgreSQL Check] This usually means psql is waiting for password input or the connection is hanging");
                        try
                        {
                            process.Kill();
                            Trace.WriteLine("[PostgreSQL Check] Process killed");
                        }
                        catch (Exception killEx)
                        {
                            Trace.WriteLine($"[PostgreSQL Check] Error killing process: {killEx.Message}");
                        }
                        
                        var timeoutMsg = "PostgreSQL connection timed out.\n\n" +
                                       "Possible causes:\n" +
                                       "• PostgreSQL server is not running\n" +
                                       "• Password authentication is failing (check pg_hba.conf)\n" +
                                       "• Network/firewall blocking connection\n" +
                                       "• Incorrect connection settings\n\n" +
                                       "Check the debug output for more details.";
                        MessageBoxHelper.ErrorOK(timeoutMsg);
                        return false;
                    }

                    Trace.WriteLine($"[PostgreSQL Check] Process exited with code: {process.ExitCode}");

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    
                    Trace.WriteLine($"[PostgreSQL Check] Output: '{output}'");
                    if (!string.IsNullOrWhiteSpace(error))
                        Trace.WriteLine($"[PostgreSQL Check] Error: '{error}'");

                    if (process.ExitCode != 0)
                    {
                        var errorMessage = $"PostgreSQL connection failed (exit code {process.ExitCode}).\n\nError: {error}\n\nPlease verify your Postgres settings and ensure the PostgreSQL server is running.";
                        Trace.WriteLine("[PostgreSQL Check] Connection failed");
                        MessageBoxHelper.ErrorOK(errorMessage);
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(output) || output.Trim() != "1")
                    {
                        Trace.WriteLine("[PostgreSQL Check] Database does not exist");
                        
                        if (!MessageBoxHelper.WarningYesNo($"PostgreSQL database '{postgresDbName}' does not exist.\n\nWould you like to create it now?"))
                        {
                            return false;
                        }

                        Trace.WriteLine("[PostgreSQL Check] User chose to create database");

                        try
                        {
                            var createDbArgs = $"-h {postgresAddress} -p {postgresPort} -U {postgresUser} -d postgres --set=connect_timeout=3 -tAc \"CREATE DATABASE \\\"{postgresDbName}\\\" WITH ENCODING 'SQL_ASCII'\"";
                            Trace.WriteLine($"[PostgreSQL Check] Creating database with args: {createDbArgs}");

                            var createDbInfo = new ProcessStartInfo
                            {
                                FileName = psqlExePath,
                                Arguments = createDbArgs,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            if (!string.IsNullOrWhiteSpace(postgresPass))
                                createDbInfo.EnvironmentVariables["PGPASSWORD"] = postgresPass;

                            using (var createProcess = Process.Start(createDbInfo))
                            {
                                if (createProcess == null)
                                {
                                    MessageBoxHelper.ErrorOK("Failed to start psql process to create database.");
                                    return false;
                                }

                                if (!createProcess.WaitForExit(5000))
                                {
                                    createProcess.Kill();
                                    MessageBoxHelper.ErrorOK("Database creation timed out.");
                                    return false;
                                }

                                var createOutput = createProcess.StandardOutput.ReadToEnd();
                                var createError = createProcess.StandardError.ReadToEnd();

                                Trace.WriteLine($"[PostgreSQL Check] Create output: {createOutput}");
                                if (!string.IsNullOrWhiteSpace(createError))
                                    Trace.WriteLine($"[PostgreSQL Check] Create error: {createError}");

                                if (createProcess.ExitCode != 0)
                                {
                                    MessageBoxHelper.ErrorOK($"Failed to create database:\n{createError}");
                                    return false;
                                }
                            }

                            Trace.WriteLine("[PostgreSQL Check] Database created successfully");

                            var alterDbArgs = $"-h {postgresAddress} -p {postgresPort} -U {postgresUser} -d postgres --set=connect_timeout=3 -tAc \"ALTER DATABASE \\\"{postgresDbName}\\\" SET standard_conforming_strings TO off\"";
                            Trace.WriteLine($"[PostgreSQL Check] Setting database parameter");

                            var alterDbInfo = new ProcessStartInfo
                            {
                                FileName = psqlExePath,
                                Arguments = alterDbArgs,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            if (!string.IsNullOrWhiteSpace(postgresPass))
                                alterDbInfo.EnvironmentVariables["PGPASSWORD"] = postgresPass;

                            using (var alterProcess = Process.Start(alterDbInfo))
                            {
                                if (alterProcess == null)
                                {
                                    MessageBoxHelper.ErrorOK("Failed to configure database settings.");
                                    return false;
                                }

                                if (!alterProcess.WaitForExit(5000))
                                {
                                    alterProcess.Kill();
                                    MessageBoxHelper.ErrorOK("Database configuration timed out.");
                                    return false;
                                }

                                var alterOutput = alterProcess.StandardOutput.ReadToEnd();
                                var alterError = alterProcess.StandardError.ReadToEnd();

                                if (!string.IsNullOrWhiteSpace(alterError))
                                    Trace.WriteLine($"[PostgreSQL Check] Alter error: {alterError}");

                                if (alterProcess.ExitCode != 0)
                                {
                                    Trace.WriteLine("[PostgreSQL Check] Failed to set database parameter (non-critical)");
                                }
                            }

                            Trace.WriteLine("[PostgreSQL Check] Opening file dialog for backup selection");

                            var openFileDialog = new OpenFileDialog
                            {
                                Title = "Select PostgreSQL Backup File",
                                Filter = "All Files (*.*)|*.*",
                                CheckFileExists = true,
                                CheckPathExists = true,
                                Multiselect = false
                            };

                            if (openFileDialog.ShowDialog() != true)
                            {
                                Trace.WriteLine("[PostgreSQL Check] User cancelled backup file selection");
                                MessageBoxHelper.InfoOK("Database created but no backup restored. You may need to manually restore data.");
                                return true;
                            }

                            var backupFile = openFileDialog.FileName;
                            Trace.WriteLine($"[PostgreSQL Check] Restoring backup from: {backupFile}");

                            var restoreArgs = $"-h {postgresAddress} -p {postgresPort} -U {postgresUser} -d \"{postgresDbName}\" -v \"{backupFile}\"";
                            Trace.WriteLine($"[PostgreSQL Check] Restore arguments: {restoreArgs}");
                            var restoreInfo = new ProcessStartInfo
                            {
                                FileName = pgRestoreExePath,
                                Arguments = restoreArgs,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            if (!string.IsNullOrWhiteSpace(postgresPass))
                                restoreInfo.EnvironmentVariables["PGPASSWORD"] = postgresPass;

                            using (var restoreProcess = Process.Start(restoreInfo))
                            {
                                if (restoreProcess == null)
                                {
                                    MessageBoxHelper.ErrorOK("Failed to start backup restoration process.");
                                    return false;
                                }

                                restoreProcess.WaitForExit();

                                if (restoreProcess.ExitCode != 0 && restoreProcess.ExitCode != 1) // pg_restore returns 1 for warnings I guess?
                                {
                                    Trace.WriteLine($"[PostgreSQL Check] Restore completed with exit code: {restoreProcess.ExitCode}");
                                    MessageBoxHelper.WarningOK($"Backup restoration completed with exit code {restoreProcess.ExitCode}. Check the console output for details.");
                                }
                            }

                            Trace.WriteLine("[PostgreSQL Check] Database created and backup restored successfully");
                            MessageBoxHelper.InfoOK($"Database '{postgresDbName}' has been created and backup restored successfully!");
                            return true;
                        }
                        catch (Exception createEx)
                        {
                            Trace.WriteLine($"[PostgreSQL Check] Exception during database creation: {createEx.Message}");
                            MessageBoxHelper.ErrorOK($"Error creating database: {createEx.Message}");
                            return false;
                        }
                    }

                    Trace.WriteLine("[PostgreSQL Check] Database exists - validation successful");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[PostgreSQL Check] Exception: {ex.Message}");
                Trace.WriteLine($"[PostgreSQL Check] Stack trace: {ex.StackTrace}");
                MessageBoxHelper.ErrorOK($"Error checking PostgreSQL database: {ex.Message}");
                return false;
            }
        }
        private static string CheckPlay(string gamepath, string gameName)
        {
            var getDir = Path.Combine(Path.GetDirectoryName(gamepath), gameName);
            if (gameName == "bldyr3b")
            {
                if (!File.Exists(Path.Combine(getDir, "bldyr3b.chd")))
                {
                    return Path.Combine(getDir, "bldyr3b.chd");
                }
            }
            if (gameName == "fghtjam")
            {
                if (!File.Exists(Path.Combine(getDir, "jam1-dvd0.chd")))
                {
                    return Path.Combine(getDir, "jam1-dvd0.chd");
                }
            }
            if (gameName == "prdgp03")
            {
                if (!File.Exists(Path.Combine(getDir, "pr21dvd0.chd")))
                {
                    return Path.Combine(getDir, "pr21dvd0.chd");
                }
            }
            if (gameName == "tekken4")
            {
                if (!File.Exists(Path.Combine(getDir, "tef1dvd0.chd")))
                {
                    return Path.Combine(getDir, "tef1dvd0.chd");
                }
            }
            if (gameName == "wanganmd")
            {
                if (!File.Exists(Path.Combine(getDir, "wmn1-a.chd")))
                {
                    return Path.Combine(getDir, "wmn1-a.chd");
                }
            }
            if (gameName == "wanganmr")
            {
                if (!File.Exists(Path.Combine(getDir, "wmr1-a.chd")))
                {
                    return Path.Combine(getDir, "wmr1-a.chd");
                }
            }
            if (gameName == "pacmanbr")
            {
                if (!File.Exists(Path.Combine(getDir, "pbr102-2-na-mpro-a13_kp006b.ic26")))
                {
                    return Path.Combine(getDir, "pbr102-2-na-mpro-a13_kp006b.ic26");
                }
                if (!File.Exists(Path.Combine(getDir, "common_system147b_bootrom.ic1")))
                {
                    return Path.Combine(getDir, "common_system147b_bootrom.ic1");
                }
            }
            return "";
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

            CloseHighScoreWindow();

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            bool changed = JoystickHelper.AutoFillOnlineId(gameProfile);
            if (changed)
            {
                JoystickHelper.SerializeGameProfile(gameProfile);
            }
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

            CloseHighScoreWindow();

            Joystick = new JoystickControl(_contentControl, this);
            Joystick.LoadNewSettings(_gameNames[gameList.SelectedIndex], (ListBoxItem)gameList.SelectedItem);
            Joystick.Listen();
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = Joystick;
        }

        /// <summary>
        /// This button actually launches the game selected in test mode, if available
        /// </summary>
        private void BtnLaunchTestMenu(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            CloseHighScoreWindow();

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            Lazydata.ParrotData.LastPlayed = gameProfile.GameNameInternal;
            JoystickHelper.Serialize();

            // Launch with test menu enabled
            if (ValidateAndRun(gameProfile, out var loader, out var dll, false, this, true))
            {
                var gameRunning = new GameRunning(gameProfile, loader, dll, true, false, false, this);
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameRunning;
            }
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

            CloseHighScoreWindow();

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            Lazydata.ParrotData.LastPlayed = gameProfile.GameNameInternal;
            JoystickHelper.Serialize();

            if (ValidateAndRun(gameProfile, out var loader, out var dll, false, this, false))
            {
                var gameRunning = new GameRunning(gameProfile, loader, dll, false, false, false, this);
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

            CloseHighScoreWindow();

            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (!File.Exists(Lazydata.ParrotData.DatXmlLocation))
            {
                MessageBoxHelper.InfoOK(Properties.Resources.LibraryNoHashes);
            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content =
                    new VerifyGame(selectedGame, this);
            }
        }

        private void BtnMoreInfo(object sender, RoutedEventArgs e)
        {
            CloseHighScoreWindow();

            string path = string.Empty;

            if (gameList.Items.Count != 0)
            {
                var selectedGame = _gameNames[gameList.SelectedIndex];

                // open game compatibility page
                if (selectedGame != null)
                {
                    path = Path.GetFileNameWithoutExtension(selectedGame.FileName);
                }
            }

            var url = "https://teknoparrot.com/Compatibility/GameDetail/" + path;
            Debug.WriteLine($"opening {url}");
            Process.Start(url);
        }

        private void BtnOnlineProfile(object sender, RoutedEventArgs e)
        {
            CloseHighScoreWindow();

            string path = string.Empty;
            if (gameList.Items.Count != 0)
            {
                var selectedGame = _gameNames[gameList.SelectedIndex];

                // open game compatibility page
                if (selectedGame != null && selectedGame.OnlineProfileURL != "")
                {
                    path = selectedGame.OnlineProfileURL;
                }
            }

            Debug.WriteLine($"opening {path}");
            Process.Start(path);
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

        private void BtnDeleteGame(object sender, RoutedEventArgs e)
        {
            CloseHighScoreWindow();

            var selectedItem = (ListBoxItem)gameList.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }
            var selected = (GameProfile)selectedItem.Tag;
            if (selected == null || selected.FileName == null) return;
            if (Lazydata.ParrotData.ConfirmGameDeletion)
            {
                var confirmMessage = string.Format(TeknoParrotUi.Properties.Resources.AddGameConfirmDelete, selected.GameNameInternal);
                if (!MessageBoxHelper.WarningYesNo(confirmMessage))
                {
                    return;
                }
            }
            var splitString = selected.FileName.Split('\\');
            try
            {
                Debug.WriteLine($@"Removing {selected.GameNameInternal} from TP...");
                File.Delete(Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {
                // ignored
            }

            ListUpdate();
        }

        private void BtnPlayOnlineClick(object sender, RoutedEventArgs e)
        {
            CloseHighScoreWindow();

            var app = Application.Current.Windows.OfType<MainWindow>().Single();
            app.BtnTPOnline2(null, null);
        }

        private void GenreBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListUpdate();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newSearchText = SearchBox.Text;

            if (string.IsNullOrWhiteSpace(_searchText) && !string.IsNullOrWhiteSpace(newSearchText))
            {
                if (gameList.SelectedIndex >= 0 && gameList.SelectedIndex < _gameNames.Count)
                {
                    _savedSelection = _gameNames[gameList.SelectedIndex].GameNameInternal;
                }
            }
            else if (!string.IsNullOrWhiteSpace(_searchText) && string.IsNullOrWhiteSpace(newSearchText))
            {
                _searchDebounceTimer.Stop();
                _searchText = string.Empty;
                _isSearchUpdate = true;
                ListUpdate(_savedSelection);
                _isSearchUpdate = false;
                _savedSelection = null;
                return;
            }

            _searchText = newSearchText;
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private System.Windows.Controls.Button CreateTimePeriodButton(string text)
        {
            return new System.Windows.Controls.Button
            {
                Content = text,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        // Add this method to validate URLs (place it near other helper methods like CloseHighScoreWindow)
        private static bool IsValidHighScoreUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);

                // List of allowed domains for high scores
                var allowedDomains = new List<string>
                {
                    "teknoparrot.com",
                    "www.teknoparrot.com"
                };

                // Must be HTTPS for security
                if (uri.Scheme != Uri.UriSchemeHttps)
                {
                    Debug.WriteLine($"Invalid URL scheme: {uri.Scheme}. Only HTTPS is allowed.");
                    return false;
                }

                // Check if the host matches any allowed domain
                var host = uri.Host.ToLowerInvariant();
                if (!allowedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"Invalid URL domain: {host}. Only {string.Join(", ", allowedDomains)} are allowed.");
                    return false;
                }

                return true;
            }
            catch (UriFormatException ex)
            {
                Debug.WriteLine($"Invalid URL format: {url}. Error: {ex.Message}");
                return false;
            }
        }

        private void BtnHighScores(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var selectedGame = _gameNames[gameList.SelectedIndex];

            // Generate URL from ProfileName
            string highScoreUrl = GetHighScoreUrlForProfile(selectedGame.ProfileName);

            if (string.IsNullOrEmpty(highScoreUrl))
                return;

            // Replace the language code in the URL based on user's language setting
            var localizedUrl = GetLocalizedHighScoreUrl(highScoreUrl);

            // Validate URL before opening
            if (!IsValidHighScoreUrl(localizedUrl))
            {
                MessageBoxHelper.ErrorOK("Invalid high score URL. Only official TeknoParrot URLs are allowed.");
                Debug.WriteLine($"Blocked attempt to open invalid URL: {localizedUrl}");
                return;
            }

            OpenHighScoreWindow(localizedUrl);
        }

        private static string GetHighScoreUrlForProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return null;

            // Map profile names to their high score page identifiers
            // Using the profile name (xml filename without .xml) as the identifier
            var highScoreGames = new Dictionary<string, string>
            {
                { "BattleGear4Tuned", "BattleGear4Tuned" },
                { "Daytona3", "Daytona3" },
                { "Daytona3NSE", "Daytona3NSE" },
                { "DeadHeat", "DeadHeat" },
                { "DirtyDrivin", "DirtyDrivin" },
                { "FarCryParadiseLost", "FarCryParadiseLost" },
                { "GaelcoChampionshipTuningRace", "GaelcoChampionshipTuningRace" },
                { "H2Overdrive", "H2Overdrive" },
                { "HOTD4", "HOTD4" },
                { "HOTDSD", "HOTDSD" },
                { "GoldenTeeLive2006", "gt06" },
                { "GoldenTeeLive2007", "gt07" },
                { "GoldenTeeLive2008", "gt08" },
                { "GoldenTeeLive2009", "gt09" },
                { "GoldenTeeLive2010", "gt10" },
                { "GoldenTeeLive2011", "gt11" },
                { "GoldenTeeLive2012", "gt12" },
                { "GoldenTeeLive2013", "gt13" },
                { "GoldenTeeLive2014", "gt14" },
                { "GoldenTeeLive2015", "gt15" },
                { "GoldenTeeLive2016", "gt16" },
                { "GoldenTeeLive2017", "gt17" },
                { "GoldenTeeLive2018", "gt18" },
                { "ID6", "ID6" },
                { "ID7", "ID7" },
                { "ID8", "ID8" },
                { "or2spdlx", "or2spdlx" },
                { "PowerPuttLive2012", "ppl12" },
                { "PowerPuttLive2013", "ppl13" },
                { "RastanSaga", "RastanSaga" },
                { "SilverStrikeBowlingLive", "silverstrikelive" },
                { "SR3", "SR3" },
                { "SRC", "SRC" },
                { "Taiko", "Taiko" },
                { "TC5", "TC5" },
                { "TargetTerrorGold", "TTG" },
                { "WMMT3", "WMMT3" },
                { "WMMT3DXP", "WMMT3DXPlus" },
                { "WMMT5", "WMMT5" },
                { "WMMT5DX", "WMMT5DX" },
                { "WMMT5DXPlus", "WMMT5DXPlus" },
                { "WMMT6", "WMMT6" },
                { "WMMT6R", "WMMT6R" },
            };

            // Check if this game has high scores
            if (highScoreGames.ContainsKey(profileName))
            {
                var gameIdentifier = highScoreGames[profileName];
                // Return base URL with placeholder for language (will be replaced later)
                return $"https://teknoparrot.com/en/Highscore/GameSpecific/{gameIdentifier}";
            }

            return null; // No high scores available for this game
        }

        private static string GetLocalizedHighScoreUrl(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return null;

            try
            {
                // Get the user's language setting
                string userLanguage = Lazydata.ParrotData.Language ?? "en-US";
                
                // Map app language codes to website language codes
                var languageMap = new Dictionary<string, string>
                {
                    { "en-US", "en" },    // US English
                    { "en", "en" },       // English (generic)
                    { "fi-FI", "fi" },    // Finnish / Suomi
                    { "ar-SA", "sa" },    // Saudi Arabia Arabic
                    { "de-DE", "de" },    // German / Deutsch
                    { "es-ES", "es" },    // Spanish / Español
                    { "fr-FR", "fr" },    // French / Français
                    { "he-IL", "il" },    // Hebrew (Israel)
                    { "it-IT", "it" },    // Italian / Italiano
                    { "ja-JP", "jp" },    // Japanese
                    { "ko-KR", "kr" },    // Korean
                    { "nl-NL", "nl" },    // Dutch / Nederlands
                    { "pl-PL", "pl" },    // Polish / Polski
                    { "pt-BR", "pt" },    // Portuguese / Português
                    { "pt-PT", "pt" },    // Portuguese (Portugal)
                    { "ru-RU", "ru" },    // Russian / Русский
                    { "zh-CN", "cn" },    // Chinese (Simplified)
                    { "zh-TW", "cn" }     // Chinese (Traditional) - using same as simplified
                };

                // Get the website language code, default to "en" if not found
                string websiteLanguageCode = "en";
                if (languageMap.ContainsKey(userLanguage))
                {
                    websiteLanguageCode = languageMap[userLanguage];
                }
                else
                {
                    // Try to match just the language part (e.g., "en" from "en-US")
                    var languagePart = userLanguage.Split('-')[0].ToLower();
                    var matchingKey = languageMap.Keys.FirstOrDefault(k => k.StartsWith(languagePart + "-"));
                    if (matchingKey != null)
                    {
                        websiteLanguageCode = languageMap[matchingKey];
                    }
                }

                // Parse the URL and replace the language code
                var uri = new Uri(originalUrl);
                var pathSegments = uri.AbsolutePath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToList();
                
                // The language code is typically the first segment after the domain
                // e.g., https://teknoparrot.com/en/Highscore/GameSpecific/gt17
                //                                  ^^
                if (pathSegments.Count > 0)
                {
                    // Replace the first segment (language code) with the user's language
                    pathSegments[0] = websiteLanguageCode;
                    
                    var newPath = "/" + string.Join("/", pathSegments);
                    var uriBuilder = new UriBuilder(uri)
                    {
                        Path = newPath
                    };
                    
                    var localizedUrl = uriBuilder.Uri.ToString();
                    Debug.WriteLine($"Localized high score URL: {originalUrl} -> {localizedUrl}");
                    
                    return localizedUrl;
                }
                
                // If we can't parse it properly, return the original URL
                Debug.WriteLine($"Could not localize URL, using original: {originalUrl}");
                return originalUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error localizing high score URL: {ex.Message}");
                return originalUrl; // Return original URL if there's any error
            }
        }

        private void OpenHighScoreWindow(string url)
        {
            // Validate URL one more time before opening (defense in depth)
            if (!IsValidHighScoreUrl(url))
            {
                Debug.WriteLine($"Security: Blocked invalid URL in OpenHighScoreWindow: {url}");
                return;
            }

            // Close existing window if one is already open
            if (_highScoreWindow != null && _highScoreWindow.IsLoaded)
            {
                _highScoreWindow.Close();
            }

            // Get the main window
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().Single();

            _highScoreWindow = new Window
            {
                Title = "High Scores",
                Width = 800,
                Height = mainWindow.ActualHeight,
                Owner = mainWindow,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = false
            };

            // Position flush to the right with no gap
            _highScoreWindow.Left = mainWindow.Left + mainWindow.ActualWidth;
            _highScoreWindow.Top = mainWindow.Top;

            // Store event handlers so we can unsubscribe later
            EventHandler locationChangedHandler = null;
            SizeChangedEventHandler sizeChangedHandler = null;

            locationChangedHandler = (s, e) =>
            {
                if (_highScoreWindow != null && _highScoreWindow.IsLoaded)
                {
                    _highScoreWindow.Left = mainWindow.Left + mainWindow.ActualWidth;
                    _highScoreWindow.Top = mainWindow.Top;
                }
            };

            sizeChangedHandler = (s, e) =>
            {
                if (_highScoreWindow != null && _highScoreWindow.IsLoaded)
                {
                    _highScoreWindow.Height = mainWindow.ActualHeight;
                    _highScoreWindow.Left = mainWindow.Left + mainWindow.ActualWidth;
                    _highScoreWindow.Top = mainWindow.Top;
                }
            };

            // Attach event handlers
            mainWindow.LocationChanged += locationChangedHandler;
            mainWindow.SizeChanged += sizeChangedHandler;

            // Clear reference and cleanup event handlers when window is closed
            _highScoreWindow.Closed += (s, e) =>
            {
                // Unsubscribe event handlers to prevent memory leaks
                mainWindow.LocationChanged -= locationChangedHandler;
                mainWindow.SizeChanged -= sizeChangedHandler;
                _highScoreWindow = null;
            };

            // Create the browser
            var chromiumBrowser = new CefSharp.Wpf.ChromiumWebBrowser
            {
                Address = url
            };

            // Disable right-click context menu for security
            chromiumBrowser.MenuHandler = new CustomMenuHandler();
            
            // Block popups from opening in new windows - navigate in same window instead
            chromiumBrowser.LifeSpanHandler = new PopupBlockingLifeSpanHandler(chromiumBrowser);

            // Create outer border with rounded corners
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Background = System.Windows.Media.Brushes.White
            };

            outerBorder.Child = chromiumBrowser;
            _highScoreWindow.Content = outerBorder;
            _highScoreWindow.Show();
        }

        // Add this class to handle context menu (disables right-click)
        public class CustomMenuHandler : CefSharp.IContextMenuHandler
        {
            public void OnBeforeContextMenu(CefSharp.IWebBrowser browserControl, CefSharp.IBrowser browser, CefSharp.IFrame frame, CefSharp.IContextMenuParams parameters, CefSharp.IMenuModel model)
            {
                model.Clear();
            }

            public bool OnContextMenuCommand(CefSharp.IWebBrowser browserControl, CefSharp.IBrowser browser, CefSharp.IFrame frame, CefSharp.IContextMenuParams parameters, CefSharp.CefMenuCommand commandId, CefSharp.CefEventFlags eventFlags)
            {
                return false;
            }

            public void OnContextMenuDismissed(CefSharp.IWebBrowser browserControl, CefSharp.IBrowser browser, CefSharp.IFrame frame)
            {
            }

            public bool RunContextMenu(CefSharp.IWebBrowser browserControl, CefSharp.IBrowser browser, CefSharp.IFrame frame, CefSharp.IContextMenuParams parameters, CefSharp.IMenuModel model, CefSharp.IRunContextMenuCallback callback)
            {
                return false;
            }
        }

        // Add this class to block popups and handle them in the same window
        public class PopupBlockingLifeSpanHandler : CefSharp.ILifeSpanHandler
        {
            private readonly CefSharp.Wpf.ChromiumWebBrowser _browser;

            public PopupBlockingLifeSpanHandler(CefSharp.Wpf.ChromiumWebBrowser browser)
            {
                _browser = browser;
            }

            public bool OnBeforePopup(CefSharp.IWebBrowser chromiumWebBrowser, CefSharp.IBrowser browser, CefSharp.IFrame frame, 
                string targetUrl, string targetFrameName, CefSharp.WindowOpenDisposition targetDisposition, bool userGesture, 
                CefSharp.IPopupFeatures popupFeatures, CefSharp.IWindowInfo windowInfo, CefSharp.IBrowserSettings browserSettings, 
                ref bool noJavascriptAccess, out CefSharp.IWebBrowser newBrowser)
            {
                newBrowser = null;

                // Validate the URL before allowing navigation
                if (!IsValidHighScoreUrl(targetUrl))
                {
                    Debug.WriteLine($"Blocked popup to invalid URL: {targetUrl}");
                    return true; // true = cancel the popup
                }

                // Instead of opening a new window, navigate in the same browser
                _browser.Load(targetUrl);
                
                return true; // true = cancel the popup (we're handling it ourselves)
            }

            public void OnAfterCreated(CefSharp.IWebBrowser chromiumWebBrowser, CefSharp.IBrowser browser)
            {
            }

            public bool DoClose(CefSharp.IWebBrowser chromiumWebBrowser, CefSharp.IBrowser browser)
            {
                return false;
            }

            public void OnBeforeClose(CefSharp.IWebBrowser chromiumWebBrowser, CefSharp.IBrowser browser)
            {
            }
        }

        private void CloseHighScoreWindow()
        {
            if (_highScoreWindow != null && _highScoreWindow.IsLoaded)
            {
                _highScoreWindow.Close();
                _highScoreWindow = null;
            }
        }
    }
}