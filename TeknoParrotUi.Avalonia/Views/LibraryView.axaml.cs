using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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

    public LibraryView()
    {
        InitializeComponent();
        Localize();
        Services.Loc.LanguageChanged += () =>
        {
            Localize();
            Refresh();
        };
        Loaded += (_, _) => Refresh();
    }

    private void Localize()
    {
        BtnLaunch.Content = "▶  " + Services.Loc.T("LibraryLaunchGame", "LAUNCH GAME");
        BtnTestMode.Content = Services.Loc.T("LibraryTestMenu", "Test Menu");
        BtnGameSettings.Content = Services.Loc.T("LibraryGameSettings", "GAME SETTINGS");
        BtnControls.Content = Services.Loc.T("LibraryControllerSetup", "CONTROLLER SETUP");
        BtnVerify.Content = Services.Loc.T("LibraryVerifyGame", "VERIFY");
        BtnAddGame.Content = Services.Loc.T("AddGame", "Add Game");
        BtnScanner.Content = Services.Loc.T("MainRomScanner", "Game Scanner");
        BtnRemoveGame.Content = Services.Loc.T("LibraryDeleteGame", "DELETE");
        SearchBox.Watermark = Services.Loc.T("LibrarySearchHint", "Search games...");
        UpdateFilterHeader();
    }

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        // The library lists the user's installed games only (same as the classic UI).
        // The full catalog lives in Add Game / Game Scanner.
        _profiles = GameProfileLoader.UserProfiles.OrderBy(DisplayName).ToList();

        RebuildFilterList();

        UpdateList();
        StatusText.Text = _profiles.Count == 0
            ? "No games installed yet. Use Add Game to pick individual titles or Game Scanner to import a romset."
            : "";
    }

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
        if (p != null)
            _lastSelectedProfile = p.ProfileName;
        GameTitle.Text = p != null ? DisplayName(p) : "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GamePathText.Text = p?.GamePath ?? "";
        BtnTestMode.IsVisible = p?.HasSeparateTestMode ?? false;

        // Emulator line with homepage link (same as the classic library)
        if (p != null)
        {
            var arch = p.Is64Bit ? "x64" : "x86";
            EmulatorText.Text = $"{Services.Loc.T("LibraryEmulator", "Emulator")}: {p.EmulatorType} ({arch})";
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
        if (info != null)
        {
            GameInfoText.Text = info.ToString().TrimEnd('\n');
            GpuStatusText.Text = $"GPU:  NVIDIA {GpuGlyph(info.nvidia)}   AMD {GpuGlyph(info.amd)}   Intel {GpuGlyph(info.intel)}";
            var issues = info.GetGpuIssues();
            ToolTip.SetTip(GpuStatusText, string.IsNullOrEmpty(issues) ? null : issues);
            GpuStatusText.IsVisible = true;
        }
        else
        {
            GameInfoText.Text = p != null ? Services.Loc.T("LibraryNoInfo", "No information available for this game.") : "";
            GpuStatusText.IsVisible = false;
        }

        LoadIcon(p);
    }

    private void EmulatorLink_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_emulatorUrl != null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_emulatorUrl) { UseShellExecute = true });
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
    private void LaunchSelected(bool testMode)
    {
        var p = Selected;
        if (p == null) return;

        if (string.IsNullOrWhiteSpace(p.GamePath) || !File.Exists(p.GamePath))
        {
            StatusText.Text = "Game executable path is not set or missing — configure it in Game Settings.";
            return;
        }

        NativeLaunchRequested?.Invoke(p, testMode);
    }
}
