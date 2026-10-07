using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    public sealed class NewsArticle
    {
        public Uri PageUrl { get; }
        public DateTimeOffset? PublishedAt { get; }
        public NewsArticle(Uri pageUrl, DateTimeOffset? publishedAt)
        {
            PageUrl = pageUrl;
            PublishedAt = publishedAt;
        }
    }

    internal sealed class Announcement
    {
        public string Content { get; }
        public Uri PageUrl => Articles[0].PageUrl;
        public IReadOnlyList<NewsArticle> Articles { get; }
        public Announcement(string content, Uri pageUrl)
            : this(content, new[] { new NewsArticle(pageUrl, null) }) { }
        public Announcement(string content, IReadOnlyList<NewsArticle> articles)
        {
            Content = content;
            Articles = articles;
        }
    }

    internal static class AnnouncementService
    {
        internal const int MaximumContentBytes = 1024 * 1024;
        private const string NewsPostPrefix = "https://www.patreon.com/TeknoParrotTeam/posts/";

        internal static bool ShouldCheckAtStartup(string[] arguments, bool debuggerAttached)
        {
            if (arguments != null && arguments.Contains("--news-test"))
                return true;
#if DEBUG
            return false;
#else
            return !debuggerAttached && (arguments == null || !arguments.Any(argument =>
                argument != null && argument.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)));
#endif
        }

        public static async Task<Announcement> CheckAsync(string sourceUrl, string previousContent,
            CancellationToken cancellationToken)
        {
            using (var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
                MaxResponseContentBufferSize = MaximumContentBytes
            })
            {
                return await CheckAsync(client, sourceUrl, previousContent, cancellationToken).ConfigureAwait(false);
            }
        }

        // Separate transport from comparison so checks can be tested without a live endpoint.
        internal static async Task<Announcement> CheckAsync(HttpClient client, string sourceUrl,
            string previousContent, CancellationToken cancellationToken)
        {
            if (!TryGetWebUrl(sourceUrl, out var sourceUri) || cancellationToken.IsCancellationRequested)
                return null;

            try
            {
                // Old saved settings keep the single-link URL. Prefer its history endpoint,
                // falling back only when an older website has not implemented it yet.
                if (sourceUri.AbsolutePath.EndsWith("/Home/NewsPostUrl", StringComparison.OrdinalIgnoreCase))
                {
                    var historyUri = new UriBuilder(sourceUri);
                    historyUri.Path = sourceUri.AbsolutePath.Substring(0, sourceUri.AbsolutePath.Length - "NewsPostUrl".Length) + "NewsPostArticles";
                    using (var historyResponse = await FetchAsync(client, historyUri.Uri, cancellationToken).ConfigureAwait(false))
                    {
                        if (historyResponse.IsSuccessStatusCode)
                            return ParseContent(await historyResponse.Content.ReadAsStringAsync().ConfigureAwait(false), previousContent, cancellationToken);
                        if (historyResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                            return null;
                    }
                }
                using (var response = await FetchAsync(client, sourceUri, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode || response.Content == null) return null;
                    return ParseContent(await response.Content.ReadAsStringAsync().ConfigureAwait(false), previousContent, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Announcement check failed: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                // Offline, timed out, or shutting down: keep the previous value and continue startup.
            }

            return null;
        }

        private static async Task<HttpResponseMessage> FetchAsync(HttpClient client, Uri uri, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
                return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        internal static Announcement ParseContent(string content, string previousContent, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(content) || content.Length > MaximumContentBytes)
                return null;
            var articles = new List<NewsArticle>();
            if (TryGetNewsPostUrl(content, out var legacyUrl))
                articles.Add(new NewsArticle(legacyUrl, null));
            else
            {
                try
                {
                    var json = JArray.Parse(content);
                    foreach (var item in json)
                    {
                        if (!(item is JObject article) ||
                            !TryGetNewsPostUrl((string)article["url"], out var url) ||
                            !DateTimeOffset.TryParse((string)article["publishedAt"], System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
                            return null;
                        articles.Add(new NewsArticle(url, date));
                    }
                    articles = articles.OrderByDescending(article => article.PublishedAt).ToList();
                }
                catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is ArgumentException || ex is InvalidCastException)
                {
                    return null;
                }
            }
            if (articles.Count == 0 || string.Equals(articles[0].PageUrl.AbsoluteUri,
                previousContent?.Trim(), StringComparison.Ordinal)) return null;
            // Remember the newest URL, so adding/editing history never reopens an already seen article.
            return new Announcement(articles[0].PageUrl.AbsoluteUri, articles.AsReadOnly());
        }

        internal static bool TryGetNewsPostUrl(string text, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            // Match the exact origin and team path before Uri can normalize backslashes or dot segments.
            if (!value.StartsWith(NewsPostPrefix, StringComparison.Ordinal) ||
                !TryGetWebUrl(value, out var candidate))
                return false;

            var post = value.Substring(NewsPostPrefix.Length);
            var suffix = post.IndexOfAny(new[] { '?', '#' });
            if (suffix >= 0) post = post.Substring(0, suffix);
            post = Uri.UnescapeDataString(post);

            // A post is one non-empty path segment. Reject encoded/double-encoded traversal and separators.
            if (post.Length == 0 || post == "." || post == ".." ||
                post.IndexOfAny(new[] { '/', '\\', '%' }) >= 0 ||
                post.Any(char.IsWhiteSpace) || post.Any(char.IsControl) ||
                !candidate.AbsoluteUri.StartsWith(NewsPostPrefix, StringComparison.Ordinal))
                return false;

            uri = candidate;
            return true;
        }

        internal static bool TryGetWebUrl(string text, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            return !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl) &&
                   Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
                   !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
        }
    }
}
