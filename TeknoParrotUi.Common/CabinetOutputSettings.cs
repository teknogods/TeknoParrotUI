using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace TeknoParrotUi.Common
{
    [Serializable]
    public sealed class CabinetOutputSettings
    {
        // Read older saved settings; the game XML now owns the output selection.
        public bool ShouldSerializeRoute() => false;
        public string Route { get; set; } = "Windows Outputs";
        public int ListenerPort { get; set; } = 8000;
        public string LineEnding { get; set; } = "CR";
        public bool Discovery { get; set; } = false;
        public int DiscoveryPort { get; set; } = 8001;

        public CabinetOutputSettings Clone() => (CabinetOutputSettings)MemberwiseClone();

        // Only emulator families already released through this UI are eligible.
        public static bool Supports(GameProfile profile)
        {
            if (profile == null) return false;
            switch (profile.EmulatorType)
            {
                case EmulatorType.TeknoModel1:
                case EmulatorType.TeknoModel2:
                case EmulatorType.TeknoHornet:
                case EmulatorType.TeknoHNG64:
                case EmulatorType.TeknoGClub:
                case EmulatorType.TeknoCobra:
                case EmulatorType.TeknoZeus:
                case EmulatorType.TeknoViper:
                case EmulatorType.TeknoVegas:
                case EmulatorType.TeknoVUnit:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsOutputField(FieldInformation field) =>
            field?.CategoryName == "General" && field.FieldName == "Outputs";

        public static string GetRoute(GameProfile profile)
        {
            var route = profile?.ConfigValues?.FirstOrDefault(IsOutputField)?.FieldValue
                ?? profile?.CabinetOutputSettings?.Route;
            return route == "Network Outputs" ? "Network Outputs" : "Windows Outputs";
        }

        public string Validate()
        {
            if (ListenerPort < 1 || ListenerPort > 65535)
                return "Port must be between 1 and 65535.";
            if (DiscoveryPort < 1 || DiscoveryPort > 65535)
                return "Discovery port must be between 1 and 65535.";
            if (LineEnding != "CR" && LineEnding != "LF" && LineEnding != "CRLF")
                return "Choose a line ending.";
            return null;
        }

        public static void Apply(GameProfile profile, ProcessStartInfo info)
        {
            if (!Supports(profile)) return;
            var settings = profile.CabinetOutputSettings ?? new CabinetOutputSettings();
            // Only one output method runs at a time. Connected receivers share the
            // same output names and the MAME-compatible name/value separator.
            info.EnvironmentVariables["TP_OUTPUT_ROUTE"] = GetRoute(profile) == "Network Outputs" ? "network" : "windows";
            var port = settings.ListenerPort >= 1 && settings.ListenerPort <= 65535 ? settings.ListenerPort : 8000;
            info.EnvironmentVariables["TP_OUTPUT_PORT"] = port.ToString(CultureInfo.InvariantCulture);
            info.EnvironmentVariables["TP_OUTPUT_LINE_ENDING"] = settings.LineEnding == "LF" || settings.LineEnding == "CRLF"
                ? settings.LineEnding : "CR";
            info.EnvironmentVariables["TP_OUTPUT_DISCOVERY"] = settings.Discovery ? "1" : "0";
            var discoveryPort = settings.DiscoveryPort >= 1 && settings.DiscoveryPort <= 65535 ? settings.DiscoveryPort : 8001;
            info.EnvironmentVariables["TP_OUTPUT_DISCOVERY_PORT"] = discoveryPort.ToString(CultureInfo.InvariantCulture);
            // Do not pass obsolete prototype overrides to older emulator builds.
            foreach (var name in new[] { "TP_OUTPUT_SCOPE", "TP_OUTPUT_SEPARATOR" })
                info.EnvironmentVariables.Remove(name);
        }
    }
}
