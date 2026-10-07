using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Avalonia.Services;

internal static class NewsHistoryChecks
{
    const string Prefix = "https://www.patreon.com/TeknoParrotTeam/posts/";
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static string Feed = "[{\"url\":\"" + Prefix + "older\",\"publishedAt\":\"2026-10-01T12:00:00Z\"},{\"url\":\"" + Prefix + "newer\",\"publishedAt\":\"2026-10-07T12:00:00Z\"}]";
    internal static void Run()
    {
        var announcement = AnnouncementService.ParseContent(Feed, "", CancellationToken.None);
        Check(announcement.Articles.Count == 2, "history retained");
        Check(announcement.PageUrl.AbsoluteUri == Prefix + "newer", "newest first");
        Check(announcement.Articles[1].PublishedAt.Value.UtcDateTime.Day == 1, "previous article date");
        Check(AnnouncementService.ParseContent(Feed, Prefix + "older", CancellationToken.None) != null, "missed news shown");
        Check(AnnouncementService.ParseContent(Feed, Prefix + "newer", CancellationToken.None) == null, "unchanged newest suppressed");
        Check(AnnouncementService.ParseContent(Prefix + "newer", "", CancellationToken.None).Articles.Count == 1, "legacy feed");
        Check(AnnouncementService.ParseContent(Prefix + "newer", " "+Prefix+"newer\n", CancellationToken.None) == null, "legacy seen marker");
        foreach (var bad in new[] { "[]", "{}", "bad json", "[null]", "[{\"url\":\"https://evil.test/\",\"publishedAt\":\"2026-10-01\"}]", "[{\"url\":\""+Prefix+"one\",\"publishedAt\":\"bad\"}]" })
            Check(AnnouncementService.ParseContent(bad, "", CancellationToken.None) == null, "reject malformed feed");
        Check(AnnouncementService.ParseContent(Feed, "", new CancellationToken(true)) == null, "cancelled");
        Check(AnnouncementService.ParseContent(new string('x', AnnouncementService.MaximumContentBytes + 1), "", CancellationToken.None) == null, "size limit");
        TestTransport().GetAwaiter().GetResult();
        Console.WriteLine("PASS: " + checks + " news history checks; Avalonia history transport and parsing.");
    }
    static async Task TestTransport()
    {
        var handler = new Handler(false);
        using (var client = new HttpClient(handler))
        {
            var news = await AnnouncementService.CheckAsync(client, "https://example.test/Home/NewsPostUrl", "", CancellationToken.None);
            Check(news.Articles.Count == 2 && handler.Calls == 1, "prefer history endpoint");
        }
        handler = new Handler(true);
        using (var client = new HttpClient(handler))
        {
            var news = await AnnouncementService.CheckAsync(client, "https://example.test/Home/NewsPostUrl", "", CancellationToken.None);
            Check(news.Articles.Count == 1 && handler.Calls == 2, "404 legacy fallback");
        }
    }
    class Handler : HttpMessageHandler
    {
        readonly bool fallback; public int Calls;
        public Handler(bool fallback) { this.fallback = fallback; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Check(request.Headers.CacheControl.NoCache && request.Headers.CacheControl.NoStore, "cache bypass");
            if (request.RequestUri.AbsolutePath.EndsWith("NewsPostArticles"))
                return Task.FromResult(new HttpResponseMessage(fallback ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new StringContent(Feed) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Prefix + "newer") });
        }
    }
}
