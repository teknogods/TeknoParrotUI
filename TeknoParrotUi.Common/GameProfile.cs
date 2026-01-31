using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum InputApi
    {
        DirectInput,
        XInput,
        RawInput,
        RawInputTrackball
    }

    public enum OnlineIdType
    {
        None,
        SegaId,
        NamcoId,
        HighscoreSerial,
        MarioKartId,
        NesysId
    }

    [Serializable]
    public class RPCS3Config
    {
        public List<RPCS3ConfigItem> ConfigItems { get; set; } = new List<RPCS3ConfigItem>();
    }

    [Serializable]
    public class RPCS3ConfigItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
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
        public bool HasTpoSupport { get; set; } = false;
        public bool IsTpoExclusive { get; set; } = false;
        public bool RequiresBepInEx { get; set; } = false;
        public bool LaunchMinimized { get; set; } = false;
        public bool LaunchSecondExecutableMinimized { get; set; } = false;
        // Fields to help us auto fill the online ids if the user is logged in via the account system
        public string OnlineIdFieldName { get; set; } = "";
        public OnlineIdType OnlineIdType { get; set; } = OnlineIdType.None;
        public bool Requires4GBPatch { get; set; } = false;
        // Rotary Encoder Configuration
        public float Rotary1Sensitivity { get; set; } = 1.0f;
        public float Rotary2Sensitivity { get; set; } = 1.0f;
        public float Rotary3Sensitivity { get; set; } = 1.0f;
        public float Rotary4Sensitivity { get; set; } = 1.0f;
        public byte Rotary1Increment { get; set; } = 5;
        public byte Rotary2Increment { get; set; } = 5;
        public byte Rotary3Increment { get; set; } = 5;
        public byte Rotary4Increment { get; set; } = 5;
        public RPCS3Config RPCS3Config { get; set; }
        public bool UseRemoteThread { get; set; } = false;
        public bool UseDirectionalPresses { get; set; } = true;
        public string GameVersion { get; set; } = "";
        public bool AllowSettingSync { get; set; } = false;
        public override string ToString()
        {
            return GameNameInternal;
        }
    }
}
