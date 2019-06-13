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

    public class Description
    {
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

        public override string ToString()
        {
            var nvidiaIssues = !string.IsNullOrEmpty(nvidia_issues) ? nvidia_issues + "\n" : string.Empty;
            var amdIssues = !string.IsNullOrEmpty(amd_issues) ? amd_issues + "\n" : string.Empty;
            var intelIssues = !string.IsNullOrEmpty(intel_issues) ? intel_issues + "\n" : string.Empty;
            return $"Platform: {platform}\n" +
                $"Release year: {release_year}\n" +
                "GPU Support:\n" +
                $"NVIDIA: {nvidia.ToString().Replace('_', ' ')}\n" +
                $"{nvidiaIssues}" +
                $"AMD: {amd.ToString().Replace('_', ' ')}\n" +
                $"{amdIssues}" +
                $"Intel: {intel.ToString().Replace('_', ' ')}\n" +
                $"{intelIssues}" +
                $"{(!string.IsNullOrEmpty(general_issues) ? $"GENERAL ISSUES:\n{general_issues}" : "")}";
        }
    }
}
