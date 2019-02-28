using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    [Serializable]
    [XmlRoot("GameProfile")]
    public class GameProfile
    {
        public string GameName { get; set; }
        public string GamePath { get; set; }
        public string TestMenuParameter { get; set; }
        public bool TestMenuIsExecutable { get; set; }
        public string ExtraParameters { get; set; }
        public string TestMenuExtraParameters { get; set; }
        public string IconName { get; set; }
        public string ValidMd5 { get; set; }
        public string Description { get; set; }
        [XmlIgnore]
        public string FileName { get; set; }
        public List<FieldInformation> ConfigValues { get; set; }
        public List<JoystickButtons> JoystickButtons { get; set; }
        public EmulationProfile EmulationProfile { get; set; }
        public int GameProfileRevision { get; set; }
        public bool HasSeparateTestMode { get; set; }
        public bool Is64Bit { get; set; }
        public EmulatorType EmulatorType { get; set; }
        public bool GunGame { get; set; }
    }
}
