using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.InputProfiles.Helpers;

namespace InputMethodAudit
{
    internal static class Wmmt3CardApiTest
    {
        public static int Run()
        {
            var failures = RunAsync().GetAwaiter().GetResult();
            if (failures.Count == 0)
            {
                Console.WriteLine("WMMT3 card API: PASS (4/4)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"WMMT3 card API: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static async Task<List<string>> RunAsync()
        {
            var failures = new List<string>();
            var current = new Uri("http://127.0.0.1:8080/api/v1/insertedCard?loadonly");
            var legacy = new Uri("http://127.0.0.1:8080/actions?insert=");

            var modern = new ScriptedHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK));
            using (var client = new HttpClient(modern))
            {
                var result = await WMMT3Cards.SendInsertRequestAsync(
                    client, current, legacy, CancellationToken.None);
                if (!result || modern.Requests.Count != 1 ||
                    modern.Requests[0] != HttpMethod.Post)
                    failures.Add("Modern API success did not use exactly one POST.");
            }

            var legacyFallback = new ScriptedHandler(
                request => request.Method == HttpMethod.Post
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK));
            using (var client = new HttpClient(legacyFallback))
            {
                var result = await WMMT3Cards.SendInsertRequestAsync(
                    client, current, legacy, CancellationToken.None);
                if (!result ||
                    legacyFallback.Requests.Count != 2 ||
                    legacyFallback.Requests[0] != HttpMethod.Post ||
                    legacyFallback.Requests[1] != HttpMethod.Get)
                    failures.Add("A missing modern endpoint did not fall back to one legacy GET.");
            }

            var transportFallback = new ScriptedHandler(
                request =>
                {
                    if (request.Method == HttpMethod.Post)
                        throw new HttpRequestException("modern endpoint unavailable");
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
            using (var client = new HttpClient(transportFallback))
            {
                var result = await WMMT3Cards.SendInsertRequestAsync(
                    client, current, legacy, CancellationToken.None);
                if (!result ||
                    transportFallback.Requests.Count != 2 ||
                    transportFallback.Requests[1] != HttpMethod.Get)
                    failures.Add("A modern transport failure did not try the legacy endpoint.");
            }

            var bothFailed = new ScriptedHandler(
                _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using (var client = new HttpClient(bothFailed))
            {
                var result = await WMMT3Cards.SendInsertRequestAsync(
                    client, current, legacy, CancellationToken.None);
                if (result || bothFailed.Requests.Count != 2)
                    failures.Add("Failed modern and legacy endpoints reported success.");
            }

            return failures;
        }

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

            public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
            {
                _response = response;
            }

            public List<HttpMethod> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request.Method);
                return Task.FromResult(_response(request));
            }
        }
    }
}
