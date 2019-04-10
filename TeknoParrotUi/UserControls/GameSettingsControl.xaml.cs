﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views;

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

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl,
            Library library)
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

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }
    }
}
