using System.Diagnostics;
using System.Globalization;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal static class NativeArcadeLaunch
    {
        public static ProcessStartInfo FromTeknoParrotUi(ProcessStartInfo info)
        {
            info.CreateNoWindow = true;
            using (var launcher = Process.GetCurrentProcess())
            {
                info.EnvironmentVariables["TP_TPUI_PARENT_PID"] = launcher.Id.ToString(CultureInfo.InvariantCulture);
                info.EnvironmentVariables["TP_TPUI_PARENT_EXE"] = launcher.MainModule.FileName;
            }
            return info;
        }
    }
}
