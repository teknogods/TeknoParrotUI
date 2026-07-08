using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common.Updater;

namespace TeknoParrotUi.Avalonia.Views;

public partial class UpdatesView : UserControl
{
    private readonly List<UpdaterComponent> _components;
    private readonly Dictionary<string, (TextBlock local, TextBlock online, Button update)> _rows = new();
    private List<UpdateCheckResult> _pendingUpdates = new();
    private bool _busy;

    public UpdatesView()
    {
        InitializeComponent();

        Localize();
        Services.Loc.LanguageChanged += Localize;

        // Component versions resolve against the TeknoParrot data folder; the
        // TeknoParrotUI component tracks this exe itself.
        _components = UpdaterComponent.BuildDefaultComponents(
            Environment.ProcessPath ?? System.IO.Path.Combine(Environment.CurrentDirectory, "TeknoParrotUi.exe"));

        foreach (var component in _components)
            RowsPanel.Children.Add(BuildRow(component));
    }

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("MainCheckUpdates", "Updates");
        BtnCheck.Content = Services.Loc.T("MainCheckUpdates", "Check for Updates");
        BtnUpdateAll.Content = Services.Loc.T("MainInstallUpdates", "Update All");
        foreach (var component in _components ?? new List<UpdaterComponent>())
        {
            if (_rows.TryGetValue(component.name, out var row))
                row.local.Text = LocalVersionText(component);
        }
    }

    private static string LocalVersionText(UpdaterComponent component) =>
        component.localVersion == UpdaterComponent.NotInstalled
            ? Services.Loc.T("UpdaterNotInstalled", "Not installed")
            : component.localVersion;

    private Control BuildRow(UpdaterComponent component)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("220,180,180,*"), Margin = new global::Avalonia.Thickness(0, 2, 0, 2) };

        var name = new TextBlock { Text = component.name, VerticalAlignment = VerticalAlignment.Center, FontWeight = global::Avalonia.Media.FontWeight.SemiBold };
        var local = new TextBlock { Text = LocalVersionText(component), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 };
        var online = new TextBlock { Text = "—", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 };
        var update = new Button { Content = Services.Loc.T("UpdaterUpdate", "Update"), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        update.Click += async (_, _) =>
        {
            var pending = _pendingUpdates.FirstOrDefault(u => u.Component.name == component.name);
            if (pending != null)
                await InstallOne(pending);
        };

        Grid.SetColumn(name, 0);
        Grid.SetColumn(local, 1);
        Grid.SetColumn(online, 2);
        Grid.SetColumn(update, 3);
        grid.Children.Add(name);
        grid.Children.Add(local);
        grid.Children.Add(online);
        grid.Children.Add(update);

        _rows[component.name] = (local, online, update);
        return grid;
    }

    private async void BtnCheck_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        BtnCheck.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;
        StatusText.Text = "Checking for updates...";
        _pendingUpdates.Clear();
        UpdaterCore.InvalidateCache();

        foreach (var component in _components)
        {
            component._localVersion = null;
            var row = _rows[component.name];
            row.local.Text = LocalVersionText(component);
            row.online.Text = "checking...";

            var result = await UpdaterCore.CheckComponent(component);
            if (result.Error != null)
            {
                row.online.Text = result.Error;
            }
            else
            {
                row.online.Text = result.OnlineVersion;
                row.update.IsVisible = result.NeedsUpdate;
                if (result.NeedsUpdate)
                    _pendingUpdates.Add(result);
            }
        }

        StatusText.Text = _pendingUpdates.Count == 0
            ? "Everything is up to date."
            : $"{_pendingUpdates.Count} update(s) available.";
        BtnUpdateAll.IsEnabled = _pendingUpdates.Count > 0;
        BtnCheck.IsEnabled = true;
        _busy = false;
    }

    private async void BtnUpdateAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (var update in _pendingUpdates.ToList())
            await InstallOne(update);
    }

    private async Task InstallOne(UpdateCheckResult update)
    {
        if (_busy) return;
        _busy = true;
        BtnCheck.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;
        Progress.IsVisible = true;
        Progress.Value = 0;
        StatusText.Text = $"Updating {update.Component.name}...";

        var row = _rows[update.Component.name];
        try
        {
            var progress = new Progress<double>(v => Dispatcher.UIThread.Post(() => Progress.Value = v));
            await Task.Run(() => UpdaterCore.InstallUpdate(update, progress));

            row.local.Text = update.Component.localVersion;
            row.update.IsVisible = false;
            _pendingUpdates.Remove(update);
            StatusText.Text = $"{update.Component.name} updated to {update.OnlineVersion}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to update {update.Component.name}: {ex.Message}";
        }
        finally
        {
            Progress.IsVisible = false;
            BtnCheck.IsEnabled = true;
            BtnUpdateAll.IsEnabled = _pendingUpdates.Count > 0;
            _busy = false;
        }
    }
}
