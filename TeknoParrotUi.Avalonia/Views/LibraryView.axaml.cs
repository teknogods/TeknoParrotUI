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
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        // The library lists the user's installed games only (same as the classic UI).
        // The full catalog lives in Add Game / Game Scanner.
        _profiles = GameProfileLoader.UserProfiles.OrderBy(DisplayName).ToList();

        // Standard TeknoParrot category list (not derived from installed games)
        var genres = Services.GenreHelper.GetGenres();
        var previous = GenreBox.SelectedItem as string;
        GenreBox.ItemsSource = genres;
        GenreBox.SelectedIndex = previous != null && genres.Contains(previous) ? genres.IndexOf(previous) : 0;

        UpdateList();
        StatusText.Text = _profiles.Count == 0
            ? "No games installed yet. Use Add Game to pick individual titles or Game Scanner to import a romset."
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
            .Where(p => Services.GenreHelper.DoesGameMatchGenre(genre, p))
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

    private void GameList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        if (p != null)
            _lastSelectedProfile = p.ProfileName;
        GameTitle.Text = p != null ? DisplayName(p) : "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GamePathText.Text = p?.GamePath ?? "";
        BtnTestMode.IsVisible = p?.HasSeparateTestMode ?? false;
        LoadIcon(p);
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

        NativeLaunchRequested?.Invoke(p, testMode);
    }
}
