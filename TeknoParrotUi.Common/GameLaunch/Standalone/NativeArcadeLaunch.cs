using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Common.GameLaunch
{
    internal static class NativeArcadeLaunch
    {
        public static void AddPresentation(GameProfile profile, string root, List<string> arguments)
        {
            string Setting(string name, string fallback) => profile.ConfigValues?.FirstOrDefault(field => field.FieldName == name)?.FieldValue ?? fallback;
            var mode = Setting("Window Mode", "Windowed");
            if (mode != "Windowed" && mode != "Fullscreen") throw new ArgumentException("Window Mode must be Windowed or Fullscreen");
            if (mode == "Fullscreen") arguments.Add("--fullscreen");
            var crt = Setting("CRT Shader", "off");
            if (!new[] { "off", "lottes", "lottes-downsample" }.Contains(crt)) throw new ArgumentException("Invalid CRT Shader");
            arguments.AddRange(new[] { "--crt", crt });
            var bezel = Setting("Use Bezel", "1");
            if (bezel != "0" && bezel != "1") throw new ArgumentException("Use Bezel must be on or off");
            if (bezel == "1")
            {
                Directory.CreateDirectory(Path.Combine(root, "bezels"));
                arguments.Add("--use-bezel");
            }
        }

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
