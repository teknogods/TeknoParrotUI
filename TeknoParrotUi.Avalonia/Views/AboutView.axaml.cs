using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        VersionText.Text = $"Avalonia UI {Assembly.GetExecutingAssembly().GetName().Version} — .NET {System.Environment.Version}";
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void BtnWebsite_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => OpenUrl("https://teknoparrot.com");
    private void BtnGitHub_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => OpenUrl("https://github.com/teknogods/TeknoParrotUI");
    private void BtnDiscord_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => OpenUrl("https://discord.gg/kmWgGDe");
}
