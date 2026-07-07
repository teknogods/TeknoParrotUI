using System;
using Avalonia.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public event Action? SavedNotification;

    public SettingsView()
    {
        InitializeComponent();
        StoozSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                StoozValue.Text = $"{(int)StoozSlider.Value}%";
        };
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
        ChkUseSto0Z.IsChecked = d.UseSto0ZDrivingHack;
        StoozSlider.Value = d.StoozPercent;
        ChkFullAxisGas.IsChecked = d.FullAxisGas;
        ChkFullAxisBrake.IsChecked = d.FullAxisBrake;
        ChkReverseAxisGas.IsChecked = d.ReverseAxisGas;
        ChkReverseAxisBrake.IsChecked = d.ReverseAxisBrake;
        TxtScoreSubmissionID.Text = d.ScoreSubmissionID;
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
        d.UseSto0ZDrivingHack = ChkUseSto0Z.IsChecked == true;
        d.StoozPercent = (int)StoozSlider.Value;
        d.FullAxisGas = ChkFullAxisGas.IsChecked == true;
        d.FullAxisBrake = ChkFullAxisBrake.IsChecked == true;
        d.ReverseAxisGas = ChkReverseAxisGas.IsChecked == true;
        d.ReverseAxisBrake = ChkReverseAxisBrake.IsChecked == true;
        d.ScoreSubmissionID = TxtScoreSubmissionID.Text ?? "";

        JoystickHelper.Serialize();
        SavedNotification?.Invoke();
    }
}
