using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Kept in the original namespace so the classic UI's TPO code is unaffected by the
// move into TeknoParrotUi.Common.
namespace TeknoParrotUi.AvailCode
{
    public enum GameId
    {
        Any,
        ID6,
        ID7,
        MKDX,
        WMMT5,
        ID8,
        ID5,
        ID4,
        ID4Exp,
        SiN
    }

    public class LobbyMember
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
    }

    public class LobbyData
    {
        public GameId GameId { get; set; }
        public ulong Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Name { get; set; }
        public ulong HostId { get; set; }
        public int MemberCount { get; set; }
        public int MaxMemberCount { get; set; }
        public List<LobbyMember> Members { get; set; }
    }

    public class LobbyClient
    {
        private string hostUri = "http://localhost:19125";
        private const string LOBBIES_ENDPOINT = "api/lobbies";

        HttpClient httpClient = null;

        public LobbyClient(string hostUri = "http://localhost:19125")
        {
            this.hostUri = hostUri;

            httpClient = new HttpClient();
        }

        public async Task CreateLobby(LobbyData data)
        {
            var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            await httpClient.PostAsync($"{hostUri}/{LOBBIES_ENDPOINT}", content);
        }

        public async Task DeleteLobby(ulong lobbyId)
        {
            await httpClient.DeleteAsync($"{hostUri}/{LOBBIES_ENDPOINT}/{lobbyId:X16}");
        }

        public async Task UpdateLobby(ulong lobbyId, LobbyData newData)
        {
            var content = new StringContent(JsonConvert.SerializeObject(newData), Encoding.UTF8, "application/json");
            await httpClient.PutAsync($"{hostUri}/{LOBBIES_ENDPOINT}/{lobbyId:X16}", content);
        }

        public async Task<List<LobbyData>> GetLobbies(GameId gameId = GameId.Any)
        {
            string gameIdStr = gameId.ToString();

            HttpResponseMessage response = await httpClient.GetAsync($"{hostUri}/{LOBBIES_ENDPOINT}?game={gameIdStr}");

            List<LobbyData> lobbies = null;

            if (response.IsSuccessStatusCode)
            {
                lobbies = JsonConvert.DeserializeObject<List<LobbyData>>(await response.Content.ReadAsStringAsync());
            }

            return lobbies;
        }

        public async Task<LobbyData> GetLobby(ulong lobbyId)
        {
            HttpResponseMessage response = await httpClient.GetAsync($"{hostUri}/{LOBBIES_ENDPOINT}/{lobbyId:X16}");

            LobbyData lobby = null;

            if (response.IsSuccessStatusCode)
            {
                lobby = JsonConvert.DeserializeObject<LobbyData>(await response.Content.ReadAsStringAsync());
            }

            return lobby;
        }
    }
}
