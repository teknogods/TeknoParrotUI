using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ParrotPatcher
{
    internal class Components
    {
        public List<UpdaterComponent> components = new List<UpdaterComponent>();

        public Components()
        {
            components = new List<UpdaterComponent>()
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
}
