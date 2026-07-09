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

    /// <summary>Live preview of the accessibility text-size zoom (persisted on Save).</summary>
    public event Action<double>? TextSizePreview;

    public UiOptionsView()
    {
        InitializeComponent();
        _capture.BindingCaptured += captured => Dispatcher.UIThread.Post(() => OnCaptured(captured));
        BuildRows();
        Localize();
        Services.Loc.LanguageChanged += Localize;
        Unloaded += (_, _) =>
        {
            _capture.Stop();
            // Leaving the page without Save: revert live previews (theme and
            // text size) to the last persisted values.
            var saved = UiOptions.Load();
            ThemeManager.Apply(saved.Theme);
            TextSizePreview?.Invoke(saved.UiScale);
        };
    }

    private readonly Dictionary<UiNavAction, TextBlock> _actionLabels = new();

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("UiOptionsTitle", "UI Options");
        HdrAppearance.Text = Services.Loc.T("UiOptionsAppearance", "Appearance");
        ThemeLabel.Text = Services.Loc.T("UiOptionsTheme", "Theme");
        ThemeHint.Text = Services.Loc.T("UiOptionsThemeHint", "System follows your OS light/dark preference. Changes apply instantly.");
        LocalizeThemeItems();
        TextSizeLabel.Text = Services.Loc.T("UiOptionsTextSize", "Text size");
        TextSizeHint.Text = Services.Loc.T("UiOptionsTextSizeHint", "Makes all text and controls bigger for better readability.");
        LocalizeScaleItems();
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
        SelectThemeItem(_options.Theme);
        SelectScaleItem(_options.UiScale);
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
        _options.Theme = SelectedTheme();
        _options.UiScale = SelectedScale();
        _options.Save();
        StatusText.Text = "Saved.";
        Saved?.Invoke(_options);
    }

    // ── Theme picker ────────────────────────────────────────────
    // Items carry the stable value in Tag; the display text is localized.
    private static readonly (string Value, string Key, string Fallback)[] ThemeChoices =
    {
        (ThemeManager.System, "UiOptionsThemeSystem", "System"),
        (ThemeManager.Light, "UiOptionsThemeLight", "Light"),
        (ThemeManager.Dark, "UiOptionsThemeDark", "Dark"),
    };

    private void LocalizeThemeItems()
    {
        var selected = CmbTheme.SelectedIndex;
        CmbTheme.Items.Clear();
        foreach (var (value, key, fallback) in ThemeChoices)
            CmbTheme.Items.Add(new ComboBoxItem { Content = Services.Loc.T(key, fallback), Tag = value });
        CmbTheme.SelectedIndex = selected >= 0 ? selected : 0;
    }

    private void SelectThemeItem(string? theme)
    {
        for (var i = 0; i < ThemeChoices.Length; i++)
        {
            if (ThemeChoices[i].Value == theme)
            {
                CmbTheme.SelectedIndex = i;
                return;
            }
        }
        CmbTheme.SelectedIndex = 0;
    }

    private string SelectedTheme() =>
        (CmbTheme.SelectedItem as ComboBoxItem)?.Tag as string ?? ThemeManager.System;

    private void CmbTheme_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Live preview — persisted only on Save
        if (IsLoaded)
            ThemeManager.Apply(SelectedTheme());
    }

    // ── Accessibility text-size zoom ────────────────────────────────────────
    private static readonly (double Value, string Key, string Fallback)[] ScaleChoices =
    {
        (1.0, "UiOptionsTextSize100", "Default (100%)"),
        (1.15, "UiOptionsTextSize115", "Large (115%)"),
        (1.3, "UiOptionsTextSize130", "Larger (130%)"),
        (1.5, "UiOptionsTextSize150", "Largest (150%)"),
    };

    private void LocalizeScaleItems()
    {
        var selected = CmbTextSize.SelectedIndex;
        CmbTextSize.Items.Clear();
        foreach (var (value, key, fallback) in ScaleChoices)
            CmbTextSize.Items.Add(new ComboBoxItem { Content = Services.Loc.T(key, fallback), Tag = value });
        CmbTextSize.SelectedIndex = selected >= 0 ? selected : 0;
    }

    private void SelectScaleItem(double scale)
    {
        for (var i = 0; i < ScaleChoices.Length; i++)
        {
            if (Math.Abs(ScaleChoices[i].Value - scale) < 0.01)
            {
                CmbTextSize.SelectedIndex = i;
                return;
            }
        }
        CmbTextSize.SelectedIndex = 0;
    }

    private double SelectedScale() =>
        (CmbTextSize.SelectedItem as ComboBoxItem)?.Tag as double? ?? 1.0;

    private void CmbTextSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Live preview — persisted only on Save
        if (IsLoaded)
            TextSizePreview?.Invoke(SelectedScale());
    }
}
