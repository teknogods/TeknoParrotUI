using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;

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
        private ComboBoxItem _comboItem;
        public string GamePath;

        public void LoadNewSettings(GameProfile gameProfile, ComboBoxItem comboItem)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;
            GamePathBox.Text = _gameProfile.GamePath;
            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            Lazydata.GamePath = string.Empty;
        }

        private void SelectExecutableForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = "Please select game executable"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
                Lazydata.GamePath = openFileDialog.FileName;
            }
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            _gameProfile.GamePath = GamePathBox.Text;
            Lazydata.GamePath = GamePathBox.Text;
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            MessageBox.Show($"Generation of {System.IO.Path.GetFileName(_gameProfile.FileName)} was succesful!", "Save Complete", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
