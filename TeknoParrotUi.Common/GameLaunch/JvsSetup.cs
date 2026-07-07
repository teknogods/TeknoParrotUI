using System.Linq;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Configures the static JVS emulator state for an emulation profile.
    /// Verbatim port of the JVS switch in the classic GameRunning view.
    /// </summary>
    public static class JvsSetup
    {
        /// <summary>Profiles that do not use the TeknoParrot_JVS named pipe.</summary>
        public static bool UsesJvsPipe(GameProfile gameProfile)
        {
            var mode = gameProfile.EmulationProfile;
            return mode != EmulationProfile.EuropaRFordRacing &&
                   mode != EmulationProfile.EuropaRSegaRally3 &&
                   mode != EmulationProfile.Theatrhythm &&
                   mode != EmulationProfile.FastIo &&
                   mode != EmulationProfile.GunslingerStratos3 &&
                   gameProfile.EmulatorType != EmulatorType.Dolphin &&
                   gameProfile.EmulatorType != EmulatorType.Play &&
                   gameProfile.EmulatorType != EmulatorType.RPCS3;
        }

        public static void InitializeAnalogBytes(EmulationProfile mode)
        {
            bool centered = mode == EmulationProfile.SegaJvsLetsGoIsland ||
                            mode == EmulationProfile.SegaJvsLetsGoJungle ||
                            mode == EmulationProfile.LuigisMansion;
            for (int i = 0; i <= 6; i += 2)
                InputCode.AnalogBytes[i] = centered ? (byte)127 : (byte)0;

            if (mode == EmulationProfile.GunslingerStratos3)
            {
                for (int i = 0; i <= 12; i += 2)
                    InputCode.AnalogBytes[i] = 127;
            }
        }

        public static void ConfigureJvsPackage(GameProfile gameProfile)
        {
            bool proMode = gameProfile.ConfigValues.Any(x => x.FieldName == "Professional Edition Enable" && x.FieldValue == "1");

            switch (gameProfile.EmulationProfile)
            {
                case EmulationProfile.VirtuaRLimit:
                case EmulationProfile.ChaseHq2:
                case EmulationProfile.WackyRaces:
                    JvsPackageEmulator.Taito = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.TaitoTypeXBattleGear:
                    JvsPackageEmulator.JvsVersion = 0x30;
                    if (proMode)
                    {
                        JvsPackageEmulator.DualJvsEmulation = true;
                        JvsPackageEmulator.ProMode = true;
                    }
                    JvsPackageEmulator.TaitoBattleGear = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.TaitoTypeXGeneric:
                    JvsPackageEmulator.JvsVersion = 0x30;
                    JvsPackageEmulator.TaitoStick = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.BorderBreak:
                    InputCode.AnalogBytes[0] = 0x7F;
                    InputCode.AnalogBytes[2] = 0x7F;
                    break;
                case EmulationProfile.NamcoPokken:
                    JvsPackageEmulator.JvsVersion = 0x31;
                    JvsPackageEmulator.JvsCommVersion = 0x31;
                    JvsPackageEmulator.JvsCommandRevision = 0x31;
                    JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NBGI_Pokken;
                    JvsPackageEmulator.Namco = true;
                    break;
                case EmulationProfile.NamcoWmmt5:
                case EmulationProfile.NamcoWmmt6RR:
                case EmulationProfile.NamcoMkdx:
                case EmulationProfile.NamcoMkdxUsa:
                case EmulationProfile.DeadHeatRiders:
                case EmulationProfile.NamcoGundamPod:
                case EmulationProfile.EXVS2:
                case EmulationProfile.EXVS2XB:
                case EmulationProfile.NamcoSynchronica:
                    JvsPackageEmulator.JvsVersion = 0x31;
                    JvsPackageEmulator.JvsCommVersion = 0x31;
                    JvsPackageEmulator.JvsCommandRevision = 0x31;
                    JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NBGI_MarioKart3;
                    JvsPackageEmulator.Namco = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.NamcoMachStorm:
                    JvsPackageEmulator.JvsVersion = 0x31;
                    JvsPackageEmulator.JvsCommVersion = 0x31;
                    JvsPackageEmulator.JvsCommandRevision = 0x31;
                    JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NamcoMultipurpose;
                    JvsPackageEmulator.Namco = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.DevThing1:
                    JvsPackageEmulator.JvsVersion = 0x30;
                    JvsPackageEmulator.TaitoStick = true;
                    JvsPackageEmulator.TaitoBattleGear = true;
                    JvsPackageEmulator.DualJvsEmulation = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x18;
                    break;
                case EmulationProfile.VirtuaTennis4:
                case EmulationProfile.ArcadeLove:
                    JvsPackageEmulator.DualJvsEmulation = true;
                    break;
                case EmulationProfile.LGS:
                    JvsPackageEmulator.JvsCommVersion = 0x30;
                    JvsPackageEmulator.JvsVersion = 0x30;
                    JvsPackageEmulator.JvsCommandRevision = 0x30;
                    JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.SegaLetsGoSafari;
                    JvsPackageEmulator.LetsGoSafari = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x16;
                    break;
                case EmulationProfile.Hotd4:
                    JvsPackageEmulator.Hotd4 = true;
                    break;
                case EmulationProfile.Xiyangyang:
                    JvsPackageEmulator.JvsCommVersion = 0x10;
                    JvsPackageEmulator.JvsVersion = 0x30;
                    JvsPackageEmulator.JvsCommandRevision = 0x13;
                    JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.SegaXiyangyang;
                    JvsPackageEmulator.Xiyangyang = true;
                    JvsPackageEmulator.JvsSwitchCount = 0x16;
                    break;
            }
        }
    }
}
