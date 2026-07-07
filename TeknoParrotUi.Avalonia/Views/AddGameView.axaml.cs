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
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        var installed = GameProfileLoader.UserProfiles.Select(p => p.ProfileName).ToHashSet();
        _available = GameProfileLoader.GameProfiles
            .Where(p => !p.IsLegacy && !installed.Contains(p.ProfileName))
            .OrderBy(p => p.GameNameInternal ?? p.ProfileName)
            .ToList();
        UpdateList();
    }

    private void UpdateList()
    {
        var search = SearchBox.Text;
        _filtered = _available
            .Where(p => string.IsNullOrWhiteSpace(search) ||
                        (p.GameNameInternal ?? p.ProfileName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        GameList.ItemsSource = _filtered.Select(p => p.GameNameInternal ?? p.ProfileName).ToList();
        CountText.Text = $"{_filtered.Count} of {_available.Count} games available";
    }

    private GameProfile? Selected =>
        GameList.SelectedIndex >= 0 && GameList.SelectedIndex < _filtered.Count ? _filtered[GameList.SelectedIndex] : null;

    private void GameList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var p = Selected;
        BtnAdd.IsEnabled = p != null;
        GameTitle.Text = p?.GameNameInternal ?? "";
        GameGenre.Text = p?.GameGenreInternal ?? "";
        GameEmulator.Text = p != null ? $"Emulator: {p.EmulatorType}" : "";
        GameIcon.Source = null;
        if (p?.IconName != null && File.Exists(Path.GetFullPath(p.IconName)))
        {
            try { GameIcon.Source = new Bitmap(Path.GetFullPath(p.IconName)); } catch { }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => UpdateList();
    private void GameList_DoubleTapped(object? sender, TappedEventArgs e) => AddSelected();
    private void BtnAdd_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => AddSelected();
    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BackRequested?.Invoke();

    private void AddSelected()
    {
        var profile = Selected;
        if (profile == null) return;

        Directory.CreateDirectory("UserProfiles");
        JoystickHelper.SerializeGameProfile(profile);

        // Reload so the user profile instance (with FileName pointing at UserProfiles) is used
        GameProfileLoader.LoadProfiles(false);
        var added = GameProfileLoader.UserProfiles.FirstOrDefault(p => p.ProfileName == profile.ProfileName) ?? profile;
        GameAdded?.Invoke(added);
    }
}
