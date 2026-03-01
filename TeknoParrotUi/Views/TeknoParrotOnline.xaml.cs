using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using TeknoParrotUi.AvailCode;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for TeknoParrotOnline.xaml
    /// </summary>
    public partial class TeknoParrotOnline
    {
        bool _isLoaded;

        public TeknoParrotOnline()
        {
            InitializeComponent();
        }

        private static bool IsBusy()
        {
            return ListenThread.IsInLobby || ListenThread.JoinLobby || ListenThread.CreateLobby ||
                   ListenThread.WaitingForCreation || ListenThread.WaitingForJoin;
        }

        private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
        {
            ListenThread.SelectedGameId = (GameId) ((FrameworkElement) GameListCombo.SelectedItem).Tag;
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
            IsEnabled = false;
        }

        private void BtnCreateLobby(object sender, RoutedEventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new TPOnlineCreate();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            ListenThread.StateSection =
                MemoryMappedFile.CreateOrOpen("TeknoParrot_NetState", Marshal.SizeOf<TpNetStateStruct.TpNetState>());
            ListenThread.StateView = ListenThread.StateSection.CreateViewAccessor();
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().Single();
            _isLoaded = true;
            new Thread(() => ListenThread.Listen(GridLobbies, BtnRefresh, BtnJoinGame, mainWindow)).Start();
            ListenThread.SelectedGameId = (GameId) ((FrameworkElement) GameListCombo.SelectedItem).Tag;
            BtnRefresh.IsEnabled = false;
            ListenThread.RefreshList = true;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // if true, it fixes ID5/6 MP but breaks everything else...?
            // ListenThread.WantsQuit = true;
        }

        private void GameListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ListenThread.SelectedGameId = (GameId) ((FrameworkElement) GameListCombo.SelectedItem).Tag;
            BtnRefresh.IsEnabled = false;
            ListenThread.RefreshList = true;
        }
    }
}