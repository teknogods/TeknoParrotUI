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
    }

    public class GithubRelease
    {
        public string target_commitish;
        public int id;
        public string tag_name;
        public List<GithubAsset> assets;
        public string name;
    }
}