using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Linux-only page: surfaces the Wine/Proton environment TeknoParrotUI
/// detected, lets the user override the wine binary path for systems where
/// it isn't at /usr/bin/wine or the packaged Proton directory, checks for
/// the dependencies games actually need (winetricks/cabextract for D3DX9
/// compat libs, CJK fonts, GTK3/WebKitGTK for TeknoParrot Online), and
/// manages GE-Proton package installs/updates.
/// </summary>
public partial class LinuxSetupView : UserControl
{
    private GithubRelease? _latestRelease;

    public LinuxSetupView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TxtWinePath.Text = Lazydata.ParrotData.CustomWinePath ?? "";
            LblUnsupportedHost.IsVisible = !ProtonPackageManager.IsSupportedHost();
            LblUnsupportedHost.Text = ProtonPackageManager.UnsupportedHostMessage;
            RefreshChecks();
            RefreshProtonList();
        };
    }

    private void AppendLog(string line)
    {
        LogBox.Text = string.IsNullOrEmpty(LogBox.Text) ? line : LogBox.Text + "\n" + line;
        LogBox.CaretIndex = LogBox.Text.Length;
    }

    private void RefreshChecks()
    {
        Task.Run(() =>
        {
            var wine = ProtonLauncher.ResolveWineBinary();
            var wineCheck = LinuxEnvironmentCheck.CheckWine(wine);
            var winetricks = LinuxEnvironmentCheck.CheckWinetricks();
            var cabextract = LinuxEnvironmentCheck.CheckCabextract();
            var cjk = LinuxEnvironmentCheck.CheckCjkFonts();
            var webview = LinuxEnvironmentCheck.CheckWebView();
            var hint = LinuxEnvironmentCheck.GetInstallHint();
            var anyMissing = !winetricks.Found || !cabextract.Found || !cjk.Found || !webview.Found;

            // If a custom path is set, warn (but never silently override it)
            // when its architecture doesn't match this host - see
            // ProtonPackageManager.DescribeArchitectureMismatch.
            var customPath = Lazydata.ParrotData.CustomWinePath;
            var customMismatch = !string.IsNullOrEmpty(customPath)
                ? ProtonPackageManager.DescribeArchitectureMismatch(customPath)
                : null;

            Dispatcher.UIThread.Post(() =>
            {
                LblWineDetected.Text = wineCheck.Found
                    ? $"Detected: {wineCheck.Detail}"
                    : $"Detected: {(string.IsNullOrEmpty(wineCheck.Detail) ? "no wine binary found - see below to set a custom path" : wineCheck.Detail)}";
                SetCheck(LblWinetricks, winetricks);
                SetCheck(LblCabextract, cabextract);
                SetCheck(LblCjkFonts, cjk);
                SetCheck(LblWebView, webview);
                LblInstallHint.Text = anyMissing ? hint : "";

                LblCustomWineWarning.IsVisible = customMismatch != null;
                LblCustomWineWarning.Text = customMismatch ?? "";
            });
        });
    }

    private static void SetCheck(TextBlock label, EnvCheckResult result)
    {
        label.Text = (result.Found ? "\u2713 " : "\u2717 ") + result.Detail;
    }

    private async void BtnBrowseWine_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select wine or Proton binary",
            AllowMultiple = false
        });
        if (files.Count > 0)
            TxtWinePath.Text = files[0].TryGetLocalPath() ?? TxtWinePath.Text;
    }

    private void BtnSaveWine_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Lazydata.ParrotData.CustomWinePath = TxtWinePath.Text ?? "";
        JoystickHelper.Serialize();
        RefreshChecks();
    }

    private void BtnClearWine_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        TxtWinePath.Text = "";
        Lazydata.ParrotData.CustomWinePath = "";
        JoystickHelper.Serialize();
        RefreshChecks();
    }

    private void BtnInstallCompat_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnInstallCompat.IsEnabled = false;
        var wine = ProtonLauncher.ResolveWineBinary();
        Task.Run(() =>
        {
            var prefixesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknoParrotUI", "prefixes");

            if (!Directory.Exists(prefixesRoot))
            {
                Dispatcher.UIThread.Post(() => AppendLog("No game prefixes set up yet - this will be applied automatically the first time you launch a game."));
            }
            else
            {
                foreach (var prefix in Directory.GetDirectories(prefixesRoot))
                {
                    var name = Path.GetFileName(prefix);
                    Dispatcher.UIThread.Post(() => AppendLog($"--- {name} ---"));
                    ProtonLauncher.InstallCompatLibraries(prefix, wine, force: true,
                        onOutput: line => Dispatcher.UIThread.Post(() => AppendLog(line)));
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                AppendLog("Done.");
                BtnInstallCompat.IsEnabled = true;
            });
        });
    }

    private void RefreshProtonList()
    {
        // ProtonPackageInfo.ToString() renders "GE-Proton11-1  x86_64 — compatible" /
        // "GE-Proton11-1-aarch64  ARM64 — incompatible with this system" - an
        // incompatible package is listed (so it's visible) but never auto-selected
        // (see ProtonPackageManager.ResolveWineBinary/IsCompatibleProtonPackage).
        ProtonList.ItemsSource = ProtonPackageManager.ListInstalledPackages();
    }

    private void BtnRefreshProton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => RefreshProtonList();

    private async void BtnCheckProtonUpdates_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!ProtonPackageManager.IsSupportedHost())
        {
            LblProtonLatest.Text = ProtonPackageManager.UnsupportedHostMessage;
            return;
        }

        BtnCheckProtonUpdates.IsEnabled = false;
        LblProtonLatest.Text = "Checking...";
        try
        {
            var releases = await ProtonReleaseManager.FetchReleases();
            _latestRelease = releases.FirstOrDefault();
            if (_latestRelease == null)
            {
                LblProtonLatest.Text = "No releases found.";
                BtnInstallLatestProton.IsVisible = false;
            }
            else
            {
                var installed = ProtonPackageManager.ListInstalledVersions();
                var already = installed.Any(v => v.Equals(_latestRelease.tag_name, StringComparison.OrdinalIgnoreCase));
                LblProtonLatest.Text = already
                    ? $"Latest: {_latestRelease.tag_name} (already installed)"
                    : $"Latest: {_latestRelease.tag_name} - not installed";
                BtnInstallLatestProton.IsVisible = !already;
            }
        }
        catch (Exception ex)
        {
            LblProtonLatest.Text = $"Could not check for updates: {ex.Message}";
        }
        finally
        {
            BtnCheckProtonUpdates.IsEnabled = true;
        }
    }

    private async void BtnInstallLatestProton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_latestRelease == null) return;
        BtnInstallLatestProton.IsEnabled = false;
        ProtonProgress.IsVisible = true;
        ProtonProgress.Value = 0;
        AppendLog($"Downloading {_latestRelease.tag_name}...");
        try
        {
            var progress = new Progress<double>(p => Dispatcher.UIThread.Post(() => ProtonProgress.Value = p * 100));
            await ProtonReleaseManager.InstallRelease(_latestRelease, progress);
            AppendLog($"Installed {_latestRelease.tag_name}.");
            RefreshProtonList();
            BtnInstallLatestProton.IsVisible = false;
        }
        catch (Exception ex)
        {
            AppendLog($"Install failed: {ex.Message}");
        }
        finally
        {
            ProtonProgress.IsVisible = false;
            BtnInstallLatestProton.IsEnabled = true;
        }
    }
}
