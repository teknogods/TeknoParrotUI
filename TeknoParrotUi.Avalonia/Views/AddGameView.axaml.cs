using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AddGameView : UserControl
{
    private List<GameProfile> _available = new();
    private List<GameProfile> _filtered = new();

    public event Action? BackRequested;
    public event Action<GameProfile>? GameAdded;

    public AddGameView()
    {
        InitializeComponent();
        Localize();
        Services.Loc.LanguageChanged += Localize;
        GenreBox.ItemsSource = Services.GenreHelper.GetGenres(includeNotInstalled: true)
            .Select(Services.GenreHelper.LocalizeGenre).ToList();
        GenreBox.SelectedIndex = 0;
        Loaded += (_, _) => Refresh();
    }

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("AddGame", "Add Game");
        SearchBox.Watermark = Services.Loc.T("LibrarySearchHint", "Search games...");
        BtnBack.Content = Services.Loc.T("Back", "Back");
        BtnAdd.Content = Services.Loc.T("AddGame", "Add Game");
    }

    private void GenreBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateList();

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        var installed = GameProfileLoader.UserProfiles.Select(p => p.ProfileName).ToHashSet();
        // Same rules as the classic Add Game view: all profiles, legacy ones only when
        // already installed, installed titles marked with a suffix.
        _available = GameProfileLoader.GameProfiles
            .Where(p => !p.IsLegacy || installed.Contains(p.ProfileName))
            .OrderBy(p => p.GameNameInternal ?? p.ProfileName)
            .ToList();
        _installed = installed;
        UpdateList();
    }

    private HashSet<string> _installed = new();

    private void UpdateList()
    {
        var search = SearchBox.Text;
        var genres = Services.GenreHelper.GetGenres(includeNotInstalled: true);
        var genre = GenreBox.SelectedIndex >= 0 && GenreBox.SelectedIndex < genres.Count
            ? genres[GenreBox.SelectedIndex]
            : "All";
        _filtered = _available
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        (p.GameNameInternal ?? p.ProfileName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(p => Services.GenreHelper.DoesGameMatchGenre(genre, p))
            .ToList();
        GameList.ItemsSource = _filtered
            .Select(p => (p.GameNameInternal ?? p.ProfileName) + (_installed.Contains(p.ProfileName) ? "   ✓ Added" : ""))
            .ToList();
        CountText.Text = $"{_filtered.Count} of {_available.Count} games";
    }

    private GameProfile? Selected =>
        GameList.SelectedIndex >= 0 && GameList.SelectedIndex < _filtered.Count ? _filtered[GameList.SelectedIndex] : null;

    private async void GameList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        BtnAdd.IsEnabled = p != null && !_installed.Contains(p.ProfileName);
        BtnAdd.Content = p != null && _installed.Contains(p.ProfileName) ? "Already Added" : "Add Game";
        GameTitle.Text = p?.GameNameInternal ?? "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GameEmulator.Text = p != null ? $"Emulator: {p.EmulatorType}" : "";
        GameIcon.Source = null;
        if (p == null) return;
        var iconPath = await Services.IconService.EnsureIconAsync(p);
        if (iconPath != null && Selected == p)
        {
            try { GameIcon.Source = new Bitmap(iconPath); } catch { }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateList();
    private void GameList_DoubleTapped(object? sender, TappedEventArgs e) => AddSelected();
    private void BtnAdd_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => AddSelected();
    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BackRequested?.Invoke();

    private void AddSelected()
    {
        var profile = Selected;
        if (profile == null || _installed.Contains(profile.ProfileName)) return;

        Directory.CreateDirectory("UserProfiles");
        JoystickHelper.SerializeGameProfile(profile);

        // Reload so the user profile instance (with FileName pointing at UserProfiles) is used
        GameProfileLoader.LoadProfiles(false);
        var added = GameProfileLoader.UserProfiles.FirstOrDefault(p => p.ProfileName == profile.ProfileName) ?? profile;
        GameAdded?.Invoke(added);
    }
}
