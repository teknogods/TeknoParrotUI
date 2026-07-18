using System;
using TeknoParrotUi.Common.Activation;

namespace InputMethodAudit
{
    internal static class ActivationContractTest
    {
        public static int Run()
        {
            try
            {
                const string header = "Windows Registry Editor Version 5.00\r\n\r\n" +
                    "[HKEY_CURRENT_USER\\SOFTWARE\\TeknoGods\\TeknoParrot]\r\n";
                True(TeknoParrotActivation.IsValidSeedExportText(
                    header + "\"PatreonSerialKey\"=hex:41,42,43\r\n"),
                    "REG_BINARY activation export");
                True(TeknoParrotActivation.IsValidSeedExportText(
                    header + "\"PatreonSerialKey\"=\"activation-code\"\r\n"),
                    "REG_SZ activation export");
                False(TeknoParrotActivation.IsValidSeedExportText(
                    header + "\"UnrelatedValue\"=hex:41\r\n"),
                    "missing activation value rejected");
                False(TeknoParrotActivation.IsValidSeedExportText(
                    "Windows Registry Editor Version 5.00\r\n" +
                    "[HKEY_CURRENT_USER\\SOFTWARE\\Other]\r\n" +
                    "\"PatreonSerialKey\"=hex:41\r\n"),
                    "wrong registry key rejected");
                False(TeknoParrotActivation.IsValidSeedExportText(
                    header + "\"PatreonSerialKey\"=dword:00000001\r\n"),
                    "unsupported registry type rejected");

                Console.WriteLine("TeknoParrot activation seed contract: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Activation contract test failed: " + error.Message);
                return 1;
            }
        }

        private static void True(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name + " was false");
        }

        private static void False(bool value, string name) => True(!value, name);
    }
}
