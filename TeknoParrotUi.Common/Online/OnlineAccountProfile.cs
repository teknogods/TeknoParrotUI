using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Online
{
    public sealed class OnlineAccountProfile
    {
        public string UserName { get; set; }
        public string Tier { get; set; }
        public string SegaId { get; set; }
        public string HighscoreSerial { get; set; }
        public string NamcoId { get; set; }
        public string MarioKartId { get; set; }
        public int? GoldenTeePcbId { get; set; }
        public string GoldenTeeCardId { get; set; }
        public bool IsSubscribed { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int? InitialDRank { get; set; }
        public List<AccountSerial> Serials { get; set; } = new();

        public sealed class AccountSerial
        {
            public string Serial { get; set; }
            public bool IsActive { get; set; }
            public bool IsInUse { get; set; }
            public bool IsGifted { get; set; }
            public DateTime? ExpireDate { get; set; }
            public override string ToString() => Serial + (IsGifted ? " (gifted)" : IsInUse ? " (in use)" : "");
        }

        public static async Task<OnlineAccountProfile> FetchAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://teknoparrot.com/api/User/Profile");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var profile = JsonSerializer.Deserialize<OnlineAccountProfile>(await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (profile == null) throw new InvalidOperationException("The account profile response was empty.");
            var data = Lazydata.ParrotData;
            data.SegaId = profile.SegaId;
            data.ScoreSubmissionID = profile.HighscoreSerial;
            data.NamcoId = profile.NamcoId;
            data.MarioKartId = profile.MarioKartId;
            if (profile.GoldenTeePcbId.HasValue && !string.IsNullOrEmpty(profile.GoldenTeeCardId))
            {
                data.GoldenTeePcbId = profile.GoldenTeePcbId.Value.ToString();
                data.GoldenTeeCardId = profile.GoldenTeeCardId;
            }
            data.IsLoggedIn = true;
            JoystickHelper.Serialize();
            return profile;
        }
    }
}
