using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Troubleshooting page (reinstated from the classic WPF UI): shows a full
/// diagnostic report - PC/OS info, module versions, Linux launch environment,
/// the last played game's (privacy-filtered) configuration, and the COMPLETE
/// log of the last run (console + launch window text) - with copy/save
/// buttons so users can attach it to bug reports when a game does not boot.
/// </summary>
public partial class TroubleshootingView : UserControl
{
    public TroubleshootingView()
    {
        InitializeComponent();
    }

    /// <summary>Regenerates the report (called every time the page is shown).</summary>
    public void Refresh()
    {
        try
        {
            ReportText.Text = TroubleshootingReport.Build(GetMonitorLines());
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            ReportText.Text = $"Error building the report: {ex}";
        }
    }

    private IReadOnlyList<string> GetMonitorLines()
    {
        var lines = new List<string>();
        try
        {
            var screens = TopLevel.GetTopLevel(this)?.Screens;
            if (screens != null)
            {
                for (var i = 0; i < screens.All.Count; i++)
                {
                    var screen = screens.All[i];
                    var primary = screen.IsPrimary ? " (Primary)" : "";
                    lines.Add($"Monitor {i + 1}: {screen.Bounds.Width}x{screen.Bounds.Height} @ scale {screen.Scaling:0.##}{primary}");
                }
            }
        }
        catch
        {
            // monitor info is informational only
        }
        return lines;
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e) => Refresh();

    private async void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(ReportText.Text ?? "");
                StatusText.Text = "Report copied to clipboard";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Copy failed: {ex.Message}";
        }
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null)
                return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save troubleshooting report",
                SuggestedFileName = $"TeknoParrot_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[] { new FilePickerFileType("Text documents") { Patterns = new[] { "*.txt" } } }
            });
            if (file == null)
                return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(ReportText.Text ?? "");
            StatusText.Text = "Report saved";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
        }
    }
}
