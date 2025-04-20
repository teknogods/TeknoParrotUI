using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using TeknoParrotUi.Common;
using TeknoParrotUi.Components;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using System.Linq;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for MainWindow.axaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Add these properties to the MainWindow class
        private string _currentTheme = "Theme33";

        public bool IsTheme33Active => _currentTheme == "Theme33";
        public bool IsWhiteoutActive => _currentTheme == "ThemeWhiteout";
        public bool IsBluehatActive => _currentTheme == "ThemeBluehat";
        public bool IsObsidianActive => _currentTheme == "ThemeObsidian";
        public bool IsEmberActive => _currentTheme == "ThemeEmber";
        public bool IsFrostActive => _currentTheme == "ThemeFrost";
        public bool IsEchoActive => _currentTheme == "ThemeEcho";
        public bool IsVoidActive => _currentTheme == "ThemeVoid";
        public bool IsCyberActive => _currentTheme == "ThemeCyber";
        public event PropertyChangedEventHandler PropertyChanged;
        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedTheme = selectedItem.Tag as string;
                if (selectedTheme != null)
                {
                    // Remove all theme classes first
                    Classes.Remove("Theme33");
                    Classes.Remove("ThemeWhiteout");
                    Classes.Remove("ThemeBluehat");
                    Classes.Remove("ThemeObsidian");
                    Classes.Remove("ThemeEmber");
                    Classes.Remove("ThemeFrost");
                    Classes.Remove("ThemeEcho");
                    Classes.Remove("ThemeVoid");
                    Classes.Remove("ThemeCyber");

                    // Set new theme class
                    string themeClass = selectedTheme == "Default" ? "Theme33" : "Theme" + selectedTheme;
                    Classes.Add(themeClass);

                    // Update current theme property
                    _currentTheme = themeClass;

                    // Notify UI that theme properties have changed to update visibility
                    OnPropertyChanged(nameof(IsTheme33Active));
                    OnPropertyChanged(nameof(IsWhiteoutActive));
                    OnPropertyChanged(nameof(IsBluehatActive));
                    OnPropertyChanged(nameof(IsObsidianActive));
                    OnPropertyChanged(nameof(IsEmberActive));
                    OnPropertyChanged(nameof(IsFrostActive));
                    OnPropertyChanged(nameof(IsEchoActive));
                    OnPropertyChanged(nameof(IsVoidActive));
                    OnPropertyChanged(nameof(IsCyberActive));

                    // Save theme preference
                    if (Lazydata.ParrotData != null)
                    {
                        Lazydata.ParrotData.UiTheme = selectedTheme;
                        JoystickHelper.Serialize();
                    }
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            // Explicitly raise the INotifyPropertyChanged event
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            DetectAndApplyLayout();
        }


        // Add the command property
        public ICommand CloseSplitViewCommand { get; }
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                if (_isMenuOpen != value)
                {
                    _isMenuOpen = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMenuOpen)));
                    Debug.WriteLine($"IsMenuOpen property changed to {_isMenuOpen}");
                }
            }
        }
        private bool _isMenuOpen;

        //public static TeknoParrotOnline TpOnline = new TeknoParrotOnline();
        //public static UserLogin UserLogin = new UserLogin();
        private readonly About _about = new About();
        private readonly Library _library;
        private readonly Patreon _patron = new Patreon();
        private readonly AddGame _addGame;
        private UpdaterDialog _updater;
        private bool _showingDialog;
        private bool _allowClose;
        public bool _updaterComplete = false;
        private bool _cefInit = false;
        public List<GitHubUpdates> updates = new List<GitHubUpdates>();
        private void DetectAndApplyLayout()
        {
            var bounds = this.Bounds;
            double width = bounds.Width;
            double height = bounds.Height;

            // Define layout modes
            bool isMobile = width < 640;
            bool isCompactMode = width >= 640 && width < 800;
            bool isMediumMode = width >= 800 && width < 1200;
            bool isWideMode = width >= 1200;
            bool isSteamDeckMode = Math.Abs(width - 1280) < 20 && Math.Abs(height - 800) < 20;
            bool isVerticalOrientation = height > width;

            // Clear all layout-specific classes
            this.Classes.Remove("MobileMode");
            this.Classes.Remove("CompactMode");
            this.Classes.Remove("MediumMode");
            this.Classes.Remove("WideMode");
            this.Classes.Remove("SteamDeckMode");
            this.Classes.Remove("VerticalMode");

            // Apply appropriate classes
            if (isMobile) this.Classes.Add("MobileMode");
            if (isCompactMode) this.Classes.Add("CompactMode");
            if (isMediumMode) this.Classes.Add("MediumMode");
            if (isWideMode) this.Classes.Add("WideMode");
            if (isSteamDeckMode) this.Classes.Add("SteamDeckMode");
            if (isVerticalOrientation) this.Classes.Add("VerticalMode");

            // Update layout for specific containers
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid != null && mainGrid.ColumnDefinitions.Count > 1)
            {
                if (isMobile || isVerticalOrientation)
                {
                    // For very small screens or vertical orientation, collapse second column
                    mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                }
                else
                {
                    // For larger screens, use proportional sizing
                    mainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                }
            }

            // Special handling for Steam Deck
            if (isSteamDeckMode)
            {
                // Adjust UI elements for better touch interaction on Steam Deck
            }
        }

        //private readonly GameScanner _gameScanner = new GameScanner();


        private SplitView _drawerHost;

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the menu state
            //IsMenuOpen = !IsMenuOpen;
            Debug.WriteLine($"Menu button clicked - toggling to {IsMenuOpen}");

            // Use direct lookup instead of caching the result
            var drawerHost = this.Find<SplitView>("DrawerHost");

            // This is the most important part - make sure it exists and update it
            if (drawerHost != null)
            {
                drawerHost.IsPaneOpen = IsMenuOpen;
                Debug.WriteLine($"Updated DrawerHost.IsPaneOpen = {drawerHost.IsPaneOpen}");
            }
            else
            {
                Debug.WriteLine("ERROR: Could not find DrawerHost control!");

                // Try additional methods to find the control
                var allControls = this.GetVisualDescendants().OfType<SplitView>().ToList();
                Debug.WriteLine($"Found {allControls.Count} SplitView controls in visual tree");

                if (allControls.Count > 0)
                {
                    allControls[0].IsPaneOpen = IsMenuOpen;
                    Debug.WriteLine($"Updated first SplitView.IsPaneOpen = {allControls[0].IsPaneOpen}");
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Set default theme based on saved preference or use "Default"
            DataContext = this;
            CloseSplitViewCommand = new TeknoParrotUi.Helpers.DelegateCommand(() =>
            {
                Debug.WriteLine("CloseMenu called from command execution");
                IsMenuOpen = false;
            });
            // var userWindowSize = new WindowSizeHelper();
            // this.WindowStartupLocation = WindowStartupLocation.Manual;
            // this.Height = userWindowSize.WindowHeight;
            // this.Width = userWindowSize.WindowWidth;
            // this.Position = new PixelPoint((int)userWindowSize.WindowLeft, (int)userWindowSize.WindowTop);

            Directory.CreateDirectory("Icons");
            _library = new Library(contentControl);
            _addGame = new AddGame(contentControl, _library);
            contentControl.Content = _library;
            Title = "TeknoParrot UI " + GameVersion.CurrentVersion;

            // Material.Avalonia snackbar initialization
            SaveCompleteSnackbar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            SaveCompleteSnackbar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;

            // For Avalonia, we'll implement our own basic message queue system
            UpdateTitleBar();
            this.Closing += Window_Closing;
            // addGameButton.Click += BtnAddGame;
            // settingsButton.Click += BtnSettings;
            // tpo2Button.Click += BtnTPOnline2;
            // debugButton.Click += BtnDebug;
            // patreonButton.Click += BtnPatreon;
            // downloadMissingIconsButton.Click += BtnDownloadMissingIcons;
            // aboutButton.Click += BtnAbout;
            // romScannerButton.Click += BtnRomScanner;
            // quitButton.Click += BtnQuit;
            // checkUpdatesButton.Click += BtnCheckUpdates;
            exitButton.Click += BtnQuit;
            minimizeButton.Click += BtnMinimize;
            this.SizeChanged += Window_SizeChanged;
            this.GetObservable(BoundsProperty).Subscribe(_ => DetectAndApplyLayout());
            this.GetObservable(WidthProperty).Subscribe(_ => DetectAndApplyLayout());
            this.GetObservable(HeightProperty).Subscribe(_ => DetectAndApplyLayout());
            ThemeSelector_SelectionChanged(null, null);
        }

        private void CloseMenu()
        {
            IsMenuOpen = false;
        }

        //this is a WIP, not working yet
        public async void redistCheck()
        {
            if (await MessageBoxHelper.ConfirmationOKCancel("It appears that this is your first time starting TeknoParrot, it is highly recommended that you install all the Visual C++ Runtimes for the highest compatibility with games. If you would like TeknoParrot to download and install them for you, click Yes, otherwise click No. If you're not sure if you have them all installed, click Yes."))
            {
                Debug.WriteLine("user chose yes, AAAAAAAAAA");
                // Implementation needed
            }
            else
            {
                Debug.WriteLine("user chose no, not gonna download them");
            }
        }

        public void ShowMessage(string message)
        {
            // Show message using our custom snackbar or notification system
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Simplified implementation for Avalonia
                if (SaveCompleteSnackbar is Panel snackbarPanel)
                {
                    // Create a simple notification
                    var notification = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(16, 8),
                        Margin = new Thickness(0, 0, 0, 8),
                        Child = new TextBlock
                        {
                            Text = message,
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap
                        }
                    };

                    snackbarPanel.Children.Add(notification);

                    // Auto-remove after delay
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };

                    timer.Tick += (s, e) =>
                    {
                        snackbarPanel.Children.Remove(notification);
                        timer.Stop();
                    };

                    timer.Start();
                }
            });
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
            UpdateTitleBar();
            contentControl.Content = _library;
        }

        /// <summary>
        /// Shuts down the Discord integration then quits the program, terminating any threads that may still be running.
        /// </summary>
        public static void SafeExit()
        {
            try
            {
                // Handle WebView cleanup for Avalonia
                // Replace with Avalonia WebView cleanup if needed
            }
            catch
            {
                // do nothing. this might happen if the TPO window hasnt been opened, so not an issue
            }

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
            var settings = new UserControls.SettingsControl(contentControl, _library);
            contentControl.Content = settings;
        }

        private async Task<bool> ShowConfirmExitAsync()
        {
            return await MessageBoxHelper.ConfirmationOKCancel(Properties.Resources.MainAreYouSure);
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
                var result = await ShowConfirmExitAsync();
                _showingDialog = false;

                //If the user didn't click "Yes", return
                if (!result) return;
            }

            // var windowSize = new WindowSizeHelper
            // {
            //     WindowHeight = this.Height,
            //     WindowWidth = this.Width,
            //     WindowTop = this.Position.Y,
            //     WindowLeft = this.Position.X
            // };
            // windowSize.Save();

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
                var result = await ShowConfirmExitAsync();
                _showingDialog = false;

                //If the user didn't click "Yes", return;
                if (!result) return;
            }

            // var windowSize = new WindowSizeHelper
            // {
            //     WindowHeight = this.Height,
            //     WindowWidth = this.Width,
            //     WindowTop = this.Position.Y,
            //     WindowLeft = this.Position.X
            // };
            // windowSize.Save();

            _allowClose = true;
            _library.Joystick.StopListening();
            SafeExit();
        }

        /// <summary>
        /// Manually trigger the update check
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCheckUpdates(object sender, RoutedEventArgs e)
        {
            checkForUpdates(false, true);
        }

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
                var url = $"https://api.github.com/repos/{(!string.IsNullOrEmpty(component.userName) ? component.userName : "teknogods")}/{reponame}/releases/tags/{component.name}{secret}";
                Debug.WriteLine($"Updater url for {component.name}: {url}");
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // With this code:
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GithubRelease>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return release;
                }
                else
                {
                    // Handle github exceptions nicely
                    string message = "Unknown exception";
                    string mediaType = response.Content.Headers.ContentType.MediaType;
                    string body = await response.Content.ReadAsStringAsync();
                    HttpStatusCode statusCode = response.StatusCode;

                    if (statusCode == HttpStatusCode.NotFound)
                    {
                        message = "Not found!";
                    }
                    else if (mediaType == "text/html")
                    {
                        message = body.Trim();
                    }
                    else if (mediaType == "application/json")
                    {
                        var json = JObject.Parse(body);
                        message = json["message"]?.ToString();

                        if (message.Contains("API rate limit exceeded"))
                            message = "Update limit exceeded, try again in an hour!";
                    }

                    throw new Exception(message);
                }
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

        private async Task CheckGithub(UpdaterComponent component)
        {
            try
            {
                var githubRelease = await GetGithubRelease(component);
                if (githubRelease != null && githubRelease.assets != null && githubRelease.assets.Count != 0)
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
                        var gh = new GitHubUpdates(component, githubRelease, localVersionString, onlineVersionString);
                        if (!updates.Exists(x => x._componentUpdated.name == gh._componentUpdated.name))
                        {
                            updates.Add(gh);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"release is null? component: {component.name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates for {component.name}:\n{ex.Message}");
            }
        }

        public async void checkForUpdates(bool secondTime, bool manual)
        {
            bool exception = false;

            if (secondTime)
            {
                foreach (UpdaterComponent com in UpdaterComponent.components)
                {
                    com._localVersion = null;
                }

                secondTime = false;
            }
            if (Lazydata.ParrotData.CheckForUpdates || manual)
            {
                ShowMessage("Checking for updates...");
                foreach (UpdaterComponent component in UpdaterComponent.components)
                {
                    try
                    {
                        await CheckGithub(component);
                    }
                    catch (Exception ex)
                    {
                        exception = true;
                        ShowMessage($"Error checking for updates for {component.name}:\n{ex.Message}");
                    }
                }
            }
            else if (!Lazydata.ParrotData.CheckForUpdates && !manual)
            {
                return;
            }
            if (updates.Count > 0)
            {
                ShowMessage("Updates are available!\nSelect \"Install Updates\" from the menu on the left hand side!");
                _updater = new UpdaterDialog(updates, contentControl, _library);
                updateButton.IsVisible = true;
                UpdateAvailableText.IsVisible = true;
            }
            else if (!exception)
            {
                ShowMessage("No updates found.");
                updateButton.IsVisible = false;
                UpdateAvailableText.IsVisible = false;
            }
        }

        /// <summary>
        /// When the window is loaded, the update checker is run and DiscordRPC is set
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool fixNeeded = false;
            //Metadata Fix
            if (!Directory.Exists(".\\Metadata"))
            {
                Directory.CreateDirectory(".\\Metadata");
                if (Directory.Exists(".\\Metadata"))
                {
                    UpdaterComponent tempComponent = new UpdaterComponent
                    {
                        name = "TeknoParrotUI",
                        location = Assembly.GetExecutingAssembly().Location
                    };
                    tempComponent._localVersion = "unknown";
#if !DEBUG
                await CheckGithub(tempComponent);
#endif

                    if (updates.Count > 0)
                    {
                        ShowMessage("Mandatory TeknoParrotUI Update to fix missing metadata!\nPlease install!");
                        _updater = new UpdaterDialog(updates, contentControl, _library);
                        contentControl.Content = _updater;
                    }
                }
                else
                {
                    throw new Exception("Unable to create Metadata folder!");
                }
            }
            if (!fixNeeded)
            {
#if !DEBUG
                await checkForUpdates(false, false);
#endif
            }

            if (Lazydata.ParrotData.UseDiscordRPC)
                DiscordRPC.UpdatePresence(new DiscordRPC.RichPresence
                {
                    details = "Main Menu",
                    largeImageKey = "teknoparrot",
                });
            // Load saved theme
            if (Lazydata.ParrotData != null && !string.IsNullOrEmpty(Lazydata.ParrotData.UiTheme))
            {
                string theme = Lazydata.ParrotData.UiTheme;
                var themeSelector = this.FindControl<ComboBox>("ThemeSelector");
                if (themeSelector != null)
                {
                    themeSelector.SelectedItem = theme;
                }
                else
                {
                    // Apply theme directly if selector not found
                    ThemeSelector_SelectionChanged(null, new SelectionChangedEventArgs(
                        SelectingItemsControl.SelectionChangedEvent,  // Use the correct event instead of property
                        new[] { "Default" },
                        new[] { theme }
                    ));
                }
            }
            else
            {
                // Default theme
                Classes.Add("Theme33");
                _currentTheme = "Theme33";
            }
        }

        /// <summary>
        /// Loads the AddGame screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnAddGame(object sender, RoutedEventArgs e)
        {
            UpdateTitleBar();
            contentControl.Content = _addGame;
        }

        /// <summary>
        /// Loads the patreon screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnPatreon(object sender, RoutedEventArgs e)
        {
            UpdateTitleBar();
            contentControl.Content = _patron;
        }

        /*        private void BtnTPOnline(object sender, RoutedEventArgs e)
                {
                    contentControl.Content = TpOnline;
                }*/

        public void BtnTPOnline2(object sender, RoutedEventArgs e)
        {
            InitCEF();
            UserLogin UserLogin = new UserLogin();
            contentControl.Content = UserLogin;
        }

        private void ColorZone_MouseDown(object sender, PointerPressedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void BtnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            contentControl.Content = _updater;
        }

        private void BtnDebug(object sender, RoutedEventArgs e)
        {
            ModMenu mm = new ModMenu(contentControl, _library);
            contentControl.Content = mm;
        }

        private void InitCEF()
        {
            if (!_cefInit)
            {
                // CEF initialization for Avalonia
                // This would need to be implemented using a WebView control for Avalonia
                // For now, it's commented out as it would need to be reimplemented
                _cefInit = true;
            }
        }

        public string GetPatreonString()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                {
                    return "(Subscribed) ";
                }
                else
                {
                    return "(Support development at teknoparrot.shop)";
                }
            }
        }

        public void UpdateTitleBar()
        {
            TitleName.Text = "TeknoParrot UI " + GetPatreonString() + GameVersion.CurrentVersion;
        }

        private async void BtnDownloadMissingIcons(object sender, RoutedEventArgs e)
        {
            if (await MessageBoxHelper.ConfirmationOKCancel(Properties.Resources.LibraryDownloadAllIcons))
            {
                try
                {
                    // Create a more Avalonia-friendly download window
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

        private void BtnRomScanner(object sender, RoutedEventArgs e)
        {
            GameScanner gameScanner = new GameScanner(contentControl, _library);
            contentControl.Content = gameScanner;
        }

        private void Window_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Avalonia.Controls.Primitives.TemplatedControl.BoundsProperty ||
                e.Property == WidthProperty ||
                e.Property == HeightProperty)
            {
                DetectAndApplyLayout();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DetectAndApplyLayout();
        }
    }
}