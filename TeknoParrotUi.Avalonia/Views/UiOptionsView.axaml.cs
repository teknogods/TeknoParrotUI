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
    private static readonly (UiNavAction Action, string Key, string Fallback)[] Actions =
    {
        (UiNavAction.Up, "UiOptionsMoveUp", "Move up"),
        (UiNavAction.Down, "UiOptionsMoveDown", "Move down"),
        (UiNavAction.Left, "UiOptionsMoveLeft", "Move left"),
        (UiNavAction.Right, "UiOptionsMoveRight", "Move right"),
        (UiNavAction.Confirm, "UiOptionsConfirm", "Confirm / activate"),
        (UiNavAction.Back, "UiOptionsBackToLibrary", "Back to library"),
        (UiNavAction.ToggleFullscreen, "UiOptionsToggleFullscreen", "Toggle fullscreen"),
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
        Localize();
        Services.Loc.LanguageChanged += Localize;
        Unloaded += (_, _) => _capture.Stop();
    }

    private readonly Dictionary<UiNavAction, TextBlock> _actionLabels = new();

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("UiOptionsTitle", "UI Options");
        HdrDisplay.Text = Services.Loc.T("UiOptionsDisplay", "Display");
        ChkStartFullscreen.Content = Services.Loc.T("UiOptionsStartFullscreen", "Start in fullscreen");
        FullscreenHint.Text = Services.Loc.T("UiOptionsFullscreenHint", "Press F11 (or Alt+Enter) at any time to toggle fullscreen.");
        HdrNav.Text = Services.Loc.T("UiOptionsControllerNav", "Controller navigation");
        NavHint.Text = Services.Loc.T("UiOptionsControllerNavHint", "Drive the interface with any gamepad, joystick or keyboard (merged input) — ideal for arcade cabinets. Bind the actions below, enable navigation and save.");
        ChkEnableNav.Content = Services.Loc.T("UiOptionsEnableNav", "Enable controller navigation");
        BtnSave.Content = Services.Loc.T("SettingsSaveSettings", "Save");
        foreach (var (action, key, fallback) in Actions)
        {
            if (_actionLabels.TryGetValue(action, out var label))
                label.Text = Services.Loc.T(key, fallback);
            UpdateButtonText(action);
        }
    }

    public void Refresh()
    {
        _options = UiOptions.Load();
        ChkStartFullscreen.IsChecked = _options.StartFullscreen;
        ChkEnableNav.IsChecked = _options.EnableControllerNavigation;
        _armed = null;
        foreach (var (action, _, _) in Actions)
            UpdateButtonText(action);
        StatusText.Text = "";
        _capture.Start(InputApi.MergedInput);
    }

    private void BuildRows()
    {
        foreach (var (action, key, fallback) in Actions)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*,Auto") };
            var text = new TextBlock { Text = Services.Loc.T(key, fallback), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            _actionLabels[action] = text;

            var bind = new Button { Content = Services.Loc.T("NotBound", "(not bound)"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
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
        _bindButtons[action].Content = Services.Loc.T("PressButtonKeyAxis", "Press a button / key / axis...");
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
                : Services.Loc.T("NotBound", "(not bound)");
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
