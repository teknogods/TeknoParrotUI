using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.Common;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Properties;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AccountPage.xaml
    /// </summary>
    public partial class AccountPage : UserControl
    {
        private string _accessToken;
        private bool _isLoggedIn = false;
        private readonly App _app;
        private static DateTime _lastDataFetchTime = DateTime.MinValue;
        private static UserProfile _cachedUserData;
        private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public AccountPage()
        {
            InitializeComponent();
            _app = (App)Application.Current;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("AccountPage Loaded");
            await CheckLoginStatus();
        }

        private async Task CheckLoginStatus()
        {
            try
            {
                var oAuthHelper = _app.OAuthHelper;

                if (await oAuthHelper.EnsureAuthenticatedAsync(false))
                {
                    _isLoggedIn = true;
                    _accessToken = oAuthHelper.GetAccessToken();
                    string userName = oAuthHelper.GetUserName();

                    LoginStatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.AccountPageLoggedInAs, userName);
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLogoutButton;

                    UserInfoCard.Visibility = Visibility.Visible;

                    if (_cachedUserData != null && DateTime.Now - _lastDataFetchTime < _cacheExpiration)
                    {
                        DisplayUserData(_cachedUserData);
                    }
                    else
                    {
                        await LoadUserData();
                    }

                    await RefreshInitialDOnlineAsync();
                    await RefreshKizunaOnlineAsync();
                }
                else
                {
                    _isLoggedIn = false;
                    LoginStatusText.Text = TeknoParrotUi.Properties.Resources.AccountPageNotLoggedIn;
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
                    UserInfoCard.Visibility = Visibility.Collapsed;
                    UserTierText.Visibility = Visibility.Collapsed;
                    await RefreshInitialDOnlineAsync();
                    await RefreshKizunaOnlineAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.AccountPageLoginError, ex.Message), TeknoParrotUi.Properties.Resources.AccountPageErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayUserData(UserProfile userData)
        {
            SegaIdTextBox.Text = userData.SegaId;
            HighscoreSerialTextBox.Text = userData.HighscoreSerial;
            NamcoIdTextBox.Text = userData.NamcoId;
            MarioKartIDTextBox.Text = userData.MarioKartId;
            GoldenTeePcbIdTextBox.Text = userData.GoldenTeePcbId?.ToString() ?? string.Empty;
            GoldenTeeCardIdTextBox.Text = userData.GoldenTeeCardId ?? string.Empty;
            UserTierText.Text = string.Format(TeknoParrotUi.Properties.Resources.AccountPageTierPrefix, userData.Tier);
            UserTierText.Visibility = Visibility.Visible;
            _initialDRank = userData.InitialDRank;

            UpdateSubscriptionUI(userData);
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

                Debug.WriteLine($"Calling user profile API with token: {_accessToken}");

#if DEBUG && USE_LOCALHOST
                var response = await httpClient.GetAsync("https://localhost:44339/api/User/Profile");
#else
                var response = await httpClient.GetAsync("https://teknoparrot.com/api/User/Profile");
#endif
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"Profile API response: {response.StatusCode}");
                Debug.WriteLine($"Profile content: {responseContent}");

                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseContent) && responseContent.StartsWith("{"))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var userData = JsonSerializer.Deserialize<UserProfile>(responseContent, options);

                    if (userData.ExpirationDate.HasValue)
                    {
                        Debug.WriteLine($"User subscription expiration date: {userData.ExpirationDate.Value}");
                    }
                    else
                    {
                        Debug.WriteLine("User subscription expiration date: Not available");
                    }

                    DisplayUserData(userData);

                    Lazydata.ParrotData.SegaId = userData.SegaId;
                    Lazydata.ParrotData.ScoreSubmissionID = userData.HighscoreSerial;
                    Lazydata.ParrotData.NamcoId = userData.NamcoId;
                    Lazydata.ParrotData.MarioKartId = userData.MarioKartId;
                    // Null while Golden Tee online is off on the website: keep what was there
                    if (userData.GoldenTeePcbId.HasValue && !string.IsNullOrEmpty(userData.GoldenTeeCardId))
                    {
                        Lazydata.ParrotData.GoldenTeePcbId = userData.GoldenTeePcbId.Value.ToString();
                        Lazydata.ParrotData.GoldenTeeCardId = userData.GoldenTeeCardId;
                    }

                    JoystickHelper.Serialize();
                    Debug.WriteLine($"Saved user data - SegaId: {userData.SegaId}");

                    _cachedUserData = userData;
                    _lastDataFetchTime = DateTime.Now;
                }
                else
                {
                    Debug.WriteLine("Response wasn't valid JSON or request failed");
                    MessageBox.Show(TeknoParrotUi.Properties.Resources.AccountPageCouldNotRetrieveUserInfo, TeknoParrotUi.Properties.Resources.AccountPageDataError, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user data: {ex.Message}");
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.AccountPageErrorLoadingUserData, ex.Message), TeknoParrotUi.Properties.Resources.AccountPageDataError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoginLogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var oAuthHelper = _app.OAuthHelper;
                LoginLogoutButton.IsEnabled = false;

                if (_isLoggedIn)
                {
                    oAuthHelper.Logout();
                    _isLoggedIn = false;

                    LoginStatusText.Text = TeknoParrotUi.Properties.Resources.AccountPageNotLoggedIn;
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
                    UserInfoCard.Visibility = Visibility.Collapsed;

                    SegaIdTextBox.Text = string.Empty;
                    HighscoreSerialTextBox.Text = string.Empty;
                    NamcoIdTextBox.Text = string.Empty;
                    MarioKartIDTextBox.Text = string.Empty;
                    GoldenTeePcbIdTextBox.Text = string.Empty;
                    GoldenTeeCardIdTextBox.Text = string.Empty;
                    UserTierText.Text = TeknoParrotUi.Properties.Resources.AccountPageTierNone;
                    UserTierText.Visibility = Visibility.Collapsed;
                    _initialDRank = null;
                    _initialDMachine = null;
                    _initialDMachineKnown = false;
                    ShowInitialDWebsite(false, R.InitialDManualNotLoggedIn);
                    ShowKizunaWebsite(false, R.KizunaManualNotLoggedIn);
                    _kizunaMachine = null;
                    _kizunaMachineKnown = false;

                    _cachedUserData = null;
                    _lastDataFetchTime = DateTime.MinValue;
                }
                else
                {
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoggingIn;
                    bool success = await oAuthHelper.AuthenticateAsync();

                    if (success)
                    {
                        _isLoggedIn = true;
                        _accessToken = oAuthHelper.GetAccessToken();
                        string userName = oAuthHelper.GetUserName();

                        LoginStatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.AccountPageLoggedInAs, userName);
                        LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLogoutButton;
                        UserInfoCard.Visibility = Visibility.Visible;
                        UserTierText.Visibility = Visibility.Visible;
                        await LoadUserData();
                        await RefreshInitialDOnlineAsync();
                        await RefreshKizunaOnlineAsync();
                    }
                    else
                    {
                        LoginStatusText.Text = TeknoParrotUi.Properties.Resources.AccountPageLoginFailed;
                        LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.AccountPageLoginLogoutErrorMessage, ex.Message), TeknoParrotUi.Properties.Resources.AccountPageLoginLogoutError, MessageBoxButton.OK, MessageBoxImage.Error);
                LoginLogoutButton.Content = _isLoggedIn ? TeknoParrotUi.Properties.Resources.AccountPageLogoutButton : TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
            }
            finally
            {
                LoginLogoutButton.IsEnabled = true;
            }
        }

        private void UpdateSubscriptionUI(UserProfile userData)
        {
            if (userData.IsSubscribed && userData.Serials != null && userData.Serials.Count > 0)
            {
                SubscriptionCard.Visibility = Visibility.Visible;
                Debug.WriteLine("Subscription card visible");
                // Set expiry date
                if (userData.ExpirationDate.HasValue)
                {
                    ExpiryDateText.Text = userData.ExpirationDate.Value.ToString("d");
                }
                else
                {
                    ExpiryDateText.Text = TeknoParrotUi.Properties.Resources.AccountPageExpiryUnknown;
                }

                // Populate serials dropdown
                var serialViewModels = userData.Serials.Select(s => new SerialViewModel { Serial = s }).ToList();
                SerialsComboBox.ItemsSource = serialViewModels;
                SerialsComboBox.DisplayMemberPath = "DisplayText";
                SerialsComboBox.ItemContainerStyle = Resources["SerialComboBoxItemStyle"] as Style;

                var activeSerial = serialViewModels.FirstOrDefault(s => s.IsActiveOnThisMachine);
                if (activeSerial != null)
                {
                    SerialsComboBox.SelectedItem = activeSerial;
                    RegisterSerialButton.Content = TeknoParrotUi.Properties.Resources.AccountPageReactivateButton;
                }
                else
                {
                    var firstAvailable = serialViewModels.FirstOrDefault(s => s.CanSelect);
                    if (firstAvailable != null)
                    {
                        SerialsComboBox.SelectedItem = firstAvailable;
                        RegisterSerialButton.Content = TeknoParrotUi.Properties.Resources.AccountPageRegisterButton;
                    }
                    else
                    {
                        RegisterSerialButton.IsEnabled = false;
                    }
                }
            }
            else
            {
                SubscriptionCard.Visibility = Visibility.Collapsed;
                Debug.WriteLine("Subscription card hidden");
            }
        }

        private void SerialsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedSerial = SerialsComboBox.SelectedItem as SerialViewModel;
            RegisterSerialButton.IsEnabled = selectedSerial != null && selectedSerial.CanSelect;
        }

        private async void RegisterSerialButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedSerial = SerialsComboBox.SelectedItem as SerialViewModel;
            if (selectedSerial == null || !selectedSerial.CanSelect)
                return;

            var dialogContent = new Grid
            {
                Width = 450,
                Margin = new Thickness(20)
            };

            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleTextBlock = new TextBlock
            {
                Text = TeknoParrotUi.Properties.Resources.AccountPageRegistrationProgress,
                TextWrapping = TextWrapping.Wrap,
                Style = Application.Current.FindResource("MaterialDesignHeadline4TextBlock") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(titleTextBlock, 0);

            var statusTextBlock = new TextBlock
            {
                Text = TeknoParrotUi.Properties.Resources.AccountPageInitializing,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(statusTextBlock, 1);

            var scrollViewer = new ScrollViewer
            {
                Height = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scrollViewer, 2);

            var outputTextBox = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Stretch,
                BorderThickness = new Thickness(0)
            };
            scrollViewer.Content = outputTextBox;

            var closeButton = new Button
            {
                Content = TeknoParrotUi.Properties.Resources.AccountPageCloseButton,
                Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                Command = DialogHost.CloseDialogCommand,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(closeButton, 3);

            dialogContent.Children.Add(titleTextBlock);
            dialogContent.Children.Add(statusTextBlock);
            dialogContent.Children.Add(scrollViewer);
            dialogContent.Children.Add(closeButton);

            DialogHost.Show(dialogContent);

            try
            {
                RegisterSerialButton.IsEnabled = false;
                bool deregisterNeeded = false;

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
                {
                    deregisterNeeded = key != null && key.GetValue("PatreonSerialKey") != null;
                }

                if (deregisterNeeded)
                {
                    statusTextBlock.Text = TeknoParrotUi.Properties.Resources.AccountPageDeregisteringCurrentKey;
                    await Task.Run(() => DeregisterCurrentKey((msg) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            outputTextBox.AppendText(msg + Environment.NewLine);
                            outputTextBox.ScrollToEnd();
                        });
                    }));
                }

                statusTextBlock.Text = TeknoParrotUi.Properties.Resources.AccountPageRegisteringNewKey;
                await Task.Run(() => RegisterNewKey(selectedSerial.Serial.Serial, (msg) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        outputTextBox.AppendText(msg + Environment.NewLine);
                        outputTextBox.ScrollToEnd();
                    });
                }));

                statusTextBlock.Text = TeknoParrotUi.Properties.Resources.AccountPageRefreshingSubscriptionInfo;
                await LoadUserData();

                statusTextBlock.Text = TeknoParrotUi.Properties.Resources.AccountPageRegistrationComplete;
                closeButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                statusTextBlock.Text = TeknoParrotUi.Properties.Resources.AccountPageRegistrationError;
                outputTextBox.AppendText($"{TeknoParrotUi.Properties.Resources.Error}: {ex.Message}" + Environment.NewLine);
                if (LocalActivationRecovery.TryHandle(Window.GetWindow(this), ex, out var removed))
                {
                    if (removed)
                    {
                        // Recreate the labels from the local state without changing the chosen paid key.
                        var serials = SerialsComboBox.Items.Cast<SerialViewModel>()
                            .Select(s => new SerialViewModel { Serial = s.Serial }).ToList();
                        SerialsComboBox.ItemsSource = serials;
                        SerialsComboBox.SelectedItem = serials.FirstOrDefault(s => s.Serial == selectedSerial.Serial);
                        RegisterSerialButton.Content = TeknoParrotUi.Properties.Resources.AccountPageRegisterButton;
                        statusTextBlock.Text = TeknoParrotUi.Properties.Resources.LocalActivationRemoved;
                        outputTextBox.AppendText(statusTextBlock.Text + Environment.NewLine);
                    }
                }
                else
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.AccountPageErrorDuringRegistration, ex.Message), TeknoParrotUi.Properties.Resources.AccountPageRegistrationError,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                outputTextBox.ScrollToEnd();
                closeButton.Visibility = Visibility.Visible;
            }
            finally
            {
                RegisterSerialButton.IsEnabled = SerialsComboBox.SelectedItem is SerialViewModel current && current.CanSelect;
            }
        }

        private void DeregisterCurrentKey(Action<string> outputCallback)
        {
            BudgieDeactivation.Deactivate(".\\TeknoParrot\\BudgieLoader.exe", outputCallback);
        }

        private void RegisterNewKey(string serialKey, Action<string> outputCallback)
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
                    outputCallback(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputCallback($"Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        // =============================================================================================================
        // Initial D Online (InitialDServer docs/IDENTITY_AND_BADGES.md 4.4). Everything here is started by the user on this
        // page; nothing runs at game launch. The rank is display only (api/User/Profile); the games get theirs from the
        // Initial D server, never from TeknoParrotUI.
        // =============================================================================================================

        private int? _initialDRank;
        private InitialDOnlineHelper.MachineInfo _initialDMachine;
        private bool _initialDMachineKnown;
        private InitialDOnlineHelper.ApiClient _initialDApi;
        private bool _initialDBusy;

        private InitialDOnlineHelper.ApiClient InitialDApi =>
            _initialDApi ??= new InitialDOnlineHelper.ApiClient(InitialDTokenAsync, InitialDFreshLoginAsync);

        private bool InitialDSupporter => _initialDRank.HasValue && _initialDRank.Value >= 1 && _initialDRank.Value <= 5;

        private async Task<string> InitialDTokenAsync()
        {
            var oAuthHelper = _app.OAuthHelper;
            return await oAuthHelper.EnsureAuthenticatedAsync(false) ? oAuthHelper.GetAccessToken() : null;
        }

        /// <summary>The step-up the secret reads need: the browser login again with prompt=login (bounded wait).</summary>
        private async Task<bool> InitialDFreshLoginAsync()
        {
            ShowInitialDMessage(R.InitialDOnlineFreshLoginPrompt, false);
            var login = _app.OAuthHelper.AuthenticateAsync(true);
            var finished = await Task.WhenAny(login, Task.Delay(TimeSpan.FromMinutes(5)));
            return finished == login && login.Result;
        }

        private async Task RefreshInitialDOnlineAsync()
        {
            // The card is always there: an Online ID can be entered by hand. Its website part needs the login and
            // teknoparrot.com's Initial D Online.
            if (!_isLoggedIn)
            {
                ShowInitialDWebsite(false, R.InitialDManualNotLoggedIn);
                UpdateInitialDView();
                return;
            }

            var result = await InitialDApi.GetMachineAsync();
            if (result.Error == InitialDOnlineHelper.ApiError.FeatureOff || result.Error == InitialDOnlineHelper.ApiError.NotLoggedIn)
            {
                ShowInitialDWebsite(false, result.Error == InitialDOnlineHelper.ApiError.FeatureOff ? R.InitialDManualWebsiteOff : R.InitialDManualNotLoggedIn);
                UpdateInitialDView();
                return;
            }

            ShowInitialDWebsite(true, null);
            if (result.Ok)
            {
                _initialDMachine = result.Value;
                _initialDMachineKnown = true;
            }
            else if (result.Error == InitialDOnlineHelper.ApiError.NoMachine)
            {
                _initialDMachine = null;
                _initialDMachineKnown = true;
            }
            else
            {
                ShowInitialDMessage(InitialDOnlineHelper.DescribeError(result), true);
            }
            UpdateInitialDView();
        }

        private string SelectedVisibility() =>
            InitialDVisPublic.IsChecked == true ? "public" :
            InitialDVisOwn.IsChecked == true ? "own" :
            InitialDVisOff.IsChecked == true ? "off" : null;

        private void UpdateInitialDView()
        {
            var rank = InitialDOnlineHelper.Rank(_initialDRank);
            InitialDRankPanel.Visibility = rank != null ? Visibility.Visible : Visibility.Collapsed;
            if (rank != null)
                StyleRankBadge(InitialDRankBadge, InitialDRankText, rank, true);
            InitialDKingAuraText.Visibility = rank != null && rank.Id7KingAura ? Visibility.Visible : Visibility.Collapsed;
            InitialDPeasantNotice.Visibility = rank != null && rank.Tier == 0 ? Visibility.Visible : Visibility.Collapsed;
            BuildInitialDLadder(rank);

            var local = InitialDOnlineHelper.LocalCredential();
            var machine = _initialDMachine;
            bool known = _initialDMachineKnown;
            bool here = machine != null && local.HasValue && InitialDOnlineHelper.SamePcbId(local.Value.PcbId, machine.PcbId);
            var visibility = SelectedVisibility();

            InitialDPcbIdTextBox.Text = machine == null ? "" : InitialDOnlineHelper.NormalizePcbId(machine.PcbId) ?? machine.PcbId;
            var text = "";
            if (known)
            {
                if (machine == null)
                {
                    text = R.InitialDOnlineNotRegistered;
                    if (InitialDSupporter && visibility == null)
                        text += " " + R.InitialDOnlineChooseVisibilityFirst;
                }
                else
                {
                    text = here ? R.InitialDOnlineRegisteredHere : R.InitialDOnlineRegisteredElsewhere;
                    if (machine.Status == "banned")
                        text += " " + R.InitialDOnlineErrorBanned;
                    else if (machine.Status == "suspended")
                        text += " " + R.InitialDOnlineMachineSuspended;
                    if (!string.IsNullOrEmpty(machine.LastSeenUtc))
                        text += " " + string.Format(R.InitialDOnlineLastSeen, FormatUtc(machine.LastSeenUtc));
                    if (!string.IsNullOrEmpty(machine.MachineChangedUtc))
                        text += " " + string.Format(R.InitialDOnlineMachineChanged, FormatUtc(machine.MachineChangedUtc));
                }
            }
            InitialDMachineText.Text = text;

            // Supporters choose who sees their rank; there is no default (spec 2.7 / 3.15). Free accounts get the notice.
            InitialDVisibilityPanel.Visibility = known && InitialDSupporter ? Visibility.Visible : Visibility.Collapsed;
            InitialDSaveVisibilityButton.Visibility = machine != null ? Visibility.Visible : Visibility.Collapsed;
            InitialDSaveVisibilityButton.IsEnabled = !_initialDBusy && visibility != null;
            if (visibility != null && rank != null)
            {
                InitialDPreviewPanel.Visibility = Visibility.Visible;
                StyleRankBadge(InitialDPreviewBadge, InitialDPreviewText, visibility == "public" ? rank : InitialDOnlineHelper.Ladder[0], true);
            }
            else
            {
                InitialDPreviewPanel.Visibility = Visibility.Collapsed;
            }

            InitialDRegisterButton.Visibility = known && machine == null ? Visibility.Visible : Visibility.Collapsed;
            InitialDRegisterButton.IsEnabled = !_initialDBusy && (!InitialDSupporter || visibility != null);
            InitialDUseHereButton.Visibility = machine != null && !here ? Visibility.Visible : Visibility.Collapsed;
            InitialDRegenerateButton.Visibility = machine != null ? Visibility.Visible : Visibility.Collapsed;
            InitialDRemoveButton.Visibility = machine != null ? Visibility.Visible : Visibility.Collapsed;
            InitialDUseHereButton.IsEnabled = !_initialDBusy;
            InitialDRegenerateButton.IsEnabled = !_initialDBusy;
            InitialDRemoveButton.IsEnabled = !_initialDBusy;

            var status = InitialDOnlineHelper.ReadNewestStatus();
            InitialDLastStatusText.Text = status == null
                ? R.InitialDOnlineNoStatus
                : string.Format(R.InitialDOnlineLastStatus, status.Title, status.WrittenUtc.ToLocalTime().ToString("g"), InitialDOnlineHelper.Describe(status));
        }

        private static string FormatUtc(string value) =>
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var utc)
                ? utc.ToLocalTime().ToString("g")
                : value;

        private static System.Windows.Media.Color RankColor(string rrggbb) =>
            System.Windows.Media.Color.FromRgb(Convert.ToByte(rrggbb.Substring(0, 2), 16), Convert.ToByte(rrggbb.Substring(2, 2), 16), Convert.ToByte(rrggbb.Substring(4, 2), 16));

        /// <summary>
        /// The insignia in its material: bronze / silver plain, gold with a glow from GENERAL, and for 5-STAR GENERAL and
        /// PRESIDENT a fire / platinum gradient with a pulsing glow (motion is exclusive to the top two ranks, spec 2.2).
        /// </summary>
        private static void StyleRankBadge(Border badge, TextBlock text, InitialDOnlineHelper.RankStyle rank, bool animate)
        {
            text.Text = rank.Display;
            text.Effect = null;
            badge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x14, 0x14, 0x18));
            if (rank.Color == null)
            {
                // PEASANT: the plain word, no colour, no frame.
                text.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xC8, 0xC8));
                badge.BorderThickness = new Thickness(0);
                return;
            }

            var main = RankColor(rank.Color);
            if (rank.Color2 != null)
            {
                var gradient = new System.Windows.Media.LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(RankColor(rank.Color2), 0.0));
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(main, 0.55));
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(RankColor(rank.Color2), 1.0));
                text.Foreground = gradient;
            }
            else
            {
                text.Foreground = new System.Windows.Media.SolidColorBrush(main);
            }
            badge.BorderBrush = new System.Windows.Media.SolidColorBrush(main);
            badge.BorderThickness = new Thickness(rank.Stars ? 2 : 1);

            if (rank.Stars)
            {
                var glow = new System.Windows.Media.Effects.DropShadowEffect { Color = main, ShadowDepth = 0, BlurRadius = rank.Motion ? 16 : 10, Opacity = 0.9 };
                text.Effect = glow;
                if (rank.Motion && animate)
                {
                    var pulse = new System.Windows.Media.Animation.DoubleAnimation(8, 28, TimeSpan.FromSeconds(1.1))
                    {
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    };
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, pulse);
                }
            }
        }

        /// <summary>The ladder, the own rank lit and the others dimmed ("locked").</summary>
        private void BuildInitialDLadder(InitialDOnlineHelper.RankStyle current)
        {
            InitialDLadderPanel.Children.Clear();
            if (current == null)
            {
                InitialDLadderPanel.Visibility = Visibility.Collapsed;
                return;
            }
            InitialDLadderPanel.Visibility = Visibility.Visible;
            foreach (var rank in InitialDOnlineHelper.Ladder)
            {
                var text = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12 };
                var badge = new Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 6), Child = text };
                StyleRankBadge(badge, text, rank, false);
                badge.Opacity = rank.Tier == current.Tier ? 1.0 : 0.45;
                InitialDLadderPanel.Children.Add(badge);
            }
        }

        private void ShowInitialDMessage(string message, bool error)
        {
            InitialDMessageText.Text = message ?? "";
            InitialDMessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            if (error)
                InitialDMessageText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x40, 0x30));
            else
                InitialDMessageText.ClearValue(TextBlock.ForegroundProperty);
        }

        /// <summary>Runs one user action (buttons disabled meanwhile), then reloads the machine card.</summary>
        private async Task RunInitialDAsync(Func<Task<(bool Ok, string Message)>> work)
        {
            if (_initialDBusy)
                return;
            _initialDBusy = true;
            UpdateInitialDView();
            ShowInitialDMessage(R.InitialDOnlineWorking, false);
            (bool Ok, string Message) outcome;
            try
            {
                outcome = await work();
            }
            catch (Exception ex)
            {
                outcome = (false, string.Format(R.InitialDOnlineErrorGeneric, ex.Message));
            }
            try
            {
                await RefreshInitialDOnlineAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: refresh failed: {ex.Message}");
            }
            finally
            {
                _initialDBusy = false;
                UpdateInitialDView();
            }
            ShowInitialDMessage(outcome.Message, !outcome.Ok);
        }

        private static (bool Ok, string Message) StoreInitialDPair(InitialDOnlineHelper.CredentialInfo pair)
        {
            var profiles = InitialDOnlineHelper.StoreCredential(pair.PcbId, pair.Secret);
            return (true, string.Format(R.InitialDOnlineSaved, profiles));
        }

        private async void InitialDRegister_Click(object sender, RoutedEventArgs e)
        {
            var visibility = SelectedVisibility();
            if (InitialDSupporter && visibility == null)
            {
                ShowInitialDMessage(R.InitialDOnlineChooseVisibilityFirst, true);
                return;
            }
            var consent = visibility == null ? null : new InitialDOnlineHelper.ConsentChoice { Visibility = visibility };
            await RunInitialDAsync(async () =>
            {
                var result = await InitialDApi.ProvisionAsync(Environment.MachineName, consent);
                return result.Ok ? StoreInitialDPair(result.Value) : (false, InitialDOnlineHelper.DescribeError(result));
            });
        }

        private async void InitialDUseHere_Click(object sender, RoutedEventArgs e)
        {
            if (!MessageBoxHelper.WarningYesNo(R.InitialDOnlineConfirmUseHere))
                return;
            await RunInitialDAsync(async () =>
            {
                var result = await InitialDApi.GetCredentialAsync();
                return result.Ok ? StoreInitialDPair(result.Value) : (false, InitialDOnlineHelper.DescribeError(result));
            });
        }

        private async void InitialDRegenerate_Click(object sender, RoutedEventArgs e)
        {
            var machine = _initialDMachine;
            if (machine == null || !MessageBoxHelper.WarningYesNo(R.InitialDOnlineConfirmRegenerate))
                return;
            await RunInitialDAsync(async () =>
            {
                var result = await InitialDApi.RegenerateAsync(machine.PcbId);
                return result.Ok ? StoreInitialDPair(result.Value) : (false, InitialDOnlineHelper.DescribeError(result));
            });
        }

        private async void InitialDRemove_Click(object sender, RoutedEventArgs e)
        {
            var machine = _initialDMachine;
            if (machine == null || !MessageBoxHelper.WarningYesNo(R.InitialDOnlineConfirmRemove))
                return;
            await RunInitialDAsync(async () =>
            {
                var result = await InitialDApi.RevokeAsync(machine.PcbId);
                if (!result.Ok)
                    return (false, InitialDOnlineHelper.DescribeError(result));
                InitialDOnlineHelper.ClearCredential(machine.PcbId);
                return (true, R.InitialDOnlineRemoved);
            });
        }

        private async void InitialDSaveVisibility_Click(object sender, RoutedEventArgs e)
        {
            var visibility = SelectedVisibility();
            if (visibility == null)
                return;
            await RunInitialDAsync(async () =>
            {
                var result = await InitialDApi.SaveConsentAsync(new InitialDOnlineHelper.ConsentChoice { Visibility = visibility });
                return result.Ok ? (true, R.InitialDOnlineVisibilitySaved) : (false, InitialDOnlineHelper.DescribeError(result));
            });
        }

        private void InitialDVisibility_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                UpdateInitialDView();
        }

        private void InitialDLearnMore_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: cannot open {e.Uri}: {ex.Message}");
            }
            e.Handled = true;
        }

        /// <summary>
        /// The card's website part (rank, the account's Online ID, register ...), or the note why it is not there; the
        /// by-hand part is always shown, with this PC's Online ID when it has one.
        /// </summary>
        private void ShowInitialDWebsite(bool visible, string note)
        {
            InitialDWebsitePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            InitialDWebsiteNote.Text = note ?? "";
            InitialDWebsiteNote.Visibility = string.IsNullOrEmpty(note) ? Visibility.Collapsed : Visibility.Visible;
            // this PC's Online ID, also right after register / regenerate / remove; the secret is never shown
            var local = InitialDOnlineHelper.LocalCredential();
            InitialDManualIdTextBox.Text = local.HasValue ? local.Value.PcbId : "";
            InitialDManualSecretBox.Clear();
        }

        /// <summary>By hand, logged in or not: the Online ID and its secret go into ParrotData and every Initial D profile.</summary>
        private void InitialDManualSave_Click(object sender, RoutedEventArgs e)
        {
            var id = InitialDOnlineHelper.NormalizePcbId(InitialDManualIdTextBox.Text);
            if (id == null)
            {
                ShowInitialDMessage(R.InitialDManualBadId, true);
                return;
            }
            if (id.StartsWith(KizunaOnlineHelper.PcbIdPrefix + "-", StringComparison.Ordinal))
            {
                ShowInitialDMessage(R.InitialDManualKizunaId, true);
                return;
            }
            var secret = (InitialDManualSecretBox.Password ?? "").Trim();
            if (!InitialDOnlineHelper.IsValidSecret(secret))
            {
                ShowInitialDMessage(R.InitialDManualBadSecret, true);
                return;
            }
            var updated = InitialDOnlineHelper.StoreCredential(id, secret);
            InitialDManualIdTextBox.Text = id;
            InitialDManualSecretBox.Clear();
            ShowInitialDMessage(string.Format(R.InitialDOnlineSaved, updated), false);
            UpdateInitialDView();
        }

        /// <summary>By hand: this PC forgets its Online ID (ParrotData and the Initial D profiles that hold it).</summary>
        private void InitialDManualClear_Click(object sender, RoutedEventArgs e)
        {
            var local = InitialDOnlineHelper.LocalCredential();
            var id = local.HasValue ? local.Value.PcbId : InitialDManualIdTextBox.Text;
            InitialDOnlineHelper.ClearCredential(id ?? "");
            InitialDManualIdTextBox.Text = "";
            InitialDManualSecretBox.Clear();
            ShowInitialDMessage(R.InitialDOnlineRemoved, false);
            UpdateInitialDView();
        }

        /// <summary>A paste into the by-hand boxes (see <see cref="PasteOnlineId"/>).</summary>
        private void InitialDManual_Pasting(object sender, DataObjectPastingEventArgs e) =>
            PasteOnlineId(e, InitialDOnlineHelper.ReadPasted(PastedText(e)), InitialDManualIdTextBox, InitialDManualSecretBox);

        // =============================================================================================================
        // Senjou no Kizuna Online (KizunaOnlineHelper): this PC's own Kizuna Online ID (a PCB ID + secret apart from
        // Initial D's) from the website's api/Kizuna/, written into the Kizuna profiles. The game plays online only, so it
        // needs it. The rank is the account's TeknoParrot.com rank (api/User/Profile, the value the Initial D card shows),
        // display only, and Kizuna always shows it: no visibility choice, no preview. Everything here is started by the
        // user on this page.
        // =============================================================================================================

        private KizunaOnlineHelper.MachineInfo _kizunaMachine;
        private bool _kizunaMachineKnown;
        private KizunaOnlineHelper.ApiClient _kizunaApi;
        private bool _kizunaBusy;

        private KizunaOnlineHelper.ApiClient KizunaApi =>
            _kizunaApi ??= new KizunaOnlineHelper.ApiClient(KizunaTokenAsync, KizunaFreshLoginAsync);

        private async Task<string> KizunaTokenAsync()
        {
            var oAuthHelper = _app.OAuthHelper;
            return await oAuthHelper.EnsureAuthenticatedAsync(false) ? oAuthHelper.GetAccessToken() : null;
        }

        /// <summary>The step-up the secret reads need, announced on the Kizuna card.</summary>
        private async Task<bool> KizunaFreshLoginAsync()
        {
            ShowKizunaMessage(R.InitialDOnlineFreshLoginPrompt, false);
            var login = _app.OAuthHelper.AuthenticateAsync(true);
            var finished = await Task.WhenAny(login, Task.Delay(TimeSpan.FromMinutes(5)));
            return finished == login && login.Result;
        }

        private async Task RefreshKizunaOnlineAsync()
        {
            // The card is always there: an Online ID can be entered by hand. Its website part needs the login and
            // teknoparrot.com's Senjou no Kizuna section.
            if (!_isLoggedIn)
            {
                ShowKizunaWebsite(false, R.KizunaManualNotLoggedIn);
                return;
            }

            var result = await KizunaApi.GetMachineAsync();
            if (result.Error == KizunaOnlineHelper.ApiError.FeatureOff || result.Error == KizunaOnlineHelper.ApiError.NotLoggedIn)
            {
                ShowKizunaWebsite(false, result.Error == KizunaOnlineHelper.ApiError.FeatureOff ? R.KizunaManualWebsiteOff : R.KizunaManualNotLoggedIn);
                return;
            }

            ShowKizunaWebsite(true, null);
            if (result.Ok)
            {
                _kizunaMachine = result.Value;
                _kizunaMachineKnown = true;
            }
            else if (result.Error == KizunaOnlineHelper.ApiError.NoMachine)
            {
                _kizunaMachine = null;
                _kizunaMachineKnown = true;
            }
            else
            {
                ShowKizunaMessage(KizunaOnlineHelper.DescribeError(result), true);
            }
            UpdateKizunaView();
        }

        /// <summary>
        /// The card's website part (rank, the account's Online ID, register ...), or the note why it is not there; the
        /// by-hand part is always shown, with this PC's Online ID when it has one.
        /// </summary>
        private void ShowKizunaWebsite(bool visible, string note)
        {
            KizunaWebsitePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            KizunaWebsiteNote.Text = note ?? "";
            KizunaWebsiteNote.Visibility = string.IsNullOrEmpty(note) ? Visibility.Collapsed : Visibility.Visible;
            // this PC's Online ID, also right after register / regenerate / remove; the secret is never shown
            var local = KizunaOnlineHelper.LocalCredential();
            KizunaManualIdTextBox.Text = local.HasValue ? local.Value.PcbId : "";
            KizunaManualSecretBox.Clear();
        }

        /// <summary>By hand: the Online ID and its secret go into ParrotData and every Senjou no Kizuna profile.</summary>
        private void KizunaManualSave_Click(object sender, RoutedEventArgs e)
        {
            var id = KizunaOnlineHelper.NormalizePcbId(KizunaManualIdTextBox.Text);
            if (id == null)
            {
                ShowKizunaMessage(R.KizunaManualBadId, true);
                return;
            }
            if (id.StartsWith(KizunaOnlineHelper.InitialDPcbIdPrefix + "-", StringComparison.Ordinal))
            {
                ShowKizunaMessage(R.KizunaManualInitialDId, true);
                return;
            }
            var secret = (KizunaManualSecretBox.Password ?? "").Trim();
            if (!KizunaOnlineHelper.IsValidSecret(secret))
            {
                ShowKizunaMessage(R.KizunaManualBadSecret, true);
                return;
            }
            var updated = KizunaOnlineHelper.StoreCredential(id, secret);
            KizunaManualIdTextBox.Text = id;
            KizunaManualSecretBox.Clear();
            ShowKizunaMessage(string.Format(R.KizunaOnlineSaved, updated), false);
            UpdateKizunaView();
        }

        /// <summary>By hand: this PC forgets its Online ID (ParrotData and the Senjou no Kizuna profiles that hold it).</summary>
        private void KizunaManualClear_Click(object sender, RoutedEventArgs e)
        {
            var local = KizunaOnlineHelper.LocalCredential();
            var id = local.HasValue ? local.Value.PcbId : KizunaManualIdTextBox.Text;
            KizunaOnlineHelper.ClearCredential(id ?? "");
            KizunaManualIdTextBox.Text = "";
            KizunaManualSecretBox.Clear();
            ShowKizunaMessage(R.KizunaOnlineRemoved, false);
            UpdateKizunaView();
        }

        /// <summary>A paste into the by-hand boxes (see <see cref="PasteOnlineId"/>).</summary>
        private void KizunaManual_Pasting(object sender, DataObjectPastingEventArgs e) =>
            PasteOnlineId(e, KizunaOnlineHelper.ReadPasted(PastedText(e)), KizunaManualIdTextBox, KizunaManualSecretBox);

        /// <summary>
        /// A paste into a card's by-hand boxes: an Online ID or a secret as teknoparrot.com shows them goes into its own
        /// box, both at once from the website's OnlineID / OnlineSecret lines. Any other text pastes as usual.
        /// </summary>
        private static void PasteOnlineId(DataObjectPastingEventArgs e, (string PcbId, string Secret) found, TextBox idBox, PasswordBox secretBox)
        {
            if (found.PcbId == null && found.Secret == null)
                return;
            e.CancelCommand();
            if (found.PcbId != null)
                idBox.Text = found.PcbId;
            if (found.Secret != null)
                secretBox.Password = found.Secret;
        }

        private static string PastedText(DataObjectPastingEventArgs e) =>
            e.DataObject.GetDataPresent(DataFormats.UnicodeText, true)
                ? e.DataObject.GetData(DataFormats.UnicodeText, true) as string
                : null;

        private void UpdateKizunaView()
        {
            // the account's rank, always shown in Kizuna: the badge and the ladder, nothing to choose
            var rank = KizunaOnlineHelper.Rank(_initialDRank);
            KizunaRankPanel.Visibility = rank != null ? Visibility.Visible : Visibility.Collapsed;
            if (rank != null)
                StyleKizunaRankBadge(KizunaRankBadge, KizunaRankText, rank, true);
            BuildKizunaLadder(rank);

            var local = KizunaOnlineHelper.LocalCredential();
            var machine = _kizunaMachine;
            bool known = _kizunaMachineKnown;
            bool here = machine != null && local.HasValue && KizunaOnlineHelper.SamePcbId(local.Value.PcbId, machine.PcbId);

            KizunaPcbIdTextBox.Text = machine == null ? "" : KizunaOnlineHelper.NormalizePcbId(machine.PcbId) ?? machine.PcbId;
            var text = "";
            if (known)
            {
                if (machine == null)
                {
                    text = R.KizunaOnlineNotRegistered;
                }
                else
                {
                    text = here ? R.KizunaOnlineRegisteredHere : R.KizunaOnlineRegisteredElsewhere;
                    if (machine.Status == "banned")
                        text += " " + R.KizunaOnlineErrorBanned;
                    else if (machine.Status == "suspended")
                        text += " " + R.InitialDOnlineMachineSuspended;
                    if (!string.IsNullOrEmpty(machine.LastSeenUtc))
                        text += " " + string.Format(R.InitialDOnlineLastSeen, FormatUtc(machine.LastSeenUtc));
                    if (!string.IsNullOrEmpty(machine.MachineChangedUtc))
                        text += " " + string.Format(R.InitialDOnlineMachineChanged, FormatUtc(machine.MachineChangedUtc));
                }
            }
            KizunaMachineText.Text = text;

            KizunaRegisterButton.Visibility = known && machine == null ? Visibility.Visible : Visibility.Collapsed;
            KizunaUseHereButton.Visibility = machine != null && !here ? Visibility.Visible : Visibility.Collapsed;
            KizunaRegenerateButton.Visibility = machine != null ? Visibility.Visible : Visibility.Collapsed;
            KizunaRemoveButton.Visibility = machine != null ? Visibility.Visible : Visibility.Collapsed;
            KizunaRegisterButton.IsEnabled = !_kizunaBusy;
            KizunaUseHereButton.IsEnabled = !_kizunaBusy;
            KizunaRegenerateButton.IsEnabled = !_kizunaBusy;
            KizunaRemoveButton.IsEnabled = !_kizunaBusy;
        }

        /// <summary>The Initial D card's badge materials (bronze, silver, gold, fire, platinum) with Kizuna's in-game mark.</summary>
        private static void StyleKizunaRankBadge(Border badge, TextBlock text, KizunaOnlineHelper.RankStyle rank, bool animate)
        {
            StyleRankBadge(badge, text, new InitialDOnlineHelper.RankStyle
            {
                Tier = rank.Tier,
                Name = rank.Name,
                Insignia = rank.Mark,
                Color = rank.Color,
                Color2 = rank.Color2,
            }, animate);
            text.Text = rank.Display;
        }

        /// <summary>Kizuna's ladder, the own rank lit and the others dimmed ("locked").</summary>
        private void BuildKizunaLadder(KizunaOnlineHelper.RankStyle current)
        {
            KizunaLadderPanel.Children.Clear();
            if (current == null)
            {
                KizunaLadderPanel.Visibility = Visibility.Collapsed;
                return;
            }
            KizunaLadderPanel.Visibility = Visibility.Visible;
            foreach (var rank in KizunaOnlineHelper.Ladder)
            {
                var text = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12 };
                var badge = new Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 6), Child = text };
                StyleKizunaRankBadge(badge, text, rank, false);
                badge.Opacity = rank.Tier == current.Tier ? 1.0 : 0.45;
                KizunaLadderPanel.Children.Add(badge);
            }
        }

        private void ShowKizunaMessage(string message, bool error)
        {
            KizunaMessageText.Text = message ?? "";
            KizunaMessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            if (error)
                KizunaMessageText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x40, 0x30));
            else
                KizunaMessageText.ClearValue(TextBlock.ForegroundProperty);
        }

        /// <summary>Runs one user action on the Kizuna card (its buttons disabled meanwhile), then reloads it.</summary>
        private async Task RunKizunaAsync(Func<Task<(bool Ok, string Message)>> work)
        {
            if (_kizunaBusy)
                return;
            _kizunaBusy = true;
            UpdateKizunaView();
            ShowKizunaMessage(R.InitialDOnlineWorking, false);
            (bool Ok, string Message) outcome;
            try
            {
                outcome = await work();
            }
            catch (Exception ex)
            {
                outcome = (false, string.Format(R.InitialDOnlineErrorGeneric, ex.Message));
            }
            try
            {
                await RefreshKizunaOnlineAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KizunaOnline: refresh failed: {ex.Message}");
            }
            finally
            {
                _kizunaBusy = false;
                UpdateKizunaView();
            }
            ShowKizunaMessage(outcome.Message, !outcome.Ok);
        }

        private static (bool Ok, string Message) StoreKizunaPair(KizunaOnlineHelper.CredentialInfo pair)
        {
            var profiles = KizunaOnlineHelper.StoreCredential(pair.PcbId, pair.Secret);
            return (true, string.Format(R.KizunaOnlineSaved, profiles));
        }

        private async void KizunaRegister_Click(object sender, RoutedEventArgs e)
        {
            // no consent step: Kizuna always shows the rank
            await RunKizunaAsync(async () =>
            {
                var result = await KizunaApi.ProvisionAsync(Environment.MachineName);
                return result.Ok ? StoreKizunaPair(result.Value) : (false, KizunaOnlineHelper.DescribeError(result));
            });
        }

        private async void KizunaUseHere_Click(object sender, RoutedEventArgs e)
        {
            if (!MessageBoxHelper.WarningYesNo(R.KizunaOnlineConfirmUseHere))
                return;
            await RunKizunaAsync(async () =>
            {
                var result = await KizunaApi.GetCredentialAsync();
                return result.Ok ? StoreKizunaPair(result.Value) : (false, KizunaOnlineHelper.DescribeError(result));
            });
        }

        private async void KizunaRegenerate_Click(object sender, RoutedEventArgs e)
        {
            var machine = _kizunaMachine;
            if (machine == null || !MessageBoxHelper.WarningYesNo(R.KizunaOnlineConfirmRegenerate))
                return;
            await RunKizunaAsync(async () =>
            {
                var result = await KizunaApi.RegenerateAsync(machine.PcbId);
                return result.Ok ? StoreKizunaPair(result.Value) : (false, KizunaOnlineHelper.DescribeError(result));
            });
        }

        private async void KizunaRemove_Click(object sender, RoutedEventArgs e)
        {
            var machine = _kizunaMachine;
            if (machine == null || !MessageBoxHelper.WarningYesNo(R.KizunaOnlineConfirmRemove))
                return;
            await RunKizunaAsync(async () =>
            {
                var result = await KizunaApi.RevokeAsync(machine.PcbId);
                if (!result.Ok)
                    return (false, KizunaOnlineHelper.DescribeError(result));
                KizunaOnlineHelper.ClearCredential(machine.PcbId);
                return (true, R.KizunaOnlineRemoved);
            });
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
            // Golden Tee online; null while the website has it switched off.
            public int? GoldenTeePcbId { get; set; }
            public string GoldenTeeCardId { get; set; }
            public bool IsSubscribed { get; set; }
            public List<SerialStatus> Serials { get; set; }
            public DateTime? ExpirationDate { get; set; }
            // Initial D Online rank from the website (0 = PEASANT .. 5 = PRESIDENT), display only; null on older sites.
            public int? InitialDRank { get; set; }
            public string InitialDRankName { get; set; }
        }

        public class SerialStatus
        {
            public string Serial { get; set; }
            public bool IsActive { get; set; }
            public bool IsInUse { get; set; }
            public DateTime? ExpireDate { get; set; }
            public bool IsGifted { get; set; }
        }

        private class SerialViewModel
        {
            public SerialStatus Serial { get; set; }

            public bool CanSelect => !Serial.IsInUse && !Serial.IsGifted;

            public bool IsActiveOnThisMachine
            {
                get
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
                    {
                        if (key != null)
                        {
                            var keyValue = key.GetValue("PatreonSerialKey");

                            if (keyValue is byte[] byteArray)
                            {
                                string currentSerial = System.Text.Encoding.UTF8.GetString(byteArray);
                                return !string.IsNullOrEmpty(currentSerial) && currentSerial.Equals(Serial.Serial);
                            }
                            else if (keyValue is string currentSerial)
                            {
                                return !string.IsNullOrEmpty(currentSerial) && currentSerial.Equals(Serial.Serial);
                            }
                        }
                        return false;
                    }
                }
            }

            public string DisplayText
            {
                get
                {
                    //string text = ObfuscateSerial(Serial.Serial);
                    string text = Serial.Serial;
                    if (IsActiveOnThisMachine)
                        text += TeknoParrotUi.Properties.Resources.AccountPageSerialInUseThisDevice;
                    else if (Serial.IsInUse)
                        text += TeknoParrotUi.Properties.Resources.AccountPageSerialInUse;
                    if (Serial.IsGifted)
                        text += TeknoParrotUi.Properties.Resources.AccountPageSerialGifted;
                    return text;
                }
            }

            // So I can record a clip of how this works without showing my serials.
            private string ObfuscateSerial(string serial)
            {
                if (string.IsNullOrEmpty(serial)) return serial;

                int dashIndex = serial.IndexOf('-');
                if (dashIndex > 0)
                {
                    string part1 = new string('X', dashIndex);
                    string part2 = new string('X', serial.Length - dashIndex - 1);
                    return part1 + "-" + part2;
                }

                return new string('X', serial.Length);
            }
        }

        private class ProgressDialogContext : INotifyPropertyChanged
        {
            private string _status = TeknoParrotUi.Properties.Resources.AccountPageInitializing;
            private string _output = string.Empty;
            private bool _isComplete = false;

            public string Status
            {
                get => _status;
                set
                {
                    if (_status != value)
                    {
                        _status = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                    }
                }
            }

            public string Output
            {
                get => _output;
                set
                {
                    if (_output != value)
                    {
                        _output = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Output)));
                    }
                }
            }

            public bool IsComplete
            {
                get => _isComplete;
                set
                {
                    if (_isComplete != value)
                    {
                        _isComplete = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComplete)));
                    }
                }
            }

            public void UpdateStatus(string status)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = status;
                });
            }

            public void AppendOutput(string text)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Output += text + Environment.NewLine;
                });
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
