using System;
using System.Collections.Generic;
using System.IO;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace InputMethodAudit
{
    internal static class LaunchEnvironmentIsolationTest
    {
        private const string ProbeVariable = "TP_LOGTOFILE";
        private const string OpenSslProbeVariable = "OPENSSL_ia32cap";
        private static readonly string[] OptionalLaunchVariables =
        {
            "TP_DIRECTHOOK",
            "TP_REMOTETHREAD",
            "tp_msysType",
            "TP_ETH",
            "TP_MSAA",
            "TP_NUSOUND",
            "TEA_DIR"
        };

        public static int Run()
        {
            var originalEnvironment = Environment.GetEnvironmentVariable(ProbeVariable);
            var originalOpenSslEnvironment =
                Environment.GetEnvironmentVariable(OpenSslProbeVariable);
            var originalOptionalEnvironment = new Dictionary<string, string>();
            foreach (var variable in OptionalLaunchVariables)
            {
                originalOptionalEnvironment[variable] =
                    Environment.GetEnvironmentVariable(variable);
            }
            var originalParrotData = Lazydata.ParrotData;
            var failures = new List<string>();

            try
            {
                Environment.SetEnvironmentVariable(ProbeVariable, "parent-value");
                Environment.SetEnvironmentVariable(OpenSslProbeVariable, "parent-cpu-value");
                foreach (var variable in OptionalLaunchVariables)
                    Environment.SetEnvironmentVariable(variable, "stale-parent-value");
                Lazydata.ParrotData = new ParrotData
                {
                    Elfldr2LogToFile = true,
                    Elfldr2NetworkAdapterName = "test-adapter"
                };

                var profile = new GameProfile
                {
                    EmulatorType = EmulatorType.ElfLdr2,
                    ConfigValues = new List<FieldInformation>
                    {
                        new FieldInformation
                        {
                            FieldName = "Windowed",
                            FieldValue = "1"
                        },
                        new FieldInformation
                        {
                            FieldName = "MSAA Level",
                            FieldValue = "4"
                        }
                    }
                };
                var game = Path.Combine(Path.GetTempPath(), "tp-env-test", "game.exe");
                var loader = Path.Combine(Path.GetTempPath(), "tp-env-test", "x64", "BudgieLoader_x64.exe");

                var startInfo = GameLaunchArguments.BuildProcessStartInfo(
                    profile,
                    game,
                    isTest: false,
                    loader,
                    "TeknoParrot64");

                Expect("1", startInfo.EnvironmentVariables["tp_windowed"], "child window mode", failures);
                Expect("1", startInfo.EnvironmentVariables[ProbeVariable], "child logging mode", failures);
                Expect("test-adapter", startInfo.EnvironmentVariables["TP_ETH"], "child network adapter", failures);
                Expect("4", startInfo.EnvironmentVariables["TP_MSAA"], "child MSAA", failures);
                Expect("parent-value", Environment.GetEnvironmentVariable(ProbeVariable), "parent logging mode", failures);

                var openSslInfo = new System.Diagnostics.ProcessStartInfo();
                GameLaunchArguments.ApplyOpenSslFix(
                    new GameProfile { EmulationProfile = EmulationProfile.ALLSSWDC },
                    openSslInfo);
                Expect(
                    ":~0x20000000",
                    openSslInfo.EnvironmentVariables[OpenSslProbeVariable],
                    "child OpenSSL CPU mask",
                    failures);
                Expect(
                    "parent-cpu-value",
                    Environment.GetEnvironmentVariable(OpenSslProbeVariable),
                    "parent OpenSSL CPU mask",
                    failures);

                Lazydata.ParrotData = new ParrotData();
                var cleanStartInfo = GameLaunchArguments.BuildProcessStartInfo(
                    new GameProfile
                    {
                        EmulatorType = EmulatorType.ElfLdr2,
                        ConfigValues = new List<FieldInformation>()
                    },
                    game,
                    isTest: false,
                    loader,
                    "TeknoParrot64");
                foreach (var variable in OptionalLaunchVariables)
                    ExpectMissing(cleanStartInfo, variable, failures);

                if (failures.Count == 0)
                {
                    Console.WriteLine("Launch environment isolation: PASS (14/14)");
                    return 0;
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ProbeVariable, originalEnvironment);
                Environment.SetEnvironmentVariable(
                    OpenSslProbeVariable,
                    originalOpenSslEnvironment);
                foreach (var entry in originalOptionalEnvironment)
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                Lazydata.ParrotData = originalParrotData;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"Launch environment isolation: FAIL ({failures.Count} failure(s))");
            return 1;
        }

        private static void Expect(
            string expected,
            string actual,
            string scenario,
            ICollection<string> failures)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                failures.Add($"{scenario}: expected '{expected}', got '{actual ?? "<null>"}'.");
        }

        private static void ExpectMissing(
            System.Diagnostics.ProcessStartInfo startInfo,
            string variable,
            ICollection<string> failures)
        {
            if (startInfo.EnvironmentVariables.ContainsKey(variable))
            {
                failures.Add(
                    $"stale child variable {variable}: expected it to be removed, got " +
                    $"'{startInfo.EnvironmentVariables[variable]}'.");
            }
        }
    }
}
