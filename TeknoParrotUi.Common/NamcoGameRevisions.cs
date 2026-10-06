using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common
{
    /// <summary>Supported ROM revisions, grouped by the merged archive used by each emulator.</summary>
    public static class NamcoGameRevisions
    {
        public const string SettingName = "Game Revision";

        // These cabinets use independent host axes. Cyber/Armadillo are
        // right/down-positive; Alpine Surfer's Swing binding drives the
        // hardware-reversed ADC and its Edge binding is right-positive.
        public static bool UsesIndependentCabinetAxes(GameProfile profile) =>
            profile?.EmulatorType == EmulatorType.TeknoS22 &&
            (profile.ProfileName == "cybrcomm" || profile.ProfileName == "adillor" || profile.ProfileName == "adillorj" || profile.ProfileName == "alpines");

        public static string PreviousControlName(GameProfile profile, string name)
        {
            if (profile?.EmulatorType == EmulatorType.TeknoS22 &&
                (profile.ProfileName == "alpines" || profile.ExecutableName == "alpines.zip"))
            {
                switch (name)
                {
                    case "Swing": return "Analog X";
                    case "Swing Left": return "Analog X Left";
                    case "Swing Right": return "Analog X Right";
                    case "Edge": return "Analog Y";
                    case "Edge Left": return "Analog Y Up";
                    case "Edge Right": return "Analog Y Down";
                    default: return name;
                }
            }
            if (profile?.EmulatorType != EmulatorType.TeknoS22 ||
                (profile.ProfileName != "cybrcomm" && profile.ExecutableName != "cybrcomm.zip")) return name;
            switch (name)
            {
                case "Left Stick X": return "Analog X";
                case "Left Stick Left": return "Analog X Left";
                case "Left Stick Right": return "Analog X Right";
                case "Left Stick Y": return "Analog Y";
                case "Left Stick Up": return "Analog Y Up";
                case "Left Stick Down": return "Analog Y Down";
                case "Right Stick X": return "Analog Z";
                case "Right Stick Left": return "Analog Z Left";
                case "Right Stick Right": return "Analog Z Right";
                case "Right Stick Y": return "Analog R";
                case "Right Stick Up": return "Analog R Up";
                case "Right Stick Down": return "Analog R Down";
                default: return name;
            }
        }

        private sealed class Revision
        {
            internal readonly string Set;
            internal readonly string Name;
            internal Revision(string set, string name) { Set = set; Name = name; }
        }

        // Mirrors TeknoS22/TeknoS23 src/machine/game_profile.cpp. Store set IDs in
        // user profiles so display labels can change without changing their selection.
        private static readonly Dictionary<EmulatorType, Revision[][]> Catalog =
            new Dictionary<EmulatorType, Revision[][]>
            {
                { EmulatorType.TeknoS22, new[]
                    {
                        new[]
                        {
                            new Revision("acedrive", "World, AD2"),
                        },
                        new[]
                        {
                            new Revision("adillor", "World, AM2 Ver.A"),
                            new Revision("adillorj", "Japan, AM1 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("airco22b", "Japan, ACS1 Ver.B"),
                        },
                        new[]
                        {
                            new Revision("alpinerd", "World, AR2 Ver.D"),
                            new Revision("alpinerc", "World, AR2 Ver.C"),
                            new Revision("alpinerjc", "Japan, AR1 Ver.C"),
                        },
                        new[]
                        {
                            new Revision("alpines", "World, AF2 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("alpinr2b", "World, ARS2 Ver.B"),
                            new Revision("alpinr2a", "World, ARS2 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("aquajet", "World, AJ2 Ver.B"),
                        },
                        new[]
                        {
                            new Revision("cybrcomm", "Japan, CY1"),
                        },
                        new[]
                        {
                            new Revision("cybrcycc", "World, CB2 Ver.C"),
                            new Revision("cybrcyccj", "Japan, CB1 Ver.C"),
                        },
                        new[]
                        {
                            new Revision("dirtdash", "World, DT2 Ver.C"),
                            new Revision("dirtdasha", "World, DT2 Ver.A"),
                            new Revision("dirtdashb", "World, DT2 Ver.B"),
                            new Revision("dirtdashj", "Japan, DT1 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("propcycl", "World, PR2 Ver.A"),
                            new Revision("propcyclj", "Japan, PR1 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("raverace", "World, RV2 Ver.B"),
                            new Revision("raveracej", "Japan, RV1 Ver.B"),
                            new Revision("raveraceja", "Japan, RV1"),
                        },
                        new[]
                        {
                            new Revision("ridgera2", "World, RRS2"),
                            new Revision("ridgera2j", "Japan, RRS1 Ver.B"),
                            new Revision("ridgera2ja", "Japan, RRS1"),
                            new Revision("ridgera28", "World, RRS8 prototype"),
                        },
                        new[]
                        {
                            new Revision("ridgerac", "World, RR2 Ver.B"),
                            new Revision("ridgeraca", "World, RR2"),
                            new Revision("ridgeracb", "US, RR3 Ver.B"),
                            new Revision("ridgeracc", "US, RR3"),
                            new Revision("ridgeracj", "Japan, RR1"),
                            new Revision("ridgerac3m", "World, RRC, three monitor"),
                        },
                        new[]
                        {
                            new Revision("ridgeracf", "World, RRF2"),
                        },
                        new[]
                        {
                            new Revision("timecris", "World, TS2 Ver.B"),
                            new Revision("timecrisa", "World, TS2 Ver.A"),
                            new Revision("timecrisj", "Japan, TS1 Ver.B"),
                        },
                        new[]
                        {
                            new Revision("tokyowar", "World, TW2 Ver.A"),
                            new Revision("tokyowarj", "Japan, TW1 Ver.A"),
                        },
                        new[]
                        {
                            new Revision("victlap", "World, ADV2 Ver.B"),
                            new Revision("victlapa", "World, ADV2"),
                            new Revision("victlapj", "Japan, ADV1 Ver.C"),
                        },
                    }
                },
                { EmulatorType.TeknoS23, new[]
                    {
                        new[]
                        {
                            new Revision("500gp", "US, 5GP3 Ver. C"),
                        },
                        new[]
                        {
                            new Revision("aking", "Japan, AG1 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("crszone", "World, CSZO4 Ver. B"),
                            new Revision("crszonev4a", "World, CSZO4 Ver. A"),
                            new Revision("crszonev3b", "US, CSZO3 Ver. B, set 1"),
                            new Revision("crszonev3b2", "US, CSZO3 Ver. B, set 2"),
                            new Revision("crszonev3a", "US, CSZO3 Ver. A"),
                            new Revision("crszonev2a", "World, CSZO2 Ver. A"),
                            new Revision("crszonev2b", "World, CSZO2 Ver. B"),
                        },
                        new[]
                        {
                            new Revision("downhill", "World, DH2 Ver. A"),
                            new Revision("downhillu", "US, DH3 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("finfurl", "World, FF2 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("finfurl2", "World, FFS2 Ver. A"),
                            new Revision("finfurl2j", "Japan, FFS1 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("gunwars", "Japan, GM1 Ver. B"),
                            new Revision("gunwarsa", "Japan, GM1 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("motoxgo", "World, MG3 Ver. A"),
                            new Revision("motoxgov2a", "US, MG2 Ver. A"),
                            new Revision("motoxgov1a", "Japan, MG1 Ver. A"),
                        },
                        new[]
                        {
                            new Revision("panicprk", "World, PNP2 Ver. A"),
                            new Revision("panicprkj", "Japan, PNP1 Ver. B"),
                        },
                        new[]
                        {
                            new Revision("raceon", "World, RO2 Ver. A"),
                            new Revision("raceonj", "Japan, RO1 Ver. B"),
                        },
                        new[]
                        {
                            new Revision("rapidrvr", "US, RD3 Ver. C"),
                            new Revision("rapidrvrv2c", "World, RD2 Ver. C"),
                            new Revision("rapidrvrp", "prototype"),
                        },
                        new[]
                        {
                            new Revision("timecrs2", "US, TSS3 Ver. B"),
                            new Revision("timecrs2v2b", "World, TSS2 Ver. B"),
                            new Revision("timecrs2v1b", "Japan, TSS1 Ver. B"),
                            new Revision("timecrs2v4a", "World, TSS4 Ver. A"),
                            new Revision("timecrs2v5a", "US, TSS5 Ver. A"),
                        },
                    }
                },
            };

        private static Revision[] Family(GameProfile profile)
        {
            if (profile == null || !Catalog.TryGetValue(profile.EmulatorType, out var families))
                return Array.Empty<Revision>();
            return families.FirstOrDefault(family => family.Any(revision =>
                string.Equals(revision.Set, profile.ProfileName, StringComparison.OrdinalIgnoreCase)))
                ?? Array.Empty<Revision>();
        }

        public static List<DynamicDropdownOption> GetOptions(GameProfile profile) =>
            Family(profile).Select(revision => new DynamicDropdownOption
            {
                DisplayName = revision.Name,
                Value = revision.Set
            }).ToList();

        public static void Populate(GameProfile profile)
        {
            var field = profile?.ConfigValues?.FirstOrDefault(value => value.FieldName == SettingName);
            if (field == null || (profile.EmulatorType != EmulatorType.TeknoS22 &&
                                  profile.EmulatorType != EmulatorType.TeknoS23)) return;
            field.DynamicOptions = GetOptions(profile);
            if (string.IsNullOrWhiteSpace(field.FieldValue)) field.FieldValue = profile.ProfileName;
        }

        public static string ResolveSet(GameProfile profile)
        {
            var selected = profile.ConfigValues?.FirstOrDefault(value => value.FieldName == SettingName)?.FieldValue;
            if (string.IsNullOrWhiteSpace(selected)) return profile.ProfileName;
            var revision = Family(profile).FirstOrDefault(candidate =>
                string.Equals(candidate.Set, selected.Trim(), StringComparison.OrdinalIgnoreCase));
            if (revision == null)
                throw new ArgumentException("Invalid Game Revision for this game. Select a supported revision in Game Settings.");
            return revision.Set;
        }
    }
}
