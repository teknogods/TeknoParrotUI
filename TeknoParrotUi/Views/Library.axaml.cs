using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.UserControls;
using TeknoParrotUi.Helpers;
using Linearstar.Windows.RawInput;
using Avalonia.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.axaml
    /// </summary>
    public partial class Library : UserControl, INotifyPropertyChanged
    {
        //Defining variables that need to be accessed by all methods
        public JoystickControl Joystick;
        private MainWindow _mainWindow;
        public event PropertyChangedEventHandler PropertyChanged;

        // Keep the existing properties but make them raise change notifications
        public bool IsTheme33Active => _mainWindow?.IsTheme33Active ?? false;
        public bool IsWhiteoutActive => _mainWindow?.IsWhiteoutActive ?? false;
        public bool IsBluehatActive => _mainWindow?.IsBluehatActive ?? false;
        public bool IsObsidianActive => _mainWindow?.IsObsidianActive ?? false;
        public bool IsEmberActive => _mainWindow?.IsEmberActive ?? false;
        public bool IsFrostActive => _mainWindow?.IsFrostActive ?? false;
        public bool IsEchoActive => _mainWindow?.IsEchoActive ?? false;
        public bool IsVoidActive => _mainWindow?.IsVoidActive ?? false;
        public bool IsCyberActive => _mainWindow?.IsCyberActive ?? false;
        public readonly List<GameProfile> _gameNames = new List<GameProfile>();
        readonly GameSettingsControl _gameSettings = new GameSettingsControl();
        private ContentControl _contentControl;
        public bool listRefreshNeeded = false;
        public static bool firstBoot = true;

        public static Bitmap defaultIcon;

        private SplitView _librarySplitView;

        private ImageBrush _gameListBackground;
        public ImageBrush GameListBackground
        {
            get => _gameListBackground;
            set
            {
                _gameListBackground = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GameListBackground)));
            }
        }

        public Library(ContentControl contentControl)
        {
            InitializeComponent();

            _gameListBackground = new ImageBrush(new Bitmap(AssetLoader.Open(new Uri("avares://TeknoParrotUi/Resources/teknoparrot_by_pooterman-db9erxd.png"))));
            _gameListBackground.Stretch = Stretch.Fill;
            _gameListBackground.AlignmentX = AlignmentX.Center;
            _gameListBackground.AlignmentY = AlignmentY.Center;
            _gameListBackground.Opacity = 0.2;

            this.Loaded += Library_Loaded;

            _contentControl = contentControl;
            DataContext = this; // Add this line
            Joystick = new JoystickControl(contentControl, this);
        }

        private void Library_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Get reference to MainWindow
            _mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow as MainWindow
                : null;

            // Subscribe to MainWindow's PropertyChanged event
            if (_mainWindow != null)
            {
                _mainWindow.PropertyChanged += MainWindow_PropertyChanged;
            }
        }
        private void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When any theme property changes in MainWindow, update our properties
            if (e.PropertyName.StartsWith("Is") && e.PropertyName.EndsWith("Active"))
            {
                NotifyAllThemeProperties();
            }
        }

        private void NotifyAllThemeProperties()
        {
            // Notify that all theme properties have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTheme33Active)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWhiteoutActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBluehatActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsObsidianActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmberActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrostActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEchoActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVoidActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCyberActive)));
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            gameList = this.FindControl<ListBox>("gameList");
            var gameIcon = this.FindControl<Image>("gameIcon");
            var ChkTestMenu = this.FindControl<ToggleSwitch>("ChkTestMenu");
            var gameOnlineProfileButton = this.FindControl<Button>("gameOnlineProfileButton");
            var playOnlineButton = this.FindControl<Button>("playOnlineButton");
            var gameLaunchButton = this.FindControl<Button>("gameLaunchButton");
            var gameInfoText = this.FindControl<TextBlock>("gameInfoText");
            var delGame = this.FindControl<Button>("delGame");
            var verifyGame = this.FindControl<Button>("verifyGame");
            var gameInfoButton = this.FindControl<Button>("gameInfoButton");
            var GameSettingsButton = this.FindControl<Button>("GameSettingsButton");
            var ControllerSettingsButton = this.FindControl<Button>("ControllerSettingsButton");
            gameList.SelectionChanged += ListBox_SelectionChanged;
            this.Loaded += UserControl_Loaded;

            gameOnlineProfileButton.Click += BtnOnlineProfile;
            playOnlineButton.Click += BtnPlayOnlineClick;
            gameLaunchButton.Click += BtnLaunchGame;
            gameInfoButton.Click += BtnMoreInfo;
            GameSettingsButton.Click += BtnGameSettings;
            ControllerSettingsButton.Click += BtnControllerSettings;
            delGame.Click += Button_Click;
            verifyGame.Click += BtnVerifyGame;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // When the control's size changes, update the layout
            if (change.Property == BoundsProperty)
            {
                UpdateResponsiveLayout();
            }
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = this;
            _librarySplitView = this.FindControl<SplitView>("LibrarySplitView");
            UpdateResponsiveLayout();
            if (gameList.Items.Count == 0 || listRefreshNeeded)
                await ListUpdate();

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    if (mainWindow._updaterComplete)
                    {
                        mainWindow.updates = new List<GitHubUpdates>();
                        mainWindow.checkForUpdates(true, false);
                        mainWindow._updaterComplete = false;
                    }
                }
            }
        }

        private void UpdateResponsiveLayout()
        {
            if (_librarySplitView == null) return;

            double width = this.Bounds.Width;
            double height = this.Bounds.Height;

            bool isMobileSize = width < 640;
            bool isVerticalOrientation = height > width;
            bool isSteamDeckSize = Math.Abs(width - 1280) < 20 && Math.Abs(height - 800) < 20;

            // First remove all mode classes
            _librarySplitView.Classes.Remove("MobileMode");
            _librarySplitView.Classes.Remove("VerticalMode");
            _librarySplitView.Classes.Remove("CompactMode");
            _librarySplitView.Classes.Remove("SteamDeckMode");

            // Apply appropriate classes based on screen size
            if (isMobileSize)
            {
                _librarySplitView.Classes.Add("MobileMode");
                _librarySplitView.DisplayMode = SplitViewDisplayMode.Overlay;
                _librarySplitView.IsPaneOpen = false;
            }
            else if (isVerticalOrientation)
            {
                _librarySplitView.Classes.Add("VerticalMode");
                _librarySplitView.DisplayMode = SplitViewDisplayMode.Overlay;
                _librarySplitView.IsPaneOpen = false;
            }
            else if (width < 1000)
            {
                _librarySplitView.Classes.Add("CompactMode");
                _librarySplitView.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            }
            else
            {
                _librarySplitView.DisplayMode = SplitViewDisplayMode.Inline;
                _librarySplitView.IsPaneOpen = true;
            }

            if (isSteamDeckSize)
            {
                _librarySplitView.Classes.Add("SteamDeckMode");
            }

            // Adjust panel width based on screen size
            _librarySplitView.OpenPaneLength = width < 800 ?
                Math.Min(250, width * 0.4) :
                Math.Min(300, width * 0.3);
        }

        static Bitmap LoadImage(string filename)
        {
            // Simple bitmap loading for Avalonia
            try
            {
                using var file = new FileStream(Path.GetFullPath(filename), FileMode.Open, FileAccess.Read, FileShare.Read);
                return new Bitmap(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image: {ex.Message}");
                return null;
            }
        }

        private static bool DownloadFile(string urlAddress, string filePath)
        {
            if (File.Exists(filePath)) return true;
            Debug.WriteLine($"Downloading {filePath} from {urlAddress}");
            try
            {
                // Use HttpClient instead of WebRequest as it's recommended in .NET
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Download and save the file
                var bytes = client.GetByteArrayAsync(urlAddress).Result;
                File.WriteAllBytes(filePath, bytes);
                return true;
            }
            catch (HttpRequestException ex) when ((int)ex.StatusCode == 404)
            {
                Debug.WriteLine($"File at {urlAddress} is missing!");
                // ignore
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error downloading file: {e.Message}");
                // ignore
            }

            return false;
        }

        public void UpdateIcon(string iconName)
        {
            var iconPath = Path.Combine("Icons", iconName);
            bool success = Lazydata.ParrotData.DownloadIcons ? DownloadFile(
                    "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" +
                    iconName, iconPath) : true;

            if (success && File.Exists(iconPath))
            {
                try
                {
                    var bitmap = LoadImage(iconPath);

                    // Create new brush with the loaded image
                    var brush = new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.Uniform,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center,
                        Opacity = 0.2
                    };

                    // Update ListBox background directly if provided
                    if (gameList != null)
                    {
                        gameList.Background = brush;
                    }

                    // Update the ImageBrush property if provided
                    if (_gameListBackground != null)
                    {
                        _gameListBackground.Source = bitmap;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating icon: {ex.Message}");

                    if (File.Exists(iconPath)) File.Delete(iconPath);

                    if (gameList != null)
                        gameList.Background = new ImageBrush(defaultIcon)
                        {
                            Stretch = Stretch.Uniform,
                            AlignmentX = AlignmentX.Center,
                            AlignmentY = AlignmentY.Center,
                            Opacity = 0.2
                        };

                    if (GameListBackground != null)
                        GameListBackground.Source = defaultIcon;
                }
            }
            else
            {
                if (gameList != null)
                    gameList.Background = new ImageBrush(defaultIcon)
                    {
                        Stretch = Stretch.Uniform,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center,
                        Opacity = 0.2
                    };

                if (GameListBackground != null)
                    GameListBackground.Source = defaultIcon;
            }
        }

        /// <summary>
        /// When the selection in the listbox is changed, this is run. It loads in the currently selected game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            var ChkTestMenu = this.FindControl<ToggleSwitch>("ChkTestMenu");
            var gameOnlineProfileButton = this.FindControl<Button>("gameOnlineProfileButton");
            var playOnlineButton = this.FindControl<Button>("playOnlineButton");
            var gameLaunchButton = this.FindControl<Button>("gameLaunchButton");
            var gameInfoText = this.FindControl<TextBlock>("gameInfoText");
            var delGame = this.FindControl<Button>("delGame");


            if (gameList == null || gameList.Items.Count == 0)
                return;

            var modifyItem = (ListBoxItem)((ListBox)sender).SelectedItem;
            var profile = _gameNames[gameList.SelectedIndex];
            UpdateIcon(profile.IconName.Split('/')[1]);

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
                ToolTip.SetTip(ChkTestMenu, Properties.Resources.LibraryToggleTestMode);
            }
            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (selectedGame.OnlineProfileURL != "")
            {
                gameOnlineProfileButton.IsVisible = true;
            }
            else
            {
                gameOnlineProfileButton.IsVisible = false;
            }

            // Check online titles and show button if required
            if (selectedGame.HasTpoSupport)
            {
                playOnlineButton.IsVisible = true;
            }
            else
            {
                playOnlineButton.IsVisible = false;
            }

            if (selectedGame.IsTpoExclusive)
            {
                gameLaunchButton.IsEnabled = false;
            }
            else
            {
                gameLaunchButton.IsEnabled = true;
            }

            gameInfoText.Text = $"{Properties.Resources.LibraryEmulator}: {selectedGame.EmulatorType} ({(selectedGame.Is64Bit ? "x64" : "x86")})\n{(selectedGame.GameInfo == null ? Properties.Resources.LibraryNoInfo : selectedGame.GameInfo.ToString())}";
            delGame.IsEnabled = true;
        }

        private void resetLibrary()
        {
            var gameIcon = this.FindControl<Image>("gameIcon");
            var gameInfoText = this.FindControl<TextBlock>("gameInfoText");

            gameIcon.Source = defaultIcon;
            _gameSettings.InitializeComponent();
            Joystick.InitializeComponent();
            gameInfoText.Text = "";
        }

        /// <summary>
        /// This updates the listbox when called
        /// </summary>
        public async Task ListUpdate(string selectGame = null)
        {
            if (!firstBoot)
            {
                GameProfileLoader.LoadProfiles(true);
            }
            else
            {
                // if this is after just booting the app, we just finished loading
                // all profiles so we don't need to do it again, nothing will have changed
                firstBoot = false;
            }


            // Clear list
            _gameNames.Clear();
            gameList.Items.Clear();

            // Populate list
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                // third-party emulators
                var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                // check the existing user profiles
                var existing = GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.GameNameInternal == gameProfile.GameNameInternal) != null;

                var item = new ListBoxItem
                {
                    Content = gameProfile.GameNameInternal +
                                (gameProfile.Patreon ? " (Subscription)" : "") +
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

            gameList.Focus();

            // No games?
            if (gameList.Items.Count == 0)
            {
                if (await MessageBoxHelper.InfoYesNo(Properties.Resources.LibraryNoGames))
                    if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.contentControl.Content = new AddGame(_contentControl, this);
                        }
                    }
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
        private async Task UserControl_LoadedAsync(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");

            if (gameList != null && (gameList.Items.Count == 0 || listRefreshNeeded))
                await ListUpdate();

            // Get MainWindow instance
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow != null && mainWindow._updaterComplete)
                {
                    mainWindow.updates = new List<GitHubUpdates>();
                    mainWindow.checkForUpdates(true, false);
                    mainWindow._updaterComplete = false;
                }
            }
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameProfile">Input profile.</param>
        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameProfile">Input profile.</param>
        /// <param name="emuOnly">If true, only validate emulator.</param>
        /// <param name="library">Reference to the Library instance.</param>
        /// <param name="_test">Whether to use test mode.</param>
        /// <returns>A tuple containing (bool success, string loaderExe, string loaderDll)</returns>
        public static async Task<(bool success, string loaderExe, string loaderDll)> ValidateAndRun(
            GameProfile gameProfile,
            bool emuOnly,
            Library library,
            bool _test)
        {
            string loaderDll = string.Empty;
            string loaderExe = string.Empty;

            bool is64Bit = _test ? gameProfile.TestExecIs64Bit : gameProfile.Is64Bit;

            // don't attempt to run 64 bit game on non-64 bit OS
            if (is64Bit && !App.Is64Bit())
            {
                await MessageBoxHelper.ErrorOK(Properties.Resources.Library64bit);
                return (false, loaderExe, loaderDll);
            }

            if (emuOnly)
            {
                return (true, loaderExe, loaderDll);
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
                    File.Copy(".\\SegaTools\\aimeio.dll", Path.Combine(Path.GetDirectoryName(gameProfile.GamePath), "aimeio.dll"), true);
                    File.Copy(".\\SegaTools\\idzhook.dll", Path.Combine(Path.GetDirectoryName(gameProfile.GamePath), "idzhook.dll"), true);
                    File.Copy(".\\SegaTools\\idzio.dll", Path.Combine(Path.GetDirectoryName(gameProfile.GamePath), "idzio.dll"), true);
                    File.Copy(".\\SegaTools\\inject.exe", Path.Combine(Path.GetDirectoryName(gameProfile.GamePath), "inject.exe"), true);
                    loaderExe = ".\\SegaTools\\inject.exe";
                    loaderDll = "idzhook";
                    break;
                default:
                    loaderDll = (is64Bit ? ".\\TeknoParrot\\TeknoParrot64" : ".\\TeknoParrot\\TeknoParrot");
                    break;
            }

            if (!File.Exists(loaderExe))
            {
                await MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindLoader, loaderExe));
                return (false, loaderExe, loaderDll);
            }

            var dll_filename = loaderDll + ".dll";
            if (loaderDll != string.Empty && !File.Exists(dll_filename) && gameProfile.EmulationProfile != EmulationProfile.SegaToolsIDZ)
            {
                await MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindLoader, dll_filename));
                return (false, loaderExe, loaderDll);
            }

            if (string.IsNullOrEmpty(gameProfile.GamePath))
            {
                await MessageBoxHelper.ErrorOK(Properties.Resources.LibraryGameLocationNotSet);
                return (false, loaderExe, loaderDll);
            }

            if (!File.Exists(gameProfile.GamePath))
            {
                await MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, gameProfile.GamePath));
                return (false, loaderExe, loaderDll);
            }

            // Check second exe
            if (gameProfile.HasTwoExecutables)
            {
                if (string.IsNullOrEmpty(gameProfile.GamePath2))
                {
                    await MessageBoxHelper.ErrorOK(Properties.Resources.LibraryGameLocation2NotSet);
                    return (false, loaderExe, loaderDll);
                }

                if (!File.Exists(gameProfile.GamePath2))
                {
                    await MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.LibraryCantFindGame, gameProfile.GamePath));
                    return (false, loaderExe, loaderDll);
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.FastIo ||
                gameProfile.EmulationProfile == EmulationProfile.Theatrhythm ||
                gameProfile.EmulationProfile == EmulationProfile.NxL2 ||
                gameProfile.EmulationProfile == EmulationProfile.GunslingerStratos3)
            {
                if (!CheckiDMAC(gameProfile.GamePath, gameProfile.Is64Bit))
                    return (false, loaderExe, loaderDll);
            }

            if (gameProfile.RequiresBepInEx)
            {
                if (!await CheckBepinEx(gameProfile.GamePath, gameProfile.Is64Bit))
                {
                    return (false, loaderExe, loaderDll);
                }
            }

            if (gameProfile.FileName.Contains("PullTheTrigger.xml"))
            {
                if (!await CheckPTTDll(gameProfile.GamePath))
                {
                    return (false, loaderExe, loaderDll);
                }
            }

            if (gameProfile.EmulationProfile == EmulationProfile.NxL2)
            {
                if (!await CheckNxl2Core(gameProfile.GamePath))
                {
                    return (false, loaderExe, loaderDll);
                }
            }

            //For banapass support (ie don't do this if banapass support is unchecked.)
            if (gameProfile.GameNameInternal == "Wangan Midnight Maximum Tune 6" &&
                gameProfile.ConfigValues.Find(x => x.FieldName == "Banapass Connection").FieldValue == "1")
            {
                if (!checkbngrw(gameProfile.GamePath))
                    return (false, loaderExe, loaderDll);
            }

            if (gameProfile.RequiresAdmin)
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    if (!admin)
                    {
                        if (!await MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryNeedsAdmin, gameProfile.GameNameInternal)))
                            return (false, loaderExe, loaderDll);
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

                err += "\n\nTry to start it anyway?";

                if (!await MessageBoxHelper.ErrorYesNo(err))
                    return (false, loaderExe, loaderDll);
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

                    if (!await MessageBoxHelper.WarningYesNo(errorMsg))
                    {
                        return (false, loaderExe, loaderDll);
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
                    // if (!string.IsNullOrWhiteSpace(t.BindNameRi) && string.IsNullOrWhiteSpace(t.RawInputButton.DevicePath))
                    // {
                    //     Debug.WriteLine("Keybind without path: button: {0} bind: {1}", t.ButtonName, t.BindNameRi);

                    // Handle special binds first
                    // if (t.BindNameRi == "Windows Mouse Cursor")
                    // {
                    //     t.RawInputButton.DevicePath = "Windows Mouse Cursor";
                    //     fixedSomething = true;
                    // }
                    // else if (t.BindNameRi == "None")
                    // {
                    //     t.RawInputButton.DevicePath = "None";
                    //     fixedSomething = true;
                    // }
                    // else if (t.BindNameRi.ToLower().StartsWith("unknown device"))
                    // {
                    //     t.RawInputButton.DevicePath = "null";
                    //     fixedSomething = true;
                    // }
                    // else
                    {
                        // Find device
                        RawInputDevice device = null;

                        // if (t.RawInputButton.DeviceType == RawDeviceType.Mouse)
                        //     device = _joystickControlRawInput.GetMouseDeviceByBindName(t.BindNameRi);
                        // else if (t.RawInputButton.DeviceType == RawDeviceType.Keyboard)
                        //     device = _joystickControlRawInput.GetKeyboardDeviceByBindName(t.BindNameRi);

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
            // if (fixedSomething)
            // {
            JoystickHelper.SerializeGameProfile(gameProfile);
            await library.ListUpdate(gameProfile.GameNameInternal);
            //}
            return (true, loaderExe, loaderDll);
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
                return MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryBadiDMAC, bngrw)).Result;
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

        private static async Task<bool> CheckNxl2Core(string gamePath)
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
                        await MessageBoxHelper.ErrorOK("NxL2Core.dll has been tampered with and no original version exists");
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
                        await MessageBoxHelper.ErrorOK("NxL2Core.dll has been tampered with and no original version exists");
                        return false;
                    }
                }
            }
            return true;
        }

        private async static Task<bool> CheckBepinEx(string gamePath, bool is64BitGame)
        {
            string dllPathBase = Path.Combine(Path.GetDirectoryName(gamePath), "winhttp.dll");
            string messageBoxText = $"This game requires BepInEx to be installed into the game folder in order to run via TP.\n" +
                    $"You need the {(is64BitGame ? "64-bit (win_x64)" : "32-bit (win_x86)")} version.\n\n" +
                    $"You can download it here: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2 \n" +
                    $"Do you want to open the download page now?";

            if (!File.Exists(dllPathBase))
            {
                if (await MessageBoxHelper.WarningYesNo(messageBoxText))
                {
                    _ = Process.Start(new ProcessStartInfo("explorer.exe", "https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2") { UseShellExecute = true });
                }
                return false;
            }

            // Let's check that its the right architecture
            if (DllArchitectureChecker.IsDll64Bit(dllPathBase, out bool is64Bit))
            {
                if (is64Bit != is64BitGame)
                {
                    string messageBoxText2 = $"This game requires BepInEx installed, but you are currently using an incompatible version.\n" +
                        $"You are using the {(is64Bit ? "64-bit (win_x64)" : "32-bit (win_x86)")} version.\n" +
                        $"You need the {(is64BitGame ? "64-bit (win_x64)" : "32-bit (win_x86)")} version.\n\n" +
                        $"You can download it here: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2 \n" +
                        $"Do you want to open the download page now?";

                    if (await MessageBoxHelper.WarningYesNo(messageBoxText2))
                    {
                        _ = Process.Start(new ProcessStartInfo("explorer.exe", "https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2") { UseShellExecute = true });
                    }
                    return false;
                }
            }
            else
            {
                await MessageBoxHelper.ErrorOK("Could not check bitness. wtf");
                return false;
            }
            return true;
        }

        private static async Task<bool> CheckPTTDll(string gamePath)
        {
            var dllPathBase = Path.Combine(Path.GetDirectoryName(gamePath), "WkWin32.dll");
            if (!File.Exists(dllPathBase))
            {
                var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(gamePath));
                var dllPathParent = Path.Combine(parentDir, "WkWin32.dll");
                if (!File.Exists(dllPathBase))
                {
                    await MessageBoxHelper.ErrorOK("WkWin32.dll could not be found. Please make sure it is next to the game .exe or else it will not run");
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
                return MessageBoxHelper.WarningYesNo(string.Format(Properties.Resources.LibraryBadiDMAC, iDmacDrv)).Result;
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
            var gameList = this.FindControl<ListBox>("gameList");
            if (gameList == null || gameList.Items.Count == 0)
                return;

            // Get MainWindow instance
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.contentControl.Content = _gameSettings;
                }
            }
        }

        /// <summary>
        /// This button opens the controller settings option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnControllerSettings(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            if (gameList == null || gameList.Items.Count == 0)
                return;

            Joystick = new JoystickControl(_contentControl, this);
            Joystick.LoadNewSettings(_gameNames[gameList.SelectedIndex], (ListBoxItem)gameList.SelectedItem);
            Joystick.Listen();

            // Get MainWindow instance
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.contentControl.Content = Joystick;
                }
            }
        }

        /// <summary>
        /// This button actually launches the game selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnLaunchGame(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            var ChkTestMenu = this.FindControl<CheckBox>("ChkTestMenu");

            if (gameList == null || gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            if (Lazydata.ParrotData.SaveLastPlayed)
            {
                Lazydata.ParrotData.LastPlayed = gameProfile.GameNameInternal;
                JoystickHelper.Serialize();
            }

            bool testMenu = false;
            if (ChkTestMenu.IsChecked.HasValue)
            {
                testMenu = ChkTestMenu.IsChecked.Value;
            }

            var result = ValidateAndRun(gameProfile, false, this, testMenu).Result;

            if (result.success)
            {
                var gameRunning = new GameRunning(gameProfile, result.loaderExe, result.loaderDll, testMenu, false, false, this);

                // Get MainWindow instance
                if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.contentControl.Content = gameRunning;
                    }
                }
            }
        }

        /// <summary>
        /// This starts the MD5 verifier that checks whether a game is a clean dump
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnVerifyGame(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            if (gameList == null || gameList.Items.Count == 0)
                return;

            var selectedGame = _gameNames[gameList.SelectedIndex];
            if (!File.Exists(selectedGame.ValidMd5))
            {
                MessageBoxHelper.InfoOK(Properties.Resources.LibraryNoHashes).Wait();
            }
            else
            {
                // Get MainWindow instance
                if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.contentControl.Content = new VerifyGame(selectedGame.GamePath, selectedGame.ValidMd5);
                    }
                }
            }
        }

        private void BtnMoreInfo(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            string path = string.Empty;

            if (gameList != null && gameList.Items.Count != 0)
            {
                var selectedGame = _gameNames[gameList.SelectedIndex];

                // open game compatibility page
                if (selectedGame != null)
                {
                    path = "compatibility/" + Path.GetFileNameWithoutExtension(selectedGame.FileName) + ".htm";
                }
            }

            var url = "https://teknoparrot.com/wiki/" + path;
            Debug.WriteLine($"opening {url}");

            // Use ProcessStartInfo to open URLs in .NET
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }

        private void BtnOnlineProfile(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            string path = string.Empty;

            if (gameList != null && gameList.Items.Count != 0)
            {
                var selectedGame = _gameNames[gameList.SelectedIndex];

                // open game compatibility page
                if (selectedGame != null && selectedGame.OnlineProfileURL != "")
                {
                    path = selectedGame.OnlineProfileURL;
                }
            }

            Debug.WriteLine($"opening {path}");

            // Use ProcessStartInfo to open URLs in .NET
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }

        private void BtnDownloadMissingIcons(object sender, RoutedEventArgs e)
        {
            if (MessageBoxHelper.WarningYesNo(Properties.Resources.LibraryDownloadAllIcons).Result)
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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var gameList = this.FindControl<ListBox>("gameList");
            var selectedItem = ((ListBoxItem)gameList?.SelectedItem);

            if (selectedItem == null)
            {
                return;
            }

            var selected = (GameProfile)selectedItem.Tag;
            if (selected == null || selected.FileName == null) return;

            var splitString = selected.FileName.Split('\\');
            try
            {
                Debug.WriteLine($@"Removing {selected.GameNameInternal} from TP...");
                File.Delete(Path.Combine("UserProfilesJSON", splitString[1]));
            }
            catch
            {
                // ignored
            }

            await ListUpdate();
        }

        private void BtnPlayOnlineClick(object sender, RoutedEventArgs e)
        {
            // Get MainWindow instance
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.BtnTPOnline2(null, null);
                }
            }
        }
    }
}