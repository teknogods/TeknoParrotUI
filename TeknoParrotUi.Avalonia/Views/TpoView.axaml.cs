using System.Diagnostics;
using Avalonia.Controls;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// TeknoParrot Online entry point. TPO is a web interface
/// (teknoparrot.com:3333/Home/Chat) that launches games through a JavaScript
/// bridge in the classic launcher's embedded browser. Until a WebView bridge
/// exists in this app, "play" sessions delegate to the classic launcher window,
/// which preserves the complete room/launch/exit-notification flow.
/// </summary>
public partial class TpoView : UserControl
{
    private const string ChatUrl = "https://teknoparrot.com:3333/Home/Chat";

    public TpoView()
    {
        InitializeComponent();
    }

    private void BtnOpenTpo_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Full TPO session: the classic UI hosts the TPO web interface with the
        // startGame/onGameProcessExited JS bridge.
        var launcher = AppEnvironment.LauncherExe;
        if (launcher == null)
        {
            StatusText.Text = "TeknoParrotUi.exe not found — TPO requires the classic launcher.";
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = System.IO.Path.GetDirectoryName(launcher)!,
            Arguments = "--tponline",
            UseShellExecute = false
        });
        StatusText.Text = "TeknoParrot Online opened.";
    }

    private void BtnOpenWeb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ChatUrl) { UseShellExecute = true });
    }
}
