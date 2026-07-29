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
        // TeknoParrotUI component tracks this app itself. On Windows that's
        // the apphost .exe (its PE resource carries the version). On Linux
        // the apphost is a native ELF launcher stub with no version resource
        // at all - point at the managed TeknoParrotUi.dll sitting next to it
        // instead (a real PE-format assembly, readable on any OS - see
        // UpdaterComponent.isManagedAssembly).
        if (OperatingSystem.IsAndroid())
        {
            _components = PlatformAppUpdater.IsAndroidAvailable
                ? PlatformAppUpdater.BuildAndroidComponents().ToList()
                : new List<UpdaterComponent>();
            BtnUpdateAll.IsVisible = _components.Any(component =>
                component.deliveryKind ==
                UpdaterDeliveryKind.AndroidRuntimeArchive);
            if (_components.Count == 0)
            {
                BtnCheck.IsEnabled = false;
                StatusText.Text =
                    "Android package updates are unavailable in this build.";
            }
        }
        else
        {
            var uiLocation = OperatingSystem.IsWindows()
                ? Environment.ProcessPath ?? System.IO.Path.Combine(
                    Environment.CurrentDirectory,
                    "TeknoParrotUi.exe")
                : System.IO.Path.Combine(AppContext.BaseDirectory, "TeknoParrotUi.dll");
            _components = UpdaterComponent.BuildDefaultComponents(uiLocation);
        }

        foreach (var component in _components)
            RowsPanel.Children.Add(BuildRow(component));
    }

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("MainCheckUpdates", "Updates");
        BtnCheck.Content = Services.Loc.T("MainCheckUpdates", "Check for Updates");
        BtnUpdateAll.Content = OperatingSystem.IsAndroid()
            ? Services.Loc.T(
                "UpdaterInstallRuntimeUpdates",
                "Install Runtime Updates")
            : Services.Loc.T("MainInstallUpdates", "Update All");
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
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 2,
            Margin = new global::Avalonia.Thickness(0, 5, 0, 5)
        };

        var name = new TextBlock
        {
            Text = component.name,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold
        };
        var local = new TextBlock
        {
            Text = LocalVersionText(component),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Opacity = 0.8
        };
        var online = new TextBlock
        {
            Text = "—",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Opacity = 0.8
        };
        var update = new Button { Content = Services.Loc.T("UpdaterUpdate", "Update"), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        update.Click += async (_, _) =>
        {
            var pending = _pendingUpdates.FirstOrDefault(u => u.Component.name == component.name);
            if (pending != null)
                await InstallOne(pending);
        };

        var localLine = BuildVersionLine(
            Services.Loc.T("UpdaterInstalledVersion", "Installed:"),
            local);
        var onlineLine = BuildVersionLine(
            Services.Loc.T("UpdaterAvailableVersion", "Available:"),
            online);

        Grid.SetColumn(name, 0);
        Grid.SetRow(name, 0);
        Grid.SetColumn(localLine, 0);
        Grid.SetRow(localLine, 1);
        Grid.SetColumn(onlineLine, 0);
        Grid.SetRow(onlineLine, 2);
        Grid.SetColumn(update, 1);
        Grid.SetRow(update, 0);
        Grid.SetRowSpan(update, 3);
        update.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(name);
        grid.Children.Add(localLine);
        grid.Children.Add(onlineLine);
        grid.Children.Add(update);

        _rows[component.name] = (local, online, update);
        return grid;
    }

    private static Control BuildVersionLine(
        string labelText,
        TextBlock value)
    {
        var line = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 6
        };
        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.65
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(value, 1);
        line.Children.Add(label);
        line.Children.Add(value);
        return line;
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
        if (OperatingSystem.IsAndroid())
            await PlatformAppUpdater.RefreshAndroidComponentsAsync(_components);

        foreach (var component in _components)
        {
            if (!OperatingSystem.IsAndroid())
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
        UpdateRuntimeAvailability();
        BtnUpdateAll.IsEnabled = HasBatchInstallUpdates();
        BtnCheck.IsEnabled = true;
        _busy = false;
    }

    private async void BtnUpdateAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var updates = OperatingSystem.IsAndroid()
            ? _pendingUpdates.Where(update =>
                update.Component.deliveryKind ==
                UpdaterDeliveryKind.AndroidRuntimeArchive).ToList()
            : _pendingUpdates.ToList();
        foreach (var update in updates)
            await InstallOne(update);
    }

    private bool HasBatchInstallUpdates() =>
        OperatingSystem.IsAndroid()
            ? IsAndroidRuntimeHostInstalled() &&
              _pendingUpdates.Any(update =>
                update.Component.deliveryKind ==
                UpdaterDeliveryKind.AndroidRuntimeArchive)
            : _pendingUpdates.Count > 0;

    private bool IsAndroidRuntimeHostInstalled() =>
        _components.Any(component =>
            component.deliveryKind == UpdaterDeliveryKind.AndroidApk &&
            string.Equals(
                component.packageIdentity,
                "com.teknoparrot.winlator",
                StringComparison.Ordinal) &&
            component.localVersion != UpdaterComponent.NotInstalled);

    private void UpdateRuntimeAvailability()
    {
        if (!OperatingSystem.IsAndroid())
            return;
        var runtimeHostInstalled = IsAndroidRuntimeHostInstalled();
        foreach (var update in _pendingUpdates.Where(update =>
                     update.Component.deliveryKind ==
                     UpdaterDeliveryKind.AndroidRuntimeArchive))
        {
            if (_rows.TryGetValue(update.Component.name, out var row))
                row.update.IsEnabled = runtimeHostInstalled;
        }
        if (!runtimeHostInstalled &&
            _pendingUpdates.Any(update =>
                update.Component.deliveryKind ==
                UpdaterDeliveryKind.AndroidRuntimeArchive))
        {
            StatusText.Text =
                "Install TeknoParrot Winlator first, then check again to install runtime updates.";
        }
    }

    private async Task InstallOne(UpdateCheckResult update)
    {
        if (_busy) return;
        if (OperatingSystem.IsAndroid() &&
            update.Component.deliveryKind ==
            UpdaterDeliveryKind.AndroidRuntimeArchive &&
            !IsAndroidRuntimeHostInstalled())
        {
            StatusText.Text =
                "Install TeknoParrot Winlator first, then check again before installing OpenParrot.";
            return;
        }
        _busy = true;
        BtnCheck.IsEnabled = false;
        BtnUpdateAll.IsEnabled = false;
        Progress.IsVisible = true;
        Progress.Value = 0;
        StatusText.Text = $"Updating {update.Component.name}...";

        // TeknoParrotUI can't replace its own running files in-process — hand off
        // to ParrotPatcher, which waits for this process to exit, extracts the
        // update and restarts the app (see UpdaterCore.LaunchSelfUpdate). Not
        // available on single-view platforms (Android) — ParrotPatcher isn't shipped there.
        bool isSelfUpdate = update.Component.name == "TeknoParrotUI" &&
                             (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());

        var row = _rows[update.Component.name];
        try
        {
            var progress = new Progress<double>(v => Dispatcher.UIThread.Post(() => Progress.Value = v));

            if (OperatingSystem.IsAndroid())
            {
                StatusText.Text = $"Downloading {update.Component.name} package...";
                var status = await PlatformAppUpdater.InstallAndroidPackageAsync(
                    update,
                    progress);
                if (update.Component.deliveryKind ==
                    UpdaterDeliveryKind.AndroidRuntimeArchive)
                {
                    row.local.Text = update.Component.localVersion;
                    row.update.IsVisible = false;
                    _pendingUpdates.Remove(update);
                }
                StatusText.Text = status;
                return;
            }

            if (isSelfUpdate)
            {
                StatusText.Text = Services.Loc.T("UpdaterSelfUpdateRestarting",
                    "Downloading update — TeknoParrotUI will restart to finish installing...");
                await Task.Run(() => UpdaterCore.LaunchSelfUpdate(update, progress));

                // Close the window — the app shuts down and ParrotPatcher takes over.
                Dispatcher.UIThread.Post(() =>
                {
                    if (TopLevel.GetTopLevel(this) is Window owner)
                        owner.Close();
                });
                return;
            }

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
            UpdateRuntimeAvailability();
            BtnUpdateAll.IsEnabled = HasBatchInstallUpdates();
            _busy = false;
        }
    }
}
