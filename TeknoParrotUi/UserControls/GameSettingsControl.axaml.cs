using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for GameSettingsControl.axaml
    /// </summary>
    public partial class GameSettingsControl : UserControl
    {
        private GameProfile _gameProfile;
        private ListBoxItem _comboItem;
        private ContentControl _contentControl;
        private Library _library;
        private InputApi _inputApi = InputApi.DirectInput;

        public GameSettingsControl()
        {
            InitializeComponent();
            DataContext = this; // Make sure this is set
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Get control references
            GameExecutableText = this.FindControl<TextBlock>("GameExecutableText");
            GamePathBox = this.FindControl<TextBox>("GamePathBox");
            GameExecutable2Text = this.FindControl<TextBlock>("GameExecutable2Text");
            GamePathBox2 = this.FindControl<TextBox>("GamePathBox2");
            GameSettingsList = this.FindControl<ItemsControl>("GameSettingsList");
        }

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl, Library library)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;

            GamePathBox.Text = _gameProfile.GamePath;
            GamePathBox2.Text = _gameProfile.GamePath2;

            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            _contentControl = contentControl;
            _library = library;

            string exeName = "";

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName))
                exeName = $" ({_gameProfile.ExecutableName})".Replace(";", " or ");

            GameExecutableText.Text = $"Game Executable{exeName}:";

            if (_gameProfile.HasTwoExecutables)
            {
                exeName = "";
                if (!string.IsNullOrEmpty(_gameProfile.ExecutableName2))
                    exeName = $" ({_gameProfile.ExecutableName2})";

                GameExecutable2Text.Text = $"Game Executable 2{exeName}:";
                GameExecutable2Text.IsVisible = true;
                GamePathBox2.IsVisible = true;
            }
            else
            {
                GameExecutable2Text.IsVisible = false;
                GamePathBox2.IsVisible = false;
            }
        }

        private void SelectExecutableForTextBox(object sender, PointerPressedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = Properties.Resources.GameSettingsSelectGameExecutable,
                    AllowMultiple = false

                };

                if (_gameProfile.ExecutableName != null)
                {
                    if (_gameProfile.ExecutableName.Contains(";"))
                    {
                        string[] exes = _gameProfile.ExecutableName.Split(';');
                        var exeBuilder = new System.Text.StringBuilder();
                        foreach (var exe in exes)
                        {
                            exeBuilder.Append($"*.{exe};");
                        }

                        string filter = exeBuilder.ToString();
                        dialog.Filters.Add(new FileDialogFilter
                        {
                            Name = "Executable",
                            Extensions = { filter.TrimEnd(';') }
                        });
                    }
                    else
                    {
                        dialog.Filters.Add(new FileDialogFilter
                        {
                            Name = "Executable",
                            Extensions = { _gameProfile.ExecutableName }
                        });
                    }
                }

                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    var result = dialog.ShowAsync(window).GetAwaiter().GetResult();

                    if (result != null && result.Any())
                    {
                        GamePathBox.Text = result[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting executable: {ex.Message}");
            }
        }

        private void SelectExecutable2ForTextBox(object sender, PointerPressedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = Properties.Resources.GameSettingsSelectGameExecutable,
                    AllowMultiple = false
                };

                if (_gameProfile.ExecutableName2 != null)
                {
                    if (_gameProfile.ExecutableName2.Contains(";"))
                    {
                        string[] exes = _gameProfile.ExecutableName2.Split(';');
                        var exeBuilder = new System.Text.StringBuilder();
                        foreach (var exe in exes)
                        {
                            exeBuilder.Append($"*.{exe};");
                        }

                        string filter = exeBuilder.ToString();
                        dialog.Filters.Add(new FileDialogFilter
                        {
                            Name = "Executable",
                            Extensions = { filter.TrimEnd(';') }
                        });
                    }
                    else
                    {
                        dialog.Filters.Add(new FileDialogFilter
                        {
                            Name = "Executable",
                            Extensions = { _gameProfile.ExecutableName2 }
                        });
                    }
                }

                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    var result = dialog.ShowAsync(window).GetAwaiter().GetResult();

                    if (result != null && result.Any())
                    {
                        GamePathBox2.Text = result[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting second executable: {ex.Message}");
            }
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            _gameProfile.GamePath = GamePathBox.Text;
            _gameProfile.GamePath2 = GamePathBox2.Text;

            foreach (var t in _gameProfile.JoystickButtons)
            {
                // if (_inputApi == InputApi.DirectInput)
                //     t.BindName = t.BindNameDi;
                // else if (_inputApi == InputApi.XInput)
                //     t.BindName = t.BindNameXi;
                // else if (_inputApi == InputApi.RawInput)
                //     t.BindName = t.BindNameRi;
            }

            JoystickHelper.SerializeGameProfile(_gameProfile);
            _contentControl.Content = _library;
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }
    }
}