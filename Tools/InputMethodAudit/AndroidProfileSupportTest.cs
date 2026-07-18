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

            foreach (var emulatorType in Enum.GetValues<EmulatorType>())
            {
                if (emulatorType is EmulatorType.OpenParrot or EmulatorType.pcsx2x6)
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
