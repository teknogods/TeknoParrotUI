using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameScannerView : UserControl
{
    private List<GameScannerCore.FoundGame> _found = new();

    public event Action? BackRequested;
    public event Action<int>? GamesAdded;

    public GameScannerView()
    {
        InitializeComponent();
    }

    private void Log(string message, bool clear = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (clear)
                LogText.Text = "";
            LogText.Text += message + Environment.NewLine;
            LogScroll.ScrollToEnd();
        });
    }

    private async void BtnBrowse_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select romset folder",
            AllowMultiple = false
        });
        if (folders.Count > 0)
        {
            FolderBox.Text = folders[0].TryGetLocalPath() ?? "";
            BtnScan.IsEnabled = !string.IsNullOrEmpty(FolderBox.Text);
        }
    }

    private async void BtnScan_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dir = FolderBox.Text;
        if (string.IsNullOrWhiteSpace(dir)) return;

        BtnScan.IsEnabled = false;
        BtnAddAll.IsEnabled = false;
        Log("Scanning romset...", clear: true);

        _found = await Task.Run(() => GameScannerCore.ScanRomFolder(dir, m => Log(m)));

        Log($"Scan complete — {_found.Count} game(s) found.");
        BtnScan.IsEnabled = true;
        BtnAddAll.IsEnabled = _found.Count > 0;
    }

    private async void BtnAddAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dir = FolderBox.Text;
        if (string.IsNullOrWhiteSpace(dir) || _found.Count == 0) return;

        BtnAddAll.IsEnabled = false;
        Log("Configuring games...");

        var added = await Task.Run(() => GameScannerCore.ConfigureFoundGames(_found, dir, m => Log(m)));

        Log($"Done — {added} game(s) added to your library.");
        GamesAdded?.Invoke(added);
        BtnAddAll.IsEnabled = true;
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BackRequested?.Invoke();
}
