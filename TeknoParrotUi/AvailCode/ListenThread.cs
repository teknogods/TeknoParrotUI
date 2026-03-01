using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.AvailCode
{
    public static class ListenThread
    {
        private static LobbyClient lobbyClient = new LobbyClient("http://104.244.72.41:19125");

        public static MemoryMappedFile StateSection;
        public static MemoryMappedViewAccessor StateView;

        // refreshing list
        public static bool RefreshList = false;
        public static GameId SelectedGameId = GameId.Any;

        // creating lobby
        public static bool CreateLobby = false;
        public static string LobbyName = "";
        public static GameId LobbyGame = GameId.Any;
        public static bool WaitingForCreation = false;

        public static bool JoinLobby = false;
        public static LobbyData LobbyToJoin = null;
        public static bool WaitingForJoin = false;

        public static bool IsInLobby = false;
        public static LobbyData CurrentLobby = null;
        public static UInt64 CurrentLobbyId = 0;

        public static Process LauncherProcess;
        public static bool WantsQuit = false;

        public static Random Rnd = new Random(107167120);

        public static async void Listen(DataGrid lobbyList, Button btnRefresh, Button btnJoinGame, MainWindow mainWindow)
        {
            TpNetStateStruct.TpNetState lastGameData = new TpNetStateStruct.TpNetState();

            DateTime lastUpdate = DateTime.UtcNow;

            while (true)
            {
                if (WantsQuit)
                    return;
                StateView.Read<TpNetStateStruct.TpNetState>(0, out TpNetStateStruct.TpNetState gameData);

                if (RefreshList)
                {
                    RefreshList = !RefreshList;

                    var lobbies = new List<LobbyData>();

                    try
                    {
                        lobbies = await lobbyClient.GetLobbies(SelectedGameId);
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        MessageBoxHelper.ErrorOK(Properties.Resources.ErrorMasterServerOffline);
                    }

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        lobbyList.Items.Clear();
                    }));

                    foreach (var lobby in lobbies)
                    {
                        if (lobby.GameId == SelectedGameId || SelectedGameId == GameId.Any)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                var item = new LobbyList
                                {
                                    LobbyName = lobby.Name,
                                    Game = lobby.GameId.ToString(),
                                    Players = $"{lobby.MemberCount}/{lobby.MaxMemberCount}",
                                    LobbyData = lobby
                                };
                                lobbyList.Items.Add(item);
                            }));
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        btnRefresh.IsEnabled = true;
                        mainWindow.IsEnabled = true;
                    });
                }

                if (WaitingForCreation)
                {
                    if (gameData.lobbyId != 0)
                    {
                        WaitingForCreation = !WaitingForCreation;

                        var newLobby = new LobbyData
                        {
                            Id = gameData.lobbyId,
                            HostId = gameData.hostId
                        };

                        var newMembers = new List<LobbyMember>();

                        for (int i = 0; i < gameData.numMembers; i++)
                        {
                            var member = new LobbyMember();
                            unsafe
                            {
                                // TODO: member names.
                                member.Name = gameData.members[i].ToString();
                                member.Id = gameData.members[i];
                            }
                            newMembers.Add(member);
                        }

                        newLobby.Members = newMembers;
                        newLobby.Name = LobbyName;
                        newLobby.GameId = LobbyGame;
                        newLobby.MaxMemberCount = 2;
                        newLobby.MemberCount = gameData.numMembers;

                        await lobbyClient.CreateLobby(newLobby);

                        IsInLobby = true;
                        CurrentLobbyId = gameData.lobbyId;
                        CurrentLobby = newLobby;
                    }
                }

                if (CreateLobby)
                {
                    CreateLobby = !CreateLobby;

                    var profileName = LobbyGame + ".xml";
                    var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName}  --tponline")
                    {
                        UseShellExecute = false
                    };
                    info.EnvironmentVariables.Add("tp_steamnet", "host");

                    LauncherProcess = Process.Start(info);

                    WaitingForCreation = true;

                    RefreshList = true;
                }

                if (WaitingForJoin)
                {
                    if (gameData.lobbyId != 0)
                    {
                        WaitingForJoin = !WaitingForJoin;

                        CurrentLobbyId = gameData.lobbyId;
                        CurrentLobby = await lobbyClient.GetLobby(CurrentLobbyId);

                        IsInLobby = true;
                    }
                }

                if (IsInLobby)
                {
                    if (!lastGameData.Equals(gameData) || (DateTime.UtcNow > lastUpdate + TimeSpan.FromSeconds(30)))
                    {
                        lastUpdate = DateTime.UtcNow;

                        CurrentLobby.HostId = gameData.hostId;

                        var newMembers = new List<LobbyMember>();

                        for (int i = 0; i < gameData.numMembers; i++)
                        {
                            var member = new LobbyMember();
                            unsafe
                            {
                                member.Name = gameData.members[i].ToString();
                                member.Id = gameData.members[i];
                            }
                            newMembers.Add(member);
                        }

                        CurrentLobby.Members = newMembers;
                        CurrentLobby.MemberCount = gameData.numMembers;

                        await lobbyClient.UpdateLobby(CurrentLobbyId, CurrentLobby);
                    }

                    if (LauncherProcess != null && LauncherProcess.HasExited)
                    {
                        IsInLobby = false;
                        CurrentLobby = null;
                        CurrentLobbyId = 0;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            btnJoinGame.IsEnabled = true;
                            mainWindow.IsEnabled = true;
                        });

                        if (gameData.hostId == gameData.steamId)
                        {
                            await lobbyClient.DeleteLobby(CurrentLobbyId);
                        }

                        var state = new TpNetStateStruct.TpNetState();
                        StateView.Write<TpNetStateStruct.TpNetState>(0, ref state);
                    }
                }

                if (JoinLobby)
                {
                    JoinLobby = !JoinLobby;

                    var profileName = LobbyToJoin.GameId.ToString() + ".xml";
                    var info = new ProcessStartInfo("TeknoParrotUi.exe", $"--profile={profileName} --tponline")
                    {
                        UseShellExecute = false
                    };
                    //info.Environment.Add("tp_steamnet", LobbyToJoin.Id.ToString("x"));
                    info.EnvironmentVariables.Add("tp_steamnet", LobbyToJoin.Id.ToString("x"));

                    LauncherProcess = Process.Start(info);

                    WaitingForJoin = true;
                }

                lastGameData = gameData;

                Thread.Sleep(100);
            }
        }
    }
}
