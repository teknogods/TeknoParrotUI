using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia;

public partial class MainWindow : Window
{
    private List<GameProfile> _profiles = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadGames();
    }

    private void LoadGames()
    {
        // Same shared core the WPF frontend uses — no WPF involved.
        JoystickHelper.DeSerialize();
        GameProfileLoader.LoadProfiles(false);
        _profiles = GameProfileLoader.GameProfiles
            .OrderBy(p => p.GameNameInternal ?? p.ProfileName)
            .ToList();
        UpdateList();
    }

    private void UpdateList()
    {
        var search = SearchBox.Text;
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _profiles
            : _profiles.Where(p => (p.GameNameInternal ?? p.ProfileName ?? string.Empty)
                .Contains(search, System.StringComparison.OrdinalIgnoreCase)).ToList();

        GameList.ItemsSource = filtered.Select(p => p.GameNameInternal ?? p.ProfileName).ToList();
        CountText.Text = $"{filtered.Count} of {_profiles.Count} game profiles — loaded via TeknoParrotUi.Common on Avalonia";
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateList();
    }
}