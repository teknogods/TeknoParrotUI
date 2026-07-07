using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Common;

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

    public SettingsView()
    {
        InitializeComponent();
        StoozSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                StoozValue.Text = $"{(int)StoozSlider.Value}%";
        };

        LanguageSelector.ItemsSource = Languages.Select(l => l.Name).ToList();

        var adapters = new[] { "(default)" }
            .Concat(NetworkInterface.GetAllNetworkInterfaces().Select(n => n.Name))
            .ToList();
        NetworkAdapterBox.ItemsSource = adapters;

        Loaded += (_, _) => LoadFromParrotData();
    }

    private void LoadFromParrotData()
    {
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

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void BtnVkc_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenUrl("https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes");

    private void BtnFfb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenUrl("https://github.com/Boomslangnz/FFBArcadePlugin/releases");

    private void BtnMultiButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        MultiButtonConfigRequested?.Invoke();

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
