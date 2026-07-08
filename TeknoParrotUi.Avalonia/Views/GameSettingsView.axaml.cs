using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Avalonia.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameSettingsView : UserControl
{
    private GameProfile? _profile;
    private readonly Dictionary<FieldInformation, Func<string>> _valueReaders = new();
    private readonly Dictionary<FieldInformation, string> _baseline = new();
    private string _baselinePath = "";
    private string _baselinePath2 = "";
    private TextBox? _gamePathBox;
    private TextBox? _gamePath2Box;

    public event Action? BackRequested;
    public event Action<string>? Saved;

    public GameSettingsView()
    {
        InitializeComponent();
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void Localize()
    {
        BtnBack.Content = Services.Loc.T("Back", "Back");
        BtnSave.Content = Services.Loc.T("SettingsSaveSettings", "Save Settings");
    }

    public void LoadProfile(GameProfile profile)
    {
        _profile = profile;
        Header.Text = $"{profile.GameNameInternal ?? profile.ProfileName} — Settings";
        _valueReaders.Clear();
        FieldsPanel.Children.Clear();

        AddCategoryHeader(Services.Loc.T("GameSettingsGameExecutableLabel", "Game Executable"));
        _gamePathBox = AddPathRow(BuildExecutableLabel("GameSettingsGameExecutableLabel", "Game Executable", profile.ExecutableName),
            profile.GamePath, profile.ExecutableName);
        if (profile.HasTwoExecutables)
            _gamePath2Box = AddPathRow(BuildExecutableLabel("GameSettingsSecondGameExecutableLabel", "Second Game Executable", profile.ExecutableName2),
                profile.GamePath2, profile.ExecutableName2);

        foreach (var category in profile.ConfigValues.Select(c => c.CategoryName).Distinct())
        {
            AddCategoryHeader(category);
            foreach (var field in profile.ConfigValues.Where(c => c.CategoryName == category))
                AddFieldEditor(field);
        }

        // Baseline for unsaved-change detection (editor values normalize e.g. "" -> "0",
        // so compare against the editors' initial output rather than raw FieldValues)
        _baseline.Clear();
        foreach (var (field, read) in _valueReaders)
            _baseline[field] = read() ?? "";
        _baselinePath = _gamePathBox?.Text ?? "";
        _baselinePath2 = _gamePath2Box?.Text ?? "";
    }

    private void AddCategoryHeader(string text)
    {
        FieldsPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = global::Avalonia.Media.FontWeight.Bold,
            Margin = new global::Avalonia.Thickness(0, 12, 0, 4)
        });
    }

    /// <summary>
    /// "Game Executable (GameProject-Win64-Shipping.exe)" — shows the expected file
    /// name(s) from the profile, matching the classic UI (';'/'|' = alternatives).
    /// </summary>
    private static string BuildExecutableLabel(string key, string fallback, string? executableName)
    {
        var label = Services.Loc.T(key, fallback);
        if (string.IsNullOrEmpty(executableName))
            return label;
        var pretty = executableName.Replace("|", " or ").Replace(";", " or ");
        return $"{label} ({pretty})";
    }

    private TextBox AddPathRow(string label, string? value, string? executableName = null)
    {
        var box = new TextBox { Text = value ?? "", MinWidth = 400 };
        var browse = new Button { Content = "Browse..." };
        browse.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            // Filter to the exact executable(s) the profile expects (classic behaviour);
            // profiles separate alternatives with '|' or ';'
            var filters = new List<FilePickerFileType>();
            if (!string.IsNullOrEmpty(executableName))
            {
                var names = executableName.Split('|', ';')
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .ToArray();
                if (names.Length > 0)
                    filters.Add(new FilePickerFileType($"{Services.Loc.T("GameSettingsGameExecutableFilter", "Game executable")} ({string.Join(", ", names)})")
                    {
                        Patterns = names
                    });
            }
            filters.Add(new FilePickerFileType(Services.Loc.T("GameSettingsAllFiles", "All Files")) { Patterns = new[] { "*.*" } });

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"{Services.Loc.T("GameSettingsSelectGameExecutable", "Select Game Executable")} — {label}",
                AllowMultiple = false,
                FileTypeFilter = filters
            });
            if (files.Count > 0)
                box.Text = files[0].TryGetLocalPath() ?? box.Text;
        };
        FieldsPanel.Children.Add(Row(label, new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { box, browse }
        }));
        return box;
    }

    private void AddFieldEditor(FieldInformation field)
    {
        Control editor;
        switch (field.FieldType)
        {
            case FieldType.Bool:
                var cb = new CheckBox { IsChecked = field.FieldValue == "1" };
                _valueReaders[field] = () => cb.IsChecked == true ? "1" : "0";
                editor = cb;
                break;

            case FieldType.Dropdown:
            case FieldType.DropdownIndex:
                var options = field.FieldOptions ?? new List<string>();
                var selected = field.FieldValue;
                if (field.FieldName == "Input API")
                {
                    // Platform-aware Input API choices: SDL2 is available everywhere;
                    // DirectInput/XInput are Windows-only (elsewhere SDL2 serves gamepad
                    // input and RawInput/Trackball selections map to SDL2 + evdev).
                    options = new List<string>(options);
                    if (!options.Contains("SDL2"))
                        options.Add("SDL2");
                    if (!OperatingSystem.IsWindows())
                    {
                        options.RemoveAll(o => o is "DirectInput" or "XInput" or "MergedInput");
                        if (selected is "DirectInput" or "XInput" or "MergedInput")
                            selected = "SDL2";
                    }
                }
                var combo = new ComboBox
                {
                    ItemsSource = options,
                    SelectedItem = selected,
                    MinWidth = 220
                };
                if (combo.SelectedItem == null && options.Count > 0)
                    combo.SelectedIndex = 0;
                _valueReaders[field] = () => combo.SelectedItem as string ?? field.FieldValue;
                editor = combo;
                break;

            case FieldType.Slider:
                var slider = new Slider
                {
                    Minimum = field.FieldMin,
                    Maximum = field.FieldMax,
                    Width = 220,
                    Value = double.TryParse(field.FieldValue, out var v) ? v : field.FieldMin
                };
                if (field.FieldStep > 0)
                {
                    slider.TickFrequency = field.FieldStep;
                    slider.IsSnapToTickEnabled = true;
                }
                var valueLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = field.FieldValue };
                slider.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Slider.ValueProperty)
                        valueLabel.Text = ((int)slider.Value).ToString();
                };
                _valueReaders[field] = () => ((int)slider.Value).ToString();
                editor = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { slider, valueLabel } };
                break;

            case FieldType.Numeric:
                var num = new NumericUpDown
                {
                    Minimum = field.FieldMin,
                    Maximum = field.FieldMax == 0 ? decimal.MaxValue : field.FieldMax,
                    Value = decimal.TryParse(field.FieldValue, out var nv) ? nv : 0,
                    MinWidth = 140
                };
                _valueReaders[field] = () => ((long)(num.Value ?? 0)).ToString();
                editor = num;
                break;

            case FieldType.KeyCapture:
                var keyBox = new KeyCaptureBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
                keyBox.HexValue = field.FieldValue ?? "0x0";
                _valueReaders[field] = () => keyBox.HexValue;
                editor = keyBox;
                break;

            case FieldType.MonitorSelection:
                var monitorCombo = new ComboBox { MinWidth = 220 };
                var screens = (TopLevel.GetTopLevel(this) as Window)?.Screens.All;
                var items = new List<string>();
                if (screens != null)
                {
                    for (int i = 0; i < screens.Count; i++)
                        items.Add($"Monitor {i + 1} ({screens[i].Bounds.Width}x{screens[i].Bounds.Height}{(screens[i].IsPrimary ? ", primary" : "")})");
                }
                if (items.Count == 0)
                    items.Add("Monitor 1");
                monitorCombo.ItemsSource = items;
                monitorCombo.SelectedIndex = int.TryParse(field.FieldValue, out var mi) && mi >= 0 && mi < items.Count ? mi : 0;
                _valueReaders[field] = () => monitorCombo.SelectedIndex.ToString();
                editor = monitorCombo;
                break;

            default:
                var tb = new TextBox { Text = field.FieldValue ?? "", MinWidth = 220 };
                _valueReaders[field] = () => tb.Text ?? "";
                editor = tb;
                break;
        }

        if (!string.IsNullOrWhiteSpace(field.Hint))
            ToolTip.SetTip(editor, field.Hint);

        FieldsPanel.Children.Add(Row(field.FieldName, editor));
    }

    private static Control Row(string label, Control editor)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("240,*"),
            Margin = new global::Avalonia.Thickness(0, 2, 0, 2)
        };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(text);
        grid.Children.Add(editor);
        return grid;
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => HandleBack();

    private async void HandleBack()
    {
        // Don't silently discard changes (e.g. a switched Input API) — losing an
        // unsaved API change makes freshly-bound controls dead in-game.
        if (HasUnsavedChanges() && TopLevel.GetTopLevel(this) is Window owner)
        {
            var result = await Services.Dialogs.ConfirmCancelAsync(owner,
                Services.Loc.T("UnsavedChanges", "Unsaved Changes"),
                Services.Loc.T("GameSettingsUnsavedPrompt", "You have unsaved settings changes. Save them before leaving?"));
            if (result == null)
                return; // cancel: stay on the settings page
            if (result == true)
            {
                SaveProfile();
                return; // SaveProfile already navigates back
            }
        }
        BackRequested?.Invoke();
    }

    private bool HasUnsavedChanges()
    {
        if (_profile == null)
            return false;
        if (_gamePathBox != null && (_gamePathBox.Text ?? "") != _baselinePath)
            return true;
        if (_gamePath2Box != null && (_gamePath2Box.Text ?? "") != _baselinePath2)
            return true;
        foreach (var (field, read) in _valueReaders)
        {
            if (_baseline.TryGetValue(field, out var original) && (read() ?? "") != original)
                return true;
        }
        return false;
    }

    private void BtnSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SaveProfile();

    private void SaveProfile()
    {
        if (_profile == null) return;

        _profile.GamePath = _gamePathBox?.Text ?? _profile.GamePath;
        if (_gamePath2Box != null)
            _profile.GamePath2 = _gamePath2Box.Text ?? _profile.GamePath2;

        foreach (var (field, read) in _valueReaders)
            field.FieldValue = read();

        Directory.CreateDirectory("UserProfiles");
        JoystickHelper.SerializeGameProfile(_profile);
        Saved?.Invoke(_profile.GameNameInternal ?? _profile.ProfileName ?? "profile");
        BackRequested?.Invoke();
    }
}
