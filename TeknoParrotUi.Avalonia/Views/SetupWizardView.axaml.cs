using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// First-time setup wizard — port of the classic SetupWizard steps:
/// Welcome → DAT/XML → Game scan → Controls → Account → Serial → Complete.
/// The scan/controls/account/serial steps open the corresponding full views and
/// return here afterwards.
/// </summary>
public partial class SetupWizardView : UserControl
{
    private int _step;

    public event Action? ScannerRequested;
    public event Action? ButtonConfigRequested;
    public event Action? AccountRequested;
    public event Action? SubscriptionRequested;
    public event Action? Finished;

    private static readonly string[] Titles =
    {
        "Welcome to TeknoParrot",
        "Games List (DAT/XML)",
        "Scan Your Games",
        "Configure Controls",
        "Account Login",
        "Subscription Serial",
        "Setup Complete"
    };

    public SetupWizardView()
    {
        InitializeComponent();
        UpdateWizardStep();
    }

    public void ReturnFromStep() => UpdateWizardStep();

    private void UpdateWizardStep()
    {
        var panels = new Control[] { WelcomePanel, DatXmlPanel, GameScanPanel, ControlsPanel, AccountPanel, SerialPanel, CompletePanel };
        for (int i = 0; i < panels.Length; i++)
            panels[i].IsVisible = i == _step;

        StepTitle.Text = Titles[_step];
        StepIndicator.Text = $"Step {_step + 1} of {Titles.Length}";
        BtnBack.IsEnabled = _step > 0;
        BtnSkip.IsVisible = _step is >= 1 and <= 5;
        BtnNext.Content = _step == Titles.Length - 1 ? "Finish" : "Next";
        StatusText.Text = "";

        if (_step == 1 && !string.IsNullOrEmpty(Lazydata.ParrotData.DatXmlLocation))
            TxtDatXmlPath.Text = Lazydata.ParrotData.DatXmlLocation;
    }

    private void BtnNext_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        switch (_step)
        {
            case 1: // Completing DAT/XML setup
                var path = TxtDatXmlPath.Text?.Trim();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    StatusText.Text = "Select a valid DAT/XML file, or use Skip.";
                    return;
                }
                Lazydata.ParrotData.DatXmlLocation = path;
                JoystickHelper.Serialize();
                break;
            case 6: // Finishing setup
                FinishSetup();
                return;
        }

        _step++;
        UpdateWizardStep();
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_step > 0)
        {
            _step--;
            UpdateWizardStep();
        }
    }

    private void BtnSkip_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _step++;
        UpdateWizardStep();
    }

    private void FinishSetup()
    {
        Lazydata.ParrotData.FirstTimeSetupComplete = true;
        JoystickHelper.Serialize();
        Finished?.Invoke();
    }

    private async void BtnBrowseDatXml_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DAT/XML file",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("DAT/XML files") { Patterns = new[] { "*.dat", "*.xml" } },
                new("All files") { Patterns = new[] { "*" } }
            }
        });
        var file = files.FirstOrDefault()?.TryGetLocalPath();
        if (file != null)
            TxtDatXmlPath.Text = file;
    }

    private void BtnDownloadDatXml_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/Eggmansworld/Datfiles/releases/tag/teknoparrot") { UseShellExecute = true });

    private void BtnOpenScanner_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => ScannerRequested?.Invoke();
    private void BtnOpenButtonConfig_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => ButtonConfigRequested?.Invoke();
    private void BtnOpenAccount_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => AccountRequested?.Invoke();
    private void BtnOpenSubscription_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SubscriptionRequested?.Invoke();
}
