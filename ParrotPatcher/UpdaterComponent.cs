using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParrotPatcher
{
    internal class UpdaterComponent
    {
        // component name
        public string name { get; set; }
        // location of file to check version from, i.e TeknoParrot\TeknoParrot.dll
        public string location { get; set; }
        // repository name, if not set it will use name as the repo name
        public string reponame { get; set; }
        // if set, the changelog button will link to the commits page, if not it will link to the release directly
        public bool opensource { get; set; } = true;
        // if set, the updater will extract the files into this folder rather than the name folder
        public string folderOverride { get; set; }
        // if set, it will grab the update from a specific github user's account, if not set it'll use teknogods
        public string userName { get; set; }
        public string fullUrl
        {
            get { return "https://github.com/" + (!string.IsNullOrEmpty(userName) ? userName : "teknogods") + "/" + (!string.IsNullOrEmpty(reponame) ? reponame : name) + "/"; }
        }
        // if set, this will write the version to a text file when extracted then refer to that when checking.
        public bool manualVersion { get; set; } = false;
        // local version number
        public string _localVersion;
        public string localVersion
        {
            get
            {
                if (_localVersion == null)
                {
                    if (File.Exists(location))
                    {
                        if (manualVersion)
                        {
                            if (File.Exists(Path.GetDirectoryName(location) + "\\.version"))
                                _localVersion = File.ReadAllText(Path.GetDirectoryName(location) + "\\.version");
                            else
                                _localVersion = "unknown";
                        }
                        else
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(location);
                            var pv = fvi.ProductVersion;
                            _localVersion = (fvi != null && pv != null) ? pv : "unknown";
                        }
                    }
                    else
                    {
                        _localVersion = "Not Installed";
                    }
                }

                return _localVersion;
            }
        }
    }
}
