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

    public event Action<GameProfile>? GameSettingsRequested;
    public event Action<GameProfile>? ControlsSetupRequested;
    public event Action<GameProfile>? VerifyRequested;
    public event Action? AddGameRequested;
    public event Action? ScannerRequested;
    public event Action<GameProfile, bool>? NativeLaunchRequested;

    public LibraryView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        // Show the user's installed games; fall back to all profiles if none are set up yet
        var source = GameProfileLoader.UserProfiles.Count > 0
            ? GameProfileLoader.UserProfiles
            : GameProfileLoader.GameProfiles;
        _profiles = source.OrderBy(p => DisplayName(p)).ToList();

        var genres = _profiles.Select(p => p.GameGenreInternal)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct().OrderBy(g => g).ToList();
        genres.Insert(0, "All Genres");
        var previous = GenreBox.SelectedItem as string;
        GenreBox.ItemsSource = genres;
        GenreBox.SelectedIndex = previous != null && genres.Contains(previous) ? genres.IndexOf(previous) : 0;

        UpdateList();
        StatusText.Text = GameProfileLoader.UserProfiles.Count == 0
            ? "No games set up yet — showing all available profiles. Use Add Game to install."
            : "";
    }

    private static string DisplayName(GameProfile p) => p.GameNameInternal ?? p.ProfileName ?? "?";

    private void UpdateList()
    {
        var search = SearchBox.Text;
        var genre = GenreBox.SelectedItem as string;

        _filtered = _profiles
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        DisplayName(p).Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(p => genre == null || genre == "All Genres" || p.GameGenreInternal == genre)
            .ToList();

        GameList.ItemsSource = _filtered.Select(DisplayName).ToList();
        if (_filtered.Count > 0)
            GameList.SelectedIndex = 0;
    }

    private GameProfile? Selected =>
        GameList.SelectedIndex >= 0 && GameList.SelectedIndex < _filtered.Count
            ? _filtered[GameList.SelectedIndex]
            : null;

    private void GameList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        GameTitle.Text = p != null ? DisplayName(p) : "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GamePathText.Text = p?.GamePath ?? "";
        BtnTestMode.IsVisible = p?.HasSeparateTestMode ?? false;
        LoadIcon(p);
    }

    private void LoadIcon(GameProfile? p)
    {
        GameIcon.Source = null;
        if (p?.IconName == null) return;
        var iconPath = Path.GetFullPath(p.IconName);
        if (File.Exists(iconPath))
        {
            try { GameIcon.Source = new Bitmap(iconPath); }
            catch { /* corrupt icon — leave blank */ }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateList();
    private void GenreBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateList();
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

        // Experimental in-process launcher (extracted pipeline); falls back to the
        // classic exe for emulator types it does not support yet.
        if (ChkNativeLaunch.IsChecked == true && Common.GameLaunch.GameSession.SupportsNativeLaunch(p))
        {
            NativeLaunchRequested?.Invoke(p, testMode);
            return;
        }

        if (!GameLauncherService.CanLaunch)
        {
            StatusText.Text = "TeknoParrotUi.exe not found — launching requires it until the native pipeline lands.";
            return;
        }

        GameLauncherService.Launch(p, testMode);
        StatusText.Text = $"Launched {DisplayName(p)}{(testMode ? " (test menu)" : "")}";
    }
}
