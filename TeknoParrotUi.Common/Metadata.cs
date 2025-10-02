using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        [JsonProperty("game_name")]
        public string game_name { get; set; } = "";
        [JsonProperty("game_genre")]
        public string game_genre { get; set; } = "";
        [JsonProperty("icon_name")]
        public string icon_name { get; set; } = "";
        [JsonProperty("platform")]
        public string platform { get; set; } = "";
        [JsonProperty("release_year")]
        public string release_year { get; set; } = "";
        [JsonConverter(typeof(StringEnumConverter), true)]
        [JsonProperty("nvidia")]
        public GPUSTATUS nvidia;
        [JsonProperty("nvidia_issues")]
        public string nvidia_issues;
        [JsonConverter(typeof(StringEnumConverter), true)]
        [JsonProperty("amd")]
        public GPUSTATUS amd;
        [JsonProperty("amd_issues")]
        public string amd_issues;
        [JsonConverter(typeof(StringEnumConverter), true)]
        public GPUSTATUS intel;
        [JsonProperty("intel_issues")]
        public string intel_issues;
        [JsonProperty("general_issues")]
        public string general_issues;
        [JsonProperty("wheel_rotation")]
        public string wheel_rotation;
        [JsonProperty("supported_versions")]
        public string[] supported_versions;
        [JsonProperty("tpo_version")]
        public string tpo_version;

        public override string ToString()
        {
            var wheelRotation = !string.IsNullOrEmpty(wheel_rotation) ? $"Wheel Rotation: {wheel_rotation}\n" : string.Empty;
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
                    }
                    else
                    {
                        versions += $", {version}";
                    }
                }
                versions += "\n";
            }

            return $"Platform: {platform}\n" +
                $"Release year: {release_year}\n" +
                $"{wheelRotation}" +
                $"{versions}" +
                $"{(!string.IsNullOrEmpty(tpo_version) ? $"TPO supports version {tpo_version}" : "")}" +
                $"{(!string.IsNullOrEmpty(general_issues) ? $"GENERAL ISSUES/NOTES:\n{general_issues}" : "")}";
        }

        public string GetGpuIssues()
        {
            var nvidiaIssues = !string.IsNullOrEmpty(nvidia_issues) ? $"NVIDIA: {nvidia_issues}\n" : string.Empty;
            var amdIssues = !string.IsNullOrEmpty(amd_issues) ? $"AMD: {amd_issues}\n" : string.Empty;
            var intelIssues = !string.IsNullOrEmpty(intel_issues) ? $"Intel: {intel_issues}\n" : string.Empty;

            var result = nvidiaIssues + amdIssues + intelIssues;
            return string.IsNullOrEmpty(result) ? string.Empty : $"GPU Issues:\n{result}";
        }
    }
}
