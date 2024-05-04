using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum InputApi
    {
        DirectInput,
        XInput,
        RawInput
    }

    [Serializable]
    [XmlRoot("GameProfile")]
    public class GameProfile
    {
        public string ProfileName { get; set; }
        public string GameNameInternal { get; set; } = "";
        public string GameGenreInternal { get; set; }
        public string GamePath { get; set; }
        public string TestMenuParameter { get; set; }
        public bool TestMenuIsExecutable { get; set; }
        public string ExtraParameters { get; set; }
        public string TestMenuExtraParameters { get; set; }
        public string IconName { get; set; }
        public string ValidMd5 { get; set; }
        public bool ResetHint { get; set; }
        public string InvalidFiles { get; set; }
        [XmlIgnore]
        public Metadata GameInfo { get; set; }
        [XmlIgnore]
        public string FileName { get; set; }
        public List<FieldInformation> ConfigValues { get; set; }
        public List<JoystickButtons> JoystickButtons { get; set; }
        public EmulationProfile EmulationProfile { get; set; }
        public int GameProfileRevision { get; set; }
        public bool HasSeparateTestMode { get; set; }
        public bool Is64Bit { get; set; }
        public bool TestExecIs64Bit { get; set; }
        public EmulatorType EmulatorType { get; set; }
        public bool Patreon { get; set; }
        public bool RequiresAdmin { get; set; }
        public int msysType { get; set; }
        public bool InvertedMouseAxis { get; set; }
        public bool GunGame { get; set; }
        public bool DevOnly { get; set; }
        public string ExecutableName { get; set; }
        public string ExecutableName2 { get; set; }
        public bool HasTwoExecutables { get; set; } = false;
        public bool LaunchSecondExecutableFirst { get; set; } = false;
        public string SecondExecutableArguments { get; set; }
        public string GamePath2 { get; set; }
        // advanced users only!
        public string CustomArguments { get; set; }
        public short xAxisMin { get; set; } = 0;
        public short xAxisMax { get; set; } = 255;
        public short yAxisMin { get; set; } = 0;
        public short yAxisMax { get; set; } = 255;
        public byte GasAxisMin { get; set; } = 0;
        public byte GasAxisMax { get; set; } = 255;
        public string OnlineProfileURL { get; set; } = "";
        public bool IsLegacy { get; set; } = false;

        public override string ToString()
        {
            return GameNameInternal;
        }
    }
}
