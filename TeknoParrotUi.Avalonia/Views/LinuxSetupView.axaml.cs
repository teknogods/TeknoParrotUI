using System;
using System.Diagnostics;
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
            PopulatePrefixModeSection();
            PopulateFullscreenScalingSection();
            RefreshChecks();
            RefreshProtonList();
        };
    }

    /// <summary>Global "Fullscreen game scaling" (Gamescope) section - see GamescopeLauncher/GamescopeLaunchPolicy.</summary>
    private void PopulateFullscreenScalingSection()
    {
        CmbFullscreenScaling.ItemsSource = new[] { "Automatic fullscreen fit", "Disabled" };
        CmbFullscreenScaling.SelectionChanged -= CmbFullscreenScaling_SelectionChanged;
        CmbFullscreenScaling.SelectedIndex = Lazydata.ParrotData.FullscreenScalingMode == LinuxFullscreenScalingMode.AutomaticFit ? 0 : 1;
        CmbFullscreenScaling.SelectionChanged += CmbFullscreenScaling_SelectionChanged;
        UpdateFullscreenScalingDescription();
        RefreshFullscreenScalingStatus();
    }

    private void UpdateFullscreenScalingDescription()
    {
        LblFullscreenScalingDescription.Text = CmbFullscreenScaling.SelectedIndex == 1
            ? "Disabled: launches games without Gamescope scaling. Use this if a game has compatibility, focus, input, or window-management problems."
            : "Automatic fullscreen fit: automatically enlarges low-resolution games to use as much of the current monitor as possible while preserving the game's aspect ratio. Black bars may appear when the game and monitor use different aspect ratios. Recommended for high-resolution (e.g. 4K) monitors.";
    }

    private void CmbFullscreenScaling_SelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        Lazydata.ParrotData.FullscreenScalingMode = CmbFullscreenScaling.SelectedIndex == 1
            ? LinuxFullscreenScalingMode.Disabled
            : LinuxFullscreenScalingMode.AutomaticFit;
        JoystickHelper.Serialize();
        UpdateFullscreenScalingDescription();
        RefreshFullscreenScalingStatus();
    }

    private void RefreshFullscreenScalingStatus()
    {
        Task.Run(() =>
        {
            var availability = GamescopeLocator.Locate();
            var display = LinuxDisplayResolver.Resolve();
            var insideGamescope = GamescopeEnvironment.IsAlreadyInsideGamescope();

            Dispatcher.UIThread.Post(() =>
            {
                LblFullscreenScalingStatus.Text =
                    $"Gamescope: {(availability.IsAvailable ? "available" : "unavailable (" + availability.Reason + ")")}\n" +
                    $"Path: {(string.IsNullOrEmpty(availability.ExecutablePath) ? "(not found)" : availability.ExecutablePath)}\n" +
                    $"Version: {(string.IsNullOrEmpty(availability.Version) ? "(unknown)" : availability.Version)}\n" +
                    $"Current monitor resolution: {(display.IsValid ? $"{display.Width}x{display.Height} ({display.Source})" : "unresolved - " + display.FailureReason)}" +
                    (insideGamescope ? "\nAlready running inside a Gamescope session - nested wrapping is skipped automatically." : "");
            });
        });
    }

    private void BtnTestGamescope_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnTestGamescope.IsEnabled = false;
        LblFullscreenScalingTestResult.Text = "Testing...";
        Task.Run(() =>
        {
            var availability = GamescopeLocator.Locate();
            string message;
            if (!availability.IsAvailable)
            {
                message = $"Gamescope test failed: {availability.Reason} - {availability.Message}";
            }
            else
            {
                var display = LinuxDisplayResolver.Resolve();
                message = display.IsValid
                    ? $"Gamescope {availability.Version} found at {availability.ExecutablePath}. Target monitor: {display.Width}x{display.Height} ({display.Source})."
                    : $"Gamescope {availability.Version} found at {availability.ExecutablePath}, but the monitor resolution could not be determined: {display.FailureReason}";
            }

            Dispatcher.UIThread.Post(() =>
            {
                LblFullscreenScalingTestResult.Text = message;
                BtnTestGamescope.IsEnabled = true;
            });
        });
    }

    /// <summary>Global Wine/Proton prefix mode (shared vs isolated) section - see WinePrefixManager.</summary>
    private void PopulatePrefixModeSection()
    {
        CmbDefaultPrefixMode.ItemsSource = new[] { "Shared prefix", "Isolated prefix" };
        CmbDefaultPrefixMode.SelectionChanged -= CmbDefaultPrefixMode_SelectionChanged;
        CmbDefaultPrefixMode.SelectedIndex = Lazydata.ParrotData.DefaultWinePrefixMode == WinePrefixMode.Isolated ? 1 : 0;
        CmbDefaultPrefixMode.SelectionChanged += CmbDefaultPrefixMode_SelectionChanged;
        UpdatePrefixModeDescription();
        UpdateSharedPathsDisplay();
    }

    private void UpdatePrefixModeDescription()
    {
        LblPrefixModeDescription.Text = CmbDefaultPrefixMode.SelectedIndex == 1
            ? "Isolated prefix: Creates a separate Wine environment for each game. Uses more disk space but provides maximum isolation."
            : "Shared prefix: Uses common TeknoParrot Wine environments for multiple games and saves disk space. Recommended for most games.";
    }

    private void UpdateSharedPathsDisplay()
    {
        var root = WinePrefixManager.DefaultDataRoot;
        var sharedWine = WinePrefixManager.SharedRoot(root, WineRunnerKind.PlainWine);
        var sharedProton = WinePrefixManager.SharedRoot(root, WineRunnerKind.Proton);
        LblSharedPaths.Text =
            $"Shared Wine prefix: {sharedWine}\n" +
            $"Shared Proton compat-data: {sharedProton}\n" +
            $"Shared Proton actual prefix: {Path.Combine(sharedProton, "pfx")}";
    }

    private void CmbDefaultPrefixMode_SelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        Lazydata.ParrotData.DefaultWinePrefixMode = CmbDefaultPrefixMode.SelectedIndex == 1 ? WinePrefixMode.Isolated : WinePrefixMode.Shared;
        JoystickHelper.Serialize();
        UpdatePrefixModeDescription();
    }

    private void BtnOpenSharedFolder_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = Path.Combine(WinePrefixManager.DefaultDataRoot, "prefixes", "shared");
        Directory.CreateDirectory(path);
        try { Process.Start(new ProcessStartInfo("xdg-open", path) { UseShellExecute = false }); }
        catch (Exception ex) { AppendLog($"Could not open folder: {ex.Message}"); }
    }

    private async void BtnResetSharedPrefix_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await Services.Dialogs.ConfirmAsync(owner, "Reset Shared Prefix",
            "Resetting the shared prefix affects every game using it. Wine registry state, cached settings, and data stored inside that prefix may be removed.");
        if (!confirmed)
            return;

        BtnResetSharedPrefix.IsEnabled = false;
        LblResetStatus.Text = "Resetting...";
        var results = await Task.Run(() => new[]
        {
            WinePrefixManager.ResetShared(WineRunnerKind.PlainWine),
            WinePrefixManager.ResetShared(WineRunnerKind.Proton)
        });
        LblResetStatus.Text = string.Join("\n", results.Select(r => r.Message));
        BtnResetSharedPrefix.IsEnabled = true;
        RefreshChecks();
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
        if (!ProtonPackageManager.IsSupportedHost())
        {
            AppendLog(ProtonPackageManager.UnsupportedHostMessage);
            return;
        }

        BtnInstallCompat.IsEnabled = false;
        var wine = ProtonLauncher.ResolveWineBinary();
        Task.Run(() =>
        {
            var root = WinePrefixManager.DefaultDataRoot;
            var prefixesRoot = Path.Combine(root, "prefixes");

            var targets = new System.Collections.Generic.List<(string Name, string ActualPrefix)>();

            // Shared environments (both compatibility groups, both runner kinds -
            // the Proton "pfx" subdirectory is the real prefix, never the
            // compat-data root itself).
            foreach (var group in new[] { WinePrefixCompatibilityGroup.Standard, WinePrefixCompatibilityGroup.Japanese })
            {
                targets.Add(($"shared/wine ({group})", WinePrefixManager.SharedRoot(root, WineRunnerKind.PlainWine, group)));
                targets.Add(($"shared/proton ({group})", Path.Combine(WinePrefixManager.SharedRoot(root, WineRunnerKind.Proton, group), "pfx")));
            }

            // Legacy per-profile isolated directories - skip the "shared" folder itself.
            if (Directory.Exists(prefixesRoot))
            {
                foreach (var dir in Directory.GetDirectories(prefixesRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (string.Equals(name, "shared", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // A Proton compat-data root has its real prefix at "pfx" -
                    // a plain wine prefix IS the directory itself.
                    var pfx = Path.Combine(dir, "pfx");
                    targets.Add((name, Directory.Exists(pfx) ? pfx : dir));
                }
            }

            var any = false;
            foreach (var (name, actualPrefix) in targets)
            {
                if (!Directory.Exists(actualPrefix))
                    continue;
                any = true;
                Dispatcher.UIThread.Post(() => AppendLog($"--- {name} ---"));
                ProtonLauncher.InstallCompatLibraries(actualPrefix, wine, force: true,
                    onOutput: line => Dispatcher.UIThread.Post(() => AppendLog(line)));
            }

            if (!any)
                Dispatcher.UIThread.Post(() => AppendLog("No initialized game prefixes yet - this will be applied automatically the first time you launch a game."));

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
