using System;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class AndroidProfileSupportTest
    {
        public static int Run()
        {
            var failures = 0;

            Check(
                "OpenParrot x86",
                new GameProfile
                {
                    EmulatorType = EmulatorType.OpenParrot,
                    Is64Bit = false
                },
                expected: true);
            Check(
                "OpenParrot x64",
                new GameProfile
                {
                    EmulatorType = EmulatorType.OpenParrot,
                    Is64Bit = true
                },
                expected: true);
            Check(
                "PCSX2X6",
                new GameProfile { EmulatorType = EmulatorType.pcsx2x6 },
                expected: true);
            var supportedDolphinProfiles = new[]
            {
                EmulationProfile.MarioKartGP,
                EmulationProfile.MarioKartGP2,
                EmulationProfile.FZeroAX,
                EmulationProfile.VirtuaStriker3,
                EmulationProfile.VirtuaStriker4
            };
            foreach (var emulationProfile in supportedDolphinProfiles)
            {
                Check(
                    "TeknoDolphin " + emulationProfile,
                    new GameProfile
                    {
                        EmulatorType = EmulatorType.Dolphin,
                        EmulationProfile = emulationProfile
                    },
                    expected: true);
            }

            var rejectedDolphinProfiles = new[]
            {
                EmulationProfile.FZeroAXMonster,
                EmulationProfile.GekitouProYakyuu,
                EmulationProfile.KeyOfAvalon,
                EmulationProfile.Tatsunoko,
                EmulationProfile.TaitoTypeXGeneric
            };
            foreach (var emulationProfile in rejectedDolphinProfiles)
            {
                Check(
                    "unsupported TeknoDolphin " + emulationProfile,
                    new GameProfile
                    {
                        EmulatorType = EmulatorType.Dolphin,
                        EmulationProfile = emulationProfile
                    },
                    expected: false);
            }

            foreach (var emulatorType in Enum.GetValues<EmulatorType>())
            {
                if (emulatorType is EmulatorType.OpenParrot or EmulatorType.pcsx2x6 or
                    EmulatorType.Dolphin)
                    continue;
                Check(
                    emulatorType.ToString(),
                    new GameProfile { EmulatorType = emulatorType },
                    expected: false);
            }

            Check("null profile", null, expected: false);
            Console.WriteLine(
                failures == 0
                    ? "Android profile support policy: PASS"
                    : $"Android profile support policy: FAIL ({failures})");
            return failures == 0 ? 0 : 1;

            void Check(string name, GameProfile profile, bool expected)
            {
                var actual = PlatformCapabilities.IsAndroidGameProfileSupported(profile);
                if (actual == expected)
                    return;
                failures++;
                Console.Error.WriteLine(
                    $"{name}: expected supported={expected}, got {actual}.");
            }
        }
    }
}
