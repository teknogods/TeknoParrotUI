using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// The whole application shell (navigation, page hosting, status bar, wizard,
/// privacy gate, controller UI navigation). Extracted from MainWindow so the
/// same UI runs under both Avalonia lifetimes:
/// - desktop: hosted in <see cref="MainWindow"/> (classic desktop lifetime)
/// - Android: assigned to ISingleViewApplicationLifetime.MainView
///
/// Window-only concerns (fullscreen, closing) are surfaced as events that the
/// desktop host wires to the real Window; on Android they are no-ops.
/// </summary>
public partial class MainView : UserControl
{
    private readonly LibraryView _library = new();
    private readonly SettingsView _settings = new();
    private readonly AboutView _about = new();
    private readonly GameSettingsView _gameSettings = new();
    private readonly JoystickSetupView _joystickSetup = new();
    private readonly AddGameView _addGame = new();
    private readonly UpdatesView _updates = new();
    private readonly AccountView _account = new();
    private readonly VerifyGameView _verify = new();
    private readonly GameScannerView _scanner = new();
    private readonly ModsView _mods = new();
    private readonly TpoView _tpo = new();
    private readonly GameRunningView _gameRunning = new();
    private readonly SubscriptionView _subscription = new();
    private readonly MultiButtonConfigView _multiButton = new();
    private readonly UiOptionsView _uiOptions = new();
    private readonly SetupWizardView _wizard = new();
    private readonly LinuxSetupView _linuxSetup = new();
    private readonly TroubleshootingView _troubleshooting = new();
    private readonly UiNavigationService _uiNav = new();
    private bool _androidLaunchPreflightBusy;

    private static bool WizardActive => !Lazydata.ParrotData.FirstTimeSetupComplete;

    /// <summary>Raised when the user (or controller nav) asks to toggle fullscreen. Desktop-only concern.</summary>
    public event Action? FullscreenToggleRequested;

    /// <summary>Raised when the app should close (privacy policy declined). Desktop-only concern.</summary>
    public event Action? CloseRequested;

    public MainView()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        NavLinuxSetup.IsVisible = OperatingSystem.IsLinux();
        NavUpdates.IsVisible = PlatformCapabilities.CanSelfUpdate;
        NavMods.IsVisible = PlatformCapabilities.CanManageDesktopComponents;
        // A 170-DIP navigation rail leaves too little room for the desktop-style
        // library/settings panes on a portrait phone. Keep it one tap away via
        // the menu button, but start Android with the content pane unobstructed.
        if (OperatingSystem.IsAndroid())
        {
            Sidebar.IsVisible = false;
            // Account login and TP Online are not ready for the Android release.
            // Leave the destinations visible so users know the features exist,
            // but make their unavailable state explicit instead of opening a
            // page that cannot complete its workflow.
            NavOnline.IsEnabled = false;
            NavOnline.Opacity = 0.5;
            ToolTip.SetTip(
                NavOnline,
                "TeknoParrot Online is currently disabled on Android.");
            NavAccount.IsEnabled = false;
            NavAccount.Opacity = 0.5;
            ToolTip.SetTip(
                NavAccount,
                "Account login is currently disabled on Android.");
        }

        UpdateSubscriptionBadge();
        LocalizeChrome();
        Loc.LanguageChanged += () =>
        {
            LocalizeChrome();
            UpdateSubscriptionBadge();
        };

        _library.GameSettingsRequested += profile =>
        {
            _gameSettings.LoadProfile(profile);
            Show(_gameSettings, "Game Settings");
        };
        _library.ControlsSetupRequested += profile =>
        {
            if (OperatingSystem.IsAndroid())
            {
                if (profile.EmulatorType is EmulatorType.pcsx2x6 or EmulatorType.Dolphin ||
                    PlatformCapabilities.IsAndroidRpcs3ProfileSupported(profile))
                {
                    var companionName = profile.EmulatorType switch
                    {
                        EmulatorType.Dolphin => "TeknoDolphin",
                        EmulatorType.RPCS3 => "RPCS3X6",
                        _ => "PCSX2X6"
                    };
                    StatusBar.Text =
                        $"{companionName} uses the TeknoParrot arcade overlay in game; " +
                        "connected Android controllers are detected automatically.";
                    return;
                }

                var error = PlatformControlsEditor.OpenAndroidEditor(profile);
                StatusBar.Text = error ?? (
                    profile.EmulatorType is EmulatorType.OpenParrot or EmulatorType.TeknoParrot
                        ? "Opened this game's arcade controls — select the Xbox controller and assign its buttons to cabinet actions."
                        : "Opened Winlator controls — use the back arrow to return.");
                return;
            }

            _joystickSetup.LoadProfile(profile);
            Show(_joystickSetup, "Controls");
        };
        _library.VerifyRequested += profile =>
        {
            Show(_verify, "Verify Files");
            _verify.StartVerification(profile);
        };
        _library.AddGameRequested += () =>
        {
            _addGame.Refresh();
            Show(_addGame, "Add Game");
        };
        _library.ScannerRequested += () => Show(_scanner, "Game Scanner");
        _library.NativeLaunchRequested += async (profile, testMode) =>
        {
            if (!PlatformCapabilities.CanLaunchGames)
            {
                StatusBar.Text = PlatformCapabilities.AndroidLaunchUnavailableMessage;
                return;
            }

            if (OperatingSystem.IsAndroid() &&
                !await EnsureAndroidLaunchReadyAsync(profile))
                return;

            // Persisted "last played" (classic behavior) - the Troubleshooting
            // report uses it when no run happened in this session yet.
            if (Lazydata.ParrotData.SaveLastPlayed)
            {
                Lazydata.ParrotData.LastPlayed = profile.GameNameInternal ?? profile.ProfileName;
                try { JoystickHelper.Serialize(); } catch { /* informational only */ }
            }
            Show(_gameRunning, "Game Running");
            _gameRunning.StartGame(profile, testMode);
        };

        _gameSettings.BackRequested += ShowLibrary;
        _gameSettings.Saved += name => StatusBar.Text = $"Saved settings for {name}";
        _joystickSetup.BackRequested += ShowLibrary;
        _joystickSetup.Saved += name => StatusBar.Text = $"Saved controls for {name}";
        _addGame.BackRequested += ShowLibrary;
        _addGame.GameAdded += profile =>
        {
            if (OperatingSystem.IsAndroid() &&
                profile.EmulatorType == EmulatorType.pcsx2x6)
            {
                StatusBar.Text =
                    $"Added {profile.GameNameInternal ?? profile.ProfileName} — " +
                    "select its System 246/256 game folder when first launched";
                _library.SelectProfile(profile);
                ShowLibrary();
                return;
            }

            if (OperatingSystem.IsAndroid() &&
                profile.EmulatorType == EmulatorType.RPCS3 &&
                PlatformCapabilities.IsAndroidRpcs3ProfileSupported(profile))
            {
                StatusBar.Text =
                    $"Added {profile.GameNameInternal ?? profile.ProfileName} — " +
                    "select the parent rpcs3 arcade folder when first launched";
                _library.SelectProfile(profile);
                ShowLibrary();
                return;
            }

            StatusBar.Text = $"Added {profile.GameNameInternal ?? profile.ProfileName} — set the game path";
            // The new game isn't selected in the library list yet (it was just added) —
            // mark it so ShowLibrary()'s next Refresh() lands back on it instead of
            // defaulting to the first entry.
            _library.SelectProfile(profile);
            _gameSettings.LoadProfile(profile);
            Show(_gameSettings, "Game Settings");
        };
        _verify.BackRequested += ShowLibrary;
        _scanner.BackRequested += () =>
        {
            if (WizardActive) ShowWizard();
            else ShowLibrary();
        };
        _scanner.GamesAdded += count => StatusBar.Text = $"Game scanner added {count} game(s)";
        _gameRunning.BackRequested += ShowLibrary;
        _gameRunning.GameExited += _ =>
        {
            // Return to the library (same game still selected) once the game stops
            if (ContentHost.Content == _gameRunning)
            {
                ShowLibrary();
                StatusBar.Text = "Game session ended";
            }
        };
        _settings.SavedNotification += () => StatusBar.Text = "Settings saved";
        _settings.MultiButtonConfigRequested += () =>
        {
            _multiButton.Refresh();
            Show(_multiButton, "Multi-Game Button Config");
        };
        _multiButton.BackRequested += () =>
        {
            if (WizardActive)
            {
                ShowWizard();
                return;
            }
            Show(_settings, "Settings");
            SetActiveNav(NavSettings);
        };
        _multiButton.Applied += count => StatusBar.Text = $"Applied bindings to {count} game(s)";

        // First-time setup wizard
        _wizard.ScannerRequested += () => Show(_scanner, "Game Scanner");
        _wizard.ButtonConfigRequested += () =>
        {
            _multiButton.Refresh();
            Show(_multiButton, "Multi-Game Button Config");
        };
        _wizard.AccountRequested += () => Show(_account, "Account");
        _wizard.SubscriptionRequested += () => Show(_subscription, "Subscription");
        _wizard.Finished += () =>
        {
            StatusBar.Text = "Setup complete — welcome to TeknoParrot!";
            ShowLibrary();
        };

        // Privacy policy gate (first run) — after we're attached so dialogs have an owner
        bool policiesShown = false;
        AttachedToVisualTree += async (_, _) =>
        {
            if (policiesShown)
                return;
            policiesShown = true;
            await ShowPoliciesGateAsync();
            await ShowPendingChangelogAsync();
            if (Lazydata.ParrotData.HasReadPoliciesNew)
            {
                var resumed = TryResumeActivePlatformSession();
                if (!resumed && OperatingSystem.IsAndroid())
                    await CheckAndroidStartupUpdatesAsync();
            }
        };

        // Player-configurable controller navigation (fullscreen is delegated to the host)
        _uiOptions.Saved += options =>
        {
            StatusBar.Text = "UI options saved";
            _uiNav.Restart(options);
            ApplyUiScale(options.UiScale);
        };
        // Defer the live preview: applying a layout transform synchronously
        // inside the ComboBox SelectionChanged (while its popup is closing)
        // crashes the layout pass — apply after the event has fully unwound.
        _uiOptions.TextSizePreview += scale =>
            Dispatcher.UIThread.Post(() => ApplyUiScale(scale), DispatcherPriority.Background);
        _uiNav.ActionTriggered += action => Dispatcher.UIThread.Post(() => PerformNavAction(action));
        var startupOptions = UiOptions.Load();
        _uiNav.Restart(startupOptions);
        ApplyUiScale(startupOptions.UiScale);

        if (WizardActive)
            ShowWizard();
        else
            Show(_library, "Library");
    }

    /// <summary>Stops background services (controller nav). Called by the desktop host on window close.</summary>
    public void Shutdown() => _uiNav.Dispose();

    private bool TryResumeActivePlatformSession()
    {
        if (!GameSessionFactory.TryGetActivePlatformProfileName(out var profileName))
            return false;

        // AttachedToVisualTree can run before LibraryView.Loaded. On Android
        // that used to race the first catalog load after a package update: a
        // perfectly valid retained session was reported as a missing profile
        // and the foreground recovery service was never reattached to its UI.
        if ((GameProfileLoader.GameProfiles?.Count ?? 0) == 0 &&
            (GameProfileLoader.UserProfiles?.Count ?? 0) == 0)
        {
            try
            {
                GameProfileLoader.LoadProfiles(false);
            }
            catch (Exception error)
            {
                StatusBar.Text = "Could not load the game catalog while restoring the Android session: " +
                                 error.Message;
                return false;
            }
        }

        // Prefer the configured user copy so the recovered view uses the
        // installed executable path and settings, then fall back to stock.
        var profile = GameProfileLoader.UserProfiles?
            .FirstOrDefault(candidate => string.Equals(
                candidate.ProfileName,
                profileName,
                StringComparison.OrdinalIgnoreCase)) ??
            GameProfileLoader.GameProfiles?
            .FirstOrDefault(candidate => string.Equals(
                candidate.ProfileName,
                profileName,
                StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            StatusBar.Text = $"Android is recovering a session for missing profile {profileName}. Re-add it or stop it from the notification.";
            return false;
        }

        _library.SelectProfile(profile);
        Show(_gameRunning, "Game Running");
        _gameRunning.StartGame(profile, testMode: false);
        StatusBar.Text = $"Reattached to {profile.GameNameInternal ?? profile.ProfileName}";
        return true;
    }

    private async System.Threading.Tasks.Task<bool>
        EnsureAndroidLaunchReadyAsync(GameProfile profile)
    {
        if (_androidLaunchPreflightBusy)
        {
            StatusBar.Text = "Android launch checks are already running.";
            return false;
        }

        _androidLaunchPreflightBusy = true;
        try
        {
            StatusBar.Text = "Checking Android runtime components...";
            var missing = await _updates.FindMissingLaunchComponentsAsync(profile);
            if (missing.Count != 0)
            {
                var install = await ShowDecisionAsync(
                    "Required component missing",
                    "This game cannot start until these Android components are installed:\n\n" +
                    string.Join("\n", missing.Select(name => "• " + name)) +
                    "\n\nOpen Updates to install the official packages.",
                    "Open Updates",
                    "Cancel");
                if (install)
                {
                    Show(_updates, "Updates");
                    await _updates.CheckForUpdatesAsync();
                }
                StatusBar.Text =
                    "Game launch stopped because a required Android component is missing.";
                return false;
            }

            if (profile.EmulatorType == EmulatorType.Dolphin)
            {
                var gameName = profile.ExecutableName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(gameName))
                {
                    StatusBar.Text =
                        "This TeknoDolphin profile does not declare an Android game image.";
                    return false;
                }
                StatusBar.Text = "Checking TeknoDolphin game files...";
                await PlatformGameCatalogSync.RefreshNowAsync();
                if (PlatformGameCatalogSync.ReadyExecutables.Contains(
                        gameName,
                        StringComparer.OrdinalIgnoreCase))
                    return true;

                if (!PlatformDolphinGameImport.IsAvailable)
                {
                    StatusBar.Text =
                        "TeknoDolphin game import is unavailable. Update TeknoParrotUI and TeknoDolphin.";
                    return false;
                }
                var import = await ShowDecisionAsync(
                    "TeknoDolphin game image required",
                    $"{profile.GameNameInternal ?? profile.ProfileName} is not installed in " +
                    $"TeknoDolphin yet.\n\nSelect {gameName}. Android grants access only " +
                    "to the file you select; TeknoDolphin copies it into private storage.",
                    "Select Game Image",
                    "Cancel");
                if (!import || !await PlatformDolphinGameImport.ImportAsync(gameName))
                {
                    StatusBar.Text =
                        "TeknoDolphin game import was cancelled or rejected.";
                    return false;
                }
                StatusBar.Text = "Validating imported TeknoDolphin game...";
                await PlatformGameCatalogSync.RefreshNowAsync();
                if (!PlatformGameCatalogSync.ReadyExecutables.Contains(
                        gameName,
                        StringComparer.OrdinalIgnoreCase))
                {
                    StatusBar.Text =
                        "The imported TeknoDolphin game image is unavailable.";
                    return false;
                }
                return true;
            }

            if (profile.EmulatorType == EmulatorType.RPCS3 &&
                PlatformCapabilities.IsAndroidRpcs3ProfileSupported(profile))
            {
                var profileName = profile.ProfileName?.Trim() ?? string.Empty;
                StatusBar.Text = "Checking RPCS3X6 arcade root...";
                await PlatformGameCatalogSync.RefreshNowAsync();
                if (!PlatformGameCatalogSync.ReadyProfileNames.Contains(
                        profileName, StringComparer.OrdinalIgnoreCase))
                {
                    if (!PlatformRpcs3x6GameImport.IsAvailable)
                    {
                        StatusBar.Text = "RPCS3X6 arcade import is unavailable. Update TeknoParrotUI and RPCS3X6.";
                        return false;
                    }
                    var import = await ShowDecisionAsync(
                        "RPCS3X6 arcade games required",
                        "Select the rpcs3 folder that contains the supported System 357/369 game folders. " +
                        "RPCS3X6 copies each game into its own isolated virtual disk because all of them use SCEEXE000.",
                        "Select rpcs3 Folder", "Cancel");
                    if (!import || !await PlatformRpcs3x6GameImport.ImportAsync())
                    {
                        StatusBar.Text = "RPCS3X6 arcade import was cancelled or found no supported games.";
                        return false;
                    }
                    StatusBar.Text = "Validating imported RPCS3X6 arcade games...";
                    await PlatformGameCatalogSync.RefreshNowAsync();
                    if (!PlatformGameCatalogSync.ReadyProfileNames.Contains(
                            profileName, StringComparer.OrdinalIgnoreCase))
                    {
                        StatusBar.Text = "This RPCS3X6 arcade root was not found in the selected folder.";
                        return false;
                    }
                }

                if (!PlatformRpcs3x6Firmware.IsAvailable ||
                    !await PlatformRpcs3x6Firmware.IsConfiguredAsync())
                {
                    var setup = await ShowDecisionAsync(
                        "RPCS3 firmware required",
                        "RPCS3X6 needs an installed PS3 system firmware before an arcade game can start.",
                        "Open RPCS3X6 Setup", "Cancel");
                    if (setup && PlatformRpcs3x6Firmware.IsAvailable)
                        await PlatformRpcs3x6Firmware.ConfigureAsync();
                    StatusBar.Text = "Install the PS3 firmware in RPCS3X6, then return and launch the game again.";
                    return false;
                }
                return true;
            }

            if (profile.EmulatorType != EmulatorType.pcsx2x6)
                return true;

            var manifestName = profile.ExecutableName?.Trim() ?? string.Empty;
            StatusBar.Text = "Checking Tekno2x6 game files...";
            await PlatformGameCatalogSync.RefreshNowAsync();
            if (!PlatformGameCatalogSync.ReadyExecutables.Contains(
                    manifestName,
                    StringComparer.OrdinalIgnoreCase))
            {
                if (!PlatformPcsx2x6GameImport.IsAvailable)
                {
                    StatusBar.Text =
                        "Tekno2x6 game import is unavailable. Update TeknoParrotUI and Tekno2x6.";
                    return false;
                }

                var import = await ShowDecisionAsync(
                    "System 246/256 game files required",
                    $"{profile.GameNameInternal ?? profile.ProfileName} is not installed in " +
                    "Tekno2x6 yet.\n\nSelect the folder containing " +
                    $"{manifestName} and its matching game-data folder. Android will grant " +
                    "access only to the folder you select; Tekno2x6 then validates and copies " +
                    "the files into its own private storage.",
                    "Select Game Folder",
                    "Cancel");
                if (!import)
                {
                    StatusBar.Text = "Game launch cancelled — game files are required.";
                    return false;
                }

                if (!await PlatformPcsx2x6GameImport.ImportAsync(manifestName))
                {
                    StatusBar.Text =
                        "Tekno2x6 did not import a complete matching game package.";
                    return false;
                }

                StatusBar.Text = "Validating imported Tekno2x6 game files...";
                await PlatformGameCatalogSync.RefreshNowAsync();
                if (!PlatformGameCatalogSync.ReadyExecutables.Contains(
                        manifestName,
                        StringComparer.OrdinalIgnoreCase))
                {
                    StatusBar.Text =
                        "The imported game package is incomplete or does not match this title.";
                    return false;
                }
            }

            if (!PlatformPcsx2x6Bios.IsAvailable)
            {
                StatusBar.Text =
                    "PCSX2X6 BIOS setup is unavailable. Update TeknoParrotUI and Tekno2x6.";
                return false;
            }

            StatusBar.Text = "Checking the PCSX2X6 BIOS...";
            if (await PlatformPcsx2x6Bios.IsConfiguredAsync())
                return true;

            var configure = await ShowDecisionAsync(
                "System 246/256 BIOS required",
                "Select both legally obtained arcade BIOS files together:\n\n" +
                "• r27v1602f.7d\n" +
                "• r27v1602f.8g\n\n" +
                "Tekno2x6 validates them and stores them privately.",
                "Select Both Files",
                "Cancel");
            if (!configure)
            {
                StatusBar.Text = "PCSX2X6 launch cancelled — a valid BIOS is required.";
                return false;
            }

            if (!await PlatformPcsx2x6Bios.ConfigureAsync())
            {
                StatusBar.Text =
                    "PCSX2X6 BIOS was not configured. Select a valid BIOS file and try again.";
                return false;
            }

            StatusBar.Text = "Validating the selected PCSX2X6 BIOS...";
            if (!await PlatformPcsx2x6Bios.IsConfiguredAsync())
            {
                StatusBar.Text =
                    "PCSX2X6 rejected the BIOS or its saved file is unavailable.";
                return false;
            }

            StatusBar.Text = "PCSX2X6 BIOS is ready.";
            return true;
        }
        catch (Exception error)
        {
            StatusBar.Text = "Android launch preflight failed: " + error.Message;
            return false;
        }
        finally
        {
            _androidLaunchPreflightBusy = false;
        }
    }

    private async System.Threading.Tasks.Task CheckAndroidStartupUpdatesAsync()
    {
        try
        {
            StatusBar.Text = "Checking Android components and updates...";
            var updateCount = await _updates.CheckForUpdatesAsync();
            var missing = _updates.MissingAndroidComponents;
            if (updateCount == 0 && missing.Count == 0)
            {
                StatusBar.Text = "Android components are installed and up to date.";
                return;
            }

            var details = missing.Count == 0
                ? $"{updateCount} Android update(s) are available."
                : "These Android components are not installed:\n\n" +
                  string.Join("\n", missing.Select(name => "• " + name)) +
                  (updateCount == 0
                      ? string.Empty
                      : $"\n\n{updateCount} install or update package(s) are available.");
            if (await ShowDecisionAsync(
                    "Android components",
                    details + "\n\nOpen Updates to install them now?",
                    "Open Updates",
                    "Later"))
            {
                Show(_updates, "Updates");
            }
            else
            {
                StatusBar.Text =
                    "Android component installation was postponed. Games requiring missing modules will remain blocked.";
            }
        }
        catch (Exception error)
        {
            // Update service/network failure must not make the library unusable.
            StatusBar.Text =
                "Could not check Android components: " + error.Message;
        }
    }

    private async System.Threading.Tasks.Task<bool> ShowDecisionAsync(
        string title,
        string message,
        string acceptText,
        string cancelText)
    {
        var accept = new Button
        {
            Content = acceptText,
            MinWidth = 110,
            Classes = { "primary" }
        };
        var cancel = new Button
        {
            Content = cancelText,
            MinWidth = 90
        };
        var body = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = message,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { cancel, accept }
                    }
                }
            }
        };
        var accepted = false;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = body
            };
            accept.Click += (_, _) =>
            {
                accepted = true;
                dialog.Close();
            };
            cancel.Click += (_, _) => dialog.Close();
            await dialog.ShowDialog(owner);
        }
        else
        {
            var previous = ContentHost.Content;
            var done = new System.Threading.Tasks.TaskCompletionSource(
                System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
            accept.Click += (_, _) =>
            {
                accepted = true;
                done.TrySetResult();
            };
            cancel.Click += (_, _) => done.TrySetResult();
            ContentHost.Content = new Border
            {
                Child = body,
                VerticalAlignment = VerticalAlignment.Center
            };
            await done.Task;
            ContentHost.Content = previous;
        }

        return accepted;
    }

    /// <summary>
    /// Accessibility text-size zoom: scales the whole shell (fonts, icons and
    /// spacing together, like browser zoom) via a layout transform — CPU-cheap
    /// and works with every view without per-view font plumbing.
    /// </summary>
    private void ApplyUiScale(double scale)
    {
        scale = double.IsFinite(scale) ? Math.Clamp(scale, 1.0, 2.0) : 1.0;
        // Always assign a concrete transform (identity at 100%) — a null
        // LayoutTransform is not tolerated on every LayoutTransformControl path.
        RootScale.LayoutTransform = new global::Avalonia.Media.ScaleTransform(scale, scale);
    }

    private void ShowWizard()
    {
        _wizard.ReturnFromStep();
        Show(_wizard, "First-Time Setup");
    }

    /// <summary>
    /// Shows the privacy notice on first run — Accept continues (flag persisted),
    /// Quit closes the app. Same gate as the classic UI. On desktop this is a
    /// modal dialog; on single-view platforms (Android) it takes over the page
    /// host until answered (separate Windows do not exist there).
    /// </summary>
    private async System.Threading.Tasks.Task ShowPoliciesGateAsync()
    {
        if (Lazydata.ParrotData.HasReadPoliciesNew)
            return;

        var accept = new Button { Content = Loc.T("PoliciesAccept", "Accept"), MinWidth = 90, Classes = { "primary" } };
        var quit = new Button { Content = Loc.T("MainQuit", "Quit"), MinWidth = 90 };
        var link = new Button { Content = "View the policies at teknoparrot.com", Background = global::Avalonia.Media.Brushes.Transparent, Padding = new Thickness(0) };
        link.Click += async (_, _) =>
            await Services.ExternalUrlLauncher.OpenAsync(
                this,
                "https://teknoparrot.com/en/Home/Policies");

        var body = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = Loc.T("AppPrivacyNoticeMessage", "TeknoParrotUI collects usage data to improve the software. By continuing, you agree to our privacy policy."),
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                },
                link,
                new StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { quit, accept }
                }
            }
        };

        bool accepted = false;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            // Desktop: classic modal dialog
            var dialog = new Window
            {
                Title = Loc.T("AppPrivacyNoticeTitle", "Privacy Notice"),
                Width = 440,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = body
            };
            accept.Click += (_, _) => { accepted = true; dialog.Close(); };
            quit.Click += (_, _) => dialog.Close();
            await dialog.ShowDialog(owner);
        }
        else
        {
            // Single-view (Android): occupy the page host until answered
            var previous = ContentHost.Content;
            var done = new System.Threading.Tasks.TaskCompletionSource();
            accept.Click += (_, _) => { accepted = true; done.TrySetResult(); };
            quit.Click += (_, _) => done.TrySetResult();
            ContentHost.Content = new Border { Child = body, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            await done.Task;
            ContentHost.Content = previous;
        }

        if (accepted)
        {
            Lazydata.ParrotData.HasReadPoliciesNew = true;
            JoystickHelper.Serialize();
        }
        else
        {
            CloseRequested?.Invoke();
        }
    }

    /// <summary>
    /// After a self-update restart (ParrotPatcher relaunches TeknoParrotUI once it
    /// finishes extracting), a ".lastupdate" marker sits next to the executable
    /// (component|version|base64-changelog — see UpdaterCore.LaunchSelfUpdate).
    /// Show a "what's new" popup with the release notes, then delete the marker.
    /// </summary>
    private async System.Threading.Tasks.Task ShowPendingChangelogAsync()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, ".lastupdate");
        if (!System.IO.File.Exists(path))
            return;

        var entries = new List<(string Name, string Version, string? Body)>();
        try
        {
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                string? body = null;
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    try { body = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])); }
                    catch { /* ignore malformed changelog payload */ }
                }
                entries.Add((parts[0], parts[1], body));
            }
        }
        catch { /* ignore unreadable marker */ }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* ignore */ }
        }

        if (entries.Count == 0 || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var list = new StackPanel { Spacing = 16 };
        foreach (var entry in entries)
        {
            var header = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock { Text = entry.Name, FontWeight = global::Avalonia.Media.FontWeight.Bold, FontSize = 16 });
            header.Children.Add(new TextBlock { Text = entry.Version, Opacity = 0.7, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center });

            var card = new Border
            {
                BorderBrush = global::Avalonia.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        header,
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(entry.Body)
                                ? Loc.T("ChangelogNoInformation", "No changelog information available.")
                                : entry.Body,
                            Opacity = string.IsNullOrWhiteSpace(entry.Body) ? 0.6 : 1.0,
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                }
            };
            list.Children.Add(card);
        }

        var closeButton = new Button { Content = Loc.T("OK", "OK"), MinWidth = 90, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Classes = { "primary" } };
        var dialog = new Window
        {
            Title = Loc.T("ChangelogTitle", "What's New"),
            Width = 520,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    closeButton,
                    new ScrollViewer { Content = list }
                }
            }
        };
        DockPanel.SetDock(closeButton, global::Avalonia.Controls.Dock.Bottom);
        closeButton.Margin = new Thickness(0, 16, 0, 0);
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    private void PerformNavAction(UiNavAction action)
    {
        switch (action)
        {
            case UiNavAction.ToggleFullscreen:
                FullscreenToggleRequested?.Invoke();
                break;
            case UiNavAction.Back:
                ShowLibrary();
                break;
            case UiNavAction.Confirm:
                ActivateFocused();
                break;
            default:
                MoveFocus(action);
                break;
        }
    }

    private void ActivateFocused()
    {
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        switch (focusManager?.GetFocusedElement())
        {
            case ToggleButton toggle: // CheckBox, ToggleSwitch, ...
                toggle.IsChecked = !(toggle.IsChecked ?? false);
                break;
            case Button button:
                button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                break;
            case ComboBox combo:
                combo.IsDropDownOpen = !combo.IsDropDownOpen;
                break;
            case Expander expander:
                expander.IsExpanded = !expander.IsExpanded;
                break;
        }
    }

    private void MoveFocus(UiNavAction direction)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

        // Inside a list, up/down move the selection
        var listBox = (focused as ListBoxItem)?.FindAncestorOfType<ListBox>() ?? focused as ListBox;
        if (listBox != null && direction is UiNavAction.Up or UiNavAction.Down && listBox.ItemCount > 0)
        {
            var index = listBox.SelectedIndex + (direction == UiNavAction.Down ? 1 : -1);
            if (index >= 0 && index < listBox.ItemCount)
            {
                listBox.SelectedIndex = index;
                listBox.ScrollIntoView(index);
                return;
            }
            // at the ends, fall through so focus can leave the list
        }

        var candidates = new List<Control>();
        CollectFocusable(this, candidates);
        if (candidates.Count == 0)
            return;

        if (focused == null || !candidates.Contains(focused))
        {
            candidates[0].Focus(NavigationMethod.Directional);
            return;
        }

        // Nearest focusable control in the requested direction
        var origin = Center(focused);
        Control? best = null;
        var bestScore = double.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate == focused)
                continue;
            var point = Center(candidate);
            double dx = point.X - origin.X, dy = point.Y - origin.Y;
            double forward, sideways;
            switch (direction)
            {
                case UiNavAction.Up: forward = -dy; sideways = System.Math.Abs(dx); break;
                case UiNavAction.Down: forward = dy; sideways = System.Math.Abs(dx); break;
                case UiNavAction.Left: forward = -dx; sideways = System.Math.Abs(dy); break;
                default: forward = dx; sideways = System.Math.Abs(dy); break;
            }
            if (forward < 1)
                continue;
            var score = forward + sideways * 2.5;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        best?.Focus(NavigationMethod.Directional);
        if (best is ListBoxItem item)
            item.FindAncestorOfType<ListBox>()?.ScrollIntoView(item.DataContext!);
    }

    private global::Avalonia.Point Center(Control control) =>
        control.TranslatePoint(new global::Avalonia.Point(control.Bounds.Width / 2, control.Bounds.Height / 2), this)
        ?? new global::Avalonia.Point(0, 0);

    private static void CollectFocusable(global::Avalonia.Visual root, List<Control> result)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is Control { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control)
                result.Add(control);
            CollectFocusable(child, result);
        }
    }

    private Func<string>? _titleProvider;

    private void LocalizeChrome()
    {
        // Localized navigation labels (classic translation keys) — icons are
        // fixed PathIcons in XAML; only the text is localized.
        NavLibraryText.Text = Loc.T("MainLibrary", "Library");
        NavOnlineText.Text = OperatingSystem.IsAndroid()
            ? Loc.T("MainTPOnlineDisabledAndroid", "TP Online (Disabled)")
            : Loc.T("MainTPOnlineNew", "TeknoParrot Online");
        NavUpdatesText.Text = Loc.T("MainCheckUpdates", "Updates");
        NavModsText.Text = Loc.T("MainMods", "Mods");
        NavSubscriptionText.Text = Loc.T("LibraryGenreSubscription", "Subscription");
        NavAccountText.Text = OperatingSystem.IsAndroid()
            ? Loc.T("MainAccountDisabledAndroid", "Login (Disabled)")
            : Loc.T("MainAccount", "Account");
        NavSettingsText.Text = Loc.T("MainSettings", "Settings");
        NavAboutText.Text = Loc.T("MainAbout", "About");
        NavLinuxSetupText.Text = Loc.T("MainLinuxSetup", "Linux Setup");
        NavTroubleshootingText.Text = Loc.T("MainTroubleshooting", "Troubleshooting");
        if (_titleProvider != null)
            PageTitle.Text = _titleProvider();
    }

    private void Show(Control view, string title) => Show(view, () => Loc.T(title, title));

    private void Show(Control view, Func<string> titleProvider)
    {
        // On Android the navigation rail is a temporary drawer. Closing it as
        // soon as a destination is chosen gives the page the full phone width
        // and mirrors the platform's expected one-tap navigation behavior.
        if (OperatingSystem.IsAndroid())
            Sidebar.IsVisible = false;
        _titleProvider = titleProvider;
        ContentHost.Content = view;
        PageTitle.Text = titleProvider();
        // Don't fight binding editors for input while they're capturing
        _uiNav.Suspended = view is JoystickSetupView or MultiButtonConfigView or UiOptionsView;
        UpdateSubscriptionBadge();
    }

    /// <summary>Whether a Patreon/subscription serial key is registered (same check as the classic App.IsPatreon).</summary>
    public static bool IsPatreon()
        => TeknoParrotUi.Common.Activation.TeknoParrotActivation.IsActivatedLocally();

    private void UpdateSubscriptionBadge()
    {
        var subbed = IsPatreon();
        SubStatusText.Text = subbed ? "Subscribed" : "Free";
        // "badge gold" tints via theme tokens — correct in both light and dark
        if (subbed)
        {
            if (!SubBadge.Classes.Contains("gold"))
                SubBadge.Classes.Add("gold");
        }
        else
        {
            SubBadge.Classes.Remove("gold");
        }
    }

    /// <summary>Opens the TeknoParrot Online page (used by --tponline and deep links).</summary>
    public void NavigateToTpo()
    {
        if (OperatingSystem.IsAndroid())
        {
            StatusBar.Text = "TeknoParrot Online is currently disabled on Android.";
            return;
        }

        Show(_tpo, "TeknoParrot Online");
        SetActiveNav(NavOnline);
    }

    private void ShowLibrary()
    {
        Show(_library, "MainLibrary");
        _library.Refresh();
        SetActiveNav(NavLibrary);
    }

    private void SetActiveNav(Button active)
    {
        foreach (var button in new[] { NavLibrary, NavOnline, NavUpdates, NavMods, NavSubscription, NavAccount, NavSettings, NavUiOptions, NavAbout, NavLinuxSetup, NavTroubleshooting })
            button.Classes.Remove("active");
        active.Classes.Add("active");
    }

    private void BtnMenu_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Sidebar.IsVisible = !Sidebar.IsVisible;

    private void NavLibrary_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowLibrary();
    }

    private void NavOnline_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
        {
            StatusBar.Text = "TeknoParrot Online is currently disabled on Android.";
            return;
        }

        Show(_tpo, "MainTPOnlineNew");
        SetActiveNav(NavOnline);
    }

    private void NavUpdates_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_updates, "MainCheckUpdates");
        SetActiveNav(NavUpdates);
    }

    private void NavMods_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_mods, "MainMods");
        SetActiveNav(NavMods);
    }

    private void NavSubscription_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_subscription, "Subscription");
        SetActiveNav(NavSubscription);
    }

    private void NavAccount_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
        {
            StatusBar.Text = "Account login is currently disabled on Android.";
            return;
        }

        Show(_account, "MainAccount");
        SetActiveNav(NavAccount);
    }

    private void NavSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_settings, "MainSettings");
        SetActiveNav(NavSettings);
    }

    private void NavUiOptions_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _uiOptions.Refresh();
        Show(_uiOptions, "UI Options");
        SetActiveNav(NavUiOptions);
    }

    private void NavAbout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_about, "MainAbout");
        SetActiveNav(NavAbout);
    }

    private void NavLinuxSetup_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_linuxSetup, "Linux Setup");
        SetActiveNav(NavLinuxSetup);
    }

    private void NavTroubleshooting_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _troubleshooting.Refresh();
        Show(_troubleshooting, "MainTroubleshooting");
        SetActiveNav(NavTroubleshooting);
    }

    private void NavExit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowExitConfirmationAsync();
    }

    private async void ShowExitConfirmationAsync()
    {
        var noButton = new Button { Content = Loc.T("AppNo", "No") };
        var yesButton = new Button { Content = Loc.T("AppYes", "Yes") };

        var body = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.T("AppExitConfirmTitle", "Exit TeknoParrot?"),
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                    },
                    new TextBlock
                    {
                        Text = Loc.T("AppExitConfirmMessage", "Do you really want to exit the application?"),
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { noButton, yesButton }
                    }
                }
            }
        };

        bool confirmed = false;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            // Desktop: classic modal dialog
            var dialog = new Window
            {
                Title = Loc.T("AppExitConfirmTitle", "Exit TeknoParrot?"),
                Width = 400,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = body
            };
            noButton.Click += (_, _) => dialog.Close();
            yesButton.Click += (_, _) => { confirmed = true; dialog.Close(); };
            await dialog.ShowDialog(owner);
        }
        else
        {
            // Single-view (Android): occupy the page host until answered
            var previous = ContentHost.Content;
            var done = new System.Threading.Tasks.TaskCompletionSource();
            noButton.Click += (_, _) => done.TrySetResult();
            yesButton.Click += (_, _) => { confirmed = true; done.TrySetResult(); };
            ContentHost.Content = new Border { Child = body, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            await done.Task;
            ContentHost.Content = previous;
        }

        if (confirmed)
        {
            CloseRequested?.Invoke();
        }
    }
}
