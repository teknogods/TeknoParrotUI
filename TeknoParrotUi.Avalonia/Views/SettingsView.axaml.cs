using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace TeknoParrotUi.Avalonia.Views;

public partial class SettingsView : UserControl
{
    private static readonly (string Name, string Tag)[] Languages =
    {
        ("English", "en-US"), ("Suomi", "fi-FI"), ("العربية", "ar-SA"), ("Deutsch", "de-DE"),
        ("Español", "es-ES"), ("Français", "fr-FR"), ("Italiano", "it-IT"), ("日本語", "ja-JP"),
        ("한국어", "ko-KR"), ("Nederlands", "nl-NL"), ("Polski", "pl-PL"), ("Português", "pt-PT"),
        ("Русский", "ru-RU"), ("中文", "zh-CN"),
    };

    public event Action? SavedNotification;
    public event Action? MultiButtonConfigRequested;
    public event Action<LegacyBindingsImporter.Result>? BindingsImported;

    public SettingsView()
    {
        InitializeComponent();
        ConfigurePlatformLayout();
        StoozSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                StoozValue.Text = $"{(int)StoozSlider.Value}%";
        };

        LanguageSelector.ItemsSource = Languages.Select(l => l.Name).ToList();
        // Live language switching — no restart required
        LanguageSelector.SelectionChanged += (_, _) =>
        {
            if (_loadingSettings || LanguageSelector.SelectedIndex < 0)
                return;
            Services.Loc.SetLanguage(Languages[LanguageSelector.SelectedIndex].Tag);
        };

        Localize();
        Services.Loc.LanguageChanged += Localize;

        var adapters = new[] { "(default)" }
            .Concat(NetworkInterface.GetAllNetworkInterfaces().Select(n => n.Name))
            .ToList();
        NetworkAdapterBox.ItemsSource = adapters;

        Loaded += (_, _) => LoadFromParrotData();
    }

    private void ConfigurePlatformLayout()
    {
        if (!OperatingSystem.IsAndroid())
            return;

        SettingsContent.MaxWidth = double.PositiveInfinity;
        SettingsContent.HorizontalAlignment = HorizontalAlignment.Stretch;
        // Android controls are edited through the companion's tested per-game
        // touch/gamepad layouts, not the desktop multi-profile binding tool.
        BtnMultiButton.IsVisible = false;
        foreach (var grid in SettingsContent.Children.OfType<Grid>())
        {
            // Desktop settings use label/editor columns as wide as 460 DIPs.
            // Stack every labelled setting on a phone so cover displays and
            // split-screen windows never gain a horizontal scrollbar.
            grid.ColumnDefinitions = new ColumnDefinitions("*");
            grid.RowDefinitions.Clear();
            for (var index = 0; index < grid.Children.Count; index++)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var index = 0; index < grid.Children.Count; index++)
            {
                var child = grid.Children[index];
                Grid.SetColumn(child, 0);
                Grid.SetColumnSpan(child, 1);
                Grid.SetRow(child, index);
                child.MinWidth = 0;
                child.Margin = new Thickness(0, index == 0 ? 0 : 4, 0, 0);
                if (child is not TextBlock)
                    child.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
    }

    private bool _loadingSettings;

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("MainSettings", "Settings");
        HdrGeneral.Text = Services.Loc.T("SettingsTabGeneral", "General");
        ChkSaveLastPlayed.Content = Services.Loc.T("SettingsLoadLastPlayed", "Remember last played game");
        ChkConfirmExit.Content = Services.Loc.T("SettingsConfirmExit", "Confirmation prompt on exit");
        ChkConfirmGameDeletion.Content = Services.Loc.T("SettingsConfirmGameDeletion", "Confirm game deletion");
        ChkDownloadIcons.Content = Services.Loc.T("SettingsDownloadIcon", "Download game icons");
        ChkCheckForUpdates.Content = Services.Loc.T("SettingsCheckForUpdates", "Check for updates");
        ChkSilentMode.Content = Services.Loc.T("SettingsHideConsoleWindows", "Hide console windows (silent mode)");
        ChkUseDiscordRPC.Content = Services.Loc.T("SettingsShowGameOnDiscord", "Show game on Discord");
        ChkDisableAnalytics.Content = Services.Loc.T("SettingsDisableAnalytics", "Disable analytics");
        ChkHideVanguardWarning.Content = Services.Loc.T("SettingsHideVanguardWarning", "Hide Vanguard warning");
        ChkHideDolphinGUI.Content = Services.Loc.T("SettingsHideDolphinGUI", "Hide Dolphin GUI");
        ChkUseSto0Z.Content = Services.Loc.T("SettingsSto0zZone", "Use StoOz driving zone hack");
        ChkFullAxisGas.Content = Services.Loc.T("SettingsFullGas", "Full axis gas");
        ChkFullAxisBrake.Content = Services.Loc.T("SettingsFullBrake", "Full axis brake");
        ChkReverseAxisGas.Content = Services.Loc.T("SettingsReverseGas", "Reverse gas axis");
        ChkReverseAxisBrake.Content = Services.Loc.T("SettingsReverseBrake", "Reverse brake axis");
        BtnImportLegacyBindings.Content = Services.Loc.T("SettingsImportLegacyBindings", "Import 1.0 Button Bindings...");
        ChkElf2LogToFile.Content = Services.Loc.T("SettingsElf2LogToFile", "Log to file");
        HdrHotkeys.Text = Services.Loc.T("SettingsGlobalHotkeys", "Global Hotkeys");
        HdrScore.Text = Services.Loc.T("SettingsScoreSubmission", "Score Submission");
        LblScoreId.Text = Services.Loc.T("SettingsScoreSubmissionID", "Score submission ID");
        HdrElf.Text = Services.Loc.T("SettingsElfldr2", "Elfldr2 (Lindbergh)");
        LblAdapter.Text = Services.Loc.T("SettingsElfldr2NetworkAdapter", "Network adapter");
        HdrDolphin.Text = Services.Loc.T("SettingsDolphin", "Dolphin");
        LblLanguage.Text = Services.Loc.T("SettingsLanguage", "Language");
        LblExitKey.Text = Services.Loc.T("SettingsExitGameKey", "Exit game key");
        LblPauseKey.Text = Services.Loc.T("SettingsPauseGameKey", "Pause game key");
        LblDat.Text = Services.Loc.T("SettingsDefaultDATXMLFile", "Default DAT/XML file");
        BtnSave.Content = Services.Loc.T("SettingsSaveSettings", "Save Settings");
    }

    private void LoadFromParrotData()
    {
        _loadingSettings = true;
        var d = Lazydata.ParrotData;
        ChkSaveLastPlayed.IsChecked = d.SaveLastPlayed;
        ChkConfirmExit.IsChecked = d.ConfirmExit;
        ChkConfirmGameDeletion.IsChecked = d.ConfirmGameDeletion;
        ChkDownloadIcons.IsChecked = d.DownloadIcons;
        ChkCheckForUpdates.IsChecked = d.CheckForUpdates;
        ChkSilentMode.IsChecked = d.SilentMode;
        ChkUseDiscordRPC.IsChecked = d.UseDiscordRPC;
        ChkDisableAnalytics.IsChecked = d.DisableAnalytics;
        ChkHideVanguardWarning.IsChecked = d.HideVanguardWarning;
        ChkUseSto0Z.IsChecked = d.UseSto0ZDrivingHack;
        StoozSlider.Value = d.StoozPercent;
        ChkFullAxisGas.IsChecked = d.FullAxisGas;
        ChkFullAxisBrake.IsChecked = d.FullAxisBrake;
        ChkReverseAxisGas.IsChecked = d.ReverseAxisGas;
        ChkReverseAxisBrake.IsChecked = d.ReverseAxisBrake;
        TxtScoreSubmissionID.Text = d.ScoreSubmissionID;
        TxtDatXml.Text = d.DatXmlLocation;
        ChkElf2LogToFile.IsChecked = d.Elfldr2LogToFile;
        ChkHideDolphinGUI.IsChecked = d.HideDolphinGUI;

        KeyExitGame.HexValue = d.ExitGameKey;
        KeyPauseGame.HexValue = d.PauseGameKey;
        KeyScoreCollapse.HexValue = d.ScoreCollapseGUIKey;

        var langIndex = Array.FindIndex(Languages, l => l.Tag == (d.Language ?? "en-US"));
        LanguageSelector.SelectedIndex = langIndex >= 0 ? langIndex : 0;

        var adapterIndex = (NetworkAdapterBox.ItemsSource as System.Collections.Generic.List<string>)?.IndexOf(d.Elfldr2NetworkAdapterName ?? "") ?? -1;
        NetworkAdapterBox.SelectedIndex = adapterIndex >= 0 ? adapterIndex : 0;
        _loadingSettings = false;
    }

    private async void BtnBrowseDatXml_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DAT/XML file",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("DAT/XML") { Patterns = new[] { "*.dat", "*.xml" } } }
        });
        if (files.Count > 0)
            TxtDatXml.Text = files[0].TryGetLocalPath() ?? TxtDatXml.Text;
    }

    private async void BtnVkc_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(
            this,
            "https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes");

    private async void BtnFfb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(
            this,
            "https://github.com/Boomslangnz/FFBArcadePlugin/releases");

    private void BtnMultiButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        MultiButtonConfigRequested?.Invoke();

    private async void BtnImportLegacyBindings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a TeknoParrotUI 1.0 installation or UserProfiles folder",
            AllowMultiple = false
        });
        var folder = folders.FirstOrDefault()?.TryGetLocalPath();
        if (folder == null)
        {
            if (folders.Count > 0)
                await Services.Dialogs.InfoAsync(owner, "Import 1.0 Bindings",
                    "This folder cannot be read as a local path. Copy the 1.0 UserProfiles folder to local storage and select it there.");
            return;
        }

        try
        {
            var preview = LegacyBindingsImporter.Scan(folder);
            if (preview.ProfileCount == 0)
            {
                await Services.Dialogs.InfoAsync(owner, "Import 1.0 Bindings", "No game profile XML files were found in UserProfiles.");
                return;
            }

            SDL2GamepadBackend.Acquire();
            try
            {
                await Task.Delay(350); // Allow the first SDL enumeration to finish.
                var devices = Enumerable.Range(0, SDL2GamepadBackend.MaxSlots)
                    .Where(SDL2GamepadBackend.IsConnected)
                    .Select(i => (Slot: i, Name: SDL2GamepadBackend.GetDeviceName(i) ?? "Joystick"))
                    .ToList();
                var choices = new Dictionary<Guid, ComboBox>();
                var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
                panel.Children.Add(new TextBlock
                {
                    Text = $"Found {preview.ProfileCount} legacy game profiles. Existing 2.0 bindings will be kept. Choose the current device for each old DirectInput GUID. Unselected devices are skipped.",
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                });
                if (OperatingSystem.IsAndroid())
                    panel.Children.Add(new TextBlock
                    {
                        Text = "Winlator uses its own controls editor. This imports only TeknoParrot's shared bindings, not Winlator touch/gamepad layouts.",
                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                    });
                foreach (var guid in preview.DirectInputDevices)
                {
                    panel.Children.Add(new TextBlock { Text = $"DirectInput device {guid}", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
                    var combo = new ComboBox { ItemsSource = new[] { "Skip this device" }
                        .Concat(devices.Select(d => $"Device {d.Slot}: {d.Name}")).ToList(), SelectedIndex = 0 };
                    choices[guid] = combo;
                    panel.Children.Add(combo);
                }
                if (preview.DirectInputDevices.Count > 0)
                    panel.Children.Add(new TextBlock
                    {
                        Text = "DirectInput control numbers usually correspond to SDL raw controls, but their order can differ by driver. Check imported bindings before play.",
                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                    });
                var importButton = new Button { Content = "Import missing bindings", HorizontalAlignment = HorizontalAlignment.Right };
                panel.Children.Add(importButton);
                var dialog = new Window
                {
                    Title = "Import 1.0 Button Bindings", Width = 580, Height = 480,
                    Content = new ScrollViewer { Content = panel }
                };
                importButton.Click += (_, _) => dialog.Close(true);
                if (await dialog.ShowDialog<bool>(owner) != true) return;

                var selected = new Dictionary<Guid, int>();
                foreach (var (guid, combo) in choices)
                {
                    var index = combo.SelectedIndex - 1;
                    if (index >= 0 && index < devices.Count &&
                        SDL2GamepadBackend.IsConnected(devices[index].Slot) &&
                        (SDL2GamepadBackend.GetDeviceName(devices[index].Slot) ?? "Joystick") == devices[index].Name)
                        selected[guid] = devices[index].Slot;
                }
                var result = LegacyBindingsImporter.Import(preview, GameProfileLoader.GameProfiles, selected);
                BindingsImported?.Invoke(result);
                await Services.Dialogs.InfoAsync(owner, "Import 1.0 Bindings",
                    $"Matched {result.ProfilesMatched} games; saved {result.ProfilesSaved}. Imported {result.GamepadBindings} XInput, {result.DirectInputBindings} DirectInput, and {result.PointerBindings} RawInput bindings. Skipped {result.Skipped}." +
                    (result.Warnings.Count > 0 ? "\n\n" + string.Join("\n", result.Warnings.Take(15)) : ""));
            }
            finally { SDL2GamepadBackend.Release(); }
        }
        catch (Exception ex)
        {
            await Services.Dialogs.InfoAsync(owner, "Import 1.0 Bindings", $"Import failed: {ex.Message}");
        }
    }

    private void BtnSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var d = Lazydata.ParrotData;
        d.SaveLastPlayed = ChkSaveLastPlayed.IsChecked == true;
        d.ConfirmExit = ChkConfirmExit.IsChecked == true;
        d.ConfirmGameDeletion = ChkConfirmGameDeletion.IsChecked == true;
        d.DownloadIcons = ChkDownloadIcons.IsChecked == true;
        d.CheckForUpdates = ChkCheckForUpdates.IsChecked == true;
        d.SilentMode = ChkSilentMode.IsChecked == true;
        d.UseDiscordRPC = ChkUseDiscordRPC.IsChecked == true;
        d.DisableAnalytics = ChkDisableAnalytics.IsChecked == true;
        d.HideVanguardWarning = ChkHideVanguardWarning.IsChecked == true;
        d.UseSto0ZDrivingHack = ChkUseSto0Z.IsChecked == true;
        d.StoozPercent = (int)StoozSlider.Value;
        d.FullAxisGas = ChkFullAxisGas.IsChecked == true;
        d.FullAxisBrake = ChkFullAxisBrake.IsChecked == true;
        d.ReverseAxisGas = ChkReverseAxisGas.IsChecked == true;
        d.ReverseAxisBrake = ChkReverseAxisBrake.IsChecked == true;
        d.ScoreSubmissionID = TxtScoreSubmissionID.Text ?? "";
        d.DatXmlLocation = TxtDatXml.Text ?? "";
        d.Elfldr2LogToFile = ChkElf2LogToFile.IsChecked == true;
        d.HideDolphinGUI = ChkHideDolphinGUI.IsChecked == true;

        d.ExitGameKey = KeyExitGame.HexValue;
        d.PauseGameKey = KeyPauseGame.HexValue;
        d.ScoreCollapseGUIKey = KeyScoreCollapse.HexValue;

        d.Language = Languages[Math.Max(0, LanguageSelector.SelectedIndex)].Tag;
        var adapter = NetworkAdapterBox.SelectedItem as string;
        d.Elfldr2NetworkAdapterName = adapter == "(default)" ? "" : adapter ?? "";

        JoystickHelper.Serialize();
        SavedNotification?.Invoke();
    }
}
