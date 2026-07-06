using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using TeknoParrotUi.Properties;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.UserControls
{
    public partial class MultiGameButtonConfig : UserControl, INotifyPropertyChanged
    {
        private readonly ContentControl _contentControl;
        private readonly List<GameProfile> _allGameProfiles;
        private readonly Library _library;
        private InputApi _currentInputApi = InputApi.MergedInput;
        private List<GameViewModel> _filteredGames = new List<GameViewModel>();
        private List<JoystickButtons> _commonButtons = new List<JoystickButtons>();
        private bool _isLoading = true;

        private JoystickControlDirectInput _joystickControlDirectInput;
        private JoystickControlXInput _joystickControlXInput;
        private JoystickControlRawInput _joystickControlRawInput;

        // Input listener helpers
        private Thread _inputListener;
        private TextBox _lastActiveTextBox;
        private bool _isListening = false;
        private bool _hasUnsavedChanges = false;
        private bool _revertingApiSelection = false;

        // Add this field to store the original text
        private Dictionary<TextBox, string> _originalTexts = new Dictionary<TextBox, string>();

        public event PropertyChangedEventHandler PropertyChanged;

        public class GameViewModel : INotifyPropertyChanged
        {
            private bool _isSelected;

            public GameProfile Profile { get; set; }
            public string GameName { get; set; } // Keep this property name, but populate from GameNameInternal
            public bool IsSelected 
            { 
                get => _isSelected; 
                set
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class ButtonViewModel
        {
            public JoystickButtons Button { get; set; }
            public string ButtonName { get => Button.ButtonName; }
            public string BindName 
            { 
                get => Button.BindName; 
                set => Button.BindName = value;
            }
            public string Availability { get; set; }
            public string MergedNamesTooltip { get; set; }
        }

        // Update the constructor to remove the event handlers setup
        public MultiGameButtonConfig(ContentControl contentControl, Library library)
        {
            InitializeComponent();

            _contentControl = contentControl;
            _library = library;
            
            // Ensure profiles are loaded
            if (GameProfileLoader.UserProfiles == null)
                GameProfileLoader.LoadProfiles(true); // true = only load user profiles
                
            // Only use user profiles - these are the ones the user can actually modify
            _allGameProfiles = new List<GameProfile>();
            
            // Add only user profiles
            if (GameProfileLoader.UserProfiles != null)
                _allGameProfiles.AddRange(GameProfileLoader.UserProfiles);

            // Initialize controller helpers
            _joystickControlDirectInput = new JoystickControlDirectInput();
            _joystickControlXInput = new JoystickControlXInput();
            _joystickControlRawInput = new JoystickControlRawInput();

            // Set up the UI
            InputApiSelector.SelectedIndex = 0; // MergedInput by default
            GameCategorySelector.SelectedIndex = 0; // All games by default
            
            // Load the game list
            LoadGameList();
            
            _isLoading = false;
            RefreshProfilesList();
        }

        private void LoadGameList()
        {
            // Preserve current selection across rebuilds (search, category or API changes)
            var previouslySelected = new HashSet<GameProfile>(_filteredGames.Where(g => g.IsSelected).Select(g => g.Profile));

            _filteredGames.Clear();
            string searchText = SearchBox.Text?.ToLower() ?? "";
            string category = ((GameCategorySelector.SelectedItem as ComboBoxItem)?.Content as string) ?? TeknoParrotUi.Properties.Resources.MultiGameButtonConfigAllGamesCategory;

            foreach (var profile in _allGameProfiles)
            {
                // Apply filtering
                bool matchesSearch = string.IsNullOrEmpty(searchText) || 
                                    profile.GameNameInternal.ToLower().Contains(searchText);

                bool matchesCategory = category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigAllGamesCategory || 
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigRacingGamesCategory && IsRacingGame(profile)) ||
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigShootingGamesCategory && IsShootingGame(profile)) ||
                                     (category == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigArcadeGamesCategory && IsArcadeGame(profile));

                // In a specific API mode, only show games that actually support that API.
                // MergedInput shows everything since bindings are filtered per game on apply.
                bool matchesApi = _currentInputApi == InputApi.MergedInput ||
                                  GetSupportedApis(profile).Contains(_currentInputApi);

                if (matchesSearch && matchesCategory && matchesApi)
                {
                    _filteredGames.Add(new GameViewModel
                    {
                        Profile = profile,
                        GameName = profile.GameNameInternal, // Use GameNameInternal here
                        IsSelected = previouslySelected.Contains(profile)
                    });
                }
            }

            GameListView.ItemsSource = null;
            GameListView.ItemsSource = _filteredGames;
            UpdateButtonConfiguration();
        }

        private bool IsRacingGame(GameProfile profile)
        {
            // Determine if the game is a racing game based on profile characteristics
            return profile.JoystickButtons.Any(b => 
                b.InputMapping == InputMapping.Analog0 || // Gas
                b.InputMapping == InputMapping.Analog2);
        }

        private bool IsShootingGame(GameProfile profile)
        {
            // Determine if the game is a shooting game based on profile characteristics
            return profile.JoystickButtons.Any(b => 
                b.InputMapping == InputMapping.P1LightGun || 
                b.InputMapping == InputMapping.P2LightGun);
        }

        private bool IsArcadeGame(GameProfile profile)
        {
            // Default for other arcade games that aren't racing or shooting
            return !IsRacingGame(profile) && !IsShootingGame(profile);
        }

        private void UpdateButtonConfiguration()
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            
            if (!selectedGames.Any())
            {
                ButtonConfigPanel.ItemsSource = null;
                StatusText.Text = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelected;
                return;
            }

            // Get all unique buttons across selected games with availability info
            var buttonViewModels = GenerateButtonViewModels(selectedGames.Select(g => g.Profile).ToList());

            // Always show all buttons now
            ButtonConfigPanel.ItemsSource = buttonViewModels;

            // Update status text
            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigGamesSelectedControlsShown, selectedGames.Count, buttonViewModels.Count);
        }

        private List<ButtonViewModel> GenerateButtonViewModels(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<ButtonViewModel>();

            // Dictionary to store unique buttons by InputMapping
            var uniqueButtons = GetAllUniqueButtons(selectedProfiles);
            var buttonViewModels = new List<ButtonViewModel>();
            
            // For each unique button, calculate in how many games it appears
            foreach (var button in uniqueButtons)
            {
                // Collect each game's own name for this mapping (e.g. "Button 1" vs "Light Punch")
                var perGame = selectedProfiles
                    .Select(p => new { GameName = p.GameNameInternal, GameButton = p.JoystickButtons.FirstOrDefault(b => b.InputMapping == button.InputMapping) })
                    .Where(x => x.GameButton != null)
                    .ToList();

                // Show a tooltip listing the differing per-game names for this merged button
                string tooltip = null;
                if (perGame.Select(x => x.GameButton.ButtonName).Distinct().Count() > 1)
                {
                    tooltip = string.Join("\n", perGame
                        .GroupBy(x => x.GameButton.ButtonName)
                        .Select(g => $"{g.Key} — {string.Join(", ", g.Select(x => x.GameName))}"));
                }

                buttonViewModels.Add(new ButtonViewModel
                {
                    Button = button,
                    Availability = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUsedInGames, perGame.Count, selectedProfiles.Count),
                    MergedNamesTooltip = tooltip
                });
            }
            
            // Order by digital/analog type, then by name
            return buttonViewModels
                .OrderBy(b => IsAnalogButton(b.Button.InputMapping) ? 1 : 0) // Digital first (0), then analog (1)
                .ThenBy(b => b.ButtonName) // Then alphabetical by name
                .ToList();
        }

        // Helper function to determine if a button is analog
        private bool IsAnalogButton(InputMapping mapping)
        {
            string mappingName = mapping.ToString();
            
            // Check if it's an analog input
            if (mappingName.StartsWith("Analog") || 
                mappingName.Contains("Axis") || 
                mappingName.EndsWith("Positive") || 
                mappingName.EndsWith("Negative") ||
                mappingName.Contains("Throttle") ||
                mappingName.Contains("Brake"))
            {
                return true;
            }
            
            return false;
        }

        private List<JoystickButtons> FindCommonButtons(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<JoystickButtons>();

            // Get the first profile's buttons
            var commonButtons = new List<JoystickButtons>(selectedProfiles.First().JoystickButtons);
            
            // For all other profiles, keep only the buttons that match by InputMapping
            foreach (var profile in selectedProfiles.Skip(1))
            {
                commonButtons = commonButtons
                    .Where(button => profile.JoystickButtons.Any(b => b.InputMapping == button.InputMapping))
                    .ToList();
            }

            return commonButtons;
        }

        private List<JoystickButtons> GetAllUniqueButtons(List<GameProfile> selectedProfiles)
        {
            if (!selectedProfiles.Any())
                return new List<JoystickButtons>();

            // Dictionary to store unique buttons by InputMapping
            Dictionary<InputMapping, JoystickButtons> uniqueButtons = new Dictionary<InputMapping, JoystickButtons>();
            
            // Gather all buttons from all profiles, keeping only one instance of each mapping
            foreach (var profile in selectedProfiles)
            {
                foreach (var button in profile.JoystickButtons)
                {
                    // If we haven't seen this InputMapping yet, add it to our dictionary
                    if (!uniqueButtons.ContainsKey(button.InputMapping))
                    {
                        // Create a clone of the button so we don't modify the original
                        var buttonClone = new JoystickButtons
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
                        UpdateBindNameForCurrentApi(buttonClone);
                        uniqueButtons[button.InputMapping] = buttonClone;
                    }
                }
            }
            
            // Group buttons by logical categories for better organization
            var groupedButtons = uniqueButtons.Values.ToList();
            
            // Sort buttons by InputMapping to ensure consistent ordering
            return groupedButtons.OrderBy(b => b.InputMapping.ToString()).ToList();
        }

        // Replace the StartListening and related methods with this implementation:

        /// <summary>
        /// Builds a combined display name for MergedInput mode showing all configured bindings.
        /// </summary>
        private static string BuildMergedBindName(string xiName, string diName, string riName = null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(xiName)) parts.Add($"XI: {xiName}");
            if (!string.IsNullOrEmpty(diName)) parts.Add($"DI: {diName}");
            if (!string.IsNullOrEmpty(riName)) parts.Add($"RI: {riName}");
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Updates the display BindName of a button based on the currently selected input API.
        /// Never destroys the underlying per-API bindings.
        /// </summary>
        private void UpdateBindNameForCurrentApi(JoystickButtons button)
        {
            switch (_currentInputApi)
            {
                case InputApi.DirectInput:
                    button.BindName = button.BindNameDi;
                    break;
                case InputApi.XInput:
                    button.BindName = button.BindNameXi;
                    break;
                case InputApi.RawInput:
                case InputApi.RawInputTrackball:
                    button.BindName = button.BindNameRi;
                    break;
                case InputApi.MergedInput:
                    button.BindName = BuildMergedBindName(button.BindNameXi, button.BindNameDi, button.BindNameRi);
                    break;
            }
        }

        /// <summary>
        /// Returns the set of input APIs a game can actually read, based on the
        /// "Input API" ConfigValue FieldOptions in its profile (same source the runtime
        /// InputListener uses). Legacy profiles without the field only read DirectInput.
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

        /// <summary>
        /// The underlying APIs that the currently selected UI mode edits.
        /// </summary>
        private HashSet<InputApi> GetApisForCurrentMode()
        {
            if (_currentInputApi == InputApi.MergedInput)
                return new HashSet<InputApi> { InputApi.DirectInput, InputApi.XInput, InputApi.RawInput, InputApi.RawInputTrackball };

            return new HashSet<InputApi> { _currentInputApi };
        }

        /// <summary>
        /// Copies only the bindings for the given APIs from source to target, leaving
        /// bindings of other APIs on the target untouched. Returns true if anything changed.
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
        /// Sets the game's "Input API" setting to match the mode the bindings were made in,
        /// so the applied bindings are actually read in-game. In MergedInput mode the runtime
        /// listens on all APIs the game supports, so nothing can go dead.
        /// </summary>
        private void SetGameInputApi(GameProfile profile)
        {
            var field = profile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
            if (field == null)
                return; // Game has a fixed input API, nothing to set

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

        private void StartListening()
        {
            StopListening();
            _isListening = true;

            switch (_currentInputApi)
            {
                case InputApi.DirectInput:
                    _joystickControlDirectInput = new JoystickControlDirectInput();
                    _joystickControlDirectInput.Listen();
                    break;
                case InputApi.XInput:
                    _joystickControlXInput = new JoystickControlXInput();
                    _joystickControlXInput.Listen();
                    break;
                case InputApi.RawInput:
                case InputApi.RawInputTrackball:
                    _joystickControlRawInput = new JoystickControlRawInput();
                    _joystickControlRawInput.Listen();
                    break;
                case InputApi.MergedInput:
                    // Only listen on APIs that at least one selected game can actually read,
                    // so e.g. RawInput can't steal a binding for games that never use it.
                    var supportedApis = new HashSet<InputApi>();
                    foreach (var game in _filteredGames.Where(g => g.IsSelected))
                        supportedApis.UnionWith(GetSupportedApis(game.Profile));

                    if (supportedApis.Contains(InputApi.XInput))
                    {
                        _joystickControlXInput = new JoystickControlXInput();
                        _joystickControlXInput.Listen();
                    }

                    if (supportedApis.Contains(InputApi.DirectInput))
                    {
                        // Exclude XInput controllers from DirectInput to avoid double-detection
                        _joystickControlDirectInput = new JoystickControlDirectInput();
                        var xinputGuids = TeknoParrotUi.Common.InputListening.XInputDeviceHelper.GetXInputDeviceGuids();
                        _joystickControlDirectInput.SetExcludedGuids(xinputGuids);
                        _joystickControlDirectInput.Listen();
                    }

                    if (supportedApis.Contains(InputApi.RawInput) || supportedApis.Contains(InputApi.RawInputTrackball))
                    {
                        // If DirectInput is also listening, capture only mice via RawInput so keyboard
                        // presses deterministically become DirectInput bindings (readable by all games,
                        // including RawInput games running in MergedInput mode).
                        _joystickControlRawInput = new JoystickControlRawInput();
                        _joystickControlRawInput.Listen(registerKeyboard: !supportedApis.Contains(InputApi.DirectInput));
                    }
                    break;
            }
        }

        private void StopListening()
        {
            _isListening = false;
            
            _joystickControlDirectInput?.StopListening();
            _joystickControlXInput?.StopListening();
            _joystickControlRawInput?.StopListening();
            
            if (_inputListener != null && _inputListener.IsAlive)
            {
                _inputListener.Join(100);
            }
            
            _inputListener = null;
        }

        // Remove these methods completely since they're not needed:
        // - CheckDirectInputState
        // - CheckXInputState
        // - CheckRawInputState

        // Replace CleanUp method to remove the event handler cleanup
        private void CleanUp()
        {
            StopListening();
        }

        private const string PROFILES_DIRECTORY = "UserProfiles\\Profiles";

        private void RefreshProfilesList()
        {
            // Create the profiles directory if it doesn't exist
            Directory.CreateDirectory(PROFILES_DIRECTORY);
            
            // Get all profile directories
            var profiles = Directory.GetDirectories(PROFILES_DIRECTORY)
                                   .Select(Path.GetFileName)
                                   .ToList();
            
            ProfilesComboBox.ItemsSource = profiles;
            
            if (profiles.Count > 0)
            {
                ProfilesComboBox.SelectedIndex = 0;
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGame, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNameRequired, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNameRequiredTitle, MessageBoxButton.OK);
                return;
            }
            
            // Sanitize the profile name for file system
            profileName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            
            string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
            Directory.CreateDirectory(profileDir);
            
            int savedCount = 0;
            
            try
            {
                foreach (var game in selectedGames)
                {
                    // Create a copy of the game profile for saving
                    var gameProfileCopy = new GameProfile
                    {
                        ProfileName = game.Profile.ProfileName,
                        GameNameInternal = game.Profile.GameNameInternal,
                        JoystickButtons = new List<JoystickButtons>()
                    };
                    
                    // Copy all joystick buttons to ensure we save all input types
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        var buttonCopy = new JoystickButtons
                        {
                            ButtonName = button.ButtonName,
                            InputMapping = button.InputMapping,
                            BindName = button.BindName,
                            BindNameDi = button.BindNameDi,
                            BindNameXi = button.BindNameXi,
                            BindNameRi = button.BindNameRi,
                            DirectInputButton = button.DirectInputButton,
                            XInputButton = button.XInputButton,
                            RawInputButton = button.RawInputButton
                        };
                        
                        gameProfileCopy.JoystickButtons.Add(buttonCopy);
                    }
                    
                    string fileName = Path.Combine(profileDir, game.Profile.ProfileName + ".xml");
                    
                    // Serialize the profile
                    using (var writer = XmlWriter.Create(fileName, new XmlWriterSettings { Indent = true }))
                    {
                        var serializer = new XmlSerializer(typeof(GameProfile));
                        serializer.Serialize(writer, gameProfileCopy);
                    }
                    
                    savedCount++;
                }
                
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSuccessfullySavedProfile, savedCount, profileName),
                               TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileSaved, MessageBoxButton.OK);
                
                // Refresh profiles list
                RefreshProfilesList();
                ProfilesComboBox.Text = profileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorSavingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectAtLeastOneGameToLoad, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectValidProfile, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNotFound, MessageBoxButton.OK);
                return;
            }
            
            string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
            int loadedCount = 0;
            
            try
            {
                foreach (var game in selectedGames)
                {
                    string fileName = Path.Combine(profileDir, game.Profile.ProfileName + ".xml");
                    
                    if (!File.Exists(fileName))
                    {
                        continue; // Skip games that don't have saved profiles
                    }
                    
                    GameProfile savedProfile;
                    using (var reader = XmlReader.Create(fileName))
                    {
                        var serializer = new XmlSerializer(typeof(GameProfile));
                        savedProfile = (GameProfile)serializer.Deserialize(reader);
                    }
                    
                    // Apply the loaded configuration to the game,
                    // restricted to APIs the game can actually read
                    var gameApis = GetSupportedApis(game.Profile);
                    foreach (var savedButton in savedProfile.JoystickButtons)
                    {
                        var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == savedButton.InputMapping);
                        if (gameButton != null)
                        {
                            CopyBindingsForApis(savedButton, gameButton, gameApis);
                        }
                    }
                    
                    loadedCount++;
                }
                
                if (loadedCount > 0)
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSuccessfullyLoadedProfile, loadedCount, profileName),
                                  TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileLoaded, MessageBoxButton.OK);
                    
                    _hasUnsavedChanges = true; // Set flag after loading a profile
                    // Update the UI to show the loaded configuration
                    UpdateButtonConfiguration();
                }
                else
                {
                    MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoMatchingConfigurations,
                                  TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoConfigurationsFound, MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorLoadingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigLoadError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectValidProfileToDelete, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileNotFound, MessageBoxButton.OK);
                return;
            }
            
            if (MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmDeleteProfile, profileName),
                              TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmDelete, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
                    Directory.Delete(profileDir, true);
                    
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileDeleted, profileName), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigProfileDeletedTitle, MessageBoxButton.OK);
                    
                    // Refresh profiles list
                    RefreshProfilesList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorDeletingProfile, ex.Message), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigDeleteError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ProfilesComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Enter key to create a new profile
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SaveProfileButton_Click(sender, e);
            }
        }

        /// <summary>
        /// Applies pending changes to the selected games and serializes them,
        /// without navigating away from the dialog.
        /// </summary>
        private void SaveChangesInPlace()
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (selectedGames.Any())
            {
                ApplyChangesToSelectedGames();
                ApplyChangesToUserProfiles(selectedGames);
            }
            _hasUnsavedChanges = false;
        }

        #region Event Handlers

        private void InputApiSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            // Ignore the event caused by reverting a cancelled switch
            if (_revertingApiSelection)
            {
                _revertingApiSelection = false;
                return;
            }

            // Ask about unsaved changes before switching input mode
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChangesSwitchApi,
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChangesTitle,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    // Stay on the previous input mode
                    if (e.RemovedItems.Count > 0)
                    {
                        _revertingApiSelection = true;
                        InputApiSelector.SelectedItem = e.RemovedItems[0];
                    }
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    SaveChangesInPlace();
                }
                // No = switch without saving; pending changes stay in memory
            }

            // First, stop any active input listening
            StopListening();

            // Update the input API from the item's Tag (locale-independent enum name)
            var selectedItem = (ComboBoxItem)InputApiSelector.SelectedItem;
            string apiString = selectedItem.Tag as string ?? selectedItem.Content.ToString();
            _currentInputApi = (InputApi)Enum.Parse(typeof(InputApi), apiString);

            // Clean up and recreate input control instances
            _joystickControlDirectInput?.StopListening();
            _joystickControlXInput?.StopListening();
            _joystickControlRawInput?.StopListening();
            
            _joystickControlDirectInput = new JoystickControlDirectInput();
            _joystickControlXInput = new JoystickControlXInput();
            _joystickControlRawInput = new JoystickControlRawInput();
            
            // Update button bindings for display
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            foreach (var game in selectedGames)
            {
                foreach (var button in game.Profile.JoystickButtons)
                {
                    UpdateBindNameForCurrentApi(button);
                }
            }

            // Rebuild the game list (specific API modes only show games that support that API)
            // and the button viewmodels
            int previouslySelectedCount = selectedGames.Count;
            LoadGameList();

            // Update status message, warning if some previously selected games were dropped
            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSwitchedToMode, selectedItem.Content);
            if (_currentInputApi != InputApi.MergedInput && previouslySelectedCount > 0)
            {
                int stillSelected = _filteredGames.Count(g => g.IsSelected);
                if (stillSelected < previouslySelectedCount)
                    StatusText.Text += " — " + string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigApiSupportCount, stillSelected, previouslySelectedCount);
            }
        }

        private void GameCategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            LoadGameList();
        }

        private void CommonButtonsOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            LoadGameList();
        }

        private void GameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var game in _filteredGames)
            {
                game.IsSelected = true;
            }
            UpdateButtonConfiguration();
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var game in _filteredGames)
            {
                game.IsSelected = false;
            }
            UpdateButtonConfiguration();
        }

        private void GameCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateButtonConfiguration();
        }

        /// <summary>
        /// Mappings that are bound to a pointer device (mouse/gun) rather than a button press.
        /// These get a device selection dropdown instead of a capture textbox, matching JoystickControl.
        /// </summary>
        private static bool IsDeviceMapping(InputMapping mapping)
        {
            return mapping == InputMapping.P1LightGun || mapping == InputMapping.P2LightGun ||
                   mapping == InputMapping.P3LightGun || mapping == InputMapping.P4LightGun ||
                   mapping == InputMapping.P1Trackball || mapping == InputMapping.P2Trackball;
        }

        private void ConfigTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            var button = txtBox?.Tag as JoystickButtons;
            if (button == null) return;

            // Light gun / trackball mappings use the device dropdown instead
            txtBox.Visibility = IsDeviceMapping(button.InputMapping) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DeviceComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var button = comboBox?.Tag as JoystickButtons;
            if (button == null) return;

            if (!IsDeviceMapping(button.InputMapping))
            {
                comboBox.Visibility = Visibility.Collapsed;
                return;
            }

            // Keep these strings hardcoded since they get saved to configuration (same as JoystickControl)
            var deviceList = new List<string> { "None", "Windows Mouse Cursor", "Unknown Device" };
            if (_joystickControlRawInput == null)
                _joystickControlRawInput = new JoystickControlRawInput();
            deviceList.AddRange(_joystickControlRawInput.GetMouseDeviceList());

            // Add current selection even if the device isn't currently available
            if (!string.IsNullOrEmpty(button.BindNameRi) && !deviceList.Contains(button.BindNameRi))
                deviceList.Add(button.BindNameRi);

            // Temporarily remove event to prevent triggering it while populating
            comboBox.SelectionChanged -= DeviceComboBox_SelectionChanged;
            comboBox.ItemsSource = deviceList;
            comboBox.SelectedItem = string.IsNullOrEmpty(button.BindNameRi) ? "None" : button.BindNameRi;
            comboBox.SelectionChanged += DeviceComboBox_SelectionChanged;

            comboBox.Visibility = Visibility.Visible;
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var button = comboBox?.Tag as JoystickButtons;
            if (button == null || comboBox.SelectedValue == null) return;

            var selectedDeviceName = comboBox.SelectedValue.ToString();
            var selectedDevice = _joystickControlRawInput.GetMouseDeviceByName(selectedDeviceName);
            string path;
            var type = Common.RawDeviceType.Mouse;

            // Keep these strings hardcoded since they get saved to configuration (same as JoystickControl)
            if (selectedDeviceName == "Windows Mouse Cursor")
            {
                path = "Windows Mouse Cursor";
            }
            else if (selectedDeviceName == "None")
            {
                path = "None";
                type = Common.RawDeviceType.None;
            }
            else if (selectedDeviceName == "Unknown Device")
            {
                path = "null";
            }
            else if (selectedDevice == null)
            {
                MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.JoystickControlDeviceNotAvailable);
                return;
            }
            else
            {
                path = selectedDevice.DevicePath;
            }

            button.RawInputButton = new RawInputButton
            {
                DevicePath = path,
                DeviceType = type,
                MouseButton = RawMouseButton.None,
                KeyboardKey = Keys.None
            };
            button.BindNameRi = selectedDeviceName;
            UpdateBindNameForCurrentApi(button);
            _hasUnsavedChanges = true;
        }

        private void ConfigTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;

            // Store original text in our dictionary instead of Tag
            _originalTexts[txtBox] = txtBox.Text;
            
            // Make the textbox read-only to prevent manual typing
            txtBox.IsReadOnly = true;
            txtBox.Text = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton;
            _lastActiveTextBox = txtBox;
            
            // Start listening for input
            StartListening();
        }

        private void ConfigTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;

            // Stop listening first to prevent further input
            StopListening();

            // If the user didn't press anything (text still shows prompt)
            if (txtBox.Text == TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton)
            {
                // Restore original text from our dictionary
                if (_originalTexts.ContainsKey(txtBox))
                {
                    txtBox.Text = _originalTexts[txtBox];
                    _originalTexts.Remove(txtBox);
                }
                else
                {
                    // Fallback to binding name
                    var buttonViewModel = txtBox.DataContext as ButtonViewModel;
                    if (buttonViewModel != null)
                    {
                        txtBox.Text = buttonViewModel.BindName;
                    }
                }
            }
            else if (txtBox.Text != TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPressAButton)
            {
                // Text was set by the joystick control, not by manual typing
                var buttonViewModel = txtBox.DataContext as ButtonViewModel;
                if (buttonViewModel != null)
                {
                    var text = txtBox.Text;
                    
                    // Update the appropriate binding
                    switch (_currentInputApi)
                    {
                        case InputApi.DirectInput:
                            buttonViewModel.Button.BindNameDi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                        case InputApi.XInput:
                            buttonViewModel.Button.BindNameXi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                        case InputApi.RawInput:
                        case InputApi.RawInputTrackball:
                            buttonViewModel.Button.BindNameRi = text;
                            buttonViewModel.Button.BindName = text;
                            break;
                        case InputApi.MergedInput:
                            // The listener that captured the input already wrote the correct
                            // per-API BindName* field via the TextBox Tag. Rebuild the merged display.
                            UpdateBindNameForCurrentApi(buttonViewModel.Button);
                            txtBox.Text = buttonViewModel.Button.BindName;
                            break;
                    }
                }
                
                // Remove the entry from our dictionary
                if (_originalTexts.ContainsKey(txtBox))
                {
                    _originalTexts.Remove(txtBox);
                }

                // After updating the binding
                _hasUnsavedChanges = true;
            }

            // Make the textbox editable again
            txtBox.IsReadOnly = false;
            _lastActiveTextBox = null;
        }

        private void ConfigTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;
            
            // If we're in binding mode (shown by read-only status)
            if (txtBox.IsReadOnly)
            {
                // Allow ESC key to cancel binding
                if (e.Key == Key.Escape)
                {
                    // Restore original text from our dictionary
                    if (_originalTexts.ContainsKey(txtBox))
                    {
                        txtBox.Text = _originalTexts[txtBox];
                        _originalTexts.Remove(txtBox);
                    }
                    
                    txtBox.IsReadOnly = false;
                    StopListening();
                    
                    // Move focus away from textbox to complete the cancellation
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(txtBox), null);
                    Keyboard.ClearFocus();
                }
                
                // Block all other keyboard input during binding
                e.Handled = true;
            }
        }

        private void ApplyToSelectedGames_Click(object sender, RoutedEventArgs e)
        {
            ApplyChangesToSelectedGames();
            _hasUnsavedChanges = true; // Set unsaved changes flag after applying
        }

        private void CopyFromGame_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (selectedGames.Count != 1)
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectExactlyOneGame, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectionError, MessageBoxButton.OK);
                return;
            }

            // Show a dialog to select which game to copy from
            var window = new Window
            {
                Title = TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSelectGameToCopyFrom,
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var panel = new DockPanel();
            var listBox = new ListBox
            {
                ItemsSource = _allGameProfiles.Select(p => p.GameNameInternal), // Use GameNameInternal here
                Margin = new Thickness(10)
            };
            
            DockPanel.SetDock(listBox, Dock.Top);
            panel.Children.Add(listBox);

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10) 
            };
            
            var cancelBtn = new Button { Content = TeknoParrotUi.Properties.Resources.Cancel, Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            cancelBtn.Click += (s, args) => window.DialogResult = false;
            
            var selectBtn = new Button { Content = "Select", Width = 80 };
            selectBtn.Click += (s, args) => 
            {
                if (listBox.SelectedItem != null)
                {
                    window.Tag = listBox.SelectedItem;
                    window.DialogResult = true;
                }
            };
            
            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(selectBtn);
            
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            panel.Children.Add(buttonPanel);
            
            window.Content = panel;
            
            if (window.ShowDialog() == true && window.Tag is string selectedGameName)
            {
                var sourceProfile = _allGameProfiles.FirstOrDefault(p => p.GameNameInternal == selectedGameName);
                var targetProfile = selectedGames[0].Profile;

                if (sourceProfile != null)
                {
                    // Copy button configurations that match by InputMapping.
                    // Only copy APIs the target game can actually read.
                    var targetApis = GetSupportedApis(targetProfile);
                    foreach (var sourceButton in sourceProfile.JoystickButtons)
                    {
                        var targetButton = targetProfile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                        if (targetButton != null)
                        {
                            CopyBindingsForApis(sourceButton, targetButton, targetApis);
                        }
                    }

                    StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationCopied, sourceProfile.GameNameInternal); // Use GameNameInternal here
                    _hasUnsavedChanges = true;
                    UpdateButtonConfiguration();
                }
            }
        }

        private void ResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGameToReset, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoSelection, MessageBoxButton.OK);
                return;
            }

            if (MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmResetConfiguration, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigConfirmReset, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var game in selectedGames)
                {
                    // Reset all button bindings for ALL input APIs to a clean state
                    foreach (var button in game.Profile.JoystickButtons)
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

                StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationReset, selectedGames.Count);
                _hasUnsavedChanges = true;
                UpdateButtonConfiguration();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // First, explicitly apply changes just like the Apply button does
            ApplyChangesToSelectedGames();
            
            // Save all changed profiles
            var modifiedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            int savedCount = 0;
            
            try
            {
                foreach (var game in modifiedGames)
                {
                    // Add debug information
                    Console.WriteLine($"Saving profile for {game.GameName}");
                    Console.WriteLine($"Profile has {game.Profile.JoystickButtons.Count} buttons");
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        Console.WriteLine($"Button: {button.ButtonName}, Binding: {button.BindName}");
                    }
                    
                    JoystickHelper.SerializeGameProfile(game.Profile);
                    savedCount++;
                }

                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesSaved, savedCount), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveSuccessful, MessageBoxButton.OK);
                _hasUnsavedChanges = false; // Clear the unsaved changes flag
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorSavingProfiles, ex.Message, ex.StackTrace), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            CleanUp();
            
            // Use the same navigation logic as BtnGoBack
            if (LaunchedFromSetupWizard && SetupWizardInstance != null)
            {
                LaunchedFromSetupWizard = false;
                SetupWizardInstance.ReturnFromButtonConfig();
            }
            else
            {
                // Original code to return to library
                _contentControl.Content = _library;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if there are unsaved changes before exiting
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChanges,
                    TeknoParrotUi.Properties.Resources.MultiGameButtonConfigUnsavedChangesTitle,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    // Save and exit
                    SaveButton_Click(sender, e);
                    return; // SaveButton_Click already handles exiting
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // Stay on the current screen
                    return;
                }
                // If No, continue with exiting without saving
            }
            
            // Discard changes, clean up, and return to library
            CleanUp();
            _contentControl.Content = _library;
        }

        private void ApplyChangesToUserProfiles(List<GameViewModel> games)
        {
            foreach (var game in games)
            {
                try
                {
                    JoystickHelper.SerializeGameProfile(game.Profile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save profile {game.GameName}: {ex.Message}");
                }
            }
        }

        private void ApplyToUserProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectAtLeastOneGameToApply, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoGamesSelectedTitle, MessageBoxButton.OK);
                return;
            }
            
            try
            {
                ApplyChangesToUserProfiles(selectedGames);
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesApplied, selectedGames.Count), TeknoParrotUi.Properties.Resources.MultiGameButtonConfigChangesAppliedTitle, MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigErrorApplyingChanges, ex.Message), TeknoParrotUi.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Extract the apply logic into a separate method to reuse in both Apply and Save buttons
        private void ApplyChangesToSelectedGames()
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            var buttonViewModels = ButtonConfigPanel.ItemsSource as List<ButtonViewModel>;
            
            if (!selectedGames.Any() || buttonViewModels == null || !buttonViewModels.Any())
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigPleaseSelectGamesAndConfigureButton, TeknoParrotUi.Properties.Resources.MultiGameButtonConfigNoSelection, MessageBoxButton.OK);
                return;
            }

            // Apply the current button configuration to all selected games
            int totalChanges = 0;
            int skippedGames = 0;
            var modeApis = GetApisForCurrentMode();

            foreach (var game in selectedGames)
            {
                int gameChanges = 0;

                // Only apply bindings for APIs this game can actually read,
                // further restricted to the APIs the current UI mode edits.
                var applicableApis = GetSupportedApis(game.Profile);
                applicableApis.IntersectWith(modeApis);

                if (applicableApis.Count == 0)
                {
                    // This game doesn't support the selected input API at all - never write dead bindings
                    skippedGames++;
                    continue;
                }

                foreach (var buttonViewModel in buttonViewModels)
                {
                    var sourceButton = buttonViewModel.Button;
                    
                    // Find the matching button in this specific game, if any
                    var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                    
                    // Skip if this game doesn't have this button
                    if (gameButton == null) continue;

                    if (CopyBindingsForApis(sourceButton, gameButton, applicableApis))
                        gameChanges++;
                }

                // Make sure the game will actually read the bindings we just applied
                SetGameInputApi(game.Profile);

                totalChanges += gameChanges;
            }

            StatusText.Text = string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigButtonConfigurationApplied, totalChanges, selectedGames.Count - skippedGames);
            if (skippedGames > 0)
                StatusText.Text += " — " + string.Format(TeknoParrotUi.Properties.Resources.MultiGameButtonConfigGamesSkippedUnsupportedApi, skippedGames);
        }

        // Add these static properties at the class level
        public static bool LaunchedFromSetupWizard { get; set; } = false;
        public static Views.SetupWizard SetupWizardInstance { get; set; } = null;

        // Modify the Go Back button click handler to check if we should return to setup wizard
        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            // If we were launched from the setup wizard, return to it
            if (LaunchedFromSetupWizard && SetupWizardInstance != null)
            {
                LaunchedFromSetupWizard = false;
                SetupWizardInstance.ReturnFromButtonConfig();
            }
            else
            {
                // Original code to return to library
                _contentControl.Content = _library;
            }
        }

        #endregion
    }
}