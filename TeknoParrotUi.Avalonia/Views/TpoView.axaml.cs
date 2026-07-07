using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.AvailCode;

namespace TeknoParrotUi.Avalonia.Views;

public partial class TpoView : UserControl
{
    private const string LobbyServer = "http://104.244.72.41:19125";
    private readonly LobbyClient _client = new(LobbyServer);
    private List<LobbyData> _lobbies = new();
    private bool _loaded;

    public TpoView()
    {
        InitializeComponent();
        GameBox.ItemsSource = Enum.GetNames(typeof(GameId)).ToList();
        GameBox.SelectedIndex = 0;
        Loaded += async (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                await RefreshLobbies();
            }
        };
    }

    private GameId SelectedGame =>
        Enum.TryParse<GameId>(GameBox.SelectedItem as string, out var id) ? id : GameId.Any;

    private async System.Threading.Tasks.Task RefreshLobbies()
    {
        StatusText.Text = "Loading lobbies...";
        try
        {
            _lobbies = await _client.GetLobbies(SelectedGame) ?? new List<LobbyData>();
        }
        catch (Exception ex)
        {
            _lobbies = new List<LobbyData>();
            StatusText.Text = $"Lobby server unreachable: {ex.Message}";
            LobbyList.ItemsSource = new List<string>();
            return;
        }

        LobbyList.ItemsSource = _lobbies
            .Select(l => $"{l.Name}  —  {l.GameId}  ({l.MemberCount}/{l.MaxMemberCount} players)")
            .ToList();
        StatusText.Text = _lobbies.Count == 0
            ? "No open lobbies. Create one or use Quick Join."
            : $"{_lobbies.Count} open lobby/lobbies. Double-click to join.";
    }

    private async void BtnRefresh_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await RefreshLobbies();

    private async void GameBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
            await RefreshLobbies();
    }

    private void LobbyList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LobbyList.SelectedIndex < 0 || LobbyList.SelectedIndex >= _lobbies.Count)
            return;
        var lobby = _lobbies[LobbyList.SelectedIndex];
        if (!GameLauncherService.LaunchTpo(lobby.GameId.ToString(), "join", lobby.Name))
        {
            StatusText.Text = "TeknoParrotUi.exe not found — cannot join.";
            return;
        }
        StatusText.Text = $"Joining {lobby.Name}...";
    }

    private void BtnQuickJoin_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedGame == GameId.Any)
        {
            StatusText.Text = "Select a specific game for Quick Join.";
            return;
        }
        if (!GameLauncherService.LaunchTpo(SelectedGame.ToString(), "quick-join"))
        {
            StatusText.Text = "TeknoParrotUi.exe not found — cannot join.";
            return;
        }
        StatusText.Text = $"Quick-joining {SelectedGame}...";
    }

    private void BtnCreate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedGame == GameId.Any)
        {
            StatusText.Text = "Select a specific game to create a room.";
            return;
        }
        var room = RoomNameBox.Text;
        if (string.IsNullOrWhiteSpace(room))
        {
            StatusText.Text = "Enter a room name.";
            return;
        }
        if (!GameLauncherService.LaunchTpo(SelectedGame.ToString(), "create", room, PasswordBox.Text))
        {
            StatusText.Text = "TeknoParrotUi.exe not found — cannot create room.";
            return;
        }
        StatusText.Text = $"Creating room {room}...";
    }
}
