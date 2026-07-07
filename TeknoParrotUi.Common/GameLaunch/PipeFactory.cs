using System.Linq;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Creates the control pipe and control sender for an emulation profile.
    /// Verbatim port of the switches in the classic GameRunning view.
    /// </summary>
    public static class PipeFactory
    {
        public static ControlPipe CreateControlPipe(EmulationProfile profile)
        {
            switch (profile)
            {
                case EmulationProfile.EuropaRFordRacing: return new EuropaRPipe();
                case EmulationProfile.EuropaRSegaRally3: return new SegaRallyPipe();
                case EmulationProfile.FastIo:
                case EmulationProfile.GunslingerStratos3: return new FastIOPipe();
                case EmulationProfile.ALLS:
                case EmulationProfile.ALLSHOTDSD:
                case EmulationProfile.ALLSFGO: return new ALLSUsbIoPipe();
                case EmulationProfile.ALLSSWDC: return new SWDCALLSUsbIoPipe();
                case EmulationProfile.ALLSSCHRONO: return new ChronoRegaliaUsbIoPipe();
                case EmulationProfile.Theatrhythm: return new FastIOPipe();
                case EmulationProfile.APM3:
                case EmulationProfile.APM3Direct:
                case EmulationProfile.GuiltyGearAPM3: return new APM3Pipe();
                case EmulationProfile.WonderlandWars:
                case EmulationProfile.Xiyangyang: return new amJvsPipe();
                case EmulationProfile.ALLSIDTA: return new SWDCALLSUsbIoPipe();
                case EmulationProfile.SegaOlympic2020: return new SWDCALLSUsbIoPipe();
                case EmulationProfile.Outrun2SPXElf2: return new amJvsPipe();
                default: return null;
            }
        }

        public static ControlSender CreateControlSender(EmulationProfile profile, GameProfile gameProfile)
        {
            bool realGearShift = gameProfile.ConfigValues.Any(x => x.FieldName == "RealGearshift" && x.FieldValue == "1");

            switch (profile)
            {
                case EmulationProfile.DeadHeat:
                case EmulationProfile.Nirin: return new DeadHeatPipe();
                case EmulationProfile.LGS: return new LGSPipe();
                case EmulationProfile.NamcoPokken: return new Pokken();
                case EmulationProfile.ExBoard: return new ExBoard();
                case EmulationProfile.ALLSHOTDSD: return new HOTDSDPipe();
                case EmulationProfile.ALLSSWDC:
                case EmulationProfile.CrossbeatsRev: return new SWDCPipe();
                case EmulationProfile.SegaJvsAime:
                case EmulationProfile.IDZ: return new AimeButton();
                case EmulationProfile.GtiClub3: return new GtiClub3();
                case EmulationProfile.Daytona3: return new Daytona3();
                case EmulationProfile.GRID: return new GRID();
                case EmulationProfile.RawThrillsFNF:
                case EmulationProfile.BlazingAngels: return new RawThrills(false);
                case EmulationProfile.RawThrillsFNFH2O: return new RawThrills(true);
                case EmulationProfile.LuigisMansion: return new LuigisMansion();
                case EmulationProfile.LostLandAdventures: return new LostLandPipe();
                case EmulationProfile.GHA: return new GHA();
                case EmulationProfile.SegaToolsIDZ: return new SegaTools();
                case EmulationProfile.TokyoCop:
                case EmulationProfile.RingRiders:
                case EmulationProfile.RadikalBikers: return new GaelcoPipe();
                case EmulationProfile.StarTrekVoyager: return new StarTrekVoyagerPipe();
                case EmulationProfile.SegaInitialD:
                case EmulationProfile.SegaInitialDLindbergh:
                    return realGearShift ? new SegaInitialDPipe() : null;
                case EmulationProfile.AliensExtermination: return new AliensExterminationPipe();
                case EmulationProfile.Contra: return new ContraPipe();
                case EmulationProfile.MarioBros: return new MarioBrosPipe();
                case EmulationProfile.NamcoMkdx: return new BanapassButton();
                case EmulationProfile.FarCry: return new FarCryPipe();
                case EmulationProfile.SilentHill: return new SilentHillPipe();
                case EmulationProfile.Taiko: return new TaikoPipe();
                case EmulationProfile.WartranTroopers: return new WartranTroopersPipe();
                case EmulationProfile.HotWheels: return new HotWheelsPipe();
                case EmulationProfile.InfinityBlade:
                case EmulationProfile.TimeCrisis5: return new TC5Pipe();
                case EmulationProfile.FrenzyExpress: return new FrenzyExpressPipe();
                case EmulationProfile.AAA: return new AAAPipe();
                case EmulationProfile.EuropaRSegaRally3: return new SegaRallyCoinPipe();
                case EmulationProfile.RawThrillsGUN: return new RawThrillsGUN();
                case EmulationProfile.DealorNoDeal: return new DealOrNoDealPipe();
                case EmulationProfile.TMNT: return new TMNTPipe();
                case EmulationProfile.EADP: return new EADPPipe();
                case EmulationProfile.MusicGunGun2:
                case EmulationProfile.GaiaAttack4:
                case EmulationProfile.HauntedMuseum:
                case EmulationProfile.HauntedMuseum2: return new MusicGunGun2Pipe();
                case EmulationProfile.PointBlankX: return new PointBlankPipe();
                case EmulationProfile.TheAct: return new TheActPipe();
                case EmulationProfile.SAO:
                case EmulationProfile.JojoLastSurvivor:
                case EmulationProfile.GundamKizuna2:
                case EmulationProfile.Tetote: return new BnusioPipe();
                case EmulationProfile.EXVS2:
                case EmulationProfile.EXVS2XB: return new BanapassButtonEXVS2();
                case EmulationProfile.WinningEleven: return new WinningElevenPipe();
                case EmulationProfile.WonderlandWars: return new WonderlandWarsPipe();
                case EmulationProfile.Friction: return new FrictionPipe();
                case EmulationProfile.Castlevania: return new CastlevaniaPipe();
                case EmulationProfile.SavageQuest: return new SavageQuestPipe();
                case EmulationProfile.NxL2: return new NxL2Pipe();
                case EmulationProfile.FastIo:
                case EmulationProfile.GunslingerStratos3: return new NesicaButton();
                case EmulationProfile.BorderBreak:
                case EmulationProfile.ALLSSCHRONO:
                case EmulationProfile.ALLSIDTA: return new AimeButton();
                case EmulationProfile.DenshaDeGo: return new NxL2Pipe();
                case EmulationProfile.TransformersShadowsRising: return new TransformersShadowsRisingPipe();
                case EmulationProfile.IncredibleTechnologies: return new IncredibleTechnologiesPipe();
                case EmulationProfile.GenericTrackball: return new GenericTrackballPipe();
                case EmulationProfile.NamcoWmmt6RR: return new BanapassButtonEXVS2();
                case EmulationProfile.MarioKartGP:
                case EmulationProfile.MarioKartGP2:
                case EmulationProfile.FZeroAX:
                case EmulationProfile.FZeroAXMonster:
                case EmulationProfile.VirtuaStriker3:
                case EmulationProfile.VirtuaStriker4:
                case EmulationProfile.GekitouProYakyuu:
                case EmulationProfile.KeyOfAvalon:
                case EmulationProfile.Tatsunoko: return new DolphinJvsPipeExtended();
                case EmulationProfile.System147: return new System147();
                case EmulationProfile.PlayInput: return new PlayPipe();
                case EmulationProfile.pcsx2x6: return new Pcsx2x6Pipe();
                case EmulationProfile.RPCS3: return new RPCS3Pipe();
                case EmulationProfile.LadyLuck: return new LadyLuckPipe();
                case EmulationProfile.KonamiAcio: return new AcioPipe();
                case EmulationProfile.cxbxr: return new CxbxPipe();
                default: return null;
            }
        }
    }
}
