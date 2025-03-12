using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views;
namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for SettingsControl.axaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        ContentControl _contentControl;
        Views.Library _library;
        bool isInitialized = false;

        public SettingsControl()
        {
            InitializeComponent();
        }

        public SettingsControl(ContentControl control, Library library)
        {
            InitializeComponent();

            // reload ParrotData from file
            JoystickHelper.DeSerialize();

            ChkUseSto0ZCheckBox.IsChecked = Lazydata.ParrotData.UseSto0ZDrivingHack;
            sTo0zZonePercent.Value = Lazydata.ParrotData.StoozPercent;
            ChkSaveLastPlayed.IsChecked = Lazydata.ParrotData.SaveLastPlayed;
            ChkUseDiscordRPC.IsChecked = Lazydata.ParrotData.UseDiscordRPC;
            ChkConfirmExit.IsChecked = Lazydata.ParrotData.ConfirmExit;
            ChkCheckForUpdates.IsChecked = Lazydata.ParrotData.CheckForUpdates;
            ChkDownloadIcons.IsChecked = Lazydata.ParrotData.DownloadIcons;
            ChkSilentMode.IsChecked = Lazydata.ParrotData.SilentMode;
            ChkUiDisableHardwareAcceleration.IsChecked = Lazydata.ParrotData.UiDisableHardwareAcceleration;
            ChkFullAxisGas.IsChecked = Lazydata.ParrotData.FullAxisGas;
            ChkFullAxisBrake.IsChecked = Lazydata.ParrotData.FullAxisBrake;
            ChkReverseAxisGas.IsChecked = Lazydata.ParrotData.ReverseAxisGas;
            ChkReverseAxisBrake.IsChecked = Lazydata.ParrotData.ReverseAxisBrake;
            textBoxExitGameKey.Text = Lazydata.ParrotData.ExitGameKey;
            textBoxPauseGameKey.Text = Lazydata.ParrotData.PauseGameKey;
            textBoxScoreSubmissionID.Text = Lazydata.ParrotData.ScoreSubmissionID;
            textBoxScoreCollapseKey.Text = Lazydata.ParrotData.ScoreCollapseGUIKey;
            ChkHideVanguardWarning.IsChecked = Lazydata.ParrotData.HideVanguardWarning;
            ChkUiElf2LogToFile.IsChecked = Lazydata.ParrotData.Elfldr2LogToFile;

            var swatchProvider = new TeknoParrotUi.Helpers.SwatchesProvider();
            UiColour.ItemsSource = swatchProvider.Swatches.Select(a => a.Name).ToList();
            UiColour.SelectedItem = Lazydata.ParrotData.UiColour;
            ChkUiDarkMode.IsChecked = Lazydata.ParrotData.UiDarkMode;
            ChkUiHolidayThemes.IsChecked = Lazydata.ParrotData.UiHolidayThemes;

            if (App.IsPatreon())
            {
                UiPatreon.IsVisible = true;  // Instead of Visibility.Visible
            }
            else
            {
                UiPatreon.IsVisible = false; // Instead of Visibility.Collapsed
            }

            _contentControl = control;
            _library = library;
            isInitialized = true;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            ChkUseSto0ZCheckBox = this.FindControl<CheckBox>("ChkUseSto0ZCheckBox");
            sTo0zZonePercent = this.FindControl<Slider>("sTo0zZonePercent");
            ChkSaveLastPlayed = this.FindControl<CheckBox>("ChkSaveLastPlayed");
            ChkUseDiscordRPC = this.FindControl<CheckBox>("ChkUseDiscordRPC");
            ChkConfirmExit = this.FindControl<CheckBox>("ChkConfirmExit");
            ChkCheckForUpdates = this.FindControl<CheckBox>("ChkCheckForUpdates");
            ChkDownloadIcons = this.FindControl<CheckBox>("ChkDownloadIcons");
            ChkUiDarkMode = this.FindControl<CheckBox>("ChkUiDarkMode");
            ChkUiDisableHardwareAcceleration = this.FindControl<CheckBox>("ChkUiDisableHardwareAcceleration");
            ChkHideVanguardWarning = this.FindControl<CheckBox>("ChkHideVanguardWarning");
            ChkFullAxisGas = this.FindControl<CheckBox>("ChkFullAxisGas");
            ChkFullAxisBrake = this.FindControl<CheckBox>("ChkFullAxisBrake");
            ChkReverseAxisGas = this.FindControl<CheckBox>("ChkReverseAxisGas");
            ChkReverseAxisBrake = this.FindControl<CheckBox>("ChkReverseAxisBrake");
            ChkSilentMode = this.FindControl<CheckBox>("ChkSilentMode");
            ChkUiHolidayThemes = this.FindControl<CheckBox>("ChkUiHolidayThemes");
            ChkUiElf2LogToFile = this.FindControl<CheckBox>("ChkUiElf2LogToFile");
            textBoxExitGameKey = this.FindControl<TextBox>("textBoxExitGameKey");
            textBoxPauseGameKey = this.FindControl<TextBox>("textBoxPauseGameKey");
            textBoxScoreSubmissionID = this.FindControl<TextBox>("textBoxScoreSubmissionID");
            textBoxScoreCollapseKey = this.FindControl<TextBox>("textBoxScoreCollapseKey");
            UiColour = this.FindControl<ComboBox>("UiColour");
            UiPatreon = this.FindControl<StackPanel>("UiPatreon");
            Elfldr2Settings = this.FindControl<StackPanel>("Elfldr2Settings");
            Elfldr2NetworkAdapterCombobox = this.FindControl<NetworkAdapterDropdown>("Elfldr2NetworkAdapterCombobox");

            UiColour.SelectionChanged += UiColour_SelectionChanged;
        }

        private ComboBoxItem CreateJoystickItem(string joystickName, string extraString = "")
        {
            var cbItem = new ComboBoxItem
            {
                Content = string.IsNullOrEmpty(extraString) ? joystickName : joystickName + " " + extraString
            };
            return cbItem;
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                Lazydata.ParrotData.UseSto0ZDrivingHack = ChkUseSto0ZCheckBox.IsChecked != null &&
                                                  ChkUseSto0ZCheckBox.IsChecked.Value;
                Lazydata.ParrotData.StoozPercent = (int)sTo0zZonePercent.Value;


                if (ChkFullAxisGas.IsChecked.HasValue)
                    Lazydata.ParrotData.FullAxisGas = ChkFullAxisGas.IsChecked.Value;
                if (ChkReverseAxisGas.IsChecked.HasValue)
                    Lazydata.ParrotData.ReverseAxisGas = ChkReverseAxisGas.IsChecked.Value;
                if (ChkFullAxisBrake.IsChecked.HasValue)
                    Lazydata.ParrotData.FullAxisBrake = ChkFullAxisBrake.IsChecked.Value;
                if (ChkReverseAxisBrake.IsChecked.HasValue)
                    Lazydata.ParrotData.ReverseAxisBrake = ChkReverseAxisBrake.IsChecked.Value;

                Lazydata.ParrotData.ExitGameKey = textBoxExitGameKey.Text;
                Lazydata.ParrotData.PauseGameKey = textBoxPauseGameKey.Text;
                Lazydata.ParrotData.ScoreSubmissionID = textBoxScoreSubmissionID.Text;
                Lazydata.ParrotData.ScoreCollapseGUIKey = textBoxScoreCollapseKey.Text;
                Lazydata.ParrotData.SaveLastPlayed = ChkSaveLastPlayed.IsChecked.Value;
                Lazydata.ParrotData.UseDiscordRPC = ChkUseDiscordRPC.IsChecked.Value;
                Lazydata.ParrotData.CheckForUpdates = ChkCheckForUpdates.IsChecked.Value;
                Lazydata.ParrotData.SilentMode = ChkSilentMode.IsChecked.Value;
                Lazydata.ParrotData.ConfirmExit = ChkConfirmExit.IsChecked.Value;
                Lazydata.ParrotData.DownloadIcons = ChkDownloadIcons.IsChecked.Value;
                Lazydata.ParrotData.UiDisableHardwareAcceleration = ChkUiDisableHardwareAcceleration.IsChecked.Value;

                Lazydata.ParrotData.UiColour = UiColour.SelectedItem.ToString();
                Lazydata.ParrotData.UiDarkMode = ChkUiDarkMode.IsChecked.Value;
                Lazydata.ParrotData.UiHolidayThemes = ChkUiHolidayThemes.IsChecked.Value;

                Lazydata.ParrotData.HideVanguardWarning = ChkHideVanguardWarning.IsChecked.Value;
                Lazydata.ParrotData.Elfldr2NetworkAdapterName = Elfldr2NetworkAdapterCombobox.SelectedAdapterName;
                Lazydata.ParrotData.Elfldr2LogToFile = ChkUiElf2LogToFile.IsChecked.Value;

                DiscordRPC.StartOrShutdown();

                JoystickHelper.Serialize();

                var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (mainWindow is MainWindow window)
                    window.ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, "ParrotData.xml"));
                _contentControl.Content = _library;
            }
            catch (Exception exception)
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.ErrorCantSaveParrotData, exception.ToString()));
            }
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }

        private void ChkTheme_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitialized)
            {
                App.LoadTheme(UiColour.SelectedItem.ToString(), ChkUiDarkMode.IsChecked.Value, ChkUiHolidayThemes.IsChecked.Value);
            }
        }

        private void UiColour_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitialized)
            {
                App.LoadTheme(UiColour.SelectedItem.ToString(), ChkUiDarkMode.IsChecked.Value, ChkUiHolidayThemes.IsChecked.Value);
            }
        }

        private void BtnVKCPage(object sender, RoutedEventArgs e)
        {
            // Open virtual key code documentation
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes",
                UseShellExecute = true
            });
        }

        private void BtnFfbProfiles(object sender, RoutedEventArgs e)
        {
            // Open FFB profiles page
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://teknogods.github.io/FFBPlugins/",
                UseShellExecute = true
            });
        }
    }
}