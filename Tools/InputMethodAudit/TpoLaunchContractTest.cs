using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class TpoLaunchContractTest
    {
        public static int Run()
        {
            var failures = new List<string>();

            if (!TPOConfig.IsTrustedWebUri(
                    new Uri("https://teknoparrot.com:3333/Home/Chat")))
                failures.Add("The configured TPO origin was rejected.");
            if (TPOConfig.IsTrustedWebUri(
                    new Uri("http://teknoparrot.com:3333/Home/Chat")))
                failures.Add("An insecure TPO origin was accepted.");
            if (TPOConfig.IsTrustedWebUri(
                    new Uri("https://teknoparrot.com.example:3333/Home/Chat")))
                failures.Add("A lookalike TPO origin was accepted.");
            if (TPOConfig.IsTrustedWebUri(
                    new Uri("https://teknoparrot.com/Home/Chat")))
                failures.Add("The wrong TPO port was accepted.");

            if (!TPOConfig.TryBuildLaunchEnvironment(
                    "room-123", "1", "Player", "4", out var environment) ||
                environment != "room-123|1|Player|4")
                failures.Add("Valid room data did not produce the expected environment.");
            if (TPOConfig.TryBuildLaunchEnvironment(
                    "room|injected", "1", "Player", "4", out _))
                failures.Add("A room delimiter injection was accepted.");
            if (TPOConfig.TryBuildLaunchEnvironment(
                    "room", "1", "Player\nInjected", "4", out _))
                failures.Add("A control character was accepted.");

            if (failures.Count == 0)
            {
                Console.WriteLine("TPO launch contract: PASS (7/7)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"TPO launch contract: FAIL ({failures.Count} failure(s))");
            return 1;
        }
    }
}
