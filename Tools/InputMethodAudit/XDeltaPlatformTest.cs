using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class XDeltaPlatformTest
    {
        public static int Run()
        {
            var failures = new List<string>();
            if (!XDelta3.IsNativeDependencySupportedPlatform(isWindows: true))
                failures.Add("Windows was rejected by the xdelta platform gate.");
            if (XDelta3.IsNativeDependencySupportedPlatform(isWindows: false))
                failures.Add("A non-Windows platform passed the xdelta platform gate.");

            if (failures.Count == 0)
            {
                Console.WriteLine("xdelta platform gate: PASS (2/2)");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine(failure);
            Console.Error.WriteLine($"xdelta platform gate: FAIL ({failures.Count} failure(s))");
            return 1;
        }
    }
}
