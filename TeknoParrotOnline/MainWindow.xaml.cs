using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using TeknoParrotOnline.AvailCode;

namespace TeknoParrotOnline
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ListenThread.StateSection = MemoryMappedFile.CreateOrOpen("TeknoParrot_NetState", Marshal.SizeOf<TpNetStateStruct.TpNetState>());
            ListenThread.StateView = ListenThread.StateSection.CreateViewAccessor();

            new Thread(() => ListenThread.Listen(GridLobbies, BtnRefresh, BtnJoinGame, this)).Start();
        }

        private bool IsBusy()
        {
            return ListenThread.IsInLobby || ListenThread.JoinLobby || ListenThread.CreateLobby || ListenThread.WaitingForCreation || ListenThread.WaitingForJoin;
        }

        private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
        {
            ListenThread.SelectedGameId = (GameId)((FrameworkElement)GameListCombo.SelectedItem).Tag;
            BtnRefresh.IsEnabled = false;
            ListenThread.RefreshList = true;
        }

        private void BtnJoinGame_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            if (!(GridLobbies.SelectedItem is LobbyList data))
                return;
            BtnJoinGame.IsEnabled = false;
            ListenThread.LobbyToJoin = data.LobbyData;
            ListenThread.JoinLobby = true;
            this.IsEnabled = false;
        }

        private void BtnShowCreateGame_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsBusy())
            {
                return;
            }
            CreateGame.IsOpen = true;
        }

        private void BtnLaunchLobby_OnClick(object sender, RoutedEventArgs e)
        {
            ListenThread.LobbyName = TxtLobbyName.Text;
            ListenThread.LobbyGame = (GameId)((FrameworkElement)GameSelectCombo.SelectedItem).Tag;
            ListenThread.CreateLobby = true;
            CreateGame.IsOpen = false;
            this.IsEnabled = false;
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            ListenThread.WantsQuit = true;
        }
    }
}
