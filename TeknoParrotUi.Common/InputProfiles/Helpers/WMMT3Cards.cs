using System.Net.Http;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.InputProfiles.Helpers
{
    internal static class WMMT3Cards
    {
        private static readonly Uri NewApiUri =
            new Uri("http://127.0.0.1:8080/api/v1/insertedCard?loadonly");
        private static readonly Uri LegacyApiUri =
            new Uri("http://127.0.0.1:8080/actions?insert=");
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        private static int insertionInFlight;

        public static void InsertCard()
        {
            // This runs from the input polling path: never block it while the local
            // card emulator is starting or absent, and suppress duplicate button
            // events until the current request has completed.
            if (Interlocked.Exchange(ref insertionInFlight, 1) != 0)
                return;

            _ = InsertCardAsync();
        }

        private static async Task InsertCardAsync()
        {
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await SendInsertRequestAsync(
                        Client,
                        NewApiUri,
                        LegacyApiUri,
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("WMMT3Cards: card insertion timed out");
            }
            finally
            {
                Volatile.Write(ref insertionInFlight, 0);
            }
        }

        internal static async Task<bool> SendInsertRequestAsync(
            HttpClient client,
            Uri newApiUri,
            Uri legacyApiUri,
            CancellationToken cancellationToken)
        {
            try
            {
                // Current YACardEmu versions use POST. Only call the legacy API
                // when the current endpoint is unavailable, otherwise one button
                // press can insert the same card twice.
                using var currentResponse = await client
                    .PostAsync(newApiUri, content: null, cancellationToken)
                    .ConfigureAwait(false);
                if (currentResponse.IsSuccessStatusCode)
                    return true;
                Debug.WriteLine(
                    $"WMMT3Cards: current API returned {(int)currentResponse.StatusCode}; trying legacy API");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(
                    $"WMMT3Cards: current API unavailable ({ex.Message}); trying legacy API");
            }

            try
            {
                using var legacyResponse = await client
                    .GetAsync(legacyApiUri, cancellationToken)
                    .ConfigureAwait(false);
                if (legacyResponse.IsSuccessStatusCode)
                    return true;
                Debug.WriteLine(
                    $"WMMT3Cards: legacy API returned {(int)legacyResponse.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"WMMT3Cards: card emulator unavailable ({ex.Message})");
            }
            return false;
        }
    }
}
