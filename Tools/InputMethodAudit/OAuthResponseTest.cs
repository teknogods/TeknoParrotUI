using System;
using System.Collections.Generic;
using TeknoParrotUi.Common.Auth;

namespace InputMethodAudit
{
    internal static class OAuthResponseTest
    {
        public static int Run()
        {
            var failures = new List<string>();

            ExpectAccepted(
                """{"access_token":"access","refresh_token":"refresh","expires_in":3600}""",
                "access",
                "refresh",
                3600,
                failures);
            ExpectAccepted(
                """{"access_token":"access","expires_in":60}""",
                "access",
                null,
                60,
                failures);
            ExpectRejected("not-json", failures);
            ExpectRejected("""{"refresh_token":"refresh","expires_in":3600}""", failures);
            ExpectRejected("""{"access_token":"access","expires_in":0}""", failures);
            ExpectRejected("null", failures);

            if (failures.Count == 0)
            {
                Console.WriteLine("OAuth token response validation: PASS (6/6)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine(
                $"OAuth token response validation: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static void ExpectAccepted(
            string json,
            string expectedAccess,
            string expectedRefresh,
            int expectedExpiry,
            ICollection<string> failures)
        {
            if (!OAuthClient.TryReadTokenResponse(
                    json,
                    out var access,
                    out var refresh,
                    out var expiry) ||
                access != expectedAccess ||
                refresh != expectedRefresh ||
                expiry != expectedExpiry)
                failures.Add($"Valid OAuth response was rejected: {json}");
        }

        private static void ExpectRejected(
            string json,
            ICollection<string> failures)
        {
            if (OAuthClient.TryReadTokenResponse(json, out _, out _, out _))
                failures.Add($"Invalid OAuth response was accepted: {json}");
        }
    }
}
