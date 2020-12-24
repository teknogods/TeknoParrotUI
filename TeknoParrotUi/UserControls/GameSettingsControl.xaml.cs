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
        public string GamePath;
        private Library _library;
        private InputApi _inputApi = InputApi.DirectInput;

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl, Library library)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;
            GamePathBox.Text = _gameProfile.GamePath;
            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            Lazydata.GamePath = string.Empty;
            _contentControl = contentControl;
            _library = library;
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
                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({_gameProfile.ExecutableName})|{_gameProfile.ExecutableName}|All files (*.*)|*.*";
            }

            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
                Lazydata.GamePath = openFileDialog.FileName;
            }
        }

        public static string Filter(string input, string[] badWords)
        {
            var re = new Regex(
                @"\b("
                + string.Join("|", badWords.Select(word =>
                    string.Join(@"\s*", word.ToCharArray())))
                + @")\b", RegexOptions.IgnoreCase);
            return re.Replace(input, match =>
            {
                return new string('*', match.Length);
            });
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

            string NameString = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Submission Name")?.FieldValue;

            if (NameString != null)
            {
                if (_gameProfile.ConfigValues.Any(x => x.FieldName == "Enable Submission (Patreon Only)" && x.FieldValue == "1"))
                {
                    if (_gameProfile.ConfigValues.Find(cv => cv.FieldName == "Submission Name").FieldValue == "")
                    {
                        MessageBox.Show("Score Submission requires a name!");
                    } 
                }

                string[] badWords = new[] { "fuck", "cunt", "fuckwit", "fag", "dick", "shit", "cock", "pussy", "ass", "asshole", "bitch", "homo", "faggot", "a$$", "@ss", "f@g", "fucker", "fucking", "fuk", "fuckin", "fucken", "teknoparrot", "tp", "arse", "@rse", "@$$", "bastard", "crap", "effing", "god", "hell", "motherfucker", "whore", "twat", "gay", "g@y", "ash0le", "assh0le", "a$$hol", "anal", };

                NameString = Filter(NameString, badWords);
                _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Submission Name").FieldValue = NameString;
            }

            JoystickHelper.SerializeGameProfile(_gameProfile);
            _gameProfile.GamePath = GamePathBox.Text;
            Lazydata.GamePath = GamePathBox.Text;
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, System.IO.Path.GetFileName(_gameProfile.FileName)));
            _library.ListUpdate(_gameProfile.GameName);
            _contentControl.Content = _library;
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            // Reload library to discard changes
            _library.ListUpdate(_gameProfile.GameName);

            _contentControl.Content = _library;
        }
    }
}
