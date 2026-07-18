using System;
using System.Collections.Generic;

namespace InputMethodAudit
{
    /// <summary>
    /// One command for the complete host-only Android launch/input regression
    /// suite. Optional arguments are forwarded as dump roots to the batch test.
    /// </summary>
    internal static class AndroidTestSuite
    {
        public static int Run(IReadOnlyList<string> batchRoots)
        {
            var tests = new (string Name, Func<int> Run)[]
            {
                ("profile support policy", AndroidProfileSupportTest.Run),
                ("managed importer", AndroidManagedImportTest.Run),
                ("shared OpenParrot archive", SharedOpenParrotArchiveAdapterTest.Run),
                ("Winlator launch contract", AndroidWinlatorContractTest.Run),
                ("controls catalog", AndroidControlsCatalogTest.Run),
                ("FastIO encoders", AndroidFastIoInputTest.Run),
                ("ALLS Initial D encoder", AndroidAllsIdtaInputTest.Run),
                ("shared-page encoders", AndroidSharedStateInputTest.Run),
                ("forwarded protocols", ForwardedInputProtocolTest.Run),
                ("CXBXR dump coverage", AndroidCxbxrCoverageTest.Run),
                ("real dump coverage", () => AndroidBatchCoverageTest.Run(batchRoots))
            };

            foreach (var test in tests)
            {
                Console.WriteLine($"\n=== Android {test.Name} ===");
                if (test.Run() != 0)
                {
                    Console.Error.WriteLine($"Android test suite: FAIL ({test.Name})");
                    return 1;
                }
            }

            Console.WriteLine("\nAndroid host-only test suite: PASS");
            return 0;
        }
    }
}
