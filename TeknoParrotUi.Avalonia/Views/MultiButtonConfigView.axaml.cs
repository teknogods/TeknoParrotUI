using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Bind buttons once, apply to many games — the Avalonia take on the classic
/// MultiGameButtonConfig. Buttons are matched across games by ButtonName; all
/// input APIs are captured simultaneously so the bindings apply regardless of
/// each target game's configured Input API.
/// </summary>
public partial class MultiButtonConfigView : UserControl
{
    private readonly List<(CheckBox box, GameProfile profile)> _games = new();
    private readonly Dictionary<string, JoystickButtons> _master = new();
    private readonly Dictionary<string, Button> _bindButtons = new();
    private readonly InputCaptureService _capture = new();
    private readonly RawInputCaptureService _rawCapture = new();
    private string? _armedButtonName;

    public event Action? BackRequested;
    public event Action<int>? Applied;

    public MultiButtonConfigView()
    {
        InitializeComponent();
        _capture.BindingCaptured += captured => Dispatcher.UIThread.Post(() => OnCaptured(captured, null, false));
        _rawCapture.BindingCaptured += (name, button, isEscape) =>
            Dispatcher.UIThread.Post(() => OnCaptured(new CapturedBinding(name, null, null), button, isEscape));
        Unloaded += (_, _) => StopCapture();
    }

    public void Refresh()
    {
        GameProfileLoader.LoadProfiles(false);
        _games.Clear();
        GamesPanel.Children.Clear();

        foreach (var profile in GameProfileLoader.UserProfiles.OrderBy(p => p.GameNameInternal ?? p.ProfileName))
        {
            var box = new CheckBox { Content = profile.GameNameInternal ?? profile.ProfileName, FontSize = 12 };
            box.IsCheckedChanged += (_, _) => RebuildButtonRows();
            _games.Add((box, profile));
            GamesPanel.Children.Add(box);
        }

        _master.Clear();
        RebuildButtonRows();
        StartCapture();
    }

    private void StartCapture()
    {
        // Capture on every API so bindings suit any target game's Input API
        _capture.Start(InputApi.MergedInput);
        if (OperatingSystem.IsWindows())
            _rawCapture.Start(registerKeyboard: true);
    }

    private void StopCapture()
    {
        _capture.Stop();
        _rawCapture.Stop();
    }

    private List<GameProfile> SelectedGames =>
        _games.Where(g => g.box.IsChecked == true).Select(g => g.profile).ToList();

    private void RebuildButtonRows()
    {
        ButtonsPanel.Children.Clear();
        _bindButtons.Clear();
        var selected = SelectedGames;
        ButtonsHeader.Text = selected.Count == 0 ? "Buttons — select target games first" : $"Buttons ({selected.Count} game(s) selected)";

        // Union of button names across selected games, in first-seen order
        var names = new List<string>();
        foreach (var game in selected)
        {
            foreach (var button in game.JoystickButtons)
            {
                if (!string.IsNullOrWhiteSpace(button.ButtonName) && !names.Contains(button.ButtonName))
                    names.Add(button.ButtonName);
            }
        }

        foreach (var name in names)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*,Auto"), Margin = new global::Avalonia.Thickness(0, 1, 0, 1) };
            var label = new TextBlock { Text = name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };

            var usedBy = SelectedGames.Count(g => g.JoystickButtons.Any(b => b.ButtonName == name));
            ToolTip.SetTip(label, $"Used by {usedBy} of {SelectedGames.Count} selected game(s)");

            var bind = new Button
            {
                Content = _master.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing.BindName) ? existing.BindName : "(not bound)",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            bind.Click += (_, _) => Arm(name);
            _bindButtons[name] = bind;

            var clear = new Button { Content = "✕", FontSize = 12, Margin = new global::Avalonia.Thickness(4, 0, 0, 0) };
            clear.Click += (_, _) =>
            {
                _master.Remove(name);
                bind.Content = "(not bound)";
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(bind, 1);
            Grid.SetColumn(clear, 2);
            grid.Children.Add(label);
            grid.Children.Add(bind);
            grid.Children.Add(clear);
            ButtonsPanel.Children.Add(grid);
        }
    }

    private void Arm(string buttonName)
    {
        if (_armedButtonName != null && _bindButtons.TryGetValue(_armedButtonName, out var previous))
            previous.Content = _master.TryGetValue(_armedButtonName, out var m) && !string.IsNullOrEmpty(m.BindName) ? m.BindName : "(not bound)";

        _armedButtonName = buttonName;
        _bindButtons[buttonName].Content = "Press a button / key / axis...";
    }

    private void OnCaptured(CapturedBinding captured, RawInputButton? rawButton, bool isEscape)
    {
        if (_armedButtonName == null || !_bindButtons.TryGetValue(_armedButtonName, out var bindButton))
            return;

        if (isEscape)
        {
            bindButton.Content = _master.TryGetValue(_armedButtonName, out var m) && !string.IsNullOrEmpty(m.BindName) ? m.BindName : "(not bound)";
            _armedButtonName = null;
            return;
        }

        if (!_master.TryGetValue(_armedButtonName, out var master))
        {
            master = new JoystickButtons { ButtonName = _armedButtonName };
            _master[_armedButtonName] = master;
        }

        if (captured.XInput != null)
        {
            master.XInputButton = captured.XInput;
            master.BindNameXi = captured.DisplayName;
        }
        if (captured.DirectInput != null)
        {
            master.DirectInputButton = captured.DirectInput;
            master.BindNameDi = captured.DisplayName;
        }
        if (rawButton != null)
        {
            master.RawInputButton = rawButton;
            master.BindNameRi = captured.DisplayName;
        }
        master.BindName = captured.DisplayName;

        bindButton.Content = captured.DisplayName;
        _armedButtonName = null;
    }

    private void BtnSelectAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (var (box, _) in _games) box.IsChecked = true;
    }

    private void BtnSelectNone_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (var (box, _) in _games) box.IsChecked = false;
    }

    private void BtnApply_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targets = SelectedGames;
        if (targets.Count == 0 || _master.Count == 0)
        {
            StatusText.Text = "Select target games and bind at least one button first.";
            return;
        }

        int updated = 0;
        foreach (var game in targets)
        {
            bool changed = false;
            foreach (var button in game.JoystickButtons)
            {
                if (string.IsNullOrWhiteSpace(button.ButtonName) || !_master.TryGetValue(button.ButtonName, out var master))
                    continue;

                if (master.XInputButton != null) { button.XInputButton = master.XInputButton; button.BindNameXi = master.BindNameXi; }
                if (master.DirectInputButton != null) { button.DirectInputButton = master.DirectInputButton; button.BindNameDi = master.BindNameDi; }
                if (master.RawInputButton != null) { button.RawInputButton = master.RawInputButton; button.BindNameRi = master.BindNameRi; }
                button.BindName = master.BindName;
                changed = true;
            }
            if (changed)
            {
                JoystickHelper.SerializeGameProfile(game);
                updated++;
            }
        }

        StatusText.Text = $"Applied bindings to {updated} game(s).";
        Applied?.Invoke(updated);
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopCapture();
        BackRequested?.Invoke();
    }
}
