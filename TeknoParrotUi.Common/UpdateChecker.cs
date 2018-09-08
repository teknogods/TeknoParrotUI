using System;

namespace TeknoParrotUi.Common
{
    public static class UpdateChecker
    {
        public static bool CheckForUpdate(string currentVersion, string newVersion)
        {
            // Validate

            // Always use format x.xx
            if (newVersion.Length != 4)
                return false;

            // Always use format x.xx
            if (currentVersion.Length != 4)
                return false;

            // Checkk that can be parsed, in case of mistake
            if (!int.TryParse(currentVersion.Replace(".", ""), out var currentVer))
                return false;

            // Check that can be parsed, instead of http error
            if (!int.TryParse(newVersion.Replace(".", ""), out var newVer))
                return false;

            // Compare
            return currentVer < newVer;
        }
    }
}
