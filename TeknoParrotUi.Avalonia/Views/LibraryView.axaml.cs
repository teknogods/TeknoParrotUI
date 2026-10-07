using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class LibraryView : UserControl
{
    private List<GameProfile> _profiles = new();
    private List<GameProfile> _filtered = new();
    // Unified filter state: one checkbox list (All / status / platforms / genres).
    // Within a section checks broaden (OR); sections combine to narrow (AND).
    private readonly HashSet<string> _checkedStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checkedPlatforms = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checkedGenres = new(StringComparer.OrdinalIgnoreCase);
    private CheckBox? _allCheckBox;
    private bool _rebuildingFilters;
    private string? _lastSelectedProfile;

    public event Action<GameProfile>? GameSettingsRequested;
    public event Action<GameProfile>? ControlsSetupRequested;
    public event Action<GameProfile>? VerifyRequested;
    public event Action? AddGameRequested;
    public event Action? ScannerRequested;
    public event Action<GameProfile, bool>? NativeLaunchRequested;
    public event Action? AccountRequested;
    public event Action? OnlineRequested;

    public LibraryView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => StopLanPresence();
        PlatformGameCatalogSync.CatalogUpdated += OnPlatformCatalogUpdated;
        BtnLaunch.IsVisible = PlatformCapabilities.CanLaunchGames;
        BtnTestMode.IsVisible = PlatformCapabilities.CanLaunchGames;
        Localize();
        Services.Loc.LanguageChanged += () =>
        {
            Localize();
            Refresh();
        };
        Loaded += (_, _) =>
        {
            ApplyResponsiveLayout(Bounds.Width);
            Refresh();
        };
        SizeChanged += (_, eventArgs) => ApplyResponsiveLayout(eventArgs.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var narrow = OperatingSystem.IsAndroid() && width > 0 && width < 620;
        if (narrow)
        {
            LayoutGrid.ColumnDefinitions = new ColumnDefinitions("*");
            LayoutGrid.RowDefinitions = new RowDefinitions("Auto,Auto,150,*");
            Grid.SetColumn(SearchBox, 0);
            Grid.SetColumn(FilterExpander, 0);
            Grid.SetColumn(GameList, 0);
            Grid.SetRow(GameList, 2);
            Grid.SetColumn(DetailsPanel, 0);
            Grid.SetRow(DetailsPanel, 3);
            Grid.SetRowSpan(DetailsPanel, 1);
            DetailsPanel.Margin = new global::Avalonia.Thickness(0, 10, 0, 0);
            return;
        }

        LayoutGrid.ColumnDefinitions = new ColumnDefinitions("*,240");
        LayoutGrid.RowDefinitions = new RowDefinitions("Auto,Auto,*");
        Grid.SetColumn(SearchBox, 0);
        Grid.SetColumn(FilterExpander, 0);
        Grid.SetColumn(GameList, 0);
        Grid.SetRow(GameList, 2);
        Grid.SetColumn(DetailsPanel, 1);
        Grid.SetRow(DetailsPanel, 0);
        Grid.SetRowSpan(DetailsPanel, 3);
        DetailsPanel.Margin = new global::Avalonia.Thickness(10, 0, 0, 0);
    }

    private void Localize()
    {
        BtnLaunch.Content = "▶  " + Services.Loc.T("LibraryLaunchGame", "LAUNCH GAME");
        BtnTestMode.Content = Services.Loc.T("LibraryTestMenu", "Test Menu");
        BtnGameSettings.Content = Services.Loc.T("LibraryGameSettings", "GAME SETTINGS");
        BtnControls.Content = Services.Loc.T("LibraryControllerSetup", "CONTROLLER SETUP");
        BtnHighScores.Content = Services.Loc.T("LibraryHighScores", "HIGH SCORES");
        BtnVerify.Content = Services.Loc.T("LibraryVerifyGame", "VERIFY");
        BtnAddGame.Content = Services.Loc.T("AddGame", "Add Game");
        BtnScanner.Content = Services.Loc.T("MainRomScanner", "Game Scanner");
        BtnRemoveGame.Content = Services.Loc.T("LibraryDeleteGame", "DELETE");
        SearchBox.PlaceholderText = Services.Loc.T("LibrarySearchHint", "Search games...");
        UpdateFilterHeader();
    }

    /// <summary>
    /// Marks the given profile as the one to reselect on the next Refresh(). Used
    /// when navigating to Settings/Controls for a game that isn't (yet) selected
    /// in the list — e.g. a freshly added game — so returning to the library
    /// lands back on it instead of defaulting to the first entry.
    /// </summary>
    public void SelectProfile(GameProfile profile)
    {
        _lastSelectedProfile = profile.ProfileName;
        // Returning from Add Game while the Library still has an unrelated
        // search term used to make the newly-added profile impossible to
        // reselect; Refresh then silently selected the first old result.
        var search = SearchBox.Text;
        if (!string.IsNullOrWhiteSpace(search) &&
            !DisplayName(profile).Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            SearchBox.Text = string.Empty;
        }
    }

    public void Refresh() => Refresh(requestPlatformCatalogRefresh: true);

    private void Refresh(bool requestPlatformCatalogRefresh)
    {
        if (requestPlatformCatalogRefresh)
            PlatformGameCatalogSync.RequestRefresh();
        GameProfileLoader.LoadProfiles(false);
        // The library lists the user's installed games only (same as the classic UI).
        // On Android, complete PCSX2X6 and TeknoDolphin sets live in the companions' scoped
        // storage. Their authenticated ready-manifest catalogs let those stock
        // profiles appear immediately without requiring a fake executable path
        // or pre-creating mutable user settings. RPCS3X6 profiles use the normal
        // Add Game flow because each user selects that game's EBOOT.BIN directly.
        var profiles = ArcadeGameRevisions.GetLibraryProfiles(GameProfileLoader.UserProfiles).ToList();
        if (OperatingSystem.IsAndroid())
        {
            var readyExecutables =
                PlatformGameCatalogSync.ReadyExecutables.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
            var representedExecutables = profiles
                .Select(profile => profile.ExecutableName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var readyProfiles =
                GameProfileLoader.GameProfiles
                    .Where(profile =>
                        profile.EmulatorType is EmulatorType.pcsx2x6 or EmulatorType.Dolphin &&
                        !string.IsNullOrWhiteSpace(profile.ExecutableName) &&
                        readyExecutables.Contains(profile.ExecutableName) &&
                        !representedExecutables.Contains(profile.ExecutableName))
                    .GroupBy(
                        profile => profile.ExecutableName,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderBy(
                            profile => profile.ProfileName,
                            StringComparer.OrdinalIgnoreCase)
                        .First())
                    .ToList();
            profiles.AddRange(readyProfiles);
        }
        _profiles = profiles.OrderBy(DisplayName).ToList();

        RebuildFilterList();

        UpdateList();
        StatusText.Text = !PlatformCapabilities.CanLaunchGames
            ? PlatformCapabilities.AndroidLaunchUnavailableMessage
            : _profiles.Count == 0
                ? "No games installed yet. Use Add Game to pick individual titles or Game Scanner to import a romset."
                : "";
    }

    private void OnPlatformCatalogUpdated(int ready) =>
        Dispatcher.UIThread.Post(() =>
        {
            // Publishing a completed companion query must not start another
            // query. Doing so creates a permanent event/refresh loop and can
            // expose a transient empty catalog while a game launch validates
            // its already-imported manifest.
            Refresh(requestPlatformCatalogRefresh: false);
            StatusText.Text =
                $"Found {ready} installed companion game{(ready == 1 ? "" : "s")}.";
        });

    private static string DisplayName(GameProfile p) => p.GameNameInternal ?? p.ProfileName ?? "?";

    private static string PlatformName(GameProfile p) =>
        string.IsNullOrWhiteSpace(p.GameInfo?.platform) ? "Unknown" : p.GameInfo.platform;

    private bool AnyFilterChecked =>
        _checkedStatus.Count + _checkedPlatforms.Count + _checkedGenres.Count > 0;

    /// <summary>
    /// Rebuilds the unified filter list: All, then Status / Platforms / Genres
    /// sections, every entry a checkbox. Platforms come from the installed
    /// games' metadata so each entry matches at least one game. Checked state
    /// survives refreshes; stale platform checks are dropped.
    /// </summary>
    private void RebuildFilterList()
    {
        _rebuildingFilters = true;

        var platforms = _profiles
            .Select(PlatformName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _checkedPlatforms.RemoveWhere(p => !platforms.Contains(p, StringComparer.OrdinalIgnoreCase));

        var items = new List<Control>();

        // "All" clears every filter; checked (and disabled) while nothing is selected
        _allCheckBox = MakeFilterCheckBox(Services.GenreHelper.LocalizeGenre("All"), isChecked: !AnyFilterChecked);
        _allCheckBox.IsEnabled = AnyFilterChecked;
        _allCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_rebuildingFilters || _allCheckBox.IsChecked != true)
                return;
            _checkedStatus.Clear();
            _checkedPlatforms.Clear();
            _checkedGenres.Clear();
            RebuildFilterList();
            UpdateList();
        };
        items.Add(_allCheckBox);

        void AddSection(string headerKey, string headerFallback, IEnumerable<string> names, HashSet<string> set, bool localize)
        {
            items.Add(new TextBlock
            {
                Text = Services.Loc.T(headerKey, headerFallback),
                Classes = { "caption" },
                FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                Margin = new global::Avalonia.Thickness(2, 8, 0, 0),
                IsHitTestVisible = false,
            });
            foreach (var name in names)
            {
                var box = MakeFilterCheckBox(localize ? Services.GenreHelper.LocalizeGenre(name) : name, set.Contains(name));
                box.IsCheckedChanged += (_, _) =>
                {
                    if (_rebuildingFilters)
                        return;
                    if (box.IsChecked == true)
                        set.Add(name);
                    else
                        set.Remove(name);
                    SyncAllCheckBox();
                    UpdateFilterHeader();
                    UpdateList();
                };
                items.Add(box);
            }
        }

        AddSection("LibraryFilterStatus", "Status", Services.GenreHelper.GetStatusFilters(), _checkedStatus, localize: true);
        AddSection("LibraryPlatforms", "Platforms", platforms, _checkedPlatforms, localize: false);
        AddSection("LibraryFilterGenres", "Genres", Services.GenreHelper.GetGenreNames(_profiles), _checkedGenres, localize: true);

        FilterList.ItemsSource = items;
        _rebuildingFilters = false;
        UpdateFilterHeader();
    }

    private static CheckBox MakeFilterCheckBox(string label, bool isChecked) => new()
    {
        Content = label,
        IsChecked = isChecked,
        FontSize = 12,
        MinHeight = 0,
        Padding = new global::Avalonia.Thickness(6, 2),
    };

    private void SyncAllCheckBox()
    {
        if (_allCheckBox == null)
            return;
        _rebuildingFilters = true;
        _allCheckBox.IsChecked = !AnyFilterChecked;
        _allCheckBox.IsEnabled = AnyFilterChecked;
        _rebuildingFilters = false;
    }

    private void UpdateFilterHeader()
    {
        var label = Services.Loc.T("LibraryFilters", "Filters");
        var count = _checkedStatus.Count + _checkedPlatforms.Count + _checkedGenres.Count;
        FilterExpander.Header = count > 0
            ? $"{label} ({count})"
            : $"{label}: {Services.GenreHelper.LocalizeGenre("All")}";
    }

    private void UpdateList()
    {
        var search = SearchBox.Text;

        _filtered = _profiles
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        DisplayName(p).Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(p => _checkedStatus.Count == 0 ||
                        _checkedStatus.Any(s => Services.GenreHelper.DoesGameMatchGenre(s, p)))
            .Where(p => _checkedPlatforms.Count == 0 || _checkedPlatforms.Contains(PlatformName(p)))
            .Where(p => _checkedGenres.Count == 0 ||
                        _checkedGenres.Any(g => Services.GenreHelper.DoesGameMatchGenre(g, p)))
            .ToList();

        GameList.ItemsSource = _filtered.Select(DisplayName).ToList();

        // Restore the previously selected game (e.g. after visiting controls/settings)
        var restoreIndex = _lastSelectedProfile != null
            ? _filtered.FindIndex(p => p.ProfileName == _lastSelectedProfile)
            : -1;
        if (restoreIndex >= 0)
        {
            GameList.SelectedIndex = restoreIndex;
            GameList.ScrollIntoView(restoreIndex);
        }
        else if (_filtered.Count > 0)
        {
            GameList.SelectedIndex = 0;
        }
    }

    private GameProfile? Selected =>
        GameList.SelectedIndex >= 0 && GameList.SelectedIndex < _filtered.Count
            ? _filtered[GameList.SelectedIndex]
            : null;

    // Same emulator homepage links as the classic library
    private static readonly Dictionary<EmulatorType, string> EmulatorUrls = new()
    {
        { EmulatorType.OpenParrot, "https://github.com/teknogods/OpenParrot" },
        { EmulatorType.Dolphin, "https://dolphin-emu.org" },
        { EmulatorType.Play, "https://purei.org" },
        { EmulatorType.RPCS3, "https://rpcs3.net" },
        { EmulatorType.cxbxr, "https://cxbx-reloaded.co.uk" },
        { EmulatorType.pcsx2x6, "https://ps2homebrew-arcade.github.io/pcsx2x6/" },
    };

    private string? _emulatorUrl;

    private static string GpuGlyph(GPUSTATUS status) => status switch
    {
        GPUSTATUS.OK => "✔",
        GPUSTATUS.NO => "✖",
        GPUSTATUS.WITH_FIX => "🔧",
        GPUSTATUS.HAS_ISSUES => "⚠",
        _ => "?"
    };

    private void GameList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        UpdateLanPresence(p);
        BtnFriends.IsVisible = Common.Online.InitialDUnifiedMode.IsMatchmakingProfile(p);
        BtnGtOnTp.IsVisible = p?.HasGtOnTp == true;
        BtnOnlineProfile.IsVisible = !string.IsNullOrWhiteSpace(p?.OnlineProfileURL);
        BtnPlayOnline.IsVisible = p?.HasTpoSupport == true && !Common.Online.InitialDUnifiedMode.IsTpoRetired(p) &&
            !OperatingSystem.IsAndroid();
        BtnLaunch.IsEnabled = p != null && !p.IsTpoExclusive;
        if (p != null)
            _lastSelectedProfile = p.ProfileName;
        GameTitle.Text = p != null ? DisplayName(p) : "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GamePathText.Text = p?.GamePath ?? "";
        var terminal = p?.ConfigValues?.Any(field => field.CategoryName == "General" && field.FieldType == FieldType.Bool &&
            field.FieldName is "TerminalMode" or "Terminal Mode") == true;
        BtnTestMode.IsVisible = p != null && PlatformCapabilities.CanLaunchGames;
        BtnTestMode.IsEnabled = terminal || (p?.HasSeparateTestMode ?? false);
        ToolTip.SetTip(BtnTestMode, BtnTestMode.IsEnabled ? null : "The test menu is accessed with in-game controls, or is unavailable.");
        BtnTestMode.Content = terminal ? Loc.T("LibraryTerminalMode", "Terminal Mode") : Loc.T("LibraryTestMode", "Test Menu");
        BtnHighScores.IsVisible = HighScoreUrlResolver.Resolve(
            p?.ProfileName,
            Lazydata.ParrotData.Language) != null;

        // Emulator line with homepage link (same as the classic library)
        if (p != null)
        {
            var arch = p.Is64Bit ? "x64" : "x86";
            var emulatorDescription = OperatingSystem.IsAndroid()
                ? p.EmulatorType switch
                {
                    EmulatorType.pcsx2x6 => "PCSX2X6 ARM64",
                    EmulatorType.Dolphin => "TeknoDolphin ARM64",
                    EmulatorType.RPCS3 when PlatformCapabilities.IsAndroidRpcs3ProfileSupported(p) => "RPCS3X6 ARM64",
                    _ => $"{p.EmulatorType} ({arch})"
                }
                : $"{p.EmulatorType} ({arch})";
            EmulatorText.Text =
                $"{Services.Loc.T("LibraryEmulator", "Emulator")}: {emulatorDescription}";
            EmulatorUrls.TryGetValue(p.EmulatorType, out _emulatorUrl);
            EmulatorLink.IsVisible = true;
            ToolTip.SetTip(EmulatorLink, _emulatorUrl);
        }
        else
        {
            EmulatorLink.IsVisible = false;
            _emulatorUrl = null;
        }

        // Metadata block: platform, release year, wheel rotation, supported
        // versions, TPO version, general issues + GPU compatibility
        var info = p?.GameInfo;
        GameInfoGrid.Children.Clear();
        GameInfoGrid.RowDefinitions.Clear();
        if (info != null)
        {
            AddInfoRow("Platform", info.platform);
            AddInfoRow("Release year", info.release_year);
            AddInfoRow("Wheel rotation", info.wheel_rotation);
            AddInfoRow("Versions", info.supported_versions == null ? null : string.Join(", ", info.supported_versions));
            AddInfoRow("TPO version", info.tpo_version);
            GameInfoText.Text = info.general_issues?.Trim() ?? "";
            var untested = info.nvidia == GPUSTATUS.NO_INFO && info.amd == GPUSTATUS.NO_INFO && info.intel == GPUSTATUS.NO_INFO;
            GpuStatusText.Text = "GPU: Untested";
            GpuStatusText.IsVisible = untested;
            GpuVendors.IsVisible = !untested;
            SetGpuVendor(GpuNvidia, "NVIDIA", info.nvidia, info.nvidia_issues);
            SetGpuVendor(GpuAmd, "AMD", info.amd, info.amd_issues);
            SetGpuVendor(GpuIntel, "Intel", info.intel, info.intel_issues);
        }
        else
        {
            GameInfoText.Text = p != null ? Services.Loc.T("LibraryNoInfo", "No information available for this game.") : "";
            GpuStatusText.IsVisible = false;
            GpuVendors.IsVisible = false;
        }

        LoadIcon(p);
    }

    private async void EmulatorLink_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_emulatorUrl != null)
            await Services.ExternalUrlLauncher.OpenAsync(this, _emulatorUrl);
    }

    private void AddInfoRow(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = GameInfoGrid.RowDefinitions.Count;
        GameInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var name = new TextBlock { Text = label, Opacity = 0.65, FontSize = 11 };
        var text = new TextBlock { Text = value, FontSize = 11, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        Grid.SetRow(name, row); Grid.SetRow(text, row); Grid.SetColumn(text, 1);
        GameInfoGrid.Children.Add(name); GameInfoGrid.Children.Add(text);
    }

    private static void SetGpuVendor(TextBlock text, string vendor, GPUSTATUS status, string? issues)
    {
        text.Text = vendor + " " + GpuGlyph(status);
        var description = status switch
        {
            GPUSTATUS.OK => "Works",
            GPUSTATUS.WITH_FIX => "Works with a fix",
            GPUSTATUS.HAS_ISSUES => "Runs with issues",
            GPUSTATUS.NO => "Not working",
            _ => "Untested"
        };
        ToolTip.SetTip(text, string.IsNullOrWhiteSpace(issues) ? description : description + ": " + issues.Trim());
    }

    private async void BtnHighScores_Click(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var url = HighScoreUrlResolver.Resolve(
            Selected?.ProfileName,
            Lazydata.ParrotData.Language);
        if (url == null)
            return;

        if (!await Services.ExternalUrlLauncher.OpenAsync(this, url.AbsoluteUri))
            StatusText.Text = Services.Loc.T(
                "LibraryHighScoresOpenFailed",
                "Could not open the high-score page.");
    }

    private async void LoadIcon(GameProfile? p)
    {
        GameIcon.Source = null;
        if (p == null) return;

        // Downloads the icon on demand (honors the DownloadIcons setting)
        var iconPath = await Services.IconService.EnsureIconAsync(p);
        if (iconPath == null || Selected != p)
            return;
        try
        {
            GameIcon.Source = new Bitmap(iconPath);
        }
        catch
        {
            // corrupt icon — delete so it re-downloads next time (classic behaviour)
            try { File.Delete(iconPath); } catch { }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateList();
    private void GameList_DoubleTapped(object? sender, TappedEventArgs e) => LaunchSelected(false);
    private void BtnLaunch_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelected(false);
    private void BtnTestMode_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelected(true);

    private void BtnGameSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected != null)
            GameSettingsRequested?.Invoke(Selected);
    }

    private void BtnControls_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected != null)
            ControlsSetupRequested?.Invoke(Selected);
    }

    private void BtnVerify_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected != null)
            VerifyRequested?.Invoke(Selected);
    }

    private void BtnAddGame_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        AddGameRequested?.Invoke();

    private void BtnScanner_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ScannerRequested?.Invoke();

    private void BtnRemoveGame_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var p = Selected;
        if (p?.FileName == null) return;
        // Only user profiles can be removed
        if (!p.FileName.Replace('\\', '/').Contains("UserProfiles/") && !File.Exists(Path.Combine("UserProfiles", Path.GetFileName(p.FileName))))
        {
            StatusText.Text = "This game is not installed (no user profile to remove).";
            return;
        }
        try
        {
            File.Delete(Path.Combine("UserProfiles", Path.GetFileName(p.FileName)));
            StatusText.Text = $"Removed {DisplayName(p)}";
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not remove: {ex.Message}";
        }
    }
    private async void LaunchSelected(bool testMode, bool friends = false)
    {
        if (_launchPending) return;
        if (!PlatformCapabilities.CanLaunchGames)
        {
            StatusText.Text = PlatformCapabilities.AndroidLaunchUnavailableMessage;
            return;
        }

        var p = Selected;
        if (p == null) return;

        // Android's scoped-storage boundary means TeknoParrotUI may not be
        // allowed to stat a Downloads file that the Winlator companion can
        // resolve as D:. The Android backend performs its own canonical path
        // validation before starting the foreground service.
        var hasImplicitAndroidCompanionGame =
            OperatingSystem.IsAndroid() &&
            !string.IsNullOrWhiteSpace(p.ExecutableName) &&
            (p.EmulatorType == EmulatorType.Dolphin ||
             p.EmulatorType == EmulatorType.RPCS3 && PlatformCapabilities.IsAndroidRpcs3ProfileSupported(p) ||
             p.EmulatorType == EmulatorType.pcsx2x6 &&
             p.ExecutableName.EndsWith(".acgame", StringComparison.OrdinalIgnoreCase));
        if ((!hasImplicitAndroidCompanionGame && string.IsNullOrWhiteSpace(p.GamePath)) ||
            (!OperatingSystem.IsAndroid() && !File.Exists(p.GamePath)))
        {
            StatusText.Text = "Game executable path is not set or missing — configure it in Game Settings.";
            return;
        }

        _launchPending = true;
        try
        {
            if (!testMode && !await Services.OnlineLaunchFlow.BeforeLaunchAsync(this, p, friends,
                () => AccountRequested?.Invoke())) return;
            if (_lanSession?.Peers().Count > 0) await Services.OnlineLaunchFlow.OfferFirewallAsync(this, p);
            StopLanPresence();
            if (testMode && p.ConfigValues?.Any(field => field.CategoryName == "General" && field.FieldType == FieldType.Bool &&
                field.FieldName is "TerminalMode" or "Terminal Mode") == true)
            {
                p = p.Clone();
                foreach (var field in p.ConfigValues.Where(field => field.CategoryName == "General" && field.FieldType == FieldType.Bool))
                    if (field.FieldName is "TerminalMode" or "Terminal Mode") field.FieldValue = "1";
                    else if (field.FieldName is "TerminalEmulator" or "Terminal Emu") field.FieldValue = "0";
                p.AllowSettingSync = false;
                testMode = false;
            }
            NativeLaunchRequested?.Invoke(p, testMode);
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { _launchPending = false; }
    }
    private bool _launchPending;
}
