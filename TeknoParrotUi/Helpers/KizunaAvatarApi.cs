using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    /// <summary>One side's avatar as the game takes it: 1 male / 2 female, voice 0..12, greeting 0..3, 13 item ids in slot order.</summary>
    public sealed class KizunaAvatar
    {
        public int Gender { get; set; } = 1;
        public int Voice { get; set; }
        public int Greeting { get; set; }
        public int[] Items { get; set; } = new int[13];

        public bool Female => Gender == 2;

        public KizunaAvatar Clone() => new KizunaAvatar { Gender = Gender, Voice = Voice, Greeting = Greeting, Items = (int[])Items.Clone() };

        public bool SameAs(KizunaAvatar other) =>
            other != null && Gender == other.Gender && Voice == other.Voice && Greeting == other.Greeting &&
            Items.Length == other.Items.Length && System.Linq.Enumerable.SequenceEqual(Items, other.Items);
    }

    /// <summary>The website's parts catalog (api/User/KizunaAvatarCatalog): the parts in slot order with their items, the game's defaults.</summary>
    public sealed class KizunaAvatarCatalogInfo
    {
        public sealed class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }

            /// <summary>The body the item was made for (1 male, 2 female), null when it suits both.</summary>
            public int? Body { get; set; }
        }

        public sealed class Category
        {
            public int Slot { get; set; }
            public string Name { get; set; }
            public bool Required { get; set; }
            public List<Item> Items { get; set; } = new List<Item>();
        }

        public sealed class BodyDefaults
        {
            public KizunaAvatar Male { get; set; }
            public KizunaAvatar Female { get; set; }
        }

        public sealed class SideDefaults
        {
            public BodyDefaults Efsf { get; set; }
            public BodyDefaults Zeon { get; set; }
        }

        public int Slots { get; set; }
        public int Voices { get; set; }
        public int MaleVoices { get; set; }
        public int Greetings { get; set; }
        public List<Category> Categories { get; set; } = new List<Category>();
        public SideDefaults Defaults { get; set; }

        public KizunaAvatar Default(int side, bool female)
        {
            var body = side == 0 ? Defaults?.Efsf : Defaults?.Zeon;
            return (female ? body?.Female : body?.Male)?.Clone();
        }
    }

    /// <summary>The account's avatar per side (api/User/KizunaAvatar): null = the game's default.</summary>
    public sealed class KizunaAvatarAccount
    {
        /// <summary>After a save: saved, removed, unchanged or invalid.</summary>
        public string Status { get; set; }
        public KizunaAvatar Efsf { get; set; }
        public KizunaAvatar Zeon { get; set; }

        public KizunaAvatar Side(int side) => side == 0 ? Efsf : Zeon;
    }

    /// <summary>
    /// TeknoParrot.com's Senjou no Kizuna avatar API, with the account's token (the same login as api/User/Profile). The
    /// website checks every avatar against the game's rules; nothing here is trusted.
    /// </summary>
    public static class KizunaAvatarApi
    {
        public enum Outcome
        {
            Ok,
            Unauthorized,
            Unavailable
        }

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>api/User/ next to the profile API (KizunaOnlineHelper.ProfileUrl).</summary>
        private static string Url(string name)
        {
            var profile = KizunaOnlineHelper.ProfileUrl;
            return profile.Substring(0, profile.LastIndexOf('/') + 1) + name;
        }

        public static Task<(Outcome Outcome, KizunaAvatarCatalogInfo Catalog)> GetCatalogAsync(string token) =>
            SendAsync<KizunaAvatarCatalogInfo>(HttpMethod.Get, "KizunaAvatarCatalog", token, null);

        public static Task<(Outcome Outcome, KizunaAvatarAccount Account)> GetAccountAsync(string token) =>
            SendAsync<KizunaAvatarAccount>(HttpMethod.Get, "KizunaAvatar", token, null);

        /// <summary>Saves a side (avatar null: back to the game's default); the answer says how it went and holds both sides.</summary>
        public static Task<(Outcome Outcome, KizunaAvatarAccount Account)> SaveAsync(string token, int side, KizunaAvatar avatar) =>
            SendAsync<KizunaAvatarAccount>(HttpMethod.Put, "KizunaAvatar", token,
                JsonSerializer.Serialize(new
                {
                    side,
                    avatar = avatar == null ? null : new { gender = avatar.Gender, voice = avatar.Voice, greeting = avatar.Greeting, items = avatar.Items }
                }));

        private static async Task<(Outcome, T)> SendAsync<T>(HttpMethod method, string name, string token, string body) where T : class
        {
            if (string.IsNullOrEmpty(token))
                return (Outcome.Unauthorized, null);
            try
            {
                using (var request = new HttpRequestMessage(method, Url(name)))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (body != null)
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var response = await Http.SendAsync(request).ConfigureAwait(true))
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                            return (Outcome.Unauthorized, null);
                        if (!response.IsSuccessStatusCode)
                            return (Outcome.Unavailable, null);
                        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        var value = JsonSerializer.Deserialize<T>(text, Json);
                        return value == null ? (Outcome.Unavailable, null) : (Outcome.Ok, value);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is IOException)
            {
                Debug.WriteLine($"KizunaAvatar: {name} failed: {ex.Message}");
                return (Outcome.Unavailable, null);
            }
        }
    }
}
