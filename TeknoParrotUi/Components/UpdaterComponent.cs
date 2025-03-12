using System.Collections.Generic;
using System.Diagnostics;  // For FileVersionInfo
using System.IO;          // For File and Path
using System.Reflection;  // For Assembly

namespace TeknoParrotUi.Components
{
    public class UpdaterComponent
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
                        _localVersion = TeknoParrotUi.Properties.Resources.UpdaterNotInstalled;
                    }
                }

                return _localVersion;
            }
        }


        public static List<UpdaterComponent> components = new List<UpdaterComponent>()
        {
            new UpdaterComponent
            {
                name = "TeknoParrotUI",
                location = Assembly.GetExecutingAssembly().Location
            },
            new UpdaterComponent
            {
                name = "OpenParrotWin32",
                location = Path.Combine("OpenParrotWin32", "OpenParrot.dll"),
                reponame = "OpenParrot"
            },
            new UpdaterComponent
            {
                name = "OpenParrotx64",
                location = Path.Combine("OpenParrotx64", "OpenParrot64.dll"),
                reponame = "OpenParrot"
            },
            new UpdaterComponent
            {
                name = "OpenSegaAPI",
                location = Path.Combine("TeknoParrot", "Opensegaapi.dll"),
                folderOverride = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "TeknoParrot",
                location = Path.Combine("TeknoParrot", "TeknoParrot.dll"),
                opensource = false
            },
            new UpdaterComponent
            {
                name = "TeknoParrotN2",
                location = Path.Combine("N2", "TeknoParrot.dll"),
                reponame = "TeknoParrot",
                opensource = false,
                folderOverride = "N2"
            },
            /*
            new UpdaterComponent
            {
                name = "SegaTools",
                location = Path.Combine("SegaTools", "idzhook.dll"),
                reponame = "SegaToolsTP",
                folderOverride = "SegaTools",
                userName = "nzgamer41"
            },
            */
            new UpdaterComponent
            {
                name = "OpenSndGaelco",
                location = Path.Combine("TeknoParrot", "OpenSndGaelco.dll"),
                folderOverride = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "OpenSndVoyager",
                location = Path.Combine("TeknoParrot", "OpenSndVoyager.dll"),
                folderOverride = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "ScoreSubmission",
                location = Path.Combine("TeknoParrot", "ScoreSubmission.dll"),
                folderOverride = "TeknoParrot",
                reponame = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "TeknoDraw",
                location = Path.Combine("TeknoParrot", "TeknoDraw64.dll"),
                folderOverride = "TeknoParrot",
                reponame = "TeknoParrot"
            },
            new UpdaterComponent
            {
                name = "TeknoParrotElfLdr2",
                location = Path.Combine("ElfLdr2", "TeknoParrot.dll"),
                reponame = "TeknoParrot",
                opensource = false,
                manualVersion = true,
                folderOverride = "ElfLdr2"
            }
        };
    }
}