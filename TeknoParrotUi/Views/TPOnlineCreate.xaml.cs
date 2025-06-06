﻿using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.AvailCode;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for TPOnlineCreate.xaml
    /// </summary>
    public partial class TPOnlineCreate
    {
        public TPOnlineCreate()
        {
            InitializeComponent();
        }

        private void BtnLaunchLobby_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtLobbyName.Text))
            {
                MessageBox.Show(TeknoParrotUi.Properties.Resources.TPOnlineCreatePleaseEnterLobbyName, TeknoParrotUi.Properties.Resources.TPOnlineCreateHoldItRightThere, MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ListenThread.LobbyName = TxtLobbyName.Text;
            ListenThread.LobbyGame = (GameId) ((FrameworkElement) GameSelectCombo.SelectedItem).Tag;
            ListenThread.CreateLobby = true;
            //Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = MainWindow.TpOnline;
            IsEnabled = false;
        }
    }
}