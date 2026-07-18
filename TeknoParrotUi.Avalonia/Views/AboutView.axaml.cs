using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var avaloniaVersion = typeof(global::Avalonia.Application).Assembly.GetName().Version;
        VersionText.Text =
            $"TeknoParrotUI {appVersion} — Avalonia {avaloniaVersion} — .NET {System.Environment.Version}";
        AndroidCredits.IsVisible = OperatingSystem.IsAndroid();
        if (OperatingSystem.IsAndroid())
        {
            LinkActions.Orientation = Orientation.Vertical;
            LinkActions.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (var button in new[] { BtnWebsite, BtnGitHub, BtnDiscord })
            {
                button.MinHeight = 48;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void Localize()
    {
        BtnWebsite.Content = Services.Loc.T("AboutWebsite", "Website");
        BtnGitHub.Content = Services.Loc.T("AboutGitHub", "GitHub");
        BtnDiscord.Content = Services.Loc.T("AboutDiscord", "Discord");
    }

    private async void BtnWebsite_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://teknoparrot.com");
    private async void BtnGitHub_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/teknogods/TeknoParrotUI");
    private async void BtnDiscord_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://discord.gg/kmWgGDe");
    private async void BtnWinlatorSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/brunodev85/winlator");
    private async void BtnWineSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://gitlab.winehq.org/wine/wine");
    private async void BtnBox64Source_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/ptitSeb/box64");
    private async void BtnDxvkSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/doitsujin/dxvk");
    private async void BtnVkd3dSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://gitlab.winehq.org/wine/vkd3d");
    private async void BtnMesaSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://gitlab.freedesktop.org/mesa/mesa");
    private async void BtnYaCardSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/GXTX/YACardEmu");
    private async void BtnCncDdrawSource_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://github.com/FunkyFr3sh/cnc-ddraw");
}
