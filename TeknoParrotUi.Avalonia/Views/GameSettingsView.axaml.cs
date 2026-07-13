using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using TeknoParrotUi.Avalonia.Controls;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Proton;

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
    private ComboBox? _wineRunnerCombo;
    private TextBox? _wineRunnerPathBox;
    private string _baselineWineRunner = "";
    private string _baselineWineRunnerPath = "";
    private const string WineRunnerNotInstalledSuffix = " (not installed)";
    private ComboBox? _prefixModeCombo;
    private TextBlock? _prefixInfoBlock;
    private TextBlock? _prefixExplainBlock;
    private string _baselinePrefixMode = "";
    private ComboBox? _fullscreenScalingCombo;
    private TextBlock? _fullscreenScalingInfoBlock;
    private string _baselineFullscreenScaling = "";

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

        _wineRunnerCombo = null;
        _wineRunnerPathBox = null;
        _prefixModeCombo = null;
        _fullscreenScalingCombo = null;
        if (OperatingSystem.IsLinux())
        {
            AddWineRunnerSection(profile);
            AddWinePrefixModeSection(profile);
            AddFullscreenScalingSection(profile);
        }

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
        _baselineWineRunner = _wineRunnerCombo?.SelectedItem as string ?? "";
        _baselineWineRunnerPath = _wineRunnerPathBox?.Text ?? "";
        _baselinePrefixMode = _prefixModeCombo?.SelectedItem as string ?? "";
        _baselineFullscreenScaling = _fullscreenScalingCombo?.SelectedItem as string ?? "";
    }

    /// <summary>
    /// Per-game Gamescope fullscreen-scaling override - a compatibility
    /// fallback switch only (no game resolution fields anywhere). Backed by
    /// GameProfile.FullscreenScalingMode; shows the same policy/availability/
    /// display information GamescopeLauncher itself uses so a user can see
    /// exactly what would happen without launching the game.
    /// </summary>
    private void AddFullscreenScalingSection(GameProfile profile)
    {
        AddCategoryHeader(Services.Loc.T("GameSettingsFullscreenScalingHeader", "Fullscreen game scaling (Linux)"));

        var options = new List<string> { "Use global default", "Automatic fullscreen fit", "Disabled" };
        _fullscreenScalingCombo = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = profile.FullscreenScalingMode switch
            {
                LinuxFullscreenScalingMode.AutomaticFit => 1,
                LinuxFullscreenScalingMode.Disabled => 2,
                _ => 0
            },
            MinWidth = 220
        };
        FieldsPanel.Children.Add(Row(Services.Loc.T("GameSettingsFullscreenScalingLabel", "Fullscreen game scaling"), _fullscreenScalingCombo));

        _fullscreenScalingInfoBlock = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontFamily = "monospace", FontSize = 11 };
        FieldsPanel.Children.Add(_fullscreenScalingInfoBlock);

        _fullscreenScalingCombo.SelectionChanged += (_, _) => UpdateFullscreenScalingPreview(profile);
        UpdateFullscreenScalingPreview(profile);
    }

    /// <summary>
    /// Resolves what the combo's CURRENT (possibly unsaved) selection would
    /// mean via the real GamescopeLaunchPolicy - never mutates
    /// <paramref name="profile"/>, so Cancel/Back still discards it.
    /// </summary>
    private void UpdateFullscreenScalingPreview(GameProfile profile)
    {
        if (_fullscreenScalingCombo == null || _fullscreenScalingInfoBlock == null)
            return;

        var gameMode = _fullscreenScalingCombo.SelectedIndex switch
        {
            1 => LinuxFullscreenScalingMode.AutomaticFit,
            2 => LinuxFullscreenScalingMode.Disabled,
            _ => LinuxFullscreenScalingMode.Default
        };
        var globalMode = Lazydata.ParrotData.FullscreenScalingMode ?? LinuxFullscreenScalingMode.Disabled;
        var isExternalEmulator = TeknoParrotUi.Common.GameLaunch.ExternalEmulatorLauncher.IsExternalEmulator(profile);
        var forced = GamescopeEnvironment.ForceGamescopeRequested;
        var noGamescope = GamescopeEnvironment.NoGamescopeRequested;

        var decision = GamescopeLaunchPolicy.Resolve(globalMode, gameMode, noGamescope, forced,
            isExternalEmulator, GamescopeEnvironment.IsAlreadyInsideGamescope(), GamescopeEnvironment.AllowNestedOverrideRequested);

        var display = LinuxDisplayResolver.Resolve();

        _fullscreenScalingInfoBlock.Text =
            $"Configured: {gameMode}    Global default: {globalMode}    Effective: {decision.EffectiveMode}\n" +
            $"External emulator profile: {isExternalEmulator}    Forced by environment: {decision.ForcedByEnvironment || noGamescope}\n" +
            $"Monitor resolution: {(display.IsValid ? $"{display.Width}x{display.Height} ({display.Source})" : "unresolved")}";
    }

    /// <summary>
    /// Per-game wine/Proton runner override (Linux only - ignored on Windows,
    /// where GameProfile.ProtonVersion/WineRunnerPath are simply never read).
    /// Backed directly by those two fields: the combo selects ProtonVersion
    /// (a packaged version name, "system" for plain system wine, or empty for
    /// the global default from the Linux Setup page); the path box, when
    /// non-empty, sets WineRunnerPath and takes priority over the combo.
    /// </summary>
    private void AddWineRunnerSection(GameProfile profile)
    {
        AddCategoryHeader(Services.Loc.T("GameSettingsWineRunnerHeader", "Wine Runner (Linux)"));

        var options = new List<string> { "Auto (default)", "System Wine" };
        options.AddRange(ProtonPackageManager.ListInstalledVersions());

        var current = profile.ProtonVersion;
        var isSystem = string.Equals(current, "system", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(current) && !isSystem && !options.Contains(current))
            options.Add(current + WineRunnerNotInstalledSuffix);

        _wineRunnerCombo = new ComboBox { ItemsSource = options, MinWidth = 260 };
        var selectedIndex = 0;
        if (isSystem)
            selectedIndex = 1;
        else if (!string.IsNullOrEmpty(current))
        {
            var idx = options.FindIndex(o => o == current || o == current + WineRunnerNotInstalledSuffix);
            if (idx >= 0)
                selectedIndex = idx;
        }
        _wineRunnerCombo.SelectedIndex = selectedIndex;
        FieldsPanel.Children.Add(Row(Services.Loc.T("GameSettingsWineRunnerLabel", "Proton/Wine version"), _wineRunnerCombo));

        _wineRunnerPathBox = new TextBox
        {
            Text = profile.WineRunnerPath ?? "",
            Watermark = "Leave empty unless this game needs a specific wine binary",
            MinWidth = 400
        };
        var browseWine = new Button { Content = "Browse..." };
        browseWine.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select wine/Proton binary",
                AllowMultiple = false
            });
            if (files.Count > 0)
                _wineRunnerPathBox.Text = files[0].TryGetLocalPath() ?? _wineRunnerPathBox.Text;
        };
        FieldsPanel.Children.Add(Row(Services.Loc.T("GameSettingsWineRunnerPathLabel", "Custom binary (overrides version above)"), new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _wineRunnerPathBox, browseWine }
        }));
    }

    /// <summary>
    /// Per-game Wine/Proton PREFIX (environment) override - shared vs isolated,
    /// independent of the runner BINARY chosen above. Backed by
    /// GameProfile.WinePrefixMode (nullable - see its docs for the legacy
    /// migration distinction). Shows a live preview of what the current combo
    /// selection would resolve to via WinePrefixManager, without saving.
    /// </summary>
    private void AddWinePrefixModeSection(GameProfile profile)
    {
        AddCategoryHeader(Services.Loc.T("GameSettingsWinePrefixModeHeader", "Wine Prefix (Environment)"));

        var options = new List<string> { "Use global default", "Shared prefix", "Isolated prefix" };
        _prefixModeCombo = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = profile.WinePrefixMode switch
            {
                WinePrefixMode.Shared => 1,
                WinePrefixMode.Isolated => 2,
                _ => 0
            },
            MinWidth = 220
        };
        FieldsPanel.Children.Add(Row(Services.Loc.T("GameSettingsWinePrefixModeLabel", "Wine prefix mode"), _prefixModeCombo));

        _prefixInfoBlock = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontFamily = "monospace", FontSize = 11 };
        FieldsPanel.Children.Add(_prefixInfoBlock);

        _prefixExplainBlock = new TextBlock
        {
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = global::Avalonia.Media.Brushes.Gray,
            Margin = new global::Avalonia.Thickness(0, 2, 0, 8)
        };
        FieldsPanel.Children.Add(_prefixExplainBlock);

        var resetButton = new Button { Content = Services.Loc.T("GameSettingsResetIsolatedPrefix", "Reset This Game's Isolated Prefix") };
        resetButton.Click += async (_, _) => await ResetIsolatedPrefixAsync(profile);
        FieldsPanel.Children.Add(resetButton);

        _prefixModeCombo.SelectionChanged += (_, _) => UpdateWinePrefixPreview(profile);
        UpdateWinePrefixPreview(profile);
    }

    /// <summary>
    /// Resolves what the combo's CURRENT (possibly unsaved) selection would
    /// mean, via the profile-agnostic WinePrefixManager.Resolve overload - does
    /// NOT mutate <paramref name="profile"/>, so Cancel/Back still discards it.
    /// </summary>
    private void UpdateWinePrefixPreview(GameProfile profile)
    {
        if (_prefixModeCombo == null || _prefixInfoBlock == null || _prefixExplainBlock == null)
            return;

        WinePrefixMode? previewMode = _prefixModeCombo.SelectedIndex switch
        {
            1 => WinePrefixMode.Shared,
            2 => WinePrefixMode.Isolated,
            _ => WinePrefixMode.Default
        };

        var wine = ProtonLauncher.ResolveWineBinary(profile);
        var runnerKind = wine != null && ProtonLauncher.FindProtonScript(wine) != null
            ? WineRunnerKind.Proton
            : WineRunnerKind.PlainWine;
        var group = TeknoParrotUi.Common.GameLaunch.GameLaunchArguments.RequiresJapaneseLocale(profile)
            ? WinePrefixCompatibilityGroup.Japanese
            : WinePrefixCompatibilityGroup.Standard;

        var env = WinePrefixManager.Resolve(WinePrefixManager.ProfileIdentifier(profile), previewMode, group, runnerKind);

        var pathLines = runnerKind == WineRunnerKind.PlainWine
            ? $"WINEPREFIX: {env.WinePrefixPath}"
            : $"Compat-data path: {env.SteamCompatDataPath}\nActual Wine prefix: {env.ActualPrefixPath}";

        _prefixInfoBlock.Text =
            $"Configured: {env.ConfiguredMode}    Effective: {env.EffectiveMode}{(env.MigratedFromLegacyIsolated ? " (existing isolated prefix kept)" : "")}\n" +
            $"Runner: {runnerKind}    Compatibility group: {env.CompatibilityGroup}\n" +
            pathLines;

        _prefixExplainBlock.Text = env.EffectiveMode == WinePrefixMode.Isolated
            ? Services.Loc.T("GameSettingsPrefixIsolatedExplain",
                "A separate Wine environment will be created for this game. This may use approximately 1-2 GB of additional disk space.")
            : Services.Loc.T("GameSettingsPrefixSharedExplain",
                "This game will use the common TeknoParrot environment. Any existing isolated prefix will be preserved.");
    }

    private async System.Threading.Tasks.Task ResetIsolatedPrefixAsync(GameProfile profile)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var wine = ProtonLauncher.ResolveWineBinary(profile);
        var runnerKind = wine != null && ProtonLauncher.FindProtonScript(wine) != null
            ? WineRunnerKind.Proton
            : WineRunnerKind.PlainWine;

        var confirmed = await Services.Dialogs.ConfirmAsync(owner,
            Services.Loc.T("GameSettingsResetIsolatedPrefixTitle", "Reset Isolated Prefix"),
            Services.Loc.T("GameSettingsResetIsolatedPrefixConfirm",
                "This deletes this game's dedicated Wine environment (if one exists) and recreates it fresh. The shared prefix and other games are never affected. Continue?"));
        if (!confirmed)
            return;

        var result = await System.Threading.Tasks.Task.Run(() => WinePrefixManager.ResetIsolated(profile, runnerKind));
        await Services.Dialogs.InfoAsync(owner,
            Services.Loc.T("GameSettingsResetIsolatedPrefixTitle", "Reset Isolated Prefix"), result.Message);
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
                    // Input is always merged (SDL2 gamepads + RawInput keyboard/
                    // mouse) — no input-system selection anymore. The dropdown
                    // survives only as a gun-flavour picker for games offering
                    // both RawInput and RawInputTrackball.
                    var gunOptions = options.FindAll(o => o is "RawInput" or "RawInputTrackball");
                    if (gunOptions.Count < 2)
                        return; // nothing to choose — hide the row entirely
                    options = gunOptions;
                    if (!options.Contains(selected ?? ""))
                        selected = options[0];
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
        if (_wineRunnerCombo != null && (_wineRunnerCombo.SelectedItem as string ?? "") != _baselineWineRunner)
            return true;
        if (_wineRunnerPathBox != null && (_wineRunnerPathBox.Text ?? "") != _baselineWineRunnerPath)
            return true;
        if (_prefixModeCombo != null && (_prefixModeCombo.SelectedItem as string ?? "") != _baselinePrefixMode)
            return true;
        if (_fullscreenScalingCombo != null && (_fullscreenScalingCombo.SelectedItem as string ?? "") != _baselineFullscreenScaling)
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

        if (_wineRunnerCombo != null)
        {
            var selected = _wineRunnerCombo.SelectedItem as string;
            _profile.ProtonVersion = selected switch
            {
                null or "Auto (default)" => null,
                "System Wine" => "system",
                _ when selected.EndsWith(WineRunnerNotInstalledSuffix, StringComparison.Ordinal)
                    => selected[..^WineRunnerNotInstalledSuffix.Length],
                _ => selected
            };
        }
        if (_wineRunnerPathBox != null)
            _profile.WineRunnerPath = string.IsNullOrWhiteSpace(_wineRunnerPathBox.Text) ? null : _wineRunnerPathBox.Text.Trim();

        if (_prefixModeCombo != null)
        {
            _profile.WinePrefixMode = _prefixModeCombo.SelectedIndex switch
            {
                1 => WinePrefixMode.Shared,
                2 => WinePrefixMode.Isolated,
                _ => WinePrefixMode.Default
            };
        }

        if (_fullscreenScalingCombo != null)
        {
            _profile.FullscreenScalingMode = _fullscreenScalingCombo.SelectedIndex switch
            {
                1 => LinuxFullscreenScalingMode.AutomaticFit,
                2 => LinuxFullscreenScalingMode.Disabled,
                _ => LinuxFullscreenScalingMode.Default
            };
        }

        foreach (var (field, read) in _valueReaders)
            field.FieldValue = read();

        Directory.CreateDirectory("UserProfiles");
        JoystickHelper.SerializeGameProfile(_profile);
        Saved?.Invoke(_profile.GameNameInternal ?? _profile.ProfileName ?? "profile");
        BackRequested?.Invoke();
    }
}
