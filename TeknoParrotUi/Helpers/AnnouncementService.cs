using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    internal sealed class Announcement
    {
        public string Content { get; }
        public Uri PageUrl { get; }

        public Announcement(string content, Uri pageUrl)
        {
            Content = content;
            PageUrl = pageUrl;
        }
    }

    internal static class AnnouncementService
    {
        internal const int MaximumContentBytes = 16 * 1024;
        private const string NewsPostPrefix = "https://www.patreon.com/TeknoParrotTeam/posts/";

        internal static bool ShouldCheckAtStartup(string[] arguments, bool debuggerAttached)
        {
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
                using (var request = new HttpRequestMessage(HttpMethod.Get, sourceUri))
                {
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
                    using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode || response.Content == null)
                            return null;

                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (cancellationToken.IsCancellationRequested || content.Length > MaximumContentBytes ||
                            string.Equals(content, previousContent, StringComparison.Ordinal) ||
                            !TryGetNewsPostUrl(content, out var pageUrl))
                            return null;

                        return new Announcement(content, pageUrl);
                    }
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
