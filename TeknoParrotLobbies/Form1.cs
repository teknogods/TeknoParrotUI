using LobbyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.MemoryMappedFiles;
using static TeknoParrotLobbies.CreateLobbyForm;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace TeknoParrotLobbies
{
    public partial class Form1 : Form
    {
        private static LobbyClient lobbyClient = new LobbyClient("http://51.15.85.127:19125");

        public static MemoryMappedFile StateSection;
        public static MemoryMappedViewAccessor StateView;

        // refreshing list
        private static bool refreshList = false;
        private static GameId selectedGameId = GameId.Any;

        // creating lobby
        public static bool createLobby = false;
        public static string lobbyName = "";
        public static GameId lobbyGame = GameId.Any;
        private static bool waitingForCreation = false;

        private static bool joinLobby = false;
        private static LobbyData lobbyToJoin = null;
        private static bool waitingForJoin = false;

        private static bool isInLobby = false;
        private static LobbyData currentLobby = null;
        private static UInt64 currentLobbyId = 0;

        private static Process launcherProcess;

        Random rnd = new Random(107167120);

        unsafe struct TpNetState
        {
            public UInt64 lobbyId;
            public UInt64 steamId;
            public UInt64 hostId;
            public int numMembers;
            public fixed ulong members[32];
        }

        private async void ListenThread()
        {
            TpNetState lastGameData = new TpNetState();

            DateTime lastUpdate = DateTime.UtcNow;

            while (true)
            {
                StateView.Read<TpNetState>(0, out TpNetState gameData);

                if (refreshList)
                {
                    refreshList = !refreshList;

                    List<LobbyData> lobbies = new List<LobbyData>();

                    try
                    {
                        lobbies = await lobbyClient.GetLobbies(selectedGameId);
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        MessageBox.Show("master server offline :(");
                    }

                    Invoke(new Action(() =>
                    {
                        lobbyListView.Items.Clear();
                    }));

                    foreach (LobbyData lobby in lobbies)
                    {
                        if (lobby.GameId == selectedGameId || selectedGameId == GameId.Any)
                        {
                            Invoke(new Action(() =>
                            {
                                ListViewItem item = new ListViewItem(new string[] { lobby.Name, lobby.GameId.ToString(), $"{lobby.MemberCount}/{lobby.MaxMemberCount}" });
                                item.Tag = lobby;
                                lobbyListView.Items.Add(item);
                            }));
                        }
                    }

                    Invoke(new Action(() =>
                    {
                        refreshLobbiesBtn.Enabled = true;
                    }));
                }

                if (waitingForCreation)
                {
                    if (gameData.lobbyId != 0)
                    {
                        waitingForCreation = !waitingForCreation;

                        LobbyData newLobby = new LobbyData();
                        newLobby.Id = gameData.lobbyId;
                        newLobby.HostId = gameData.hostId;

                        List<LobbyMember> newMembers = new List<LobbyMember>();

                        for (int i = 0; i < gameData.numMembers; i++)
                        {
                            LobbyMember member = new LobbyMember();
                            unsafe
                            {
                                // TODO: member names.
                                member.Name = gameData.members[i].ToString();
                                member.Id = gameData.members[i];
                            }
                            newMembers.Add(member);
                        }

                        newLobby.Members = newMembers;
                        newLobby.Name = lobbyName;
                        newLobby.GameId = lobbyGame;
                        newLobby.MaxMemberCount = 2;
                        newLobby.MemberCount = gameData.numMembers;

                        await lobbyClient.CreateLobby(newLobby);

                        isInLobby = true;
                        currentLobbyId = gameData.lobbyId;
                        currentLobby = newLobby;
                    }
                }

                if (createLobby)
                {
                    createLobby = !createLobby;

                    var profileName = lobbyGame.ToString() + ".xml";
                    ProcessStartInfo info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName}");
                    info.UseShellExecute = false;
                    info.Environment.Add("tp_steamnet", "host");

                    launcherProcess = Process.Start(info);

                    waitingForCreation = true;

                    refreshList = true;
                }

                if (waitingForJoin)
                {
                    if (gameData.lobbyId != 0)
                    {
                        waitingForJoin = !waitingForJoin;

                        currentLobbyId = gameData.lobbyId;
                        currentLobby = await lobbyClient.GetLobby(currentLobbyId);

                        isInLobby = true;
                    }
                }

                if (isInLobby)
                {
                    if (!lastGameData.Equals(gameData) || (DateTime.UtcNow > lastUpdate + TimeSpan.FromSeconds(30)))
                    {
                        lastUpdate = DateTime.UtcNow;

                        currentLobby.HostId = gameData.hostId;

                        List<LobbyMember> newMembers = new List<LobbyMember>();

                        for (int i = 0; i < gameData.numMembers; i++)
                        {
                            LobbyMember member = new LobbyMember();
                            unsafe
                            {
                                member.Name = gameData.members[i].ToString();
                                member.Id = gameData.members[i];
                            }
                            newMembers.Add(member);
                        }

                        currentLobby.Members = newMembers;
                        currentLobby.MemberCount = gameData.numMembers;

                        await lobbyClient.UpdateLobby(currentLobbyId, currentLobby);
                    }

                    if (launcherProcess != null && launcherProcess.HasExited)
                    {
                        isInLobby = false;
                        currentLobby = null;
                        currentLobbyId = 0;

                        joinLobbyBtn.Enabled = true;

                        if (gameData.hostId == gameData.steamId)
                        {
                            await lobbyClient.DeleteLobby(currentLobbyId);
                        }

                        TpNetState state = new TpNetState();
                        StateView.Write<TpNetState>(0, ref state);
                    }
                }

                if (joinLobby)
                {
                    joinLobby = !joinLobby;

                    var profileName = lobbyToJoin.GameId.ToString() + ".xml";
                    ProcessStartInfo info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName}");
                    info.UseShellExecute = false;
                    info.Environment.Add("tp_steamnet", lobbyToJoin.Id.ToString("x"));

                    launcherProcess = Process.Start(info);

                    waitingForJoin = true;
                }

                lastGameData = gameData;

                Thread.Sleep(100);
            }
        }

        public Form1()
        {
            StateSection = MemoryMappedFile.CreateOrOpen("TeknoParrot_NetState", Marshal.SizeOf<TpNetState>());
            StateView = StateSection.CreateViewAccessor();

            InitializeComponent();

            MessageBox.Show("This is just a temporary UI.", "TeknoParrot Lobbies", MessageBoxButtons.YesNo);

            new Thread(ListenThread).Start();

            // init game selector for refreshing list
            comboBox1.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox1.DisplayMember = "Text";
            comboBox1.ValueMember = "ID";
            comboBox1.DataSource = new ComboItem[]
            {
                new ComboItem { ID = GameId.Any, Text = "All" },
                new ComboItem { ID = GameId.ID6, Text = "Initial D 6 AA" },
                new ComboItem { ID = GameId.ID7, Text = "Initial D 7 AA X" },
                new ComboItem { ID = GameId.MKDX, Text = "Mario Kart DX" }
            };

            lobbyListView.Columns.Add("Lobby Name", 200);
            lobbyListView.Columns.Add("Game", 50);
            lobbyListView.Columns.Add("Players", 50);
        }

        private void createLobbyBtn_Click(object sender, EventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            CreateLobbyForm create = new CreateLobbyForm();
            create.Show();
        }

        private void refreshLobbiesBtn_Click(object sender, EventArgs e)
        {
            selectedGameId = (GameId)comboBox1.SelectedValue;
            refreshLobbiesBtn.Enabled = false;
            refreshList = true;
        }

        private void joinLobbyBtn_Click(object sender, EventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            joinLobbyBtn.Enabled = false;
            lobbyToJoin = (LobbyData)lobbyListView.SelectedItems[0].Tag;
            joinLobby = true;
        }

        private bool IsBusy()
        {
            return isInLobby || joinLobby || createLobby || waitingForCreation || waitingForJoin;
        }

        private void handleListViewClick(object sender, EventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            joinLobbyBtn.Enabled = false;
            lobbyToJoin = (LobbyData)lobbyListView.SelectedItems[0].Tag;
            joinLobby = true;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Environment.Exit(1);
        }
    }
}
