using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum InputApi
    {
        DirectInput,
        XInput,
        RawInput,
        RawInputTrackball,
        MergedInput
    }

    public enum OnlineIdType
    {
        None,
        SegaId,
        NamcoId,
        HighscoreSerial,
        MarioKartId,
        NesysId,
        // Initial D Online (ID4 JP / ID5 / ID6 / ID7 / ID8): the PCB ID in OnlineIdFieldName plus its secret in the
        // "OnlineSecret" field, both from the teknoparrot.com account (InitialDOnlineHelper).
        InitialD,
        // Golden Tee online: the Card ID in OnlineIdFieldName plus the "PCB ID" field, both from the teknoparrot.com
        // account (GoldenTeeOnlineHelper).
        GoldenTee,
        // Senjou no Kizuna Online (GundamPod, online only): the account's own Kizuna PCB ID (AAKZ-...) plus its secret
        // in the same two fields, from the teknoparrot.com account like InitialD's but a pair of its own
        // (KizunaOnlineHelper).
        Kizuna
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
        /// <summary>The test menu executable is the game's terminal, another program of the cabinet set (Senjou no
        /// Kizuna's n_gun_terminal_rel_opt_es1): the library's TEST button reads TERMINAL and launches it.</summary>
        public bool TestMenuIsTerminal { get; set; }
        public string ExtraParameters { get; set; }
        public string TestMenuExtraParameters { get; set; }
        /// <summary>Four-character title ID for the native managed-APM test loader (x64).</summary>
        public string ApmTestGameId { get; set; }
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
        // Add games here only after their input transport supports full 16-bit axes.
        [XmlIgnore]
        public bool HighResolutionAxis =>
            string.Equals(ExecutableName, "arkndd.dll", StringComparison.OrdinalIgnoreCase) ||
            ((EmulationProfile == EmulationProfile.TeknoViper || EmulationProfile == EmulationProfile.TeknoHornet) &&
             ExecutableName?.StartsWith("sscope", StringComparison.OrdinalIgnoreCase) == true) ||
            (EmulationProfile == EmulationProfile.TeknoViper &&
             string.Equals(ExecutableName, "sogeki.zip", StringComparison.OrdinalIgnoreCase));
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
        // Shows the "GT on TP" button (the Golden Tee golfer and equipment editor, Tools\GTonTP) under Play.
        public bool HasGtOnTp { get; set; } = false;
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
        // False treats directions as independent buttons (e.g. simultaneous DDR pads).
        public bool UseDirectionalPresses { get; set; } = true;
        public string GameVersion { get; set; } = "";
        public bool AllowSettingSync { get; set; } = false;
        public bool Use16BitAnalog { get; set; } = false;
        public CabinetOutputSettings CabinetOutputSettings { get; set; } = new CabinetOutputSettings();

        public bool ShouldSerializeCabinetOutputSettings() => TeknoParrotUi.Common.CabinetOutputSettings.Supports(this);

        public GameProfile Clone()
        {
            var copy = (GameProfile)MemberwiseClone();
            copy.CabinetOutputSettings = CabinetOutputSettings?.Clone();
            copy.ConfigValues = ConfigValues?.Select(field => field?.Clone()).ToList();
            copy.JoystickButtons = JoystickButtons?.Select(button => button?.Clone()).ToList();
            if (RPCS3Config != null)
            {
                copy.RPCS3Config = new RPCS3Config
                {
                    ConfigItems = RPCS3Config.ConfigItems?.Select(item => item == null ? null : new RPCS3ConfigItem
                    {
                        Category = item.Category,
                        Name = item.Name,
                        Value = item.Value
                    }).ToList()
                };
            }
            copy.GameInfo = GameInfo?.Clone();
            return copy;
        }

        public override string ToString()
        {
            return GameNameInternal;
        }
    }
}
