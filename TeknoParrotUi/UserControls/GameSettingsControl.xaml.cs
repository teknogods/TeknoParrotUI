using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for GameSettingsControl.xaml
    /// </summary>
    public partial class GameSettingsControl : UserControl
    {
        public GameSettingsControl()
        {
            InitializeComponent();
        }

        private GameProfile _gameProfile;
        private ListBoxItem _comboItem;
        private ContentControl _contentControl;
        private Library _library;
        private InputApi _inputApi = InputApi.DirectInput;

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
                exeName = $" ({_gameProfile.ExecutableName})".Replace(";", Properties.Resources.GameSettingsExecutableOr);

            GameExecutableText.Text = $"{Properties.Resources.GameSettingsGameExecutableLabel}{exeName}:";

            if (_gameProfile.HasTwoExecutables)
            {
                exeName = "";

                if (!string.IsNullOrEmpty(_gameProfile.ExecutableName2))
                    exeName = $" ({_gameProfile.ExecutableName2})".Replace(";", Properties.Resources.GameSettingsExecutableOr);

                GameExecutable2Text.Text = $"{Properties.Resources.GameSettingsSecondGameExecutableLabel}{exeName}:";

                GameExecutable2Text.Visibility = Visibility.Visible;
                GamePathBox2.Visibility = Visibility.Visible;
            }
            else
            {
                GameExecutable2Text.Visibility = Visibility.Collapsed;
                GamePathBox2.Visibility = Visibility.Collapsed;
            }
        }

        private void SelectExecutableForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = Properties.Resources.GameSettingsSelectGameExecutable
            };

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName))
            {
                string[] fileNames = _gameProfile.ExecutableName.Split('|');
                string allFilesFilter = string.Join(";", fileNames);

                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({allFilesFilter})|{allFilesFilter}|" +
                                        string.Join("|", fileNames.Select(name => $"{name}|{name}")) +
                                        $"|{Properties.Resources.GameSettingsAllFiles}|*.*";
            }

            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
            }
        }

        private void SelectExecutable2ForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = Properties.Resources.GameSettingsSelectGameExecutable
            };

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName2))
            {
                string[] fileNames = _gameProfile.ExecutableName2.Split('|');
                string allFilesFilter = string.Join(";", fileNames);

                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({allFilesFilter})|{allFilesFilter}|" +
                                        string.Join("|", fileNames.Select(name => $"{name}|{name}")) +
                                        $"|{Properties.Resources.GameSettingsAllFiles}|*.*";
            }

            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
            }
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            string inputApiString = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;

            if (inputApiString != null)
                _inputApi = (InputApi)Enum.Parse(typeof(InputApi), inputApiString);

            foreach (var t in _gameProfile.JoystickButtons)
            {
                if (_inputApi == InputApi.DirectInput)
                    t.BindName = t.BindNameDi;
                else if (_inputApi == InputApi.XInput)
                    t.BindName = t.BindNameXi;
                else if (_inputApi == InputApi.RawInput)
                    t.BindName = t.BindNameRi;
            }

            JoystickHelper.SerializeGameProfile(_gameProfile);
            _gameProfile.GamePath = GamePathBox.Text;
            _gameProfile.GamePath2 = GamePathBox2.Text;
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, System.IO.Path.GetFileName(_gameProfile.FileName)));
            _library.ListUpdate(_gameProfile.GameNameInternal);
            _contentControl.Content = _library;
        }
        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            // Reload library to discard changes
            _library.ListUpdate(_gameProfile.GameNameInternal);

            _contentControl.Content = _library;
        }
    }
}
