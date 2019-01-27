using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MahApps.Metro.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGameWindow.xaml
    /// </summary>
    public partial class AddGameWindow : MetroWindow
    {
        public AddGameWindow()
        {
            InitializeComponent();
        }
        GameProfile selected = new GameProfile();
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };

                gameListBox.Items.Add(item);
            }
        }

        private void GameListBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            selected = GameProfileLoader.GameProfiles[gameListBox.SelectedIndex];
            selectedGame.Text = selected.GameName;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Adding " + selected.GameName + " to TP...");
            string[] splitString = selected.FileName.Split('\\');
            File.Copy(selected.FileName, "UserProfiles\\" + splitString[1]);
            this.Close();
        }
    }
}
