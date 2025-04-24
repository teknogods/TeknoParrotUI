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

namespace TeknoParrotUi.UserControls
{
    public partial class MultiGameButtonConfig : UserControl, INotifyPropertyChanged
    {
        private readonly ContentControl _contentControl;
        private readonly List<GameProfile> _allGameProfiles;
        private readonly Library _library;
        private InputApi _currentInputApi = InputApi.DirectInput;
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
        private Timer _inputCheckTimer;

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
            InputApiSelector.SelectedIndex = 0; // DirectInput by default
            GameCategorySelector.SelectedIndex = 0; // All games by default
            
            // Load the game list
            LoadGameList();
            
            _isLoading = false;
            RefreshProfilesList();
        }

        private void LoadGameList()
        {
            _filteredGames.Clear();
            string searchText = SearchBox.Text?.ToLower() ?? "";
            string category = ((GameCategorySelector.SelectedItem as ComboBoxItem)?.Content as string) ?? "All Games";

            foreach (var profile in _allGameProfiles)
            {
                // Apply filtering
                bool matchesSearch = string.IsNullOrEmpty(searchText) || 
                                    profile.GameNameInternal.ToLower().Contains(searchText);

                bool matchesCategory = category == "All Games" || 
                                     (category == "Racing Games" && IsRacingGame(profile)) ||
                                     (category == "Shooting Games" && IsShootingGame(profile)) ||
                                     (category == "Arcade Games" && IsArcadeGame(profile));

                if (matchesSearch && matchesCategory)
                {
                    _filteredGames.Add(new GameViewModel
                    {
                        Profile = profile,
                        GameName = profile.GameNameInternal, // Use GameNameInternal here
                        IsSelected = false
                    });
                }
            }

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
                StatusText.Text = "No games selected";
                return;
            }

            // Get all unique buttons across selected games with availability info
            var buttonViewModels = GenerateButtonViewModels(selectedGames.Select(g => g.Profile).ToList());

            // Always show all buttons now
            ButtonConfigPanel.ItemsSource = buttonViewModels;

            // Update status text
            StatusText.Text = $"{selectedGames.Count} games selected, {buttonViewModels.Count} unique controls shown";
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
                int count = selectedProfiles.Count(p => 
                    p.JoystickButtons.Any(b => b.InputMapping == button.InputMapping));
                    
                buttonViewModels.Add(new ButtonViewModel
                {
                    Button = button,
                    Availability = $"Used in {count} of {selectedProfiles.Count} selected games"
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
                MessageBox.Show("Please select at least one game to save its configuration.", "No Games Selected", MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Please enter a profile name.", "Profile Name Required", MessageBoxButton.OK);
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
                
                MessageBox.Show($"Successfully saved controller configuration for {savedCount} games to profile '{profileName}'.", 
                               "Profile Saved", MessageBoxButton.OK);
                
                // Refresh profiles list
                RefreshProfilesList();
                ProfilesComboBox.Text = profileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profile: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show("Please select at least one game to load configuration for.", "No Games Selected", MessageBoxButton.OK);
                return;
            }
            
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                MessageBox.Show("Please select a valid profile to load.", "Profile Not Found", MessageBoxButton.OK);
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
                    
                    // Apply the loaded configuration to the game
                    foreach (var savedButton in savedProfile.JoystickButtons)
                    {
                        var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == savedButton.InputMapping);
                        if (gameButton != null)
                        {
                            // Copy all input types regardless of current input API
                            gameButton.DirectInputButton = savedButton.DirectInputButton;
                            gameButton.XInputButton = savedButton.XInputButton;
                            gameButton.RawInputButton = savedButton.RawInputButton;
                            gameButton.BindNameDi = savedButton.BindNameDi;
                            gameButton.BindNameXi = savedButton.BindNameXi;
                            gameButton.BindNameRi = savedButton.BindNameRi;
                            
                            // Update the current display binding based on current input API
                            switch (_currentInputApi)
                            {
                                case InputApi.DirectInput:
                                    gameButton.BindName = savedButton.BindNameDi;
                                    break;
                                case InputApi.XInput:
                                    gameButton.BindName = savedButton.BindNameXi;
                                    break;
                                case InputApi.RawInput:
                                case InputApi.RawInputTrackball:
                                    gameButton.BindName = savedButton.BindNameRi;
                                    break;
                            }
                        }
                    }
                    
                    loadedCount++;
                }
                
                if (loadedCount > 0)
                {
                    MessageBox.Show($"Successfully loaded controller configuration for {loadedCount} games from profile '{profileName}'.", 
                                  "Profile Loaded", MessageBoxButton.OK);
                    
                    // Update the UI to show the loaded configuration
                    UpdateButtonConfiguration();
                }
                else
                {
                    MessageBox.Show("No matching game configurations found in the selected profile.", 
                                  "No Configurations Found", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading profile: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string profileName = ProfilesComboBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(profileName) || !Directory.Exists(Path.Combine(PROFILES_DIRECTORY, profileName)))
            {
                MessageBox.Show("Please select a valid profile to delete.", "Profile Not Found", MessageBoxButton.OK);
                return;
            }
            
            if (MessageBox.Show($"Are you sure you want to delete the profile '{profileName}'? This cannot be undone.", 
                              "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    string profileDir = Path.Combine(PROFILES_DIRECTORY, profileName);
                    Directory.Delete(profileDir, true);
                    
                    MessageBox.Show($"Profile '{profileName}' has been deleted.", "Profile Deleted", MessageBoxButton.OK);
                    
                    // Refresh profiles list
                    RefreshProfilesList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting profile: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #region Event Handlers

        private void InputApiSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            // First, stop any active input listening
            StopListening();
            
            // Update the input API
            string apiString = ((ComboBoxItem)InputApiSelector.SelectedItem).Content.ToString();
            switch (apiString)
            {
                case "DirectInput":
                    _currentInputApi = InputApi.DirectInput;
                    break;
                case "XInput":
                    _currentInputApi = InputApi.XInput;
                    break;
                case "RawInput":
                    _currentInputApi = InputApi.RawInput;
                    break;
                case "RawInputTrackball":
                    _currentInputApi = InputApi.RawInputTrackball;
                    break;
            }

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
                    // Update the visible binding name based on the current input API
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
                    }
                }
            }
            
            // Rebuild the button viewmodels and update the UI
            UpdateButtonConfiguration();

            // Update status message
            StatusText.Text = $"Switched to {apiString} mode";
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

        private void ConfigTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var txtBox = sender as TextBox;
            if (txtBox == null) return;

            // Store original text in our dictionary instead of Tag
            _originalTexts[txtBox] = txtBox.Text;
            
            // Make the textbox read-only to prevent manual typing
            txtBox.IsReadOnly = true;
            txtBox.Text = "Press a button...";
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
            if (txtBox.Text == "Press a button...")
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
            else if (txtBox.Text != "Press a button...")
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
                    }
                }
                
                // Remove the entry from our dictionary
                if (_originalTexts.ContainsKey(txtBox))
                {
                    _originalTexts.Remove(txtBox);
                }
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
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            var buttonViewModels = ButtonConfigPanel.ItemsSource as List<ButtonViewModel>;
            
            if (!selectedGames.Any() || buttonViewModels == null || !buttonViewModels.Any())
            {
                MessageBox.Show("Please select games and configure at least one button.", "No Selection", MessageBoxButton.OK);
                return;
            }

            // Apply the current button configuration to all selected games
            int totalChanges = 0;
            
            foreach (var game in selectedGames)
            {
                int gameChanges = 0;
                
                foreach (var buttonViewModel in buttonViewModels)
                {
                    var sourceButton = buttonViewModel.Button;
                    
                    // Find the matching button in this specific game, if any
                    var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                    
                    // Skip if this game doesn't have this button
                    if (gameButton == null) continue;
                    
                    // Apply the binding based on the current input API
                    switch (_currentInputApi)
                    {
                        case InputApi.DirectInput:
                            if (gameButton.DirectInputButton != sourceButton.DirectInputButton || 
                                gameButton.BindNameDi != sourceButton.BindNameDi)
                            {
                                gameButton.DirectInputButton = sourceButton.DirectInputButton;
                                gameButton.BindNameDi = sourceButton.BindNameDi;
                                gameButton.BindName = sourceButton.BindNameDi;
                                gameChanges++;
                            }
                            break;
                        case InputApi.XInput:
                            if (gameButton.XInputButton != sourceButton.XInputButton ||
                                gameButton.BindNameXi != sourceButton.BindNameXi)
                            {
                                gameButton.XInputButton = sourceButton.XInputButton;
                                gameButton.BindNameXi = sourceButton.BindNameXi;
                                gameButton.BindName = sourceButton.BindNameXi;
                                gameChanges++;
                            }
                            break;
                        case InputApi.RawInput:
                        case InputApi.RawInputTrackball:
                            if (gameButton.RawInputButton != sourceButton.RawInputButton ||
                                gameButton.BindNameRi != sourceButton.BindNameRi)
                            {
                                gameButton.RawInputButton = sourceButton.RawInputButton;
                                gameButton.BindNameRi = sourceButton.BindNameRi;
                                gameButton.BindName = sourceButton.BindNameRi;
                                gameChanges++;
                            }
                            break;
                    }
                }
                
                totalChanges += gameChanges;
            }

            StatusText.Text = $"Button configuration applied: {totalChanges} changes across {selectedGames.Count} games";
        }

        private void CopyFromGame_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (selectedGames.Count != 1)
            {
                MessageBox.Show("Please select exactly one game to copy from.", "Selection Error", MessageBoxButton.OK);
                return;
            }

            // Show a dialog to select which game to copy from
            var window = new Window
            {
                Title = "Select Game to Copy From",
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
            
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
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
                    // Copy button configurations that match by InputMapping
                    foreach (var sourceButton in sourceProfile.JoystickButtons)
                    {
                        var targetButton = targetProfile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                        if (targetButton != null)
                        {
                            // Copy the binding based on the current input API
                            switch (_currentInputApi)
                            {
                                case InputApi.DirectInput:
                                    targetButton.DirectInputButton = sourceButton.DirectInputButton;
                                    targetButton.BindNameDi = sourceButton.BindNameDi;
                                    break;
                                case InputApi.XInput:
                                    targetButton.XInputButton = sourceButton.XInputButton;
                                    targetButton.BindNameXi = sourceButton.BindNameXi;
                                    break;
                                case InputApi.RawInput:
                                case InputApi.RawInputTrackball:
                                    targetButton.RawInputButton = sourceButton.RawInputButton;
                                    targetButton.BindNameRi = sourceButton.BindNameRi;
                                    break;
                            }
                            targetButton.BindName = sourceButton.BindName;
                        }
                    }

                    StatusText.Text = $"Button configuration copied from {sourceProfile.GameNameInternal}"; // Use GameNameInternal here
                    UpdateButtonConfiguration();
                }
            }
        }

        private void ResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            if (!selectedGames.Any())
            {
                MessageBox.Show("Please select at least one game to reset.", "No Selection", MessageBoxButton.OK);
                return;
            }

            if (MessageBox.Show("This will reset button configurations to default for the selected games. Continue?", "Confirm Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var game in selectedGames)
                {
                    // Reset all button bindings for the current input API
                    foreach (var button in game.Profile.JoystickButtons)
                    {
                        switch (_currentInputApi)
                        {
                            case InputApi.DirectInput:
                                button.DirectInputButton = null;
                                button.BindNameDi = "";
                                break;
                            case InputApi.XInput:
                                button.XInputButton = null;
                                button.BindNameXi = "";
                                break;
                            case InputApi.RawInput:
                            case InputApi.RawInputTrackball:
                                button.RawInputButton = null;
                                button.BindNameRi = "";
                                break;
                        }
                        button.BindName = "";
                    }
                }

                StatusText.Text = $"Button configuration reset for {selectedGames.Count} games";
                UpdateButtonConfiguration();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Apply any pending changes from the UI to the game profiles
            var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
            var buttonViewModels = ButtonConfigPanel.ItemsSource as List<ButtonViewModel>;
            
            // Apply any pending changes from the UI to the selected game profiles
            if (buttonViewModels != null && buttonViewModels.Any())
            {
                foreach (var game in selectedGames)
                {
                    foreach (var buttonViewModel in buttonViewModels)
                    {
                        var sourceButton = buttonViewModel.Button;
                        var gameButton = game.Profile.JoystickButtons.FirstOrDefault(b => b.InputMapping == sourceButton.InputMapping);
                        if (gameButton != null)
                        {
                            // Apply the binding based on the current input API
                            switch (_currentInputApi)
                            {
                                case InputApi.DirectInput:
                                    gameButton.DirectInputButton = sourceButton.DirectInputButton;
                                    gameButton.BindNameDi = sourceButton.BindNameDi;
                                    gameButton.BindName = sourceButton.BindNameDi;
                                    break;
                                case InputApi.XInput:
                                    gameButton.XInputButton = sourceButton.XInputButton;
                                    gameButton.BindNameXi = sourceButton.BindNameXi;
                                    gameButton.BindName = sourceButton.BindNameXi;
                                    break;
                                case InputApi.RawInput:
                                case InputApi.RawInputTrackball:
                                    gameButton.RawInputButton = sourceButton.RawInputButton;
                                    gameButton.BindNameRi = sourceButton.BindNameRi;
                                    gameButton.BindName = sourceButton.BindNameRi;
                                    break;
                            }
                        }
                    }
                }
            }

            // Save all changed profiles (original code continues here)
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

                MessageBox.Show($"Changes saved for {savedCount} games.", "Save Successful", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profiles: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            CleanUp();
            _contentControl.Content = _library;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
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
                MessageBox.Show("Please select at least one game to apply changes to.", "No Games Selected", MessageBoxButton.OK);
                return;
            }
            
            try
            {
                ApplyChangesToUserProfiles(selectedGames);
                MessageBox.Show($"Changes applied to {selectedGames.Count} user profiles.", "Changes Applied", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}