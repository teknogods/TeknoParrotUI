using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using WPFFolderBrowser;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Views
{
    public partial class SetupWizard : UserControl
    {
        private ContentControl _contentControl;
        private Library _library;
        private int _currentStep = 0;
        private string _datXmlPath = string.Empty;
        private string _gamesPath = string.Empty;
        private bool _isLoggedIn = false;
        private string _accessToken;
        private UserProfile _userData;
        private ProcessStartInfo _cmdStartInfo;
        private Process _cmdProcess;

        // Step titles
        private readonly string[] _stepTitles = {
            TeknoParrotUi.Properties.Resources.SetupWizardWelcomeToTeknoParrotUI,
            TeknoParrotUi.Properties.Resources.SetupWizardConfigureDATXMLFile,
            TeknoParrotUi.Properties.Resources.SetupWizardScanForGames,
            TeknoParrotUi.Properties.Resources.SetupWizardConfigureControls,
            TeknoParrotUi.Properties.Resources.SetupWizardAccountLogin,
            TeknoParrotUi.Properties.Resources.SetupWizardRegisterSerial,
            TeknoParrotUi.Properties.Resources.SetupWizardSetupComplete
        };

        public SetupWizard(ContentControl contentControl, Library library)
        {
            InitializeComponent();
            _contentControl = contentControl;
            _library = library;

            // Start at step 1
            UpdateWizardStep();
        }

        private void UpdateWizardStep()
        {
            // Update step indicator
            StepTitle.Text = _stepTitles[_currentStep];
            StepIndicator.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardStepXOfY, _currentStep + 1, _stepTitles.Length);

            // Hide all step panels
            WelcomePanel.Visibility = Visibility.Collapsed;
            DatXmlPanel.Visibility = Visibility.Collapsed;
            GamesScanPanel.Visibility = Visibility.Collapsed;
            ControlsPanel.Visibility = Visibility.Collapsed;
            AccountLoginPanel.Visibility = Visibility.Collapsed;
            SerialPanel.Visibility = Visibility.Collapsed;
            CompletePanel.Visibility = Visibility.Collapsed;

            // Show the current step panel
            switch (_currentStep)
            {
                case 0: // Welcome
                    WelcomePanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = false;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 1: // DAT/XML
                    DatXmlPanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 2: // Game scan
                    GamesScanPanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 3: // Controls
                    ControlsPanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 4: // Account Login
                    AccountLoginPanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 5: // Serial Registration
                    SerialPanel.Visibility = Visibility.Visible;
                    UpdateSerialPanelVisibility();
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardNext;
                    BtnSkip.Visibility = Visibility.Visible;
                    break;

                case 6: // Complete
                    CompletePanel.Visibility = Visibility.Visible;
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = TeknoParrotUi.Properties.Resources.SetupWizardFinish;
                    BtnSkip.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            // Handle current step completion
            switch (_currentStep)
            {
                case 0: // Completing Welcome
                    _currentStep++;
                    UpdateWizardStep();
                    break;

                case 1: // Completing DAT/XML setup
                    if (ValidateDatXmlStep())
                    {
                        _currentStep++;
                        UpdateWizardStep();
                    }
                    break;

                case 2: // Completing Game Scan
                    if (ValidateGameScanStep())
                    {
                        _currentStep++;
                        UpdateWizardStep();
                    }
                    break;

                case 3: // Completing Controls setup
                    _currentStep++;
                    UpdateWizardStep();
                    break;

                case 4: // Completing Account Login
                    _currentStep++;
                    UpdateWizardStep();
                    break;

                case 5: // Completing Serial Registration
                    _currentStep++;
                    UpdateWizardStep();
                    break;

                case 6: // Finishing setup
                    FinishSetup();
                    break;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                UpdateWizardStep();
            }
        }

        private bool ValidateDatXmlStep()
        {
            if (string.IsNullOrEmpty(TxtDatXmlPath.Text) || !File.Exists(TxtDatXmlPath.Text))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.SetupWizardSelectValidDATXMLFile, TeknoParrotUi.Properties.Resources.SetupWizardInvalidFile, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Save the DAT/XML path
            _datXmlPath = TxtDatXmlPath.Text;
            Lazydata.ParrotData.DatXmlLocation = _datXmlPath;

            return true;
        }

        private bool ValidateGameScanStep()
        {
            // At minimum, make sure the user has clicked Scan
            if (!ChkGamesScanned.IsChecked.HasValue || !ChkGamesScanned.IsChecked.Value)
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.SetupWizardScanForGamesBeforeContinuing, TeknoParrotUi.Properties.Resources.SetupWizardScanRequired, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private void BtnBrowseDatXml_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = TeknoParrotUi.Properties.Resources.SetupWizardDATXMLFilesFilter,
                Title = TeknoParrotUi.Properties.Resources.SetupWizardSelectDATXMLFile
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtDatXmlPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnDownloadDatXml_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Eggmansworld/Datfiles/releases/tag/teknoparrot");
        }

        private void BtnBrowseGamesFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new WPFFolderBrowserDialog();
            folderDialog.Title = TeknoParrotUi.Properties.Resources.SetupWizardSelectGamesFolder;
            if (folderDialog.ShowDialog() == true)
            {
                TxtGamesFolder.Text = folderDialog.FileName;
                _gamesPath = folderDialog.FileName;
            }
        }

        private async void BtnScanGames_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we have a valid DAT/XML file and games folder
            if (string.IsNullOrEmpty(_datXmlPath) || !File.Exists(_datXmlPath))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.SetupWizardSelectValidDATXMLFirst, TeknoParrotUi.Properties.Resources.SetupWizardInvalidDATXML, MessageBoxButton.OK, MessageBoxImage.Warning);
                _currentStep = 1;
                UpdateWizardStep();
                return;
            }

            if (string.IsNullOrEmpty(_gamesPath) || !Directory.Exists(_gamesPath))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.SetupWizardSelectValidGamesFolder, TeknoParrotUi.Properties.Resources.SetupWizardInvalidFolder, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable UI elements during scan
            BtnScanGames.IsEnabled = false;
            BtnBrowseGamesFolder.IsEnabled = false;
            BtnNext.IsEnabled = false;
            BtnBack.IsEnabled = false;
            ChkGamesScanned.IsChecked = false;

            ScanResultsText.Text = TeknoParrotUi.Properties.Resources.SetupWizardStartingScan;

            // Use the existing GameScanner implementation but just to scan, not to display UI
            int gamesFound = 0;

            try
            {
                // Run the scanning process on a background thread
                await Task.Run(() =>
                {
                    // Use the TeknoParrot game scanner logic but we'll handle the UI ourselves
                    var gameProfiles = GameProfileLoader.GameProfiles;

                    // Recursive directory search
                    var foldersToScan = new List<string>() { _gamesPath };
                    var scannedPaths = new HashSet<string>();
                    var foundGames = new List<Tuple<GameProfile, string>>();

                    while (foldersToScan.Count > 0)
                    {
                        string currentFolder = foldersToScan[0];
                        foldersToScan.RemoveAt(0);

                        if (scannedPaths.Contains(currentFolder))
                            continue;

                        scannedPaths.Add(currentFolder);

                        try
                        {
                            // Get subdirectories
                            foreach (var dir in Directory.GetDirectories(currentFolder))
                            {
                                if (!scannedPaths.Contains(dir))
                                    foldersToScan.Add(dir);
                            }

                            // Update UI with current folder
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ScanResultsText.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardScanning, currentFolder);
                            });

                            // Check if this folder contains any games
                            foreach (var gameProfile in gameProfiles)
                            {
                                // Check if the folder contains the game files for this profile
                                bool hasValidFiles = false;
                                if (!string.IsNullOrEmpty(gameProfile.ExecutableName))
                                {
                                    string exePath = Path.Combine(currentFolder, gameProfile.ExecutableName);
                                    hasValidFiles = File.Exists(exePath);
                                }

                                if (hasValidFiles)
                                {
                                    foundGames.Add(new Tuple<GameProfile, string>(gameProfile, currentFolder));

                                    // Update UI with game count
                                    gamesFound++;
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        ScanResultsText.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardFoundXGamesSoFar, gamesFound);
                                    });

                                    break; // Skip other game profiles for this folder
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error scanning folder {currentFolder}: {ex.Message}");
                        }
                    }

                    // Create profiles for found games
                    foreach (var foundGame in foundGames)
                    {
                        try
                        {
                            // Create a new profile by copying the source profile
                            var sourceProfile = foundGame.Item1;
                            var gameProfile = new GameProfile
                            {
                                GamePath = foundGame.Item2,
                                IconName = sourceProfile.IconName,
                                ProfileName = sourceProfile.ProfileName,
                                ExecutableName = sourceProfile.ExecutableName,
                                // Copy other properties as needed
                            };

                            // Copy the joystick buttons if available
                            if (sourceProfile.JoystickButtons != null && sourceProfile.JoystickButtons.Count > 0)
                            {
                                gameProfile.JoystickButtons = new List<JoystickButtons>();
                                foreach (var button in sourceProfile.JoystickButtons)
                                {
                                    gameProfile.JoystickButtons.Add(new JoystickButtons
                                    {
                                        ButtonName = button.ButtonName,
                                        DirectInputButton = button.DirectInputButton,
                                        XInputButton = button.XInputButton,
                                        RawInputButton = button.RawInputButton,
                                        BindName = button.BindName,
                                        BindNameDi = button.BindNameDi,
                                        BindNameXi = button.BindNameXi,
                                        BindNameRi = button.BindNameRi
                                    });
                                }
                            }

                            JoystickHelper.SerializeGameProfile(gameProfile);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error creating profile for {foundGame.Item1.ProfileName}: {ex.Message}");
                        }
                    }
                });

                // Reload all profiles
                GameProfileLoader.LoadProfiles(true);

                // Update the library
                _library.ListUpdate(null);

                // Update UI when done
                if (gamesFound > 0)
                {
                    ScanResultsText.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardScanCompleteFoundXGames, gamesFound);
                    ChkGamesScanned.IsChecked = true;
                }
                else
                {
                    ScanResultsText.Text = TeknoParrotUi.Properties.Resources.SetupWizardScanCompleteNoGames;
                }
            }
            catch (Exception ex)
            {
                ScanResultsText.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardErrorDuringScan, ex.Message);
                Debug.WriteLine($"Scan error: {ex}");
            }
            finally
            {
                // Re-enable UI elements
                BtnScanGames.IsEnabled = true;
                BtnBrowseGamesFolder.IsEnabled = true;
                BtnNext.IsEnabled = true;
                BtnBack.IsEnabled = true;
            }
        }

        private void BtnConfigureControls_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to the multi-game button configuration screen
            var multiGameButtonConfig = new UserControls.MultiGameButtonConfig(_contentControl, _library);
            _contentControl.Content = multiGameButtonConfig;

            // We'll return to the wizard when they close that screen
            // You would need to implement a way to return to this wizard
            // For simplicity, we'll just mark the step as completed
            ChkControlsConfigured.IsChecked = true;
        }


        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                TeknoParrotUi.Properties.Resources.SetupWizardSkipSetupConfirmation,
                TeknoParrotUi.Properties.Resources.SetupWizardSkipSetup,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Don't forget to mark as complete so it doesn't ask every time
                Lazydata.ParrotData.FirstTimeSetupComplete = true;
                JoystickHelper.Serialize();

                _contentControl.Content = _library;
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnLogin.IsEnabled = false;
                BtnLogin.Content = TeknoParrotUi.Properties.Resources.SetupWizardLoggingIn;
                LoginStatus.Text = TeknoParrotUi.Properties.Resources.SetupWizardLaunchingBrowser;

                var oAuthHelper = ((App)Application.Current).OAuthHelper;
                bool success = await oAuthHelper.AuthenticateAsync();

                if (success)
                {
                    _isLoggedIn = true;
                    _accessToken = oAuthHelper.GetAccessToken();
                    string userName = oAuthHelper.GetUserName();

                    LoginStatus.Text = TeknoParrotUi.Properties.Resources.SetupWizardLoginSuccessful;
                    BtnLogin.Content = TeknoParrotUi.Properties.Resources.SetupWizardLoggedIn;
                    BtnLogin.IsEnabled = false;
                    AccountInfoPanel.Visibility = Visibility.Visible;
                    TxtUsername.Text = userName;

                    await LoadUserData();
                }
                else
                {
                    LoginStatus.Text = TeknoParrotUi.Properties.Resources.SetupWizardLoginFailed;
                    BtnLogin.Content = TeknoParrotUi.Properties.Resources.SetupWizardTryAgain;
                    BtnLogin.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LoginStatus.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, ex.Message);
                BtnLogin.Content = TeknoParrotUi.Properties.Resources.SetupWizardTryAgain;
                BtnLogin.IsEnabled = true;
            }
        }

        private async Task LoadUserData()
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

#if DEBUG
                var response = await httpClient.GetAsync("https://localhost:44339/api/User/Profile");
#else
                var response = await httpClient.GetAsync("https://teknoparrot.com/api/User/Profile");
#endif

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        _userData = JsonSerializer.Deserialize<UserProfile>(responseContent, options);

                        TxtTier.Text = _userData.Tier;

                        // Save IDs to Lazydata
                        Lazydata.ParrotData.SegaId = _userData.SegaId;
                        Lazydata.ParrotData.ScoreSubmissionID = _userData.HighscoreSerial;
                        Lazydata.ParrotData.NamcoId = _userData.NamcoId;
                        Lazydata.ParrotData.MarioKartId = _userData.MarioKartId;

                        JoystickHelper.Serialize();

                        // Update serial panel if user has serials
                        if (_userData.IsSubscribed && _userData.Serials != null && _userData.Serials.Count > 0)
                        {
                            OnlineSerialBorder.Visibility = Visibility.Visible;

                            var serialItems = _userData.Serials
                                .Where(s => !s.IsInUse && !s.IsGifted)
                                .Select(s => s.Serial)
                                .ToList();

                            SerialsComboBox.ItemsSource = serialItems;

                            if (serialItems.Count > 0)
                            {
                                SerialsComboBox.SelectedIndex = 0;
                            }
                            else
                            {
                                RegisterSerialButton.IsEnabled = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoginStatus.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardErrorLoadingUserData, ex.Message);
            }
        }

        // Serial Registration Methods
        private void UpdateSerialPanelVisibility()
        {
            if (_isLoggedIn && _userData != null && _userData.IsSubscribed &&
                _userData.Serials != null && _userData.Serials.Count > 0)
            {
                OnlineSerialBorder.Visibility = Visibility.Visible;
            }
            else
            {
                OnlineSerialBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async void RegisterSerialButton_Click(object sender, RoutedEventArgs e)
        {
            if (SerialsComboBox.SelectedItem == null)
                return;

            string selectedSerial = SerialsComboBox.SelectedItem.ToString();
            SerialOutputList.Visibility = Visibility.Visible;
            SerialOutputList.Items.Clear();

            try
            {
                RegisterSerialButton.IsEnabled = false;

                // Check if deregistration is needed
                bool deregisterNeeded = false;
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
                {
                    deregisterNeeded = key != null && key.GetValue("PatreonSerialKey") != null;
                }

                // Deregister if needed
                if (deregisterNeeded)
                {
                    SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardDeregisteringCurrentKey);
                    await DeregisterCurrentKey();
                }

                // Register the new key
                SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardRegisteringNewKey);
                await RegisterKey(selectedSerial);

                SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardRegistrationComplete);
                SerialStatusField.Text = TeknoParrotUi.Properties.Resources.SetupWizardSerialSuccessfullyRegistered;
            }
            catch (Exception ex)
            {
                SerialOutputList.Items.Add(string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, ex.Message));
                SerialStatusField.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, ex.Message);
            }
            finally
            {
                RegisterSerialButton.IsEnabled = true;
            }
        }

        private async void RegisterManualButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSerialManual.Text))
            {
                SerialStatusField.Text = TeknoParrotUi.Properties.Resources.SetupWizardPleaseEnterSerialKey;
                return;
            }

            SerialOutputList.Visibility = Visibility.Visible;
            SerialOutputList.Items.Clear();

            try
            {
                RegisterManualButton.IsEnabled = false;

                // Check if deregistration is needed
                bool deregisterNeeded = false;
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
                {
                    deregisterNeeded = key != null && key.GetValue("PatreonSerialKey") != null;
                }

                // Deregister if needed
                if (deregisterNeeded)
                {
                    SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardDeregisteringCurrentKey);
                    await DeregisterCurrentKey();
                }

                // Register the new key
                SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardRegisteringNewKey);
                await RegisterKey(TxtSerialManual.Text);

                SerialOutputList.Items.Add(TeknoParrotUi.Properties.Resources.SetupWizardRegistrationComplete);
                SerialStatusField.Text = TeknoParrotUi.Properties.Resources.SetupWizardSerialSuccessfullyRegistered;
            }
            catch (Exception ex)
            {
                SerialOutputList.Items.Add(string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, ex.Message));
                SerialStatusField.Text = string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, ex.Message);
            }
            finally
            {
                RegisterManualButton.IsEnabled = true;
            }
        }

        private Task DeregisterCurrentKey()
        {
            return Task.Run(() =>
            {
                var process = new Process();
                var startInfo = new ProcessStartInfo
                {
                    FileName = ".\\TeknoParrot\\BudgieLoader.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = "-deactivate"
                };

                process.StartInfo = startInfo;
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => SerialOutputList.Items.Add(e.Data));
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => SerialOutputList.Items.Add(string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, e.Data)));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot", true);
                if (key != null)
                {
                    key.DeleteValue("PatreonSerialKey", false);
                    key.Close();
                }
            });
        }

        private Task RegisterKey(string serialKey)
        {
            return Task.Run(() =>
            {
                var process = new Process();
                var startInfo = new ProcessStartInfo
                {
                    FileName = ".\\TeknoParrot\\BudgieLoader.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"-register {serialKey}"
                };

                process.StartInfo = startInfo;
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => SerialOutputList.Items.Add(e.Data));
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => SerialOutputList.Items.Add(string.Format(TeknoParrotUi.Properties.Resources.SetupWizardError, e.Data)));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            });
        }

        private void FinishSetup()
        {
            // Save all settings
            JoystickHelper.Serialize();

            // Set a flag indicating setup is complete
            Lazydata.ParrotData.FirstTimeSetupComplete = true;
            JoystickHelper.Serialize();

            // Return to the main library
            _contentControl.Content = _library;

            // Show a welcome message
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(TeknoParrotUi.Properties.Resources.SetupWizardSetupCompleteMessage);
        }

        public void ReturnFromButtonConfig()
        {
            // Put this wizard back in the content control
            _contentControl.Content = this;

            // Mark the controls as configured since we're returning from the button config
            ChkControlsConfigured.IsChecked = true;

            // Update UI to show we're still on the controls step
            UpdateWizardStep();
        }

        private void BtnSkipDatXml_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                TeknoParrotUi.Properties.Resources.SetupWizardSkipDATXMLConfirmation,
                TeknoParrotUi.Properties.Resources.SetupWizardSkipSteps,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _currentStep = 4; // Jump to Account Login step
                UpdateWizardStep();
            }
        }

        private void BtnSkipGameScan_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                TeknoParrotUi.Properties.Resources.SetupWizardSkipGameScanningConfirmation,
                TeknoParrotUi.Properties.Resources.SetupWizardSkipStep,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _currentStep = 3; // Jump to Controls step
                UpdateWizardStep();
            }
        }

        private void BtnSkipControls_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                TeknoParrotUi.Properties.Resources.SetupWizardSkipControlsSetupConfirmation,
                TeknoParrotUi.Properties.Resources.SetupWizardSkipStep,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _currentStep = 4; // Jump to Account Login step
                UpdateWizardStep();
            }
        }

        private class UserProfile
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public string Tier { get; set; }
            public string SegaId { get; set; }
            public string HighscoreSerial { get; set; }
            public string NamcoId { get; set; }
            public string MarioKartId { get; set; }
            public bool IsSubscribed { get; set; }
            public List<SerialStatus> Serials { get; set; }
            public DateTime? ExpirationDate { get; set; }
        }

        public class SerialStatus
        {
            public string Serial { get; set; }
            public bool IsActive { get; set; }
            public bool IsInUse { get; set; }
            public DateTime? ExpireDate { get; set; }
            public bool IsGifted { get; set; }
        }
    }
}