using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common
{
    public class GithubAsset
    {
        public string browser_download_url;
        public string name;
        public long size;
        /// <summary>
        /// Authoritative content digest supplied by GitHub or the TeknoParrot
        /// updater service, formatted as "sha256:&lt;64 hex characters&gt;".
        /// Android runtime packages are rejected when this is absent.
        /// </summary>
        public string digest;
    }

    public class GithubRelease
    {
        public string target_commitish;
        public int id;
        public string tag_name;
        public List<GithubAsset> assets;
        public string name;
        public string body;
    }
}
