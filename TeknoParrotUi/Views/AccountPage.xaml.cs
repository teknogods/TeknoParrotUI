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
using TeknoParrotUi.Properties;

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
                }
                else
                {
                    _isLoggedIn = false;
                    LoginStatusText.Text = TeknoParrotUi.Properties.Resources.AccountPageNotLoggedIn;
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
                    UserInfoCard.Visibility = Visibility.Collapsed;
                    UserTierText.Visibility = Visibility.Collapsed;
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
            UserTierText.Text = string.Format(TeknoParrotUi.Properties.Resources.AccountPageTierPrefix, userData.Tier);
            UserTierText.Visibility = Visibility.Visible;

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

#if DEBUG
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
                    await oAuthHelper.LogoutAsync();
                    _isLoggedIn = false;

                    LoginStatusText.Text = TeknoParrotUi.Properties.Resources.AccountPageNotLoggedIn;
                    LoginLogoutButton.Content = TeknoParrotUi.Properties.Resources.AccountPageLoginButton;
                    UserInfoCard.Visibility = Visibility.Collapsed;

                    SegaIdTextBox.Text = string.Empty;
                    HighscoreSerialTextBox.Text = string.Empty;
                    NamcoIdTextBox.Text = string.Empty;
                    MarioKartIDTextBox.Text = string.Empty;
                    UserTierText.Text = TeknoParrotUi.Properties.Resources.AccountPageTierNone;
                    UserTierText.Visibility = Visibility.Collapsed;

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
                Text = "Registration Progress",
                Style = Application.Current.FindResource("MaterialDesignHeadline4TextBlock") as Style,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(titleTextBlock, 0);

            var statusTextBlock = new TextBlock
            {
                Text = TeknoParrotUi.Properties.Resources.AccountPageInitializing,
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
                outputTextBox.AppendText($"Error: {ex.Message}" + Environment.NewLine);
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.AccountPageErrorDuringRegistration, ex.Message), TeknoParrotUi.Properties.Resources.AccountPageRegistrationError,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                closeButton.Visibility = Visibility.Visible;
            }
            finally
            {
                RegisterSerialButton.IsEnabled = true;
            }
        }

        private void DeregisterCurrentKey(Action<string> outputCallback)
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

            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot", true);
            if (key != null)
            {
                key.DeleteValue("PatreonSerialKey", false);
                key.Close();
            }
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