using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum GPUSTATUS
    {
        NO_INFO,
        // no support at all
        NO,
        // runs fine
        OK,
        // requires fix from Discord
        WITH_FIX,
        // runs but with issues
        HAS_ISSUES
    }

    public class Metadata
    {
        public string game_name { get; set; } = "";
        public string game_genre { get; set; } = "";
        public string icon_name { get; set; } = "";
        public string platform;
        public string release_year;
        [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS nvidia;
        public string nvidia_issues;
        [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS amd;
        public string amd_issues;
        [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS intel;
        public string intel_issues;
        public string general_issues;
        public string wheel_rotation;
        public string[] supported_versions;
        public string tpo_version;

        public override string ToString()
        {
            var nvidiaIssues = !string.IsNullOrEmpty(nvidia_issues) ? nvidia_issues + "\n" : string.Empty;
            var amdIssues = !string.IsNullOrEmpty(amd_issues) ? amd_issues + "\n" : string.Empty;
            var intelIssues = !string.IsNullOrEmpty(intel_issues) ? intel_issues + "\n" : string.Empty;
            var wheelRotation = !string.IsNullOrEmpty(wheel_rotation) ? $"Wheel Rotation: { wheel_rotation}\n" : string.Empty;
            var versions = "";

            if (supported_versions != null && supported_versions.Length > 0)
            {
                versions = "Supported versions: ";
                bool first = true;
                foreach (var version in supported_versions)
                {
                    if (first)
                    {
                        versions += $"{version}";
                        first = false;
                    } else
                    {
                        versions += $", {version}";
                    }
                }
                versions += "\n";
            }

            return $"Platform: {platform}\n" +
                $"Release year: {release_year}\n" +
                "GPU Support:\n" +
                $"NVIDIA: {nvidia.ToString().Replace('_', ' ')}\n" +
                $"{nvidiaIssues}" +
                $"AMD: {amd.ToString().Replace('_', ' ')}\n" +
                $"{amdIssues}" +
                $"Intel: {intel.ToString().Replace('_', ' ')}\n" +
                $"{intelIssues}" +
                $"{wheelRotation}" +
                $"{versions}" +
                $"{(!string.IsNullOrEmpty(tpo_version) ? $"TPO supports version {tpo_version}" : "")}" + 
                $"{(!string.IsNullOrEmpty(general_issues) ? $"GENERAL ISSUES:\n{general_issues}" : "")}";
        }
    }
}
