using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia;

public partial class MainWindow : Window
{
    private List<GameProfile> _profiles = new();
    private List<GameProfile> _filtered = new();

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
        _filtered = string.IsNullOrWhiteSpace(search)
            ? _profiles
            : _profiles.Where(p => (p.GameNameInternal ?? p.ProfileName ?? string.Empty)
                .Contains(search, System.StringComparison.OrdinalIgnoreCase)).ToList();

        GameList.ItemsSource = _filtered.Select(p => p.GameNameInternal ?? p.ProfileName).ToList();
        CountText.Text = $"{_filtered.Count} of {_profiles.Count} game profiles — double-click to launch";
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateList();
    }

    private void GameList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (GameList.SelectedIndex < 0 || GameList.SelectedIndex >= _filtered.Count)
            return;

        var profile = _filtered[GameList.SelectedIndex];
        var launcher = FindLauncher();
        if (launcher == null)
        {
            CountText.Text = "TeknoParrotUi.exe not found — cannot launch (native Avalonia launch coming later)";
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher)!,
            Arguments = $"--profile={Path.GetFileName(profile.FileName)}",
            UseShellExecute = false
        };
        Process.Start(psi);
        CountText.Text = $"Launched {profile.GameNameInternal ?? profile.ProfileName} via TeknoParrotUi";
    }

    /// <summary>
    /// Finds TeknoParrotUi.exe: next to this executable (deployed layout),
    /// or in the repository dev output folder.
    /// </summary>
    private static string? FindLauncher()
    {
        var candidates = new[]
        {
            Path.Combine(System.AppContext.BaseDirectory, "TeknoParrotUi.exe"),
            Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\bin\x86\Debug\TeknoParrotUi.exe")),
            Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\bin\x86\Release\TeknoParrotUi.exe")),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}