using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Options menu for the Avalonia UI itself: fullscreen behaviour and
/// player-configurable merged-input navigation bindings.
/// </summary>
public partial class UiOptionsView : UserControl
{
    private static readonly (UiNavAction Action, string Label)[] Actions =
    {
        (UiNavAction.Up, "Move up"),
        (UiNavAction.Down, "Move down"),
        (UiNavAction.Left, "Move left"),
        (UiNavAction.Right, "Move right"),
        (UiNavAction.Confirm, "Confirm / activate"),
        (UiNavAction.Back, "Back to library"),
        (UiNavAction.ToggleFullscreen, "Toggle fullscreen"),
    };

    private readonly InputCaptureService _capture = new();
    private readonly Dictionary<UiNavAction, Button> _bindButtons = new();
    private UiOptions _options = new();
    private UiNavAction? _armed;

    public event Action<UiOptions>? Saved;

    public UiOptionsView()
    {
        InitializeComponent();
        _capture.BindingCaptured += captured => Dispatcher.UIThread.Post(() => OnCaptured(captured));
        BuildRows();
        Unloaded += (_, _) => _capture.Stop();
    }

    public void Refresh()
    {
        _options = UiOptions.Load();
        ChkStartFullscreen.IsChecked = _options.StartFullscreen;
        ChkEnableNav.IsChecked = _options.EnableControllerNavigation;
        _armed = null;
        foreach (var (action, _) in Actions)
            UpdateButtonText(action);
        StatusText.Text = "";
        _capture.Start(InputApi.MergedInput);
    }

    private void BuildRows()
    {
        foreach (var (action, label) in Actions)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*,Auto") };
            var text = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

            var bind = new Button { Content = "(not bound)", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            bind.Click += (_, _) => Arm(action);
            _bindButtons[action] = bind;

            var clear = new Button { Content = "✕", FontSize = 12, Margin = new global::Avalonia.Thickness(4, 0, 0, 0) };
            clear.Click += (_, _) =>
            {
                _options.NavigationBindings.Remove(action.ToString());
                if (_armed == action) _armed = null;
                UpdateButtonText(action);
            };

            Grid.SetColumn(text, 0);
            Grid.SetColumn(bind, 1);
            Grid.SetColumn(clear, 2);
            grid.Children.Add(text);
            grid.Children.Add(bind);
            grid.Children.Add(clear);
            BindingsPanel.Children.Add(grid);
        }
    }

    private void Arm(UiNavAction action)
    {
        if (_armed is { } previous)
            UpdateButtonText(previous);
        _armed = action;
        _bindButtons[action].Content = "Press a button / key / axis...";
    }

    private void OnCaptured(CapturedBinding captured)
    {
        if (_armed is not { } action)
            return;
        _options.NavigationBindings[action.ToString()] = captured.DisplayName;
        _armed = null;
        UpdateButtonText(action);
    }

    private void UpdateButtonText(UiNavAction action)
    {
        _bindButtons[action].Content =
            _options.NavigationBindings.TryGetValue(action.ToString(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "(not bound)";
    }

    private void BtnSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _options.StartFullscreen = ChkStartFullscreen.IsChecked == true;
        _options.EnableControllerNavigation = ChkEnableNav.IsChecked == true;
        _options.Save();
        StatusText.Text = "Saved.";
        Saved?.Invoke(_options);
    }
}
