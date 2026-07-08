using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Configure buttons once, apply to many games — full port of the classic
/// MultiGameButtonConfig: input mode selection (MergedInput default), category
/// and search filters, named profile save/load/delete, copy-from-game, reset,
/// per-API binding copies restricted to APIs each game can actually read, and
/// lightgun/trackball device dropdowns.
/// </summary>
public partial class MultiButtonConfigView : UserControl
{
    private const string ProfilesDirectory = "UserProfiles\\Profiles";

    private sealed class GameEntry
    {
        public required CheckBox Box { get; init; }
        public required GameProfile Profile { get; init; }
    }

    private readonly List<GameProfile> _allGameProfiles = new();
    private readonly List<GameEntry> _filteredGames = new();
    private readonly Dictionary<InputMapping, JoystickButtons> _master = new();
    private readonly Dictionary<InputMapping, Button> _bindButtons = new();
    private readonly InputCaptureService _capture = new();
    private readonly RawInputCaptureService _rawCapture = new();
    private InputApi _currentInputApi = InputApi.MergedInput;
    private InputMapping? _armedMapping;
    private bool _isLoading = true;
    private bool _hasUnsavedChanges;

    public event Action? BackRequested;
    public event Action<int>? Applied;

    public MultiButtonConfigView()
    {
        InitializeComponent();

        InputApiSelector.ItemsSource = new[] { "Merged Input (All APIs)", "DirectInput", "XInput", "RawInput" };
        CategorySelector.ItemsSource = new[] { "All Games", "Racing Games", "Shooting Games", "Arcade Games" };
        Localize();
        Services.Loc.LanguageChanged += Localize;

        _capture.BindingCaptured += captured => Dispatcher.UIThread.Post(() => OnCaptured(captured));
        _rawCapture.BindingCaptured += (name, button, isEscape) =>
            Dispatcher.UIThread.Post(() => OnRawCaptured(name, button, isEscape));
        Unloaded += (_, _) => StopListening();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    private void Localize()
    {
        var apiIndex = InputApiSelector.SelectedIndex;
        InputApiSelector.ItemsSource = new[]
        {
            Services.Loc.T("MultiGameButtonConfigMergedInput", "Merged Input (All APIs)"),
            "DirectInput", "XInput", "RawInput"
        };
        InputApiSelector.SelectedIndex = apiIndex >= 0 ? apiIndex : 0;

        var catIndex = CategorySelector.SelectedIndex;
        CategorySelector.ItemsSource = new[]
        {
            Services.Loc.T("MultiGameButtonConfigAllGamesCategory", "All Games"),
            Services.Loc.T("MultiGameButtonConfigRacingGamesCategory", "Racing Games"),
            Services.Loc.T("MultiGameButtonConfigShootingGamesCategory", "Shooting Games"),
            Services.Loc.T("MultiGameButtonConfigArcadeGamesCategory", "Arcade Games")
        };
        CategorySelector.SelectedIndex = catIndex >= 0 ? catIndex : 0;

        SearchBox.Watermark = Services.Loc.T("MultiGameButtonConfigSearchGames", "Search games...");
        BtnSaveProfile.Content = Services.Loc.T("MultiGameButtonConfigSaveProfile", "Save Profile");
        BtnLoadProfile.Content = Services.Loc.T("MultiGameButtonConfigLoadProfile", "Load Profile");
        BtnBack.Content = Services.Loc.T("Back", "Back");
        BtnCopyFromGame.Content = Services.Loc.T("MultiGameButtonConfigCopyFromGame", "Copy From Game...");
        BtnResetDefault.Content = Services.Loc.T("MultiGameButtonConfigResetToDefaults", "Reset to Default");
        BtnApply.Content = Services.Loc.T("MultiGameButtonConfigApplyToSelected", "Apply to Selected Games");
        BtnSave.Content = Services.Loc.T("SettingsSaveSettings", "Save");
    }

    public void Refresh()
    {
        _isLoading = true;

        GameProfileLoader.LoadProfiles(false);
        _allGameProfiles.Clear();
        _allGameProfiles.AddRange(GameProfileLoader.UserProfiles.OrderBy(p => p.GameNameInternal ?? p.ProfileName));

        _master.Clear();
        _hasUnsavedChanges = false;
        InputApiSelector.SelectedIndex = 0; // MergedInput by default
        CategorySelector.SelectedIndex = 0;
        SearchBox.Text = "";

        _isLoading = false;
        LoadGameList();
        RefreshProfilesList();
    }

    // ---------- filtering / game list ----------

    private static bool IsRacingGame(GameProfile p) =>
        p.JoystickButtons.Any(b => b.InputMapping is InputMapping.Analog0 or InputMapping.Analog2);

    private static bool IsShootingGame(GameProfile p) =>
        p.JoystickButtons.Any(b => b.InputMapping is InputMapping.P1LightGun or InputMapping.P2LightGun);

    private static bool IsArcadeGame(GameProfile p) => !IsRacingGame(p) && !IsShootingGame(p);

    /// <summary>
    /// APIs a game can actually read, from the "Input API" ConfigValue FieldOptions
    /// (same source the runtime input listener uses). Legacy profiles = DirectInput only.
    /// </summary>
    private static HashSet<InputApi> GetSupportedApis(GameProfile profile)
    {
        var result = new HashSet<InputApi>();
        var field = profile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
        if (field?.FieldOptions != null)
        {
            foreach (var option in field.FieldOptions)
            {
                if (Enum.TryParse(option, out InputApi api) && api != InputApi.MergedInput)
                    result.Add(api);
            }
        }
        if (result.Count == 0)
            result.Add(InputApi.DirectInput);
        return result;
    }

    private HashSet<InputApi> GetApisForCurrentMode() =>
        _currentInputApi == InputApi.MergedInput
            ? new HashSet<InputApi> { InputApi.DirectInput, InputApi.XInput, InputApi.RawInput, InputApi.RawInputTrackball }
            : new HashSet<InputApi> { _currentInputApi };

    private void LoadGameList()
    {
        if (_isLoading) return;

        // Preserve selection across rebuilds (search, category or API changes)
        var previouslySelected = new HashSet<GameProfile>(SelectedGames);

        _filteredGames.Clear();
        GamesPanel.Children.Clear();

        var search = SearchBox.Text?.ToLowerInvariant() ?? "";
        var categoryIndex = CategorySelector.SelectedIndex;

        foreach (var profile in _allGameProfiles)
        {
            var name = profile.GameNameInternal ?? profile.ProfileName ?? "";
            bool matchesSearch = string.IsNullOrEmpty(search) || name.ToLowerInvariant().Contains(search);
            bool matchesCategory = categoryIndex switch
            {
                1 => IsRacingGame(profile),
                2 => IsShootingGame(profile),
                3 => IsArcadeGame(profile),
                _ => true
            };
            // Specific API modes only show games that actually support that API
            bool matchesApi = _currentInputApi == InputApi.MergedInput || GetSupportedApis(profile).Contains(_currentInputApi);

            if (!matchesSearch || !matchesCategory || !matchesApi)
                continue;

            var box = new CheckBox
            {
                Content = name,
                FontSize = 12,
                IsChecked = previouslySelected.Contains(profile)
            };
            box.IsCheckedChanged += (_, _) => { if (!_isLoading) RebuildButtonRows(); };
            _filteredGames.Add(new GameEntry { Box = box, Profile = profile });
            GamesPanel.Children.Add(box);
        }

        RebuildButtonRows();
    }

    private List<GameProfile> SelectedGames =>
        _filteredGames.Where(g => g.Box.IsChecked == true).Select(g => g.Profile).ToList();

    // ---------- button rows ----------

    private static bool IsAnalogButton(InputMapping mapping)
    {
        var name = mapping.ToString();
        return name.StartsWith("Analog") || name.Contains("Axis") || name.EndsWith("Positive") ||
               name.EndsWith("Negative") || name.Contains("Throttle") || name.Contains("Brake");
    }

    private static bool IsDeviceMapping(InputMapping mapping) =>
        mapping is InputMapping.P1LightGun or InputMapping.P2LightGun or InputMapping.P3LightGun
            or InputMapping.P4LightGun or InputMapping.P1Trackball or InputMapping.P2Trackball;

    private static string BuildMergedBindName(string? xiName, string? diName, string? riName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(xiName)) parts.Add($"XI: {xiName}");
        if (!string.IsNullOrEmpty(diName)) parts.Add($"DI: {diName}");
        if (!string.IsNullOrEmpty(riName)) parts.Add($"RI: {riName}");
        return string.Join(" | ", parts);
    }

    private void UpdateBindNameForCurrentApi(JoystickButtons button)
    {
        button.BindName = _currentInputApi switch
        {
            InputApi.DirectInput => button.BindNameDi,
            InputApi.XInput => button.BindNameXi,
            InputApi.RawInput or InputApi.RawInputTrackball => button.BindNameRi,
            InputApi.MergedInput => BuildMergedBindName(button.BindNameXi, button.BindNameDi, button.BindNameRi),
            _ => button.BindName
        };
    }

    /// <summary>
    /// Copies only the given APIs' bindings from source to target, leaving other
    /// APIs untouched. Returns true if anything changed.
    /// </summary>
    private bool CopyBindingsForApis(JoystickButtons source, JoystickButtons target, HashSet<InputApi> apis)
    {
        bool changed = false;

        if (apis.Contains(InputApi.DirectInput))
        {
            if (target.DirectInputButton != source.DirectInputButton || target.BindNameDi != source.BindNameDi)
                changed = true;
            target.DirectInputButton = source.DirectInputButton;
            target.BindNameDi = source.BindNameDi;
        }

        if (apis.Contains(InputApi.XInput))
        {
            if (target.XInputButton != source.XInputButton || target.BindNameXi != source.BindNameXi)
                changed = true;
            target.XInputButton = source.XInputButton;
            target.BindNameXi = source.BindNameXi;
        }

        if (apis.Contains(InputApi.RawInput) || apis.Contains(InputApi.RawInputTrackball))
        {
            if (target.RawInputButton != source.RawInputButton || target.BindNameRi != source.BindNameRi)
                changed = true;
            target.RawInputButton = source.RawInputButton;
            target.BindNameRi = source.BindNameRi;
        }

        UpdateBindNameForCurrentApi(target);
        return changed;
    }

    /// <summary>
    /// Sets the game's "Input API" setting to match the mode the bindings were made
    /// in, so the applied bindings are actually read in-game.
    /// </summary>
    private void SetGameInputApi(GameProfile profile)
    {
        var field = profile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
        if (field == null)
            return;

        if (_currentInputApi == InputApi.MergedInput)
        {
            if (field.FieldOptions != null && !field.FieldOptions.Contains("MergedInput"))
                field.FieldOptions.Add("MergedInput");
            field.FieldValue = "MergedInput";
        }
        else if (field.FieldOptions == null || field.FieldOptions.Contains(_currentInputApi.ToString()))
        {
            field.FieldValue = _currentInputApi.ToString();
        }
    }

    private void RebuildButtonRows()
    {
        ButtonsPanel.Children.Clear();
        _bindButtons.Clear();
        _armedMapping = null;

        var selected = SelectedGames;
        if (selected.Count == 0)
        {
            ButtonsHeader.Text = "Buttons — select target games first";
            StopListening();
            return;
        }

        // Union of buttons across selected games, keyed by InputMapping; keep a
        // master clone so edits don't touch the real profiles until Apply
        var perGameCounts = new Dictionary<InputMapping, List<(string GameName, string ButtonName)>>();
        foreach (var game in selected)
        {
            foreach (var button in game.JoystickButtons)
            {
                if (!_master.ContainsKey(button.InputMapping))
                {
                    var clone = new JoystickButtons
                    {
                        ButtonName = button.ButtonName,
                        BindName = button.BindName,
                        BindNameDi = button.BindNameDi,
                        BindNameXi = button.BindNameXi,
                        BindNameRi = button.BindNameRi,
                        DirectInputButton = button.DirectInputButton,
                        XInputButton = button.XInputButton,
                        RawInputButton = button.RawInputButton,
                        InputMapping = button.InputMapping
                    };
                    UpdateBindNameForCurrentApi(clone);
                    _master[button.InputMapping] = clone;
                }

                if (!perGameCounts.TryGetValue(button.InputMapping, out var list))
                    perGameCounts[button.InputMapping] = list = new List<(string, string)>();
                list.Add((game.GameNameInternal ?? game.ProfileName ?? "?", button.ButtonName ?? ""));
            }
        }

        var mappings = perGameCounts.Keys
            .OrderBy(m => IsAnalogButton(m) ? 1 : 0) // digital first, then analog
            .ThenBy(m => _master[m].ButtonName)
            .ToList();

        foreach (var mapping in mappings)
        {
            var master = _master[mapping];
            var usage = perGameCounts[mapping];

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*,110,Auto"), Margin = new global::Avalonia.Thickness(0, 1, 0, 1) };
            var label = new TextBlock { Text = master.ButtonName, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };

            // Tooltip listing differing per-game names for this merged button
            if (usage.Select(u => u.ButtonName).Distinct().Count() > 1)
            {
                ToolTip.SetTip(label, string.Join("\n", usage
                    .GroupBy(u => u.ButtonName)
                    .Select(g => $"{g.Key} — {string.Join(", ", g.Select(u => u.GameName))}")));
            }

            Control editor;
            if (IsDeviceMapping(mapping))
            {
                editor = BuildDeviceCombo(master);
            }
            else
            {
                var bind = new Button
                {
                    Content = string.IsNullOrEmpty(master.BindName) ? Services.Loc.T("NotBound", "(not bound)") : master.BindName,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                bind.Click += (_, _) => Arm(mapping);
                _bindButtons[mapping] = bind;
                editor = bind;
            }

            var availability = new TextBlock
            {
                Text = $"{usage.Count}/{selected.Count} game(s)",
                FontSize = 11,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new global::Avalonia.Thickness(6, 0, 0, 0)
            };

            var clear = new Button { Content = "✕", FontSize = 12, Margin = new global::Avalonia.Thickness(4, 0, 0, 0) };
            ToolTip.SetTip(clear, "Clear this binding (all APIs)");
            clear.Click += (_, _) =>
            {
                master.DirectInputButton = null;
                master.BindNameDi = "";
                master.XInputButton = null;
                master.BindNameXi = "";
                master.RawInputButton = null;
                master.BindNameRi = "";
                master.BindName = "";
                if (_bindButtons.TryGetValue(mapping, out var b))
                    b.Content = Services.Loc.T("NotBound", "(not bound)");
                _hasUnsavedChanges = true;
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(editor, 1);
            Grid.SetColumn(availability, 2);
            Grid.SetColumn(clear, 3);
            grid.Children.Add(label);
            grid.Children.Add(editor);
            grid.Children.Add(availability);
            grid.Children.Add(clear);
            ButtonsPanel.Children.Add(grid);
        }

        ButtonsHeader.Text = $"Buttons ({selected.Count} game(s) selected, {mappings.Count} controls)";
        StartListening();
    }

    private ComboBox BuildDeviceCombo(JoystickButtons master)
    {
        // Keep these strings hardcoded — they get saved to configuration
        var deviceList = new List<string> { "None", "Windows Mouse Cursor", "Unknown Device" };
        if (OperatingSystem.IsWindows())
            deviceList.AddRange(_rawCapture.GetMouseDeviceList());
        if (!string.IsNullOrEmpty(master.BindNameRi) && !deviceList.Contains(master.BindNameRi))
            deviceList.Add(master.BindNameRi);

        var combo = new ComboBox
        {
            ItemsSource = deviceList,
            SelectedItem = string.IsNullOrEmpty(master.BindNameRi) ? "None" : master.BindNameRi,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not string selectedDeviceName)
                return;

            string path;
            var type = RawDeviceType.Mouse;
            if (selectedDeviceName == "Windows Mouse Cursor")
            {
                path = "Windows Mouse Cursor";
            }
            else if (selectedDeviceName == "None")
            {
                path = "None";
                type = RawDeviceType.None;
            }
            else if (selectedDeviceName == "Unknown Device")
            {
                path = "null";
            }
            else
            {
                var devicePath = OperatingSystem.IsWindows() ? _rawCapture.GetMouseDevicePathByName(selectedDeviceName) : null;
                if (devicePath == null)
                {
                    StatusText.Text = $"Device \"{selectedDeviceName}\" is not currently available.";
                    return;
                }
                path = devicePath;
            }

            master.RawInputButton = new RawInputButton
            {
                DevicePath = path,
                DeviceType = type,
                MouseButton = RawMouseButton.None,
                KeyboardKey = Keys.None
            };
            master.BindNameRi = selectedDeviceName;
            UpdateBindNameForCurrentApi(master);
            _hasUnsavedChanges = true;
        };

        return combo;
    }

    // ---------- input capture ----------

    private void StartListening()
    {
        StopListening();

        // Only listen on APIs at least one selected game can actually read
        var supported = new HashSet<InputApi>();
        foreach (var game in SelectedGames)
            supported.UnionWith(GetSupportedApis(game));

        switch (_currentInputApi)
        {
            case InputApi.DirectInput:
                _capture.Start(InputApi.DirectInput);
                break;
            case InputApi.XInput:
                _capture.Start(InputApi.XInput);
                break;
            case InputApi.RawInput:
            case InputApi.RawInputTrackball:
                if (OperatingSystem.IsWindows())
                    _rawCapture.Start(registerKeyboard: true);
                break;
            case InputApi.MergedInput:
                bool useXi = supported.Contains(InputApi.XInput);
                bool useDi = supported.Contains(InputApi.DirectInput);
                if (useXi && useDi) _capture.Start(InputApi.MergedInput);
                else if (useXi) _capture.Start(InputApi.XInput);
                else if (useDi) _capture.Start(InputApi.DirectInput);

                if (OperatingSystem.IsWindows() &&
                    (supported.Contains(InputApi.RawInput) || supported.Contains(InputApi.RawInputTrackball)))
                {
                    // If DirectInput is also listening, capture only mice via RawInput so
                    // keyboard presses deterministically become DirectInput bindings
                    _rawCapture.Start(registerKeyboard: !useDi);
                }
                break;
        }
    }

    private void StopListening()
    {
        _capture.Stop();
        _rawCapture.Stop();
    }

    private void Arm(InputMapping mapping)
    {
        if (_armedMapping is { } previous && _bindButtons.TryGetValue(previous, out var prevButton))
            prevButton.Content = string.IsNullOrEmpty(_master[previous].BindName) ? Services.Loc.T("NotBound", "(not bound)") : _master[previous].BindName;

        _armedMapping = mapping;
        _bindButtons[mapping].Content = Services.Loc.T("PressButtonKeyAxis", Services.Loc.T("PressButtonKeyAxis", "Press a button / key / axis..."));
    }

    private void OnCaptured(CapturedBinding captured)
    {
        if (_armedMapping is not { } mapping || !_master.TryGetValue(mapping, out var master))
            return;

        switch (_currentInputApi)
        {
            case InputApi.DirectInput when captured.DirectInput != null:
                master.DirectInputButton = captured.DirectInput;
                master.BindNameDi = captured.DisplayName;
                break;
            case InputApi.XInput when captured.XInput != null:
                master.XInputButton = captured.XInput;
                master.BindNameXi = captured.DisplayName;
                break;
            case InputApi.MergedInput:
                if (captured.XInput != null) { master.XInputButton = captured.XInput; master.BindNameXi = captured.DisplayName; }
                if (captured.DirectInput != null) { master.DirectInputButton = captured.DirectInput; master.BindNameDi = captured.DisplayName; }
                break;
            default:
                return;
        }

        FinishCapture(mapping, master);
    }

    private void OnRawCaptured(string name, RawInputButton button, bool isEscape)
    {
        if (_armedMapping is not { } mapping || !_master.TryGetValue(mapping, out var master))
            return;

        if (isEscape)
        {
            if (_bindButtons.TryGetValue(mapping, out var b))
                b.Content = string.IsNullOrEmpty(master.BindName) ? Services.Loc.T("NotBound", "(not bound)") : master.BindName;
            _armedMapping = null;
            return;
        }

        if (_currentInputApi is not (InputApi.RawInput or InputApi.RawInputTrackball or InputApi.MergedInput))
            return;

        master.RawInputButton = button;
        master.BindNameRi = name;
        FinishCapture(mapping, master);
    }

    private void FinishCapture(InputMapping mapping, JoystickButtons master)
    {
        UpdateBindNameForCurrentApi(master);
        if (_bindButtons.TryGetValue(mapping, out var b))
            b.Content = string.IsNullOrEmpty(master.BindName) ? Services.Loc.T("NotBound", "(not bound)") : master.BindName;
        _armedMapping = null;
        _hasUnsavedChanges = true;
    }

    // ---------- named profiles ----------

    private void RefreshProfilesList()
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var profiles = Directory.GetDirectories(ProfilesDirectory).Select(Path.GetFileName).ToList();
        ProfilesComboBox.ItemsSource = profiles;
        if (profiles.Count > 0)
            ProfilesComboBox.SelectedIndex = 0;
    }

    private void ProfilesComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfilesComboBox.SelectedItem is string name)
            ProfileNameBox.Text = name;
    }

    private string? CurrentProfileName
    {
        get
        {
            var name = ProfileNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = ProfilesComboBox.SelectedItem as string;
            return string.IsNullOrWhiteSpace(name) ? null : string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }
    }

    private async void BtnSaveProfile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count == 0)
        {
            StatusText.Text = "Select at least one game to save a profile.";
            return;
        }

        var profileName = CurrentProfileName;
        if (profileName == null)
        {
            StatusText.Text = "Enter a profile name first.";
            return;
        }

        // Persist the master (pending) bindings into the saved snapshot too
        ApplyMasterToGames(selectedGames, quiet: true);

        var profileDir = Path.Combine(ProfilesDirectory, profileName);
        Directory.CreateDirectory(profileDir);

        int savedCount = 0;
        try
        {
            foreach (var game in selectedGames)
            {
                var copy = new GameProfile
                {
                    ProfileName = game.ProfileName,
                    GameNameInternal = game.GameNameInternal,
                    JoystickButtons = game.JoystickButtons.Select(b => new JoystickButtons
                    {
                        ButtonName = b.ButtonName,
                        InputMapping = b.InputMapping,
                        BindName = b.BindName,
                        BindNameDi = b.BindNameDi,
                        BindNameXi = b.BindNameXi,
                        BindNameRi = b.BindNameRi,
                        DirectInputButton = b.DirectInputButton,
                        XInputButton = b.XInputButton,
                        RawInputButton = b.RawInputButton
                    }).ToList()
                };

                var fileName = Path.Combine(profileDir, game.ProfileName + ".xml");
                using var writer = XmlWriter.Create(fileName, new XmlWriterSettings { Indent = true });
                new XmlSerializer(typeof(GameProfile)).Serialize(writer, copy);
                savedCount++;
            }

            RefreshProfilesList();
            ProfilesComboBox.SelectedItem = profileName;
            StatusText.Text = $"Saved bindings of {savedCount} game(s) to profile \"{profileName}\".";
        }
        catch (Exception ex)
        {
            if (OwnerWindow != null)
                await Dialogs.InfoAsync(OwnerWindow, "Save Error", $"Error saving profile: {ex.Message}");
        }
    }

    private async void BtnLoadProfile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count == 0)
        {
            StatusText.Text = "Select at least one game to load the profile into.";
            return;
        }

        var profileName = CurrentProfileName;
        if (profileName == null || !Directory.Exists(Path.Combine(ProfilesDirectory, profileName)))
        {
            StatusText.Text = "Select a valid profile to load.";
            return;
        }

        var profileDir = Path.Combine(ProfilesDirectory, profileName);
        int loadedCount = 0;
        try
        {
            foreach (var game in selectedGames)
            {
                var fileName = Path.Combine(profileDir, game.ProfileName + ".xml");
                if (!File.Exists(fileName))
                    continue; // Skip games that don't have saved profiles

                GameProfile savedProfile;
                using (var reader = XmlReader.Create(fileName))
                    savedProfile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(reader)!;

                // Apply to the game, restricted to APIs the game can actually read
                var gameApis = GetSupportedApis(game);
                foreach (var savedButton in savedProfile.JoystickButtons)
                {
                    var gameButton = game.JoystickButtons.FirstOrDefault(b => b.InputMapping == savedButton.InputMapping);
                    if (gameButton != null)
                        CopyBindingsForApis(savedButton, gameButton, gameApis);
                }
                loadedCount++;
            }

            if (loadedCount > 0)
            {
                _hasUnsavedChanges = true;
                // Rebuild the master set from the freshly loaded bindings
                _master.Clear();
                RebuildButtonRows();
                StatusText.Text = $"Loaded profile \"{profileName}\" into {loadedCount} game(s). Use Save to write them to disk.";
            }
            else
            {
                StatusText.Text = $"Profile \"{profileName}\" has no configurations for the selected game(s).";
            }
        }
        catch (Exception ex)
        {
            if (OwnerWindow != null)
                await Dialogs.InfoAsync(OwnerWindow, "Load Error", $"Error loading profile: {ex.Message}");
        }
    }

    private async void BtnDeleteProfile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profileName = CurrentProfileName;
        if (profileName == null || !Directory.Exists(Path.Combine(ProfilesDirectory, profileName)))
        {
            StatusText.Text = "Select a valid profile to delete.";
            return;
        }

        if (OwnerWindow == null || !await Dialogs.ConfirmAsync(OwnerWindow, "Confirm Delete", $"Delete profile \"{profileName}\"?"))
            return;

        try
        {
            Directory.Delete(Path.Combine(ProfilesDirectory, profileName), true);
            RefreshProfilesList();
            StatusText.Text = $"Profile \"{profileName}\" deleted.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error deleting profile: {ex.Message}";
        }
    }

    // ---------- apply / save ----------

    /// <summary>
    /// Applies the master bindings to the given games — only for APIs each game can
    /// read, further restricted to the APIs the current UI mode edits (WPF rules).
    /// </summary>
    private (int changes, int applied, int skipped) ApplyMasterToGames(List<GameProfile> games, bool quiet = false)
    {
        int totalChanges = 0, skippedGames = 0;
        var modeApis = GetApisForCurrentMode();

        foreach (var game in games)
        {
            var applicableApis = GetSupportedApis(game);
            applicableApis.IntersectWith(modeApis);
            if (applicableApis.Count == 0)
            {
                skippedGames++; // never write dead bindings
                continue;
            }

            int gameChanges = 0;
            foreach (var (mapping, master) in _master)
            {
                var gameButton = game.JoystickButtons.FirstOrDefault(b => b.InputMapping == mapping);
                if (gameButton == null)
                    continue;
                if (CopyBindingsForApis(master, gameButton, applicableApis))
                    gameChanges++;
            }

            // Make sure the game will actually read the bindings we just applied
            SetGameInputApi(game);
            totalChanges += gameChanges;
        }

        if (!quiet)
        {
            StatusText.Text = $"Applied {totalChanges} binding change(s) to {games.Count - skippedGames} game(s).";
            if (skippedGames > 0)
                StatusText.Text += $" {skippedGames} game(s) skipped (input mode not supported).";
        }

        return (totalChanges, games.Count - skippedGames, skippedGames);
    }

    private void BtnApply_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count == 0 || _master.Count == 0)
        {
            StatusText.Text = "Select games and configure at least one button first.";
            return;
        }
        var (_, applied, _) = ApplyMasterToGames(selectedGames);
        _hasUnsavedChanges = true;
        Applied?.Invoke(applied);
    }

    private async void BtnSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count == 0)
        {
            StatusText.Text = "Select at least one game to save.";
            return;
        }

        ApplyMasterToGames(selectedGames, quiet: true);

        int savedCount = 0;
        try
        {
            foreach (var game in selectedGames)
            {
                JoystickHelper.SerializeGameProfile(game);
                savedCount++;
            }
            _hasUnsavedChanges = false;
            StatusText.Text = $"Saved {savedCount} game profile(s) to disk.";
            Applied?.Invoke(savedCount);
        }
        catch (Exception ex)
        {
            if (OwnerWindow != null)
                await Dialogs.InfoAsync(OwnerWindow, "Save Error", $"Error saving profiles: {ex.Message}");
        }
    }

    private async void BtnCopyFromGame_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count != 1)
        {
            StatusText.Text = "Select exactly one target game to copy into.";
            return;
        }

        if (OwnerWindow == null)
            return;

        // Source game picker dialog
        var listBox = new ListBox
        {
            ItemsSource = _allGameProfiles.Select(p => p.GameNameInternal ?? p.ProfileName).ToList(),
            Margin = new global::Avalonia.Thickness(10)
        };
        var selectBtn = new Button { Content = "Select", MinWidth = 80 };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 80, Margin = new global::Avalonia.Thickness(0, 0, 8, 0) };
        var dialog = new Window
        {
            Title = "Select game to copy from",
            Width = 400,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new global::Avalonia.Thickness(10),
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Children = { cancelBtn, selectBtn }
                    },
                    new ScrollViewer { Content = listBox }
                }
            }
        };
        string? chosen = null;
        selectBtn.Click += (_, _) => { chosen = listBox.SelectedItem as string; dialog.Close(); };
        cancelBtn.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(OwnerWindow);

        if (chosen == null)
            return;

        var sourceProfile = _allGameProfiles.FirstOrDefault(p => (p.GameNameInternal ?? p.ProfileName) == chosen);
        var targetProfile = selectedGames[0];
        if (sourceProfile == null)
            return;

        // Copy matching buttons — only APIs the target game can actually read
        var targetApis = GetSupportedApis(targetProfile);
        foreach (var sourceButton in sourceProfile.JoystickButtons)
        {
            var targetButton = targetProfile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
            if (targetButton != null)
                CopyBindingsForApis(sourceButton, targetButton, targetApis);
        }

        _hasUnsavedChanges = true;
        _master.Clear();
        RebuildButtonRows();
        StatusText.Text = $"Button configuration copied from {sourceProfile.GameNameInternal}. Use Save to write it to disk.";
    }

    private async void BtnResetDefault_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedGames = SelectedGames;
        if (selectedGames.Count == 0)
        {
            StatusText.Text = "Select at least one game to reset.";
            return;
        }

        if (OwnerWindow == null || !await Dialogs.ConfirmAsync(OwnerWindow, "Confirm Reset",
                $"Reset ALL button bindings (every input API) of {selectedGames.Count} game(s)?"))
            return;

        foreach (var game in selectedGames)
        {
            foreach (var button in game.JoystickButtons)
            {
                button.DirectInputButton = null;
                button.BindNameDi = "";
                button.XInputButton = null;
                button.BindNameXi = "";
                button.RawInputButton = null;
                button.BindNameRi = "";
                button.BindName = "";
            }
        }

        _hasUnsavedChanges = true;
        _master.Clear();
        RebuildButtonRows();
        StatusText.Text = $"Button configuration of {selectedGames.Count} game(s) reset. Use Save to write it to disk.";
    }

    // ---------- filters / navigation ----------

    private async void InputApiSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        // Ask about unsaved changes before switching input mode
        if (_hasUnsavedChanges && OwnerWindow != null)
        {
            var result = await Dialogs.ConfirmCancelAsync(OwnerWindow, "Unsaved Changes",
                "You have unsaved binding changes. Save them before switching input mode?");
            if (result == true)
                BtnSave_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs());
            // null (cancel) still switches in this port; pending changes stay in memory either way
        }

        StopListening();
        _currentInputApi = InputApiSelector.SelectedIndex switch
        {
            1 => InputApi.DirectInput,
            2 => InputApi.XInput,
            3 => InputApi.RawInput,
            _ => InputApi.MergedInput
        };

        foreach (var master in _master.Values)
            UpdateBindNameForCurrentApi(master);

        int previouslySelected = SelectedGames.Count;
        LoadGameList();

        StatusText.Text = $"Switched to {InputApiSelector.SelectedItem} mode.";
        if (_currentInputApi != InputApi.MergedInput && previouslySelected > 0)
        {
            int stillSelected = SelectedGames.Count;
            if (stillSelected < previouslySelected)
                StatusText.Text += $" {stillSelected}/{previouslySelected} selected game(s) support this input mode.";
        }
    }

    private void CategorySelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading) LoadGameList();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isLoading) LoadGameList();
    }

    private void BtnSelectAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoading = true;
        foreach (var game in _filteredGames) game.Box.IsChecked = true;
        _isLoading = false;
        RebuildButtonRows();
    }

    private void BtnSelectNone_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isLoading = true;
        foreach (var game in _filteredGames) game.Box.IsChecked = false;
        _isLoading = false;
        RebuildButtonRows();
    }

    private async void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_hasUnsavedChanges && OwnerWindow != null)
        {
            var result = await Dialogs.ConfirmCancelAsync(OwnerWindow, "Unsaved Changes",
                "You have unsaved changes. Save them before leaving?");
            if (result == null)
                return; // cancel — stay
            if (result == true)
            {
                var selectedGames = SelectedGames;
                if (selectedGames.Count > 0)
                {
                    ApplyMasterToGames(selectedGames, quiet: true);
                    foreach (var game in selectedGames)
                        JoystickHelper.SerializeGameProfile(game);
                }
                _hasUnsavedChanges = false;
            }
        }

        StopListening();
        BackRequested?.Invoke();
    }
}
