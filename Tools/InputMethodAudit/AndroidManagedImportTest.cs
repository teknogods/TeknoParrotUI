using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace InputMethodAudit
{
    internal static class AndroidManagedImportTest
    {
        public static int Run()
        {
            try
            {
                var recipeDirectory = FindRecipeDirectory();
                VerifyWinlatorPreparationOrder(recipeDirectory);
                var recipes = AndroidLaunchRecipeCatalog.LoadAll(recipeDirectory);
                VerifyAndroidPhysicalScannerFallback(recipeDirectory);
                VerifyAndroidDocumentPathResolution();
                var diagnosticByDefault = recipes
                    .Where(recipe => !recipe.PerformanceModeDefault)
                    .Select(recipe => recipe.ProfileName)
                    .ToArray();
                if (diagnosticByDefault.Length != 0)
                    throw new InvalidOperationException(
                        "Android recipes must default to performance mode: " +
                        string.Join(", ", diagnosticByDefault));
                True(recipes.Count >= 35, "recipe catalog did not regress below baseline");
                Equal(recipes.Count, recipes.Count(recipe => recipe.Validated),
                    "validated recipe count");
                True(recipes.Count(recipe => recipe.PerformanceModeDefault) >= 16,
                    "known-working performance-mode recipe count did not regress");
                VerifyDebugLoggingProfileRoundTrip();
                var sr3 = recipes.Single(recipe => recipe.ProfileName == "SR3");
                True(sr3.Validated, "SR3 enabled for physical device validation");
                Equal(AndroidLaunchRecipe.InputProtocolSegaRally, sr3.InputProtocol,
                    "SR3 input protocol");
                Equal(9001, sr3.ControlsProfileId, "SR3 controls profile");
                Equal(60, sr3.FrameRateLimit, "SR3 frame-rate limit");
                True(sr3.PerformanceModeDefault, "SR3 performance mode default");

                var cosplay = recipes.Single(recipe => recipe.ProfileName == "3DCosplayMahjong");
                Equal(AndroidLaunchRecipe.InputProtocolJvs, cosplay.InputProtocol,
                    "Cosplay input protocol");
                Equal(9004, cosplay.ControlsProfileId, "Cosplay controls profile");
                Equal(60, cosplay.FrameRateLimit, "Cosplay frame-rate limit");
                True(cosplay.PerformanceModeDefault, "Cosplay performance mode default");
                True(recipes.Single(recipe => recipe.ProfileName == "PuzzleBobble")
                    .PerformanceModeDefault, "Puzzle Bobble performance mode default");

                var battleGear4 = recipes.Single(
                    recipe => recipe.ProfileName == "BattleGear4");
                Equal(AndroidLaunchRecipe.CompatibilityPresetBattleGear4Original,
                    battleGear4.CompatibilityPreset,
                    "original Battle Gear 4 Box64/x87 compatibility preset");

                var justiceLeague = recipes.Single(
                    recipe => recipe.ProfileName == "JusticeLeague");
                Equal(AndroidLaunchRecipe.CompatibilityPresetJusticeLeagueWow64Transition,
                    justiceLeague.CompatibilityPreset,
                    "Justice League prefix-local WOW64 transition recovery preset");

                var wonderland = recipes.Single(recipe => recipe.ProfileName == "WonderlandWars");
                Equal(AndroidLaunchRecipe.InputProtocolSharedWonderlandWars,
                    wonderland.InputProtocol, "Wonderland Wars shared input protocol");
                Equal(AndroidLaunchRecipe.CompatibilityPresetSharedJvsDualIo,
                    wonderland.CompatibilityPreset, "Wonderland Wars dual I/O preset");
                var hauntedMuseum = recipes.Single(
                    recipe => recipe.ProfileName == "HauntedMuseum");
                Equal(AndroidLaunchRecipe.InputProtocolSharedTaitoGun,
                    hauntedMuseum.InputProtocol, "HauntedMuseum shared gun protocol");
                Equal(AndroidLaunchRecipe.CompatibilityPresetSharedJvsDualIo,
                    hauntedMuseum.CompatibilityPreset, "HauntedMuseum dual I/O preset");
                var gaiaAttack4 = recipes.Single(
                    recipe => recipe.ProfileName == "GaiaAttack4");
                Equal(AndroidLaunchRecipe.InputProtocolSharedTaitoGun,
                    gaiaAttack4.InputProtocol, "GaiaAttack4 shared gun protocol");
                Equal(AndroidLaunchRecipe.CompatibilityPresetGaiaAttack4Media,
                    gaiaAttack4.CompatibilityPreset,
                    "GaiaAttack4 dual I/O plus mixed-codec media preset");
                var musicGunGun2 = recipes.Single(
                    recipe => recipe.ProfileName == "MusicGunGun2");
                Equal(AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic,
                    musicGunGun2.InputProtocol,
                    "Music Gun Gun 2 decision-preserving shared gun protocol");
                Equal(9074, musicGunGun2.ControlsProfileId,
                    "Music Gun Gun 2 dedicated controls profile");
                Equal(1360, musicGunGun2.ResolutionWidth,
                    "Music Gun Gun 2 native D3D9 enumeration width");
                Equal(768, musicGunGun2.ResolutionHeight,
                    "Music Gun Gun 2 native D3D9 enumeration height");
                Equal(AndroidLaunchRecipe.CompatibilityPresetMusicGunGunNativeFullscreen,
                    musicGunGun2.CompatibilityPreset,
                    "Music Gun Gun 2 native-fullscreen dual I/O preset");
                hauntedMuseum = recipes.Single(
                    recipe => recipe.ProfileName == "HauntedMuseum");
                Equal(9061, hauntedMuseum.ControlsProfileId,
                    "Haunted Museum trigger-only controls profile");
                var hauntedMuseum2 = recipes.Single(
                    recipe => recipe.ProfileName == "HauntedMuseumII");
                Equal(AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2,
                    hauntedMuseum2.InputProtocol,
                    "Haunted Museum II action-aware shared gun protocol");
                Equal(9060, hauntedMuseum2.ControlsProfileId,
                    "Haunted Museum II trigger/action controls profile");
                Equal(AndroidLaunchRecipe.CompatibilityPresetSharedJvsDualIo,
                    hauntedMuseum2.CompatibilityPreset,
                    "Haunted Museum II dual I/O preset");
                var wonderlandFolder = "/storage/emulated/0/Download/TeknoParrotGames/wonderlandwars";
                var wonderlandFound = ManagedAndroidGameImporter.Scan(
                    new[] { Folder("wonderlandwars", wonderlandFolder, "carol_nu.exe") },
                    recipes,
                    new[] { new GameProfile
                    {
                        ProfileName = "WonderlandWars",
                        GameNameInternal = "Wonderland Wars"
                    } });
                Equal(1, wonderlandFound.Count, "Wonderland Wars folder match");
                Equal(wonderlandFolder + "/carol_nu.exe",
                    wonderlandFound[0].GameExecutablePath,
                    "Wonderland Wars executable");

                var shiningProfiles = new[]
                {
                    new GameProfile { ProfileName = "ShiningForceCross", GameNameInternal = "Shining Force Cross" },
                    new GameProfile { ProfileName = "ShiningForceCrossRaid", GameNameInternal = "Shining Force Cross Raid" },
                    new GameProfile { ProfileName = "ShiningForceCrossElysion", GameNameInternal = "Shining Force Cross Elysion" },
                    new GameProfile { ProfileName = "ShiningForceCrossExlesia", GameNameInternal = "Shining Force Cross Exlesia" }
                };
                var shiningFolders = new[]
                {
                    Folder("SBRT_Ver_1_03_00", "/storage/emulated/0/Download/TeknoParrotGames/SBRT_Ver_1_03_00", "project_f-ringedge-release.exe"),
                    Folder("RAID", "/storage/emulated/0/Download/TeknoParrotGames/RAID", "project_f-ringedge-release.exe"),
                    Folder("ELYSION", "/storage/emulated/0/Download/TeknoParrotGames/ELYSION", "project_f-ringedge-release.exe"),
                    Folder("EXLESIA", "/storage/emulated/0/Download/TeknoParrotGames/EXLESIA", "project_f-ringedge-release.exe")
                };
                var shiningFound = ManagedAndroidGameImporter.Scan(
                    shiningFolders, recipes, shiningProfiles);
                Equal(4, shiningFound.Count, "Shining Force Cross family folder matches");
                foreach (var profile in shiningProfiles)
                {
                    var recipe = recipes.Single(item => item.ProfileName == profile.ProfileName);
                    Equal(AndroidLaunchRecipe.InputProtocolJvs, recipe.InputProtocol,
                        profile.ProfileName + " JVS protocol");
                    Equal(AndroidLaunchRecipe.CompatibilityPresetDirectTouchJvs,
                        recipe.CompatibilityPreset,
                        profile.ProfileName + " direct-touch JVS preset");
                    Equal("1", recipe.ProfileConfigOverrides.Single(item =>
                            item.FieldName == "AMDCrashFix").FieldValue,
                        profile.ProfileName + " non-NVIDIA OpenGL fix");
                    Equal("1", recipe.ProfileConfigOverrides.Single(item =>
                            item.FieldName == "HideCursor").FieldValue,
                        profile.ProfileName + " cabinet cursor suppression");
                }

                var profiles = new[]
                {
                    new GameProfile
                    {
                        ProfileName = "RastanSaga",
                        GameNameInternal = "Rastan Saga for NESiCAxLive"
                    },
                    new GameProfile
                    {
                        ProfileName = "3DCosplayMahjong",
                        GameNameInternal = "3D Cosplay Mahjong"
                    },
                    new GameProfile
                    {
                        ProfileName = "SR3",
                        GameNameInternal = "Sega Rally 3"
                    }
                };
                var folders = new[]
                {
                    Folder(
                        "Rastan Saga[401500]",
                        "/storage/emulated/0/Download/TeknoParrotGames/Rastan Saga[401500]"),
                    Folder(
                        "3D Cosplay Mahjong - 401300",
                        "/storage/emulated/0/Download/TeknoParrotGames/3D Cosplay Mahjong - 401300"),
                    Folder(
                        "SegaRally3",
                        "/storage/emulated/0/Download/TeknoParrotGames/SegaRally3",
                        "SegaRally3/Rally/Rally.exe")
                };
                var found = ManagedAndroidGameImporter.Scan(folders, recipes, profiles);
                Equal(3, found.Count, "matched game count");
                Equal(
                    "/storage/emulated/0/Download/TeknoParrotGames/SegaRally3/SegaRally3/Rally/Rally.exe",
                    found.Single(game => game.ProfileName == "SR3").GameExecutablePath,
                    "SR3 executable");
                Equal(
                    "/storage/emulated/0/Download/TeknoParrotGames/Rastan Saga[401500]/game.exe",
                    found.Single(game => game.ProfileName == "RastanSaga").GameExecutablePath,
                    "Rastan executable");
                Equal(
                    "/storage/emulated/0/Download/TeknoParrotGames/3D Cosplay Mahjong - 401300/game.exe",
                    found.Single(game => game.ProfileName == "3DCosplayMahjong").GameExecutablePath,
                    "Cosplay executable");

                var ringBatch = new[]
                {
                    (Folder: "SegaRacingClassic", Profile: "SRC", Executable: "d1a.exe"),
                    (Folder: "KODrive_RingWide", Profile: "KODrive",
                        Executable: "exe/M-DriveR_RingWide.exe"),
                    (Folder: "SegaDreamRaiders", Profile: "SDR", Executable: "prg/game.exe"),
                    (Folder: "GoldenGun", Profile: "GG",
                        Executable: "exe/RingGunR_RingWide.exe"),
                    (Folder: "LetsGoIsland3D", Profile: "LGI3D", Executable: "LGI.exe")
                };
                var ringProfiles = ringBatch.Select(entry => new GameProfile
                {
                    ProfileName = entry.Profile,
                    GameNameInternal = entry.Folder
                }).ToArray();
                var ringFolders = ringBatch.Select(entry => Folder(
                    entry.Folder,
                    "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Folder,
                    entry.Executable)).ToArray();
                var ringLogs = new List<string>();
                var ringFound = ManagedAndroidGameImporter.Scan(
                    ringFolders, recipes, ringProfiles, ringLogs.Add);
                if (ringFound.Count != ringBatch.Length)
                    throw new InvalidOperationException(
                        $"Ring batch count: expected '{ringBatch.Length}', got " +
                        $"'{ringFound.Count}'. {string.Join(" | ", ringLogs)}");
                Equal(
                    "/storage/emulated/0/Download/TeknoParrotGames/KODrive_RingWide/exe/M-DriveR_RingWide.exe",
                    ringFound.Single(game => game.ProfileName == "KODrive").GameExecutablePath,
                    "Ko Drive nested executable");

                var nextBatch = new[]
                {
                    ("Akai Katana Shin (2012-07-12)[Taito NESiCAxLive][TP]", "AkaiKatanaShinNesica"),
                    ("Aquapazza Aquaplus Dream Match (2.01.00)(2013-06-18)[Taito NESiCAxLive][TP]", "AquapazzaAquaplusDreamMatch"),
                    ("Arcana Heart 2 - EXBOARD", "ArcanaHeart2Exboard"),
                    ("Arcana Heart 3 - EXBOARD", "ArcanaHeart3Exboard"),
                    ("Battle Gear 4 (2005)[Taito Type X+][TP]", "BattleGear4"),
                    ("Battle Gear 4 Tuned (2.08)(2007-06-18)[Taito Type X+][TP]", "BattleGear4Tuned"),
                    ("BattleFantasia", "BattleFantasia"),
                    ("Blazblue Calaminity Trigger", "BlazBlueCalaminityTrigger"),
                    ("Chase H.Q. 2 (2.05-2.08)(2007)(JPN,USA,EXP)[Taito Type X2][TP]", "ChaseHQ2"),
                    ("Crazy Speed Arcade (2010)[UNIS PC][TP]", "CrazySpeed"),
                    ("Daemon Bride - EXBOARD", "DaemonBrideExboard"),
                    ("Dirty Drivin' (1.14)(2011-05-11)[Raw Thrills PC][TP]", "DirtyDrivin"),
                    ("Frenzy Express (2001)[Uniana PC][TP]", "FrenzyExpress"),
                    ("GRID (2010)[Sega Europa-R][TP]", "GRID"),
                    ("GTI Club - Supermini Festa! (2008)[Konami PC][TP]", "GtiClub3"),
                    ("H2Overdrive (2010-04-20)[Raw Thrills PC][TP]", "H2Overdrive"),
                    ("Puzzle Bobble - 301200", "PuzzleBobble"),
                    ("Suggoi Arcana Heart 2.6 - EXBOARD", "SuggoiArcanaHeart2Exboard"),
                    ("Tetris The Grand Master 3 Terror Instinct", "TetrisTheGrandMaster3TerrorInstinct"),
                    ("Vampire Savior - The Lord of Vampire[303600]", "VampireSavior"),
                    ("WackyRaces", "WackyRaces")
                };
                var nextProfiles = nextBatch.Select(entry => new GameProfile
                {
                    ProfileName = entry.Item2,
                    GameNameInternal = entry.Item1
                }).ToArray();
                var nextFolders = nextBatch.Select(entry =>
                {
                    var recipe = recipes.Single(candidate => candidate.ProfileName == entry.Item2);
                    var path = "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Item1;
                    return Folder(entry.Item1, path, recipe.Import.ExecutableCandidates[0]);
                }).ToArray();
                var nextLogs = new List<string>();
                var nextFound = ManagedAndroidGameImporter.Scan(
                    nextFolders, recipes, nextProfiles, nextLogs.Add);
                if (nextFound.Count != 21)
                    throw new InvalidOperationException(
                        $"next_test matched game count: expected '21', got '{nextFound.Count}'. " +
                        string.Join(" | ", nextLogs));
                Equal(21, nextFound.Select(game => game.ProfileName).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count(), "next_test unique profile count");
                var linuxTest4 = new[]
                {
                    ("Raiden III - 401401", "RaidenIIINesica"),
                    ("Raiden IV - 401801", "RaidenIVNesica")
                };
                var linuxTest4Profiles = linuxTest4.Select(entry => new GameProfile
                {
                    ProfileName = entry.Item2,
                    GameNameInternal = entry.Item1
                }).ToArray();
                var linuxTest4Folders = linuxTest4.Select(entry => Folder(
                    entry.Item1,
                    "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Item1,
                    "game.exe")).ToArray();
                var linuxTest4Found = ManagedAndroidGameImporter.Scan(
                    linuxTest4Folders,
                    recipes,
                    linuxTest4Profiles);
                Equal(2, linuxTest4Found.Count, "linux_test4 Raiden recipe count");
                var officialRaidenFolders = new[]
                {
                    ("Raiden III for NESiCAxLive", "RaidenIIINesica"),
                    ("Raiden IV for NESiCAxLive", "RaidenIVNesica")
                };
                var officialRaidenProfiles = officialRaidenFolders.Select(entry =>
                    new GameProfile
                    {
                        ProfileName = entry.Item2,
                        GameNameInternal = entry.Item1
                    }).ToArray();
                var officialRaidenFound = ManagedAndroidGameImporter.Scan(
                    officialRaidenFolders.Select(entry => Folder(
                        entry.Item1,
                        "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Item1,
                        "game.exe")),
                    recipes,
                    officialRaidenProfiles);
                Equal(2, officialRaidenFound.Count,
                    "official Raiden NESiCA recipe count");
                foreach (var expected in officialRaidenFolders)
                {
                    Equal(expected.Item2, officialRaidenFound.Single(game =>
                        string.Equals(
                            game.FolderPath,
                            "/storage/emulated/0/Download/TeknoParrotGames/" + expected.Item1,
                            StringComparison.Ordinal)).ProfileName,
                        expected.Item1 + " exact profile match");
                }
                foreach (var profileName in linuxTest4.Select(entry => entry.Item2))
                {
                    var recipe = recipes.Single(candidate =>
                        candidate.ProfileName == profileName);
                    Equal(AndroidLaunchRecipe.InputProtocolFastIo, recipe.InputProtocol,
                        profileName + " fast-I/O protocol");
                    Equal(9013, recipe.ControlsProfileId,
                        profileName + " two-button controls");
                    Equal(60, recipe.FrameRateLimit,
                        profileName + " frame-rate limit");
                    Equal(string.Empty, recipe.CompatibilityPreset,
                        profileName + " native NESiCA presentation");
                }
                var smallestNextTest3 = new[]
                {
                    ("Street Fighter Zero 3 (2014)[Taito NESiCAxLive][TP]",
                        "StreetFighterZero3", "game.exe"),
                    ("The Rumble Fish 2 (2012)[Taito NESiCAxLive][TP]",
                        "RumbleFish2Nesica", "game/Game.exe"),
                    ("Suggoi! Arcana Heart 2 (2.6)(2012)[Taito NESiCAxLive][TP]",
                        "SuggoiArcanaHeart2Nesica", "game.exe"),
                    ("The Fast and the Furious - Super Bikes (1.1.24)(2008-09-22)[Raw Thrills PC][TP]",
                        "FNFSB", "sdaemon.exe")
                };
                var smallestProfiles = smallestNextTest3.Select(entry => new GameProfile
                {
                    ProfileName = entry.Item2,
                    GameNameInternal = entry.Item1
                }).ToArray();
                var smallestFolders = smallestNextTest3.Select(entry => Folder(
                    entry.Item1,
                    "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Item1,
                    entry.Item3)).ToArray();
                var smallestLogs = new List<string>();
                var smallestFound = ManagedAndroidGameImporter.Scan(
                    smallestFolders, recipes, smallestProfiles, smallestLogs.Add);
                if (smallestFound.Count != smallestNextTest3.Length)
                    throw new InvalidOperationException(
                        $"next_test3 smallest batch count: expected '{smallestNextTest3.Length}', " +
                        $"got '{smallestFound.Count}'. {string.Join(" | ", smallestLogs)}");
                Equal(smallestNextTest3.Length, smallestFound.Select(game => game.ProfileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    "next_test3 smallest unique profile count");
                var currentNextTest3 = new[]
                {
                    ("Valve Limit R (2004)[Taito Type X][TP]",
                        "VirtuaRLimit", "launcher.exe"),
                    ("Trouble Witches AC Episode1 - Daughters of Amalgam (2008)[Taito Type X][TP]",
                        "TroubleWitches", "game.exe"),
                    ("Trouble Witches AC Episode1 - Daughters of Amalgam (1.12)(2011)[Taito NESiCAxLive][TP]",
                        "TroubleWitchesNesica", "game.exe"),
                    ("Taisen Hot Gimmick 5 - Mirai Eigou (2.04)(2005-12-08)(JPN)[Taito Type X][TP]",
                        "TaisenHotGimmick5", "game.exe"),
                    ("The King of Fighters XII (2009)[Taito Type X2][TP]",
                        "KingofFightersXII", "game.exe")
                };
                var currentProfiles = currentNextTest3.Select(entry => new GameProfile
                {
                    ProfileName = entry.Item2,
                    GameNameInternal = entry.Item1
                }).ToArray();
                var currentFolders = currentNextTest3.Select(entry => Folder(
                    entry.Item1,
                    "/storage/emulated/0/Download/TeknoParrotGames/" + entry.Item1,
                    entry.Item3)).ToArray();
                var currentLogs = new List<string>();
                var currentFound = ManagedAndroidGameImporter.Scan(
                    currentFolders, recipes, currentProfiles, currentLogs.Add);
                if (currentFound.Count != currentNextTest3.Length)
                    throw new InvalidOperationException(
                        $"next_test3 current batch count: expected '{currentNextTest3.Length}', " +
                        $"got '{currentFound.Count}'. {string.Join(" | ", currentLogs)}");
                Equal(currentNextTest3.Length, currentFound.Select(game => game.ProfileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    "next_test3 current unique profile count");
                Equal(AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit,
                    recipes.Single(recipe => recipe.ProfileName == "VirtuaRLimit").InputProtocol,
                    "Valve Limit R JVS protocol");
                Equal(AndroidLaunchRecipe.InputProtocolSharedRawThrillsSuperBikes,
                    recipes.Single(recipe => recipe.ProfileName == "FNFSB").InputProtocol,
                    "Super Bikes wheel-isolated shared-state protocol");
                Equal(AndroidLaunchRecipe.CompatibilityPresetGameWorkingDirectory,
                    recipes.Single(recipe => recipe.ProfileName == "FNFSB").CompatibilityPreset,
                    "Super Bikes executable-relative asset loading");
                Equal(AndroidLaunchRecipe.CompatibilityPresetBuiltinDdraw,
                    recipes.Single(recipe => recipe.ProfileName == "DragonDanceNesica")
                        .CompatibilityPreset,
                    "Dragon Dance local DDrawCompat bypass");
                Equal(AndroidLaunchRecipe.CompatibilityPresetXactLocalRegister,
                    recipes.Single(recipe => recipe.ProfileName == "GtiClub3")
                        .CompatibilityPreset,
                    "GTI Club local XACT COM registration");
                var underNight = recipes.Single(recipe => recipe.ProfileName == "UnderNightAPM3");
                Equal(AndroidLaunchRecipe.InputProtocolApm3, underNight.InputProtocol,
                    "Under Night APM3 protocol");
                Equal(9033, underNight.ControlsProfileId, "Under Night APM3 controls profile");
                Equal(60, underNight.FrameRateLimit, "Under Night frame-rate limit");
                var underNightFolder =
                    "Under Night In-Birth Exe - Late [cl-r] (APM3 Edition)(3.30.0)(2021)[Sega ALLS][TP]";
                var underNightFound = ManagedAndroidGameImporter.Scan(
                    new[] { Folder(underNightFolder,
                        "/storage/emulated/0/Download/TeknoParrotGames/" + underNightFolder,
                        "RingGame.exe") },
                    recipes,
                    new[] { new GameProfile
                    {
                        ProfileName = "UnderNightAPM3",
                        GameNameInternal = "Under Night In-Birth Exe:Late[cl-r]"
                    } });
                Equal(1, underNightFound.Count, "Under Night APM3 folder match");
                var apmButtons = new uint[4];
                var apmAxes = new short[64];
                var apmReport = new byte[AndroidApm3InputEncoder.ReportSize];
                apmButtons[0] = (1u << (int)ForwardedInputButton.Test) |
                                (1u << (int)ForwardedInputButton.Start) |
                                (1u << (int)ForwardedInputButton.Button1) |
                                (1u << (int)ForwardedInputButton.Button6) |
                                (1u << (int)ForwardedInputButton.Button7) |
                                (1u << (int)ForwardedInputButton.Button8);
                apmAxes[0] = short.MinValue;
                apmAxes[1] = short.MaxValue;
                apmAxes[4] = short.MaxValue;
                apmAxes[5] = short.MaxValue;
                AndroidApm3InputEncoder.BuildReport(apmButtons, apmAxes, apmReport);
                Equal((byte)1, apmReport[0], "APM3 test byte");
                Equal((byte)1, apmReport[3], "APM3 down byte from axis");
                Equal((byte)1, apmReport[4], "APM3 left byte from axis");
                Equal((byte)1, apmReport[6], "APM3 start byte");
                Equal((byte)1, apmReport[7], "APM3 button 1 byte");
                Equal((byte)1, apmReport[12], "APM3 button 6 byte");
                Equal((byte)1, apmReport[13], "APM3 extension button 7 byte");
                Equal((byte)1, apmReport[14], "APM3 extension button 8 byte");
                Equal((byte)0, apmReport[15], "APM3 reserved byte");
                var wmmt6R = recipes.Single(recipe => recipe.ProfileName == "WMMT6R");
                Equal("x64", wmmt6R.GuestArchitecture, "WMMT6R guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsWmmt, wmmt6R.InputProtocol,
                    "WMMT6R JVS protocol");
                Equal(9012, wmmt6R.ControlsProfileId, "WMMT6R controls profile");
                Equal(60, wmmt6R.FrameRateLimit, "WMMT6R frame-rate limit");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWmmtNoTerminal,
                    wmmt6R.CompatibilityPreset, "WMMT6R no-terminal compatibility preset");
                var wmmt6RR = recipes.Single(recipe => recipe.ProfileName == "WMMT6RR");
                Equal("x64", wmmt6RR.GuestArchitecture, "WMMT6RR guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsWmmt, wmmt6RR.InputProtocol,
                    "WMMT6RR JVS protocol");
                Equal("TeknoParrot", wmmt6RR.LibraryDirectory,
                    "WMMT6RR TeknoParrot runtime directory");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWmmtTerminal,
                    wmmt6RR.CompatibilityPreset, "WMMT6RR terminal compatibility preset");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWmmtNoTerminal,
                    recipes.Single(recipe => recipe.ProfileName == "WMMT5")
                        .CompatibilityPreset,
                    "WMMT5 no-terminal compatibility preset");
                foreach (var profileName in new[] { "WMMT5DX", "WMMT5DXPlus" })
                {
                    Equal(AndroidLaunchRecipe.CompatibilityPresetWmmtTerminal,
                        recipes.Single(recipe => recipe.ProfileName == profileName)
                            .CompatibilityPreset,
                        $"{profileName} terminal compatibility preset");
                }
                Equal(AndroidLaunchRecipe.CompatibilityPresetWmmtNoTerminal,
                    recipes.Single(recipe => recipe.ProfileName == "WMMT6")
                        .CompatibilityPreset,
                    "WMMT6 no-terminal compatibility preset");
                var taiko = recipes.Single(recipe => recipe.ProfileName == "Taiko");
                Equal("x64", taiko.GuestArchitecture, "Taiko guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolSharedTaiko, taiko.InputProtocol,
                    "Taiko shared-state protocol");
                Equal(9022, taiko.ControlsProfileId, "Taiko controls profile");
                Equal(120, taiko.FrameRateLimit,
                    "Taiko native cabinet frame-rate limit");
                Equal(1280, taiko.ResolutionWidth, "Taiko Android render width");
                Equal(720, taiko.ResolutionHeight, "Taiko Android render height");
                Equal(AndroidLaunchRecipe.CompatibilityPresetTaikoCustomResolution,
                    taiko.CompatibilityPreset, "Taiko custom-resolution compatibility preset");
                var dirtyDrivin = recipes.Single(recipe => recipe.ProfileName == "DirtyDrivin");
                Equal(AndroidLaunchRecipe.CompatibilityPresetDirtyDrivingFullscreen,
                    dirtyDrivin.CompatibilityPreset,
                    "Dirty Drivin reserved-address compatibility preset");
                Equal(@".\OpenParrotWin32\OpenParrot",
                    dirtyDrivin.Arguments[0],
                    "Dirty Drivin shared OpenParrot core");
                var wackyRaces = recipes.Single(recipe => recipe.ProfileName == "WackyRaces");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWackyRacesNetwork,
                    wackyRaces.CompatibilityPreset,
                    "Wacky Races network compatibility preset");
                Equal(AndroidLaunchRecipe.DisplayModeCentered,
                    wackyRaces.DisplayMode,
                    "Wacky Races decorated-window centering policy");
                var chaseHq2 = recipes.Single(recipe => recipe.ProfileName == "ChaseHQ2");
                Equal(AndroidLaunchRecipe.CompatibilityPresetChaseHq2,
                    chaseHq2.CompatibilityPreset,
                    "Chase H.Q. 2 input and media compatibility preset");
                var eadp = recipes.Single(recipe => recipe.ProfileName == "EADP");
                Equal(@".\OpenParrotWin32\OpenParrot",
                    eadp.Arguments[0],
                    "EADP shared OpenParrot core");
                var starWars = recipes.Single(recipe => recipe.ProfileName == "StarWars");
                Equal(AndroidLaunchRecipe.CompatibilityPresetStarWars,
                    starWars.CompatibilityPreset,
                    "Star Wars low-resolution display compatibility preset");
                Equal(960, starWars.ResolutionWidth,
                    "Star Wars Android render width");
                Equal(540, starWars.ResolutionHeight,
                    "Star Wars Android render height");
                var mkdx = recipes.Single(recipe => recipe.ProfileName == "MKDX");
                Equal("x86", mkdx.GuestArchitecture, "Mario Kart DX guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsMkdx, mkdx.InputProtocol,
                    "Mario Kart DX JVS protocol");
                Equal(9034, mkdx.ControlsProfileId, "Mario Kart DX controls profile");
                Equal("TeknoParrot", mkdx.LibraryDirectory,
                    "Mario Kart DX normal TeknoParrot runtime directory");
                Equal(AndroidLaunchRecipe.CompatibilityPresetParkedEntrypoint,
                    mkdx.CompatibilityPreset,
                    "Mario Kart DX parked-entry-point injection preset");
                var resolvedMkdx = mkdx.Resolve(
                    @"D:\TeknoParrotGames\Mario Kart Arcade GP DX\MK_AGP3_FINAL.exe");
                Equal(@"E:\TeknoParrotRuntime\OpenParrotWin32\OpenParrotLoader.exe",
                    resolvedMkdx.LoaderExecutable, "Mario Kart DX loader path");
                Equal(@".\TeknoParrot\TeknoParrot",
                    resolvedMkdx.Arguments[0], "Mario Kart DX debug core argument");
                var mkdxUsa106 = recipes.Single(recipe => recipe.ProfileName == "MKDXUSA106");
                Equal("x86", mkdxUsa106.GuestArchitecture,
                    "Mario Kart DX USA 1.06 guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsMkdx, mkdxUsa106.InputProtocol,
                    "Mario Kart DX USA 1.06 JVS protocol");
                Equal(9034, mkdxUsa106.ControlsProfileId,
                    "Mario Kart DX USA 1.06 controls profile");
                Equal(AndroidLaunchRecipe.CompatibilityPresetParkedEntrypoint,
                    mkdxUsa106.CompatibilityPreset,
                    "Mario Kart DX USA 1.06 parked-entry-point injection preset");
                Equal(1280, mkdxUsa106.ResolutionWidth,
                    "Mario Kart DX USA 1.06 Android render width");
                Equal(720, mkdxUsa106.ResolutionHeight,
                    "Mario Kart DX USA 1.06 Android render height");
                Equal("1", mkdxUsa106.ProfileConfigOverrides.Single(item =>
                        item.CategoryName == "General" &&
                        item.FieldName == "CustomResolution").FieldValue,
                    "Mario Kart DX USA 1.06 custom-resolution core switch");

                var initialD8 = recipes.Single(recipe => recipe.ProfileName == "ID8");
                Equal("x86", initialD8.GuestArchitecture, "Initial D8 guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsInitialD, initialD8.InputProtocol,
                    "Initial D8 JVS protocol");
                Equal(9036, initialD8.ControlsProfileId, "Initial D8 controls profile");
                Equal("TeknoParrot", initialD8.LibraryDirectory,
                    "Initial D8 normal TeknoParrot runtime directory");
                Equal(AndroidLaunchRecipe.CompatibilityPresetInitialD8,
                    initialD8.CompatibilityPreset,
                    "Initial D8 picodaemon prelaunch preset");
                Equal(60, initialD8.FrameRateLimit, "Initial D8 frame-rate limit");
                var resolvedInitialD8 = initialD8.Resolve(
                    @"D:\TeknoParrotGames\id8\InitialD8_GLW_RE_SBZZ_redumped_.exe");
                Equal(@".\TeknoParrot\TeknoParrot", resolvedInitialD8.Arguments[0],
                    "Initial D8 debug core argument");
                Equal(@"D:\TeknoParrotGames\id8\InitialD8_GLW_RE_SBZZ_redumped_.exe",
                    resolvedInitialD8.Arguments[1], "Initial D8 game argument");

                var initialDTheArcade = recipes.Single(recipe => recipe.ProfileName == "IDTAS5");
                Equal("x64", initialDTheArcade.GuestArchitecture,
                    "Initial D The Arcade guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolAllsIdta,
                    initialDTheArcade.InputProtocol,
                    "Initial D The Arcade ALLS input protocol");
                Equal(9075, initialDTheArcade.ControlsProfileId,
                    "Initial D The Arcade Aime-aware controls profile");
                Equal("TeknoParrot", initialDTheArcade.LibraryDirectory,
                    "Initial D The Arcade TeknoParrot runtime directory");
                Equal(AndroidLaunchRecipe.CompatibilityPresetInitialDTheArcade,
                    initialDTheArcade.CompatibilityPreset,
                    "Initial D The Arcade two-process preset");

                var cruisnBlast = recipes.Single(recipe => recipe.ProfileName == "CruisnBlast");
                Equal("x86", cruisnBlast.GuestArchitecture,
                    "Cruis'n Blast guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolSharedRawThrills,
                    cruisnBlast.InputProtocol, "Cruis'n Blast shared-state protocol");
                Equal(9037, cruisnBlast.ControlsProfileId,
                    "Cruis'n Blast controls profile");
                Equal(1280, cruisnBlast.ResolutionWidth,
                    "Cruis'n Blast Android render width");
                Equal(720, cruisnBlast.ResolutionHeight,
                    "Cruis'n Blast Android render height");
                Equal("1", cruisnBlast.ProfileConfigOverrides.Single(item =>
                        item.CategoryName == "General" &&
                        item.FieldName == "Enable Custom Resolution").FieldValue,
                    "Cruis'n Blast custom-resolution core switch");
                Equal("1280x720", cruisnBlast.ProfileConfigOverrides.Single(item =>
                        item.CategoryName == "General" &&
                        item.FieldName == "Custom Resolution").FieldValue,
                    "Cruis'n Blast custom-resolution value");
                Equal("0", cruisnBlast.ProfileConfigOverrides.Single(item =>
                        item.CategoryName == "General" &&
                        item.FieldName == "Enable Higher Res Shadows").FieldValue,
                    "Cruis'n Blast Android high-resolution shadow guard");
                Equal("ElfLdr2", cruisnBlast.LibraryDirectory,
                    "Cruis'n Blast ElfLoader2 runtime directory");
                var resolvedCruisn = cruisnBlast.Resolve(
                    @"D:\TeknoParrotGames\Cruis'n Blast\game");
                Equal(@"E:\TeknoParrotRuntime\ElfLdr2\elfloader.exe",
                    resolvedCruisn.LoaderExecutable, "Cruis'n Blast loader path");
                Equal(@"D:\TeknoParrotGames\Cruis'n Blast\game",
                    resolvedCruisn.Arguments[0], "Cruis'n Blast ELF argument");
                Equal(1280, resolvedCruisn.ResolutionWidth,
                    "resolved Cruis'n Blast width");
                Equal(720, resolvedCruisn.ResolutionHeight,
                    "resolved Cruis'n Blast height");

                var terminator = recipes.Single(recipe => recipe.ProfileName == "Terminator");
                Equal("x86", terminator.GuestArchitecture,
                    "Terminator guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolSharedRawThrillsGun,
                    terminator.InputProtocol, "Terminator shared gun protocol");
                Equal(9044, terminator.ControlsProfileId,
                    "Terminator controls profile");
                Equal("ElfLdr2", terminator.LibraryDirectory,
                    "Terminator ElfLoader2 runtime directory");
                var resolvedTerminator = terminator.Resolve(
                    @"D:\TeknoParrotGames\Terminator Salvation\game");
                Equal(@"E:\TeknoParrotRuntime\ElfLdr2\elfloader.exe",
                    resolvedTerminator.LoaderExecutable, "Terminator loader path");
                Equal(@"D:\TeknoParrotGames\Terminator Salvation\game",
                    resolvedTerminator.Arguments[0], "Terminator ELF argument");

                var wmmt3DxPlus = recipes.Single(recipe => recipe.ProfileName == "WMMT3DXP");
                Equal("x86", wmmt3DxPlus.GuestArchitecture,
                    "WMMT3DX+ guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolJvsWmmt,
                    wmmt3DxPlus.InputProtocol, "WMMT3DX+ JVS protocol");
                Equal(9012, wmmt3DxPlus.ControlsProfileId,
                    "WMMT3DX+ controls profile");
                Equal(60, wmmt3DxPlus.FrameRateLimit,
                    "WMMT3DX+ frame-rate limit");
                Equal(1280, wmmt3DxPlus.ResolutionWidth,
                    "WMMT3DX+ custom-resolution width");
                Equal(720, wmmt3DxPlus.ResolutionHeight,
                    "WMMT3DX+ custom-resolution height");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWmmt3YaCard,
                    wmmt3DxPlus.CompatibilityPreset,
                    "WMMT3DX+ YACardEmu prelaunch preset");
                Equal("1", wmmt3DxPlus.ProfileConfigOverrides.Single(item =>
                        item.CategoryName == "General" &&
                        item.FieldName == "CustomResolution").FieldValue,
                    "WMMT3DX+ custom-resolution core switch");
                Equal("ElfLdr2", wmmt3DxPlus.LibraryDirectory,
                    "WMMT3DX+ ElfLoader2 runtime directory");
                var resolvedWmmt3DxPlus = wmmt3DxPlus.Resolve(
                    @"D:\TeknoParrotGames\Wangan Midnight Maximum Tune 3DX+\main");
                Equal(@"E:\TeknoParrotRuntime\ElfLdr2\elfloader.exe",
                    resolvedWmmt3DxPlus.LoaderExecutable, "WMMT3DX+ loader path");
                Equal(@"D:\TeknoParrotGames\Wangan Midnight Maximum Tune 3DX+\main",
                    resolvedWmmt3DxPlus.Arguments[0], "WMMT3DX+ ELF argument");
                Equal(1280, resolvedWmmt3DxPlus.ResolutionWidth,
                    "resolved WMMT3DX+ width");
                Equal(720, resolvedWmmt3DxPlus.ResolutionHeight,
                    "resolved WMMT3DX+ height");

                var elfGamesFound = ManagedAndroidGameImporter.Scan(
                    new[]
                    {
                        Folder("Cruis'n Blast (1.25)",
                            "/storage/emulated/0/Download/TeknoParrotGames/Cruis'n Blast (1.25)",
                            "game"),
                        Folder("Terminator Salvation (1.25.00)",
                            "/storage/emulated/0/Download/TeknoParrotGames/Terminator Salvation (1.25.00)",
                            "game"),
                        Folder("Wangan Midnight Maximum Tune 3DX+ (EXP)(2010)",
                            "/storage/emulated/0/Download/TeknoParrotGames/Wangan Midnight Maximum Tune 3DX+ (EXP)(2010)",
                            "main")
                    },
                    recipes,
                    new[]
                    {
                        new GameProfile { ProfileName = "CruisnBlast", GameNameInternal = "Cruis'n Blast" },
                        new GameProfile { ProfileName = "Terminator", GameNameInternal = "Terminator Salvation" },
                        new GameProfile { ProfileName = "WMMT3DXP", GameNameInternal = "Wangan Midnight Maximum Tune 3DX PLUS" }
                    });
                Equal(3, elfGamesFound.Count, "ElfLoader2 game import count");

                var luigi = recipes.Single(recipe => recipe.ProfileName == "LuigisMansion");
                Equal("x64", luigi.GuestArchitecture, "Luigi guest architecture");
                Equal(AndroidLaunchRecipe.InputProtocolSharedLuigiMansion,
                    luigi.InputProtocol, "Luigi shared-state protocol");
                Equal(9035, luigi.ControlsProfileId, "Luigi controls profile");
                Equal("TeknoParrot", luigi.LibraryDirectory,
                    "Luigi phone-only debug library directory");
                Equal(AndroidLaunchRecipe.CompatibilityPresetWineD3dParkedEntrypoint,
                    luigi.CompatibilityPreset,
                    "Luigi x64 WineD3D parked-entrypoint preset");
                var resolvedLuigi = luigi.Resolve(
                    @"D:\TeknoParrotGames\LuigisMansion_PAL\GameFiles\LuigiMansionGameFiles\exe\x64\VACUUM.exe");
                Equal(@"E:\TeknoParrotRuntime\OpenParrotWin64\OpenParrotLoader64.exe",
                    resolvedLuigi.LoaderExecutable, "Luigi loader path");
                Equal(@".\TeknoParrot\TeknoParrot64",
                    resolvedLuigi.Arguments[0], "Luigi debug core argument");

                var debugCoreFound = ManagedAndroidGameImporter.Scan(
                    new[]
                    {
                        Folder("Mario Kart Arcade GP DX (1.06.35-OF)(2022-08-01)(USA)[Namco ES3A][TP]",
                            "/storage/emulated/0/Download/TeknoParrotGames/Mario Kart Arcade GP DX (1.06.35-OF)(2022-08-01)(USA)[Namco ES3A][TP]",
                            "MK_AGP3_FINAL.exe"),
                        Folder("LuigiMansionGameFiles",
                            "/storage/emulated/0/Download/TeknoParrotGames/LuigiMansionGameFiles",
                            "exe/x64/VACUUM.exe")
                    },
                    recipes,
                    new[]
                    {
                        new GameProfile { ProfileName = "MKDX", GameNameInternal = "Mario Kart Arcade GP DX" },
                        new GameProfile { ProfileName = "MKDXUSA106", GameNameInternal = "Mario Kart Arcade GP DX 1.06 USA" },
                        new GameProfile { ProfileName = "LuigisMansion", GameNameInternal = "Luigi's Mansion Arcade" }
                    });
                Equal(2, debugCoreFound.Count, "phone-only debug-core import count");
                Equal("MKDXUSA106",
                    debugCoreFound.Single(game => game.GameExecutablePath.EndsWith(
                        "/MK_AGP3_FINAL.exe", StringComparison.Ordinal)).ProfileName,
                    "Mario Kart USA 1.06-specific recipe wins folder matching");
                var grid = recipes.Single(recipe => recipe.ProfileName == "GRID");
                Equal(AndroidLaunchRecipe.CompatibilityPresetLargeAddressAwareDdraw,
                    grid.CompatibilityPreset,
                    "GRID large-address-aware DirectDraw compatibility preset");
                var homura = recipes.Single(recipe => recipe.ProfileName == "Homura");
                Equal(AndroidLaunchRecipe.CompatibilityPresetBox64Interpreter,
                    homura.CompatibilityPreset,
                    "Homura Box64 interpreter and PulseAudio stability preset");
                Equal(AndroidLaunchRecipe.DisplayModeCentered, dirtyDrivin.DisplayMode,
                    "Dirty Drivin windowed-centered display policy");
                Equal(recipes.Count, recipes.Count(recipe =>
                    AndroidLaunchRecipe.IsSupportedDisplayMode(recipe.DisplayMode)),
                    "validated Android display policy count");
                True(smallestFound.Single(game => game.ProfileName == "RumbleFish2Nesica")
                    .GameExecutablePath.EndsWith("/game/Game.exe", StringComparison.Ordinal),
                    "Rumble Fish nested executable");
                var streetFighterZero3 = recipes.Single(recipe =>
                    recipe.ProfileName == "StreetFighterZero3");
                Equal(@".\OpenParrotWin32Legacy\OpenParrot",
                    streetFighterZero3.Arguments[0],
                    "Street Fighter Zero 3 Fold6-qualified OpenParrot fallback");
                Equal("OpenParrotWin32Legacy", streetFighterZero3.LibraryDirectory,
                    "Street Fighter Zero 3 isolated legacy runtime directory");
                var vampireSavior = recipes.Single(recipe =>
                    recipe.ProfileName == "VampireSavior");
                Equal(@".\OpenParrotWin32Legacy\OpenParrot",
                    vampireSavior.Arguments[0],
                    "Vampire Savior Fold6-qualified OpenParrot fallback");
                Equal("OpenParrotWin32Legacy", vampireSavior.LibraryDirectory,
                    "Vampire Savior isolated legacy runtime directory");
                var battleGear4Tuned = recipes.Single(recipe =>
                    recipe.ProfileName == "BattleGear4Tuned");
                Equal(AndroidLaunchRecipe.CompatibilityPresetNone,
                    battleGear4Tuned.CompatibilityPreset,
                    "Battle Gear 4 Tuned does not inherit the original BG4 preset");
                Equal(@".\OpenParrotWin32\OpenParrot",
                    battleGear4Tuned.Arguments[0],
                    "Battle Gear 4 Tuned public OpenParrot core");
                Equal("OpenParrotWin32", battleGear4Tuned.LibraryDirectory,
                    "Battle Gear 4 Tuned public runtime directory");
                Equal(string.Empty, battleGear4Tuned.CompatibilityPreset,
                    "Battle Gear 4 Tuned keeps Box64 dynarec enabled");
                var kofXiii = recipes.Single(recipe =>
                    recipe.ProfileName == "KingofFightersXIII");
                True(kofXiii.ResolutionWidth == 0 && kofXiii.ResolutionHeight == 0,
                    "KOF XIII leaves the proven movie-working guest desktop unchanged");
                Equal(AndroidLaunchRecipe.DisplayModeCentered, kofXiii.DisplayMode,
                    "KOF XIII centered Android display mode");
                var tekken7Fr = recipes.Single(recipe =>
                    recipe.ProfileName == "Tekken7FR");
                Equal("-windowed -ResX=1280 -ResY=720",
                    tekken7Fr.Arguments[2],
                    "Tekken 7 Fated Retribution working UE4 window contract");
                var tetris = recipes.Single(recipe =>
                    recipe.ProfileName == "TetrisTheGrandMaster3TerrorInstinct");
                Equal(AndroidLaunchRecipe.InputProtocolJvs, tetris.InputProtocol,
                    "Tetris input protocol");
                Equal(9011, tetris.ControlsProfileId, "Tetris controls profile");
                Equal(60, tetris.FrameRateLimit, "Tetris frame-rate limit");
                Equal(1280, tetris.ResolutionWidth, "Tetris resolution width");
                Equal(720, tetris.ResolutionHeight, "Tetris resolution height");
                True(tetris.PerformanceModeDefault, "Tetris performance mode default");
                var resolvedTetris = tetris.Resolve(
                    @"D:\TeknoParrotGames\Tetris The Grand Master 3 Terror Instinct\game.exe");
                Equal(1280, resolvedTetris.ResolutionWidth, "resolved Tetris resolution width");
                Equal(720, resolvedTetris.ResolutionHeight, "resolved Tetris resolution height");
                True(resolvedTetris.PerformanceModeDefault,
                    "resolved Tetris performance mode default");
                var wackyCandidates = ManagedAndroidGameImporter.GetCandidateExecutablePaths(
                    "WackyRaces", recipes);
                True(wackyCandidates.Contains(
                    "TypeXsys_Wacky_Races/Launcher.exe", StringComparer.OrdinalIgnoreCase),
                    "Wacky direct-scan candidate");
                Equal(0, ManagedAndroidGameImporter.GetCandidateExecutablePaths(
                    "Unrelated Windows Game", recipes).Count,
                    "unrelated direct-scan candidate count");

                var rastan = recipes.Single(recipe => recipe.ProfileName == "RastanSaga");
                Equal(AndroidLaunchRecipe.InputProtocolFastIo, rastan.InputProtocol,
                    "Rastan input protocol");
                Equal(9003, rastan.ControlsProfileId, "Rastan controls profile");
                Equal(60, rastan.FrameRateLimit, "Rastan frame-rate limit");
                True(rastan.PerformanceModeDefault, "Rastan performance mode default");
                var resolved = rastan.Resolve(@"D:\TeknoParrotGames\Rastan Saga[401500]\game.exe");
                Equal(1, resolved.ContainerId, "container id");
                Equal("teknoparrot-x86-v1", resolved.ContainerTemplate, "container template");
                Equal(
                    @"E:\TeknoParrotRuntime\OpenParrotWin32\OpenParrotLoader.exe",
                    resolved.LoaderExecutable,
                    "loader path");
                Equal(
                    @"D:\TeknoParrotGames\Rastan Saga[401500]\game.exe",
                    resolved.Arguments[1],
                    "game argument");
                Equal(AndroidLaunchRecipe.InputProtocolFastIo, resolved.InputProtocol,
                    "resolved Rastan input protocol");
                Equal(9003, resolved.ControlsProfileId, "resolved Rastan controls profile");
                Equal(60, resolved.FrameRateLimit, "resolved Rastan frame-rate limit");
                Equal(AndroidLaunchRecipe.DisplayModeCentered, resolved.DisplayMode,
                    "resolved Rastan windowed-centered display policy");

                var resolvedCosplay = cosplay.Resolve(
                    @"D:\TeknoParrotGames\3D Cosplay Mahjong - 401300\game.exe");
                Equal(AndroidLaunchRecipe.InputProtocolJvs, resolvedCosplay.InputProtocol,
                    "resolved Cosplay input protocol");
                Equal(9004, resolvedCosplay.ControlsProfileId,
                    "resolved Cosplay controls profile");
                Equal(60, resolvedCosplay.FrameRateLimit,
                    "resolved Cosplay frame-rate limit");
                Equal(AndroidLaunchRecipe.DisplayModeCentered, resolvedCosplay.DisplayMode,
                    "resolved Cosplay windowed-centered display policy");

                var resolvedSr3 = sr3.Resolve(
                    @"D:\TeknoParrotGames\SegaRally3\SegaRally3\Rally\Rally.exe");
                Equal(
                    @"E:\TeknoParrotRuntime\OpenParrotWin32\OpenParrotLoader.exe",
                    resolvedSr3.LoaderExecutable,
                    "SR3 loader path");
                Equal(
                    @".\OpenParrotWin32\OpenParrot",
                    resolvedSr3.Arguments[0],
                    "SR3 OpenParrot.dll argument");
                Equal(
                    @"E:\TeknoParrotRuntime\OpenParrotWin32",
                    resolvedSr3.LibraryDirectory,
                    "SR3 library path");
                Equal(AndroidLaunchRecipe.InputProtocolSegaRally, resolvedSr3.InputProtocol,
                    "resolved SR3 input protocol");
                Equal(9001, resolvedSr3.ControlsProfileId, "resolved SR3 controls profile");
                Equal(60, resolvedSr3.FrameRateLimit, "resolved SR3 frame-rate limit");

                True(ManagedAndroidGameImporter.IsWinlatorDownloadPath(
                    "/storage/emulated/0/Download/TeknoParrotGames/Test/game.exe"),
                    "Download path accepted");
                True(ManagedAndroidGameImporter.IsWinlatorSharedGamePath(
                    "/storage/emulated/0/TeknoParrotGames/Test/game.exe"),
                    "restricted shared game library accepted");
                Equal(
                    @"G:\Test\game.exe",
                    AndroidWinlatorGamePath.ToDosPath(
                        "/storage/emulated/0/TeknoParrotGames/Test/game.exe",
                        "/storage/emulated/0/Download"),
                    "restricted shared game library mapped to G");
                Equal(
                    @"D:\TeknoParrotGames\Test\game.exe",
                    AndroidWinlatorGamePath.ToDosPath(
                        "/storage/emulated/0/Download/TeknoParrotGames/Test/game.exe",
                        "/storage/emulated/0/Download"),
                    "legacy Downloads game library mapped to D");
                Equal(
                    @"H:\MachStorm\ACE7_WIN_10.exe",
                    AndroidWinlatorGamePath.ToDosPath(
                        "/storage/1234-ABCD/TeknoParrotGames/MachStorm/ACE7_WIN_10.exe",
                        "/storage/emulated/0/Download"),
                    "restricted removable-card game library mapped to H");
                Equal(
                    @"H:\MachStorm\ACE7_WIN_10.exe",
                    AndroidWinlatorGamePath.ToDosPath(
                        @"H:\MachStorm\ACE7_WIN_10.exe",
                        "/storage/emulated/0/Download"),
                    "canonical removable-card DOS path accepted");
                True(ManagedAndroidGameImporter.IsWinlatorSharedGamePath(
                    "/storage/1234-ABCD/Arcade/MachStorm/ACE7_WIN_10.exe"),
                    "arbitrary removable-card game folder accepted as a scoped drive");
                var removableScoped = AndroidWinlatorGamePath.Resolve(
                    "/storage/1234-ABCD/Arcade/MachStorm/ACE7_WIN_10.exe",
                    "/storage/emulated/0/Download");
                Equal(@"I:\ACE7_WIN_10.exe", removableScoped.DosPath,
                    "arbitrary removable-card executable mapped to scoped I drive");
                Equal("/storage/1234-ABCD/Arcade/MachStorm",
                    removableScoped.ScopedGameDirectory,
                    "only the selected removable-card executable directory is exposed");
                True(AndroidProfileConfig.IsBooleanEnabled(
                        "[General]\nReverse Y Axis=1\n",
                        "General",
                        "Reverse Y Axis"),
                    "Android forwarded profile boolean enabled");
                False(AndroidProfileConfig.IsBooleanEnabled(
                        "[Other]\nReverse Y Axis=1\n[General]\nReverse Y Axis=0\n",
                        "General",
                        "Reverse Y Axis"),
                    "Android forwarded profile boolean is section-aware");
                True(ManagedAndroidGameImporter.IsWinlatorDownloadPath(
                    "/storage/emulated/0/Documents/Test/game.exe"),
                    "non-Download shared-storage path accepted through a scoped drive");
                True(ManagedAndroidGameImporter.IsWinlatorSharedGamePath(
                    "/storage/emulated/0/TeknoParrotGamesBackup/Test/game.exe"),
                    "arbitrary primary-storage game folder accepted as a scoped drive");
                var primaryScoped = AndroidWinlatorGamePath.Resolve(
                    "/storage/emulated/0/Arcade/Fighters/game.exe",
                    "/storage/emulated/0/Download");
                Equal(@"I:\game.exe", primaryScoped.DosPath,
                    "arbitrary primary-storage executable mapped to scoped I drive");
                Equal("/storage/emulated/0/Arcade/Fighters",
                    primaryScoped.ScopedGameDirectory,
                    "only the selected primary-storage executable directory is exposed");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.Resolve(
                        "/storage/emulated/0/Android/data/example/game.exe",
                        "/storage/emulated/0/Download"),
                    "protected Android application storage rejected");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.Resolve(
                        "/storage/emulated/0/Android/game.exe",
                        "/storage/emulated/0/Download"),
                    "shared Android root rejected because it contains protected storage");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.Resolve(
                        "/storage/1234-ABCD/Android/obb/example/game.exe",
                        "/storage/emulated/0/Download"),
                    "protected removable-card Android application storage rejected");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.Resolve(
                        "/storage/emulated/0/game.exe",
                        "/storage/emulated/0/Download"),
                    "primary storage root is never exposed as a scoped drive");
                True(AndroidRpcs3x6GamePath.IsConfigured(
                        "/storage/emulated/0/arcade/rpcs3/DSPS/" +
                        "dev_hdd0/game/SCEEXE000/USRDIR/EBOOT.BIN"),
                    "direct RPCS3X6 EBOOT path accepted");
                True(AndroidRpcs3x6GamePath.IsConfigured(
                        "/storage/1234-ABCD/arcade/rpcs3/DSPS/" +
                        "dev_hdd0/game/SCEEXE000/USRDIR/eboot.bin"),
                    "removable-storage RPCS3X6 EBOOT path accepted");
                False(AndroidRpcs3x6GamePath.IsConfigured(
                        "/storage/emulated/0/arcade/rpcs3/DSPS"),
                    "RPCS3X6 folder path rejected");
                False(AndroidRpcs3x6GamePath.IsConfigured(
                        "/storage/emulated/0/arcade/rpcs3/DSPS/" +
                        "dev_hdd0/game/OTHER/USRDIR/EBOOT.BIN"),
                    "unrelated RPCS3 title path rejected");
                False(ManagedAndroidGameImporter.IsWinlatorDownloadPath(
                    "/storage/emulated/0/Download/../Documents/game.exe"),
                    "traversal rejected");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.ToDosPath(
                        @"E:\Photos\private.jpg",
                        "/storage/emulated/0/Download"),
                    "private runtime drive rejected for game files");
                Throws<InvalidOperationException>(
                    () => AndroidWinlatorGamePath.ToDosPath(
                        @"G:\Test/bad.exe",
                        "/storage/emulated/0/Download"),
                    "mixed DOS separators rejected");

                var originalLoader = rastan.LoaderExecutable;
                rastan.LoaderExecutable = @"..\escape.exe";
                Throws<InvalidDataException>(() => rastan.Validate(), "recipe traversal rejected");
                rastan.LoaderExecutable = originalLoader;
                rastan.DisplayMode = "stretch";
                Throws<InvalidDataException>(() => rastan.Validate(), "unknown display mode rejected");

                Console.WriteLine("Android recipe schema/validation: PASS");
                Console.WriteLine("Android per-game display/debug profile round trip: PASS");
                Console.WriteLine("Android folder matching for Rastan/Cosplay: PASS");
                Console.WriteLine("Android physical scanner fallback: PASS");
                Console.WriteLine("Android Wonderland Wars folder matching: PASS");
                Console.WriteLine("Android Sega Ring nested executable matching: PASS");
                Console.WriteLine("Android next_test 21-game batch matching: PASS");
                Console.WriteLine("Android next_test3 current five-game matching: PASS");
                Console.WriteLine("Android Tetris three-button/60 FPS recipe: PASS");
                Console.WriteLine("Android Sega Rally 3/OpenParrot.dll recipe: PASS");
                Console.WriteLine("Android restricted shared-game path boundary: PASS");
                Console.WriteLine("Android Winlator launch resolution: PASS");
                Console.WriteLine("Android display-before-LAA preparation order: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android managed import test failed: " + error.Message);
                return 1;
            }
        }

        private static AndroidGameFolderSnapshot Folder(
            string name,
            string path,
            string executable = "game.exe") =>
            new AndroidGameFolderSnapshot(
                name,
                path,
                new List<AndroidGameFileSnapshot>
                {
                    new AndroidGameFileSnapshot(executable, path + "/" + executable),
                    new AndroidGameFileSnapshot("data/attract.bin", path + "/data/attract.bin")
                });

        private static void VerifyDebugLoggingProfileRoundTrip()
        {
            var serializer = new XmlSerializer(typeof(GameProfile));
            var explicitPerformanceProfile = new GameProfile
            {
                ProfileName = "AndroidLoggingRoundTrip",
                AndroidDebugLogging = false,
                AndroidDisplayMode = AndroidDisplayMode.AspectFit
            };
            using var writer = new StringWriter();
            serializer.Serialize(writer, explicitPerformanceProfile);
            var xml = writer.ToString();
            True(xml.Contains("<AndroidDebugLogging>false</AndroidDebugLogging>",
                StringComparison.Ordinal), "explicit performance mode serialized");
            True(xml.Contains("<AndroidDisplayMode>AspectFit</AndroidDisplayMode>",
                StringComparison.Ordinal), "explicit Android display mode serialized");
            using var reader = new StringReader(xml);
            var restored = (GameProfile)serializer.Deserialize(reader)!;
            Equal<bool?>(false, restored.AndroidDebugLogging,
                "explicit performance mode round trip");
            Equal<AndroidDisplayMode?>(AndroidDisplayMode.AspectFit, restored.AndroidDisplayMode,
                "explicit Android display mode round trip");

            var inheritedProfile = new GameProfile { ProfileName = "AndroidLoggingInherited" };
            using var inheritedWriter = new StringWriter();
            serializer.Serialize(inheritedWriter, inheritedProfile);
            False(inheritedWriter.ToString().Contains("AndroidDebugLogging",
                StringComparison.Ordinal), "inherited debug default omitted");
            False(inheritedWriter.ToString().Contains("AndroidDisplayMode",
                StringComparison.Ordinal), "inherited display default omitted");
        }

        private static void VerifyWinlatorPreparationOrder(string recipeDirectory)
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(recipeDirectory, "..", ".."));
            var winlatorRoot = FindWinlatorRoot(repositoryRoot);
            var activityPath = Path.Combine(
                winlatorRoot,
                "app", "app", "src", "main", "java", "com", "winlator",
                "XServerDisplayActivity.java");
            var source = File.ReadAllText(activityPath);
            var wineGStreamerPatchPath = Path.Combine(
                winlatorRoot,
                "app", "app", "src", "main", "java", "com", "winlator", "core",
                "WineGStreamerOutputFormatPatch.java");
            var wineGStreamerPatchSource = File.ReadAllText(wineGStreamerPatchPath);
            var gameSessionServicePath = Path.Combine(
                repositoryRoot, "TeknoParrotUi.Avalonia.Android", "GameSessionService.cs");
            var gameSessionServiceSource = File.ReadAllText(gameSessionServicePath);
            var manifestPath = Path.Combine(
                winlatorRoot,
                "app", "app", "src", "main", "AndroidManifest.xml");
            var manifestSource = File.ReadAllText(manifestPath);
            var bootstrapPath = Path.Combine(
                repositoryRoot, "Tools", "ProtonPipeHelper", "windows_path_bootstrap.c");
            var bootstrapSource = File.ReadAllText(bootstrapPath);
            var activityContractPath = Path.Combine(
                winlatorRoot,
                "app", "teknoparrot-bridge", "src", "main", "java", "com",
                "winlator", "teknoparrot", "ActivityLaunchContract.java");
            var activityContractSource = File.ReadAllText(activityContractPath);
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_PARKED_ENTRYPOINT = \"parked-entrypoint\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_PARKED_ENTRYPOINT.equals(value)",
                    StringComparison.Ordinal),
                "MKDX parked-entrypoint preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_WINED3D_PARKED_ENTRYPOINT =",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_WINED3D_PARKED_ENTRYPOINT.equals(value)",
                    StringComparison.Ordinal),
                "Luigi WineD3D parked-entrypoint preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_WINED3D_REMOTE_THREAD =",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_WINED3D_REMOTE_THREAD.equals(value)",
                    StringComparison.Ordinal),
                "WineD3D remote-thread preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_GAME_WORKING_DIRECTORY = \"game-working-directory\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_GAME_WORKING_DIRECTORY.equals(value)",
                    StringComparison.Ordinal),
                "game working-directory preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_BUILTIN_DDRAW = \"builtin-ddraw\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_BUILTIN_DDRAW.equals(value)",
                    StringComparison.Ordinal),
                "builtin DirectDraw preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_XACT_LOCAL_REGISTER = \"xact-local-register\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_XACT_LOCAL_REGISTER.equals(value)",
                    StringComparison.Ordinal),
                "local XACT registration preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_EADP_DUAL_IO = \"eadp-dual-io\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_EADP_DUAL_IO.equals(value)",
                    StringComparison.Ordinal),
                "EADP dual-I/O preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_SHARED_JVS_DUAL_IO = \"shared-jvs-dual-io\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_SHARED_JVS_DUAL_IO.equals(value)",
                    StringComparison.Ordinal),
                "shared-state plus JVS preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_GAIA_ATTACK4_MEDIA = \"gaia-attack4-media\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_GAIA_ATTACK4_MEDIA.equals(value)",
                    StringComparison.Ordinal),
                "Gaia Attack 4 media preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_KOF_XII_WINE_GSTREAMER =",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "\"kof-xii-wine-gstreamer\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_KOF_XII_WINE_GSTREAMER.equals(value)",
                    StringComparison.Ordinal),
                "KOF XII guarded Wine-GStreamer preset accepted by the Activity contract");
            True(source.Contains(
                    "!ensurePreparedWineGStreamerOutputFormat()",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"kof-xii-wine-gstreamer\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "drive_c/windows/syswow64/winegstreamer.dll",
                    StringComparison.Ordinal) &&
                wineGStreamerPatchSource.Contains(
                    "f35d717eaf5340260107dc38211a192c9fbf87fde53abc82e1ea2c123b4e8cf3",
                    StringComparison.Ordinal) &&
                wineGStreamerPatchSource.Contains(
                    "7, 13, 8, 12, 11, 14, 9",
                    StringComparison.Ordinal) &&
                wineGStreamerPatchSource.Contains(
                    "writeAt(prefixDll, FORMAT_ORDER_OFFSET, ORIGINAL_FORMAT_ORDER)",
                    StringComparison.Ordinal),
                "KOF XII Wine-GStreamer patch is exact-build, prefix-local, and restorable");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_MUSIC_GUNGUN_NATIVE_FULLSCREEN =",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "\"music-gungun-native-fullscreen\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_MUSIC_GUNGUN_NATIVE_FULLSCREEN.equals(value)",
                    StringComparison.Ordinal),
                "Music Gun Gun native-fullscreen preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_BATTLE_GEAR_4_ORIGINAL =",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "\"battle-gear-4-original\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_BATTLE_GEAR_4_ORIGINAL.equals(value)",
                    StringComparison.Ordinal),
                "original Battle Gear 4 preset accepted by the Activity contract");
            True(activityContractSource.Contains(
                    "COMPATIBILITY_PRESET_DIRECT_TOUCH_JVS = \"direct-touch-jvs\"",
                    StringComparison.Ordinal) &&
                activityContractSource.Contains(
                    "!COMPATIBILITY_PRESET_DIRECT_TOUCH_JVS.equals(value)",
                    StringComparison.Ordinal),
                "direct-touch plus JVS preset accepted by the Activity contract");
            True(source.Contains(
                    "dxwrapper = DXWrappers.WINED3D;", StringComparison.Ordinal) &&
                source.Contains(
                    "\"wined3d-parked-entrypoint\".equals(", StringComparison.Ordinal),
                "Luigi compatibility preset selects WineD3D and parked injection");
            True(source.Contains(
                    "\"wined3d-remote-thread\".equals(", StringComparison.Ordinal),
                "WineD3D can be selected without changing the x86 injection mode");
            True(source.Contains(
                    "boolean isKofXiiiLaunch = isKofXiiiPreparedLaunch();",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "if (isKofXiiiLaunch)",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"TP_KOFXIII_QUARTZ_NULL_GUARD\", \"1\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "normalized.contains(\"\\\\the king of fighters xiii (2010)\")",
                    StringComparison.Ordinal),
                "KOF XIII movie-continuation guard remains exact-title scoped");
            True(source.Contains(
                    "else if (isKofXiiiPreparedLaunch())",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "screenInfo = new ScreenInfo(\"1360x768\");",
                    StringComparison.Ordinal),
                "KOF XIII fits its untouched native window through a title-scoped guest desktop");
            True(source.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(
                    "if (isKofXiiiPreparedLaunch() || isKofXiiiClimaxPreparedLaunch())\n" +
                    "            return configuredPreset;",
                    StringComparison.Ordinal),
                "KOF XIII and Climax retain the movie-safe configured Box64 preset in production");
            True(source.Contains(
                    "normalizedExecutable.endsWith(\"\\\\openparrotloader64.exe\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "normalizedExecutable.contains(\"\\\\openparrotwin64\\\\\")",
                    StringComparison.Ordinal),
                "private TeknoParrot cores retain x64 bridge mode through the loader path");
            True(source.Contains(
                    "boolean usesUnsuffixedControlPipe = isJvsPipePair ||",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "preparedWindowsLaunch.compatibilityPreset);",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "String pipeName = usesUnsuffixedControlPipe ?",
                    StringComparison.Ordinal),
                "Initial D The Arcade exposes the unsuffixed desktop USB-I/O pipe");
            True(source.Contains(
                    "TeknoParrotBridgeLauncherComponent teknoParrotJvsBridgeLauncherComponent",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "pipe --name TeknoParrot_JVS", StringComparison.Ordinal) &&
                source.Contains(
                    "if (isInitialDTheArcade || isEadpDualIo || isSharedJvsDualIo)",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "environment.addComponent(teknoParrotJvsBridgeLauncherComponent)",
                    StringComparison.Ordinal),
                "Initial D The Arcade, EADP, and Wonderland launch their secondary desktop JVS pipe");
            True(gameSessionServiceSource.Contains(
                    "if (RequiresJvsBridge(record.InputProtocol))",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "var isAdditionalJvsPipe =", StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolJvsInitialD", StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolSharedEadp",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolSharedWonderlandWars",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolSharedTaitoGun",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "AndroidLaunchRecipe.InputProtocolSharedTaitoGun =>",
                    StringComparison.Ordinal) &&
                gameSessionServiceSource.Contains(
                    "sharedState.AsSpan(1)",
                    StringComparison.Ordinal),
                "hybrid shared-state profiles authenticate and serve secondary JVS channels");
            True(source.Contains(
                    "touchpadView.setDirectTouchMode(true);", StringComparison.Ordinal) &&
                source.Contains(
                    "\"shared-jvs-dual-io\".equals(", StringComparison.Ordinal) &&
                source.Contains(
                    "\"gaia-attack4-media\".equals(", StringComparison.Ordinal) &&
                source.Contains(
                    "\"music-gungun-native-fullscreen\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"direct-touch-jvs\".equals(", StringComparison.Ordinal),
                "Wonderland and Shining enable title-scoped absolute press-and-drag cabinet touch");
            True(source.Contains(
                    "boolean useNativeGuestFullscreen =", StringComparison.Ordinal) &&
                source.Contains(
                    "useNativeGuestFullscreen) ? \"0\" : \"1\";",
                    StringComparison.Ordinal),
                "Music Gun Gun and En-Eins request native guest fullscreen without transforming the Android surface");
            True(source.Contains(
                    "\"battle-gear-4-original\".equals(\n" +
                    "                preparedWindowsLaunch.compatibilityPreset))\n" +
                    "            return Box64Preset.PERFORMANCE;",
                    StringComparison.Ordinal),
                "original Battle Gear 4 keeps the required Box64 performance preset with diagnostics enabled");
            var setupStart = source.IndexOf(
                "private void setupXEnvironment()", StringComparison.Ordinal);
            var displayCall = source.IndexOf(
                "!ensurePreparedDisplayIni()", setupStart, StringComparison.Ordinal);
            var largeAddressAwareCall = source.IndexOf(
                "!ensurePreparedLargeAddressAwareExecutable()", setupStart,
                StringComparison.Ordinal);
            True(setupStart >= 0 && displayCall >= 0 && largeAddressAwareCall >= 0,
                "Winlator launch preparation calls found");
            True(displayCall < largeAddressAwareCall,
                "display INI applied before private LAA executable staging");
            True(source.Contains(
                    "boolean applyWackyNetwork = \"wacky-races-network\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "networkAdapterIp = new NetworkHelper(this).getIPv4Address();",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "result.add(\"Cab1IP=\" + networkAdapterIp);",
                    StringComparison.Ordinal),
                "Wacky Races binds DirectPlay to the Android adapter address");
            True(manifestSource.Contains(
                    "android.permission.CHANGE_WIFI_MULTICAST_STATE",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "wifiManager.createMulticastLock(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"TeknoParrotWmmtTerminal\"",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"wmmt-terminal\".equals(preparedWindowsLaunch.compatibilityPreset)",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "wmmtTerminalMulticastLock.release();",
                    StringComparison.Ordinal),
                "WMMT terminal session owns the Android Wi-Fi multicast lock");
            True(source.Contains(
                    "key.equalsIgnoreCase(\"Terminal Emu\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "key.equalsIgnoreCase(\"Terminal Mode\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "result.add(\"Terminal Emu=\" + terminalEmulatorValue);",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "result.add(\"Terminal Mode=0\");",
                    StringComparison.Ordinal),
                "WMMT launch preparation recognizes aliases and emits core-compatible terminal keys");
            True(source.Contains(
                    "envVars.put(\"TP_ANDROID_WMMT_TERMINAL_UNICAST\", \"127.0.0.1\");",
                    StringComparison.Ordinal),
                "WMMT terminal uses Wine guest loopback for local unicast delivery");
            True(!source.Contains(
                    "TP_ANDROID_WMMT_TERMINAL_UNICAST_PORT",
                    StringComparison.Ordinal),
                "WMMT terminal preserves the core protocol's UDP 50765 destination");
            True(source.Contains(
                    "envVars.put(\"TP_ANDROID_WMMT_TERMINAL_DIRECT_RECV\", \"1\");",
                    StringComparison.Ordinal),
                "WMMT terminal enables the Android-only direct receive fallback");
            var parkedEntryPointSelection = source.IndexOf(
                "else if (isPreparedBridge64Bit() ||", StringComparison.Ordinal);
            var wackyParkedSelection = parkedEntryPointSelection >= 0
                ? source.IndexOf(
                    "\"wacky-races-network\".equals(", parkedEntryPointSelection,
                    StringComparison.Ordinal)
                : -1;
            var wackyParkedDelay = wackyParkedSelection >= 0
                ? source.IndexOf(
                    "envVars.put(\"TP_ENTRYPOINT_REMOTETHREAD_MS\", \"3000\");",
                    wackyParkedSelection, StringComparison.Ordinal)
                : -1;
            var mkdxParkedSelection = wackyParkedSelection >= 0
                ? source.IndexOf(
                    "\"parked-entrypoint\".equals(", wackyParkedSelection,
                    StringComparison.Ordinal)
                : -1;
            var wackyManagedInit = wackyParkedDelay >= 0
                ? source.IndexOf(
                    "envVars.put(\"TP_LOADER_MANAGED_INIT\", \"1\");",
                    wackyParkedDelay, StringComparison.Ordinal)
                : -1;
            True(parkedEntryPointSelection >= 0 &&
                wackyParkedSelection > parkedEntryPointSelection &&
                mkdxParkedSelection > wackyParkedSelection &&
                wackyParkedDelay > mkdxParkedSelection &&
                wackyParkedDelay > wackyParkedSelection &&
                wackyManagedInit > wackyParkedDelay,
                "Wacky Races uses parked-entry-point x86 injection");
            var mkdxBigBlockGuard = source.IndexOf(
                "if (isMarioKartDxPreparedLaunch())", mkdxParkedSelection,
                StringComparison.Ordinal);
            var mkdxBigBlockDisable = mkdxBigBlockGuard >= 0
                ? source.IndexOf(
                    "envVars.put(\"BOX64_DYNAREC_BIGBLOCK\", \"0\");",
                    mkdxBigBlockGuard, StringComparison.Ordinal)
                : -1;
            True(mkdxBigBlockGuard > mkdxParkedSelection &&
                mkdxBigBlockDisable > mkdxBigBlockGuard &&
                source.Contains(
                    ".endsWith(\"\\\\mk_agp3_final.exe\");",
                    StringComparison.Ordinal),
                "Mario Kart DX disables Box64 joined blocks after invalid SEH unwind frames");
            True(source.Contains(
                    "\"wined3d-parked-entrypoint\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "if (isMarioKartDxLaunch)",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "dxwrapper = DXWrappers.DXVK;",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "DefaultVersion.INTERMEDIATE_DXVK",
                    StringComparison.Ordinal),
                "Mario Kart DX keeps its forced D3D10 path on the stable DXVK 2.3.1 renderer");
            True(source.Contains(
                    "\"d3d9=b;mscoree,mshtml=d\"",
                    StringComparison.Ordinal) &&
                !source.Contains(
                    "\"TP_MKDX_HIDDEN_MOVIE_DEVICE\"",
                    StringComparison.Ordinal),
                "Mario Kart DX keeps its normal WineD3D movie device beside the DXVK D3D10 renderer");
            var mkdxBigBlockEnd = source.IndexOf(
                "envVars.remove(\"BOX64_DYNAREC_VOLATILE_METADATA\");",
                mkdxBigBlockGuard, StringComparison.Ordinal);
            var mkdxPreparedBlock = mkdxBigBlockGuard >= 0 &&
                mkdxBigBlockEnd > mkdxBigBlockGuard
                    ? source.Substring(
                        mkdxBigBlockGuard,
                        mkdxBigBlockEnd - mkdxBigBlockGuard)
                    : string.Empty;
            True(!mkdxPreparedBlock.Contains(
                    "BOX64_DYNAREC_FASTNAN", StringComparison.Ordinal) &&
                !mkdxPreparedBlock.Contains(
                    "BOX64_DYNAREC_FASTROUND", StringComparison.Ordinal) &&
                !mkdxPreparedBlock.Contains(
                    "BOX64_DYNAREC_X87DOUBLE", StringComparison.Ordinal) &&
                !mkdxPreparedBlock.Contains(
                    "BOX64_SYNC_ROUNDING", StringComparison.Ordinal),
                "Mario Kart DX does not repeat the disproven Crazy Speed x87 override");
            True(source.Contains(
                    "!ensurePreparedMarioKartStackExecutable()",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "final long requiredStackReserve = 16L * 1024L * 1024L;",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "File stagedDirectory = new File(source.getParentFile(), \".teknoparrot-stack\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"TP_GAME_WORKING_DIRECTORY\", FileUtils.getDirname(gameExecutable));",
                    StringComparison.Ordinal) &&
                !bootstrapSource.Contains(
                    "SetCurrentDirectoryW(game_working_directory)",
                    StringComparison.Ordinal) &&
                bootstrapSource.Contains(
                    "GetFullPathNameW(argv[3]",
                    StringComparison.Ordinal) &&
                bootstrapSource.Contains(
                    "SetEnvironmentVariableW(L\"PATH\", game_combined_path)",
                    StringComparison.Ordinal),
                "Mario Kart DX patches an exact-name sixteen MiB stack copy while preserving loader context");
            True(source.Contains(
                    "\"game-working-directory\".equals(preparedWindowsLaunch.compatibilityPreset)",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"TP_GAME_WORKING_DIRECTORY\", workingDirectory);",
                    StringComparison.Ordinal),
                "opt-in recipes run executable-relative asset loaders from the game directory");
            var parkedInjectionBranch = source.IndexOf(
                "else if (isPreparedBridge64Bit()", StringComparison.Ordinal);
            True(parkedInjectionBranch >= 0 && source.IndexOf(
                    "\"game-working-directory\".equals(",
                    parkedInjectionBranch, StringComparison.Ordinal) > parkedInjectionBranch,
                "Super Bikes uses primary-thread parked OpenParrot initialization");
            True(source.Contains(
                    "topLevelWindow.getParent() != xServer.windowManager.rootWindow",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "!topLevelWindow.getParent().isDesktopWindow()",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "topLevelWindow = topLevelWindow.getParent();",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "topLevelWindow, position[0], position[1]",
                    StringComparison.Ordinal),
                "decorated Wine windows center inside the hidden desktop wrapper");
            True(source.Contains(
                    "preparedWindowsLaunch.controlsProfileId == 9008",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"TP_HIDE_WINDOW_MENU\", \"1\");",
                    StringComparison.Ordinal) &&
                bootstrapSource.Contains(
                    "environment_flag_enabled(L\"TP_HIDE_WINDOW_MENU\")",
                    StringComparison.Ordinal) &&
                bootstrapSource.Contains(
                    "menu_changed = SetMenu(window, NULL);",
                    StringComparison.Ordinal) &&
                bootstrapSource.Contains(
                    "if (!process_in_tree)",
                    StringComparison.Ordinal),
                "Battle Gear 4 menu policy never mutates Wine-reparented game windows");
            True(source.Contains(
                    "boolean preserveExecutableName = \"large-address-aware\".equals(preset) ||",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"dirty-driving-fullscreen\".equals(preset);",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "? new File(source.getParentFile(), \".teknoparrot-laa\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"TP_GAME_WORKING_DIRECTORY\"",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "FileUtils.getDirname(gameExecutable));",
                    StringComparison.Ordinal),
                "Dirty Drivin preserves its executable basename and original DLL search directory in private LAA staging");
            var workaroundPath = Path.Combine(
                winlatorRoot,
                "app", "app", "src", "main", "java", "com", "winlator",
                "core", "Win32AppWorkarounds.java");
            var workarounds = File.ReadAllText(workaroundPath);
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"media-wmv\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "wincomponents.put(\"directshow\", \"1\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "wincomponents.put(\"wmdecoder\", \"1\")",
                    StringComparison.Ordinal),
                "legacy 32-bit WMV preset installs native DirectShow decoders");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"wine-gstreamer\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "applyWineGStreamerWorkaround();",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "envVars.put(\"TP_BOX64_WINEGSTREAMER_FIX\", \"1\")",
                    StringComparison.Ordinal),
                "64-bit media preset uses the isolated Wine-GStreamer Box64 runtime");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"gaia-attack4-media\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "Gaia Attack 4 mixes WMV3 and Indeo 5 AVI files",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "applyWineGStreamerWorkaround();",
                    StringComparison.Ordinal),
                "Gaia Attack 4 routes mixed WMV3 and Indeo 5 movies through Wine-GStreamer");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"dirty-driving-fullscreen\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "envVars.put(\"BOX64_RESERVE_HIGH\", \"1\")",
                    StringComparison.Ordinal),
                "Dirty Drivin reserves unfragmented high 32-bit address space");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"wacky-races-network\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "wincomponents.put(\"directplay\", \"1\")",
                    StringComparison.Ordinal),
                "Wacky Races installs the title-scoped native DirectPlay runtime");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"builtin-ddraw\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "envVars.put(\"WINEDLLOVERRIDES\", \"ddraw=b\")",
                    StringComparison.Ordinal),
                "Dragon Dance bypasses its incompatible local DDrawCompat without mutating the dump");
            True(workarounds.Contains(
                    "compatibilityPreset.equals(\"xact-local-register\")",
                    StringComparison.Ordinal) &&
                workarounds.Contains(
                    "wincomponents.put(\"xaudio\", \"1\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "createLocalXactWinePreflight(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "wine C:\\\\windows\\\\syswow64\\\\regsvr32.exe /s ",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "C:\\\\teknoparrot-service\\\\",
                    StringComparison.Ordinal),
                "GTI Club installs and registers its exact local 32-bit XACT COM server");
            True(source.Contains(
                    "boolean multithreadedAlsaClients = preparedWindowsLaunch == null ||",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "!\"chase-hq2\".equals(preparedWindowsLaunch.compatibilityPreset);",
                    StringComparison.Ordinal),
                "Chase H.Q. 2 avoids Android 16 ALSA client thread churn");
            True(source.Contains(
                    "boolean applyTaikoCustomResolution = \"taiko-custom-resolution\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "boolean usesSpacedResolutionKeys = applyTaikoCustomResolution;",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "result.add(\"Custom Resolution (Stretches)=1\");",
                    StringComparison.Ordinal),
                "Taiko custom resolution activates OpenParrot's spaced INI fields");
            True(source.Contains(
                    "String normalizedProfileConfig = preparedWindowsLaunch.profileConfigIni",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "Could not prepare the complete TeknoParrot.ini profile.",
                    StringComparison.Ordinal),
                "complete profile INI seeds Winlator launch preparation");
            var cleanupCall = source.IndexOf(
                "ProcessHelper.killGuestProcesses();",
                setupStart, StringComparison.Ordinal);
            var environmentStart = source.IndexOf(
                "environment.startEnvironmentComponents();",
                setupStart, StringComparison.Ordinal);
            True(cleanupCall >= 0 && environmentStart >= 0 && cleanupCall < environmentStart,
                "stale same-UID Wine guests cleaned before a prepared launch");
            True(source.Contains(
                    "xServerView.setPreserveEGLContextOnPause(false);",
                    StringComparison.Ordinal),
                "prepared Android sessions release the retained EGL context during teardown");
            True(source.Contains(
                    "envVars.put(\"TP_STARWARS_JVS_POLL_MS\", \"4\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"BOX64_DYNAREC_CALLRET\", \"0\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"BOX64_DYNAREC_STRONGMEM\", \"1\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"BOX64_DYNAREC_BIGBLOCK\", \"2\");",
                    StringComparison.Ordinal),
                "Star Wars Android launcher requests fast guarded JVS translation");
            var cxbxrTransitionGuard = source.IndexOf(
                "else if (isPreparedCxbxrPerformanceTitle())",
                StringComparison.Ordinal);
            var cxbxrTransitionEnd = cxbxrTransitionGuard >= 0
                ? source.IndexOf(
                    "else {",
                    cxbxrTransitionGuard,
                    StringComparison.Ordinal)
                : -1;
            var cxbxrCallretGuard = cxbxrTransitionGuard >= 0
                ? source.IndexOf(
                    "envVars.put(\"BOX64_DYNAREC_CALLRET\", \"0\");",
                    cxbxrTransitionGuard,
                    StringComparison.Ordinal)
                : -1;
            var cxbxrStrongmemGuard = cxbxrTransitionGuard >= 0
                ? source.IndexOf(
                    "envVars.put(\"BOX64_DYNAREC_STRONGMEM\", \"1\");",
                    cxbxrTransitionGuard,
                    StringComparison.Ordinal)
                : -1;
            True(
                cxbxrTransitionGuard >= 0 &&
                cxbxrCallretGuard > cxbxrTransitionGuard &&
                cxbxrStrongmemGuard > cxbxrTransitionGuard &&
                cxbxrTransitionEnd > cxbxrCallretGuard &&
                cxbxrTransitionEnd > cxbxrStrongmemGuard,
                "CXBXR VC3/GS performance path guards call prediction and memory ordering");
            True(source.Contains(
                    "\"LoggedModules = 0x00007000\\n\" +",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"LoggedModules = 0x00000780\\n\" +",
                    StringComparison.Ordinal) &&
                source.Contains(
                    ": \"LoggedModules = 0x0\"",
                    StringComparison.Ordinal),
                "CXBXR per-game diagnostics enable focused modules and reset production logging");
            True(source.Contains(
                    "isPreparedCxbxrVirtuaCop3Title()",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"cooperative_self_suspend\",",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "virtuaCop3 ? \"1\" : \"0\"",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"ff_nv2a_blend_matrices\",",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "virtuaCop3 ? \"0\" : \"1\"",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"scheduler_io_trace\", \"0\"",
                    StringComparison.Ordinal),
                "CXBXR VC3 uses scoped suspension and skinning fixes without production trace overhead");
            True(source.Contains(
                    "\"box64-interpreter\".equals(",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "isPreparedCxbxrPulseAudioTitle()))",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "envVars.put(\"BOX64_DYNAREC\", \"0\");",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "audioDriver = AudioDrivers.PULSEAUDIO;",
                    StringComparison.Ordinal),
                "Homura and HOD3 use scoped PulseAudio stability guards");
            True(source.Contains(
                    "preparedWindowsLaunch.resolutionWidth + \"x\" +",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "preparedWindowsLaunch.resolutionHeight);",
                    StringComparison.Ordinal),
                "managed recipe resolution overrides the Winlator container desktop");
            var presetSelection = source.IndexOf(
                "private String selectBox64Preset()", StringComparison.Ordinal);
            var starWarsPreset = source.IndexOf(
                "\"star-wars\".equals(preparedWindowsLaunch.compatibilityPreset)",
                presetSelection, StringComparison.Ordinal);
            var starWarsPerformance = source.IndexOf(
                "return Box64Preset.PERFORMANCE;",
                starWarsPreset, StringComparison.Ordinal);
            True(presetSelection >= 0 && starWarsPreset >= 0 &&
                starWarsPerformance > starWarsPreset,
                "Star Wars selects the guarded Box64 performance preset");
            var bigBuckWorldGuard = source.IndexOf(
                "if (isBigBuckWorldPreparedLaunch())",
                presetSelection, StringComparison.Ordinal);
            var elfLoaderPerformance = source.IndexOf(
                "isPreparedElfLoaderLaunch() &&",
                bigBuckWorldGuard, StringComparison.Ordinal);
            True(bigBuckWorldGuard >= 0 && elfLoaderPerformance > bigBuckWorldGuard,
                "Big Buck Hunter World keeps the stable Box64 preset before the generic ElfLoader fast path");
            True(source.Contains(
                    "normalized.contains(\"\\\\big buck world \")",
                    StringComparison.Ordinal),
                "Big Buck Hunter World preset guard is title-scoped instead of controls-profile scoped");
            True(!source.Contains(
                    "applyPreparedTitleScopedCoreSelection();",
                    StringComparison.Ordinal) &&
                !source.Contains(
                    "\\\\OpenParrotWin32\\\\OpenParrotEADP",
                    StringComparison.Ordinal) &&
                !source.Contains(
                    "\\\\OpenParrotWin32\\\\OpenParrotDirty",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "preparedWineDebug += \",+loaddll\";",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "preparedWineDebug += \",+reg\";",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "installPreparedEadpPhysxRuntime(systemRegFile);",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"PhysXCore Path\", engineDosPath",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "new File(engineRoot, \"v2.8.0\")",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "registryRoot + \"\\\\PhysX_A32_Engines\", \"2.8.0\", 0x36",
                    StringComparison.Ordinal) &&
                source.Contains(
                    "\"HwSelection\", \"CPU\"",
                    StringComparison.Ordinal),
                "shared OpenParrot core retains EADP's registered PhysX 2.8.0 diagnostics");
            var tekkenConfiguredPreset = source.IndexOf(
                ".endsWith(\"\\\\tekkengame-win64-shipping.exe\")",
                presetSelection, StringComparison.Ordinal);
            var genericOpenParrotPerformance = source.IndexOf(
                "String runtimeArgument = preparedWindowsLaunch.arguments[0];",
                presetSelection, StringComparison.Ordinal);
            True(tekkenConfiguredPreset >= 0 &&
                genericOpenParrotPerformance > tekkenConfiguredPreset,
                "Tekken 7 keeps the compatible configured Box64 preset before the generic OpenParrot fast path");

            var processHelperPath = Path.Combine(
                winlatorRoot,
                "app", "app", "src", "main", "java", "com", "winlator",
                "core", "ProcessHelper.java");
            var processHelper = File.ReadAllText(processHelperPath);
            True(processHelper.Contains(
                    "// Reaching this path already requires an explicitly enabled",
                    StringComparison.Ordinal) &&
                processHelper.Contains(
                    "Log.d(TAG, line);",
                    StringComparison.Ordinal),
                "explicit per-game diagnostics reach logcat in release APKs");
            True(processHelper.Contains(
                    "Os.stat(processPath).st_uid == Os.getuid()",
                    StringComparison.Ordinal) &&
                processHelper.Contains(
                    "normalizedCommandLine.contains(\"/rootfs/opt/wine/\")",
                    StringComparison.Ordinal) &&
                processHelper.Contains(
                    "pstat.pid > parentPID ||",
                    StringComparison.Ordinal) &&
                processHelper.Contains(
                    "pstat.guestProcess)",
                    StringComparison.Ordinal),
                "orphan cleanup uses full Wine command line and application UID");

            var inputBridgePath = Path.Combine(
                winlatorRoot,
                "app", "teknoparrot-bridge", "src", "main", "java",
                "com", "winlator", "teknoparrot", "ForwardedInputActivityBridge.java");
            var inputBridge = File.ReadAllText(inputBridgePath);
            True(inputBridge.Contains(
                    "{MotionEvent.AXIS_LTRIGGER, MotionEvent.AXIS_BRAKE}",
                    StringComparison.Ordinal) &&
                inputBridge.Contains(
                    "{MotionEvent.AXIS_RTRIGGER, MotionEvent.AXIS_GAS}",
                    StringComparison.Ordinal) &&
                inputBridge.Contains(
                    "(candidateValue - candidate.getMin()) / span",
                    StringComparison.Ordinal),
                "physical controller trigger aliases feed canonical pedal axes");

            var sessionServicePath = Path.Combine(
                repositoryRoot,
                "TeknoParrotUi.Avalonia.Android", "GameSessionService.cs");
            var sessionService = File.ReadAllText(sessionServicePath);
            True(sessionService.Contains(
                    "HealthSampleInterval = TimeSpan.FromSeconds(5)",
                    StringComparison.Ordinal) &&
                sessionService.Contains(
                    "Startup is the most allocation-heavy part of Wine/Box64/DXVK.",
                    StringComparison.Ordinal) &&
                sessionService.Contains(
                    "public bool IsCritical => LowMemory || ThermalStatus >= 5;",
                    StringComparison.Ordinal) &&
                sessionService.Contains(
                    "public bool ShouldWarn => HasLowMemoryHeadroom || ThermalStatus >= 3;",
                    StringComparison.Ordinal) &&
                sessionService.Contains(
                    "AvailableMiB <= Math.Max(512, ThresholdMiB * 2);",
                    StringComparison.Ordinal) &&
                sessionService.Contains(
                    "never stop a game using stale resource telemetry.",
                    StringComparison.Ordinal) &&
                !sessionService.Contains(
                    "hasConnected ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(45)",
                    StringComparison.Ordinal),
                "Android launch and gameplay resource pressure sampled every five seconds");

            var regressionRunnerPath = Path.Combine(
                repositoryRoot, "Tools", "Run-AndroidGameRegression.ps1");
            var regressionRunner = File.ReadAllText(regressionRunnerPath);
            True(regressionRunner.Contains(
                    "function Stop-GameSession",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "com.teknoparrot.ui.action.ADB_STOP_GAME_SESSION",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "protected ADB control receiver",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "function Wait-ForLibraryHierarchy",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "The TeknoParrot notification Stop action was not found.",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "partly clipped notification action",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "scrolling every time can move a top row off-screen",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "Stop-GameSession",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "function ConvertTo-AdbInputText",
                    StringComparison.Ordinal) &&
                regressionRunner.Contains(
                    "$encodedSearch = ConvertTo-AdbInputText $SearchText",
                    StringComparison.Ordinal) &&
                !regressionRunner.Contains(
                    "@('shell', 'am', 'force-stop', 'com.teknoparrot.winlator') | Out-Null\n}",
                    StringComparison.Ordinal),
                "Android game regression uses shell-safe search and protected managed-session teardown");

            var importRunner = File.ReadAllText(Path.Combine(
                repositoryRoot, "Tools", "Import-AndroidLaunchableGames.ps1"));
            True(importRunner.Contains(
                    "Scan Launchable Games",
                    StringComparison.Ordinal) &&
                importRunner.Contains(
                    "Import Found Games",
                    StringComparison.Ordinal) &&
                importRunner.Contains(
                    "Game Scanner could not be positioned above the fixed launch controls.",
                    StringComparison.Ordinal) &&
                importRunner.Contains(
                    "Done — added",
                    StringComparison.Ordinal),
                "Android managed import runner scans and imports without launch-control overlap");

            var adbControlReceiverPath = Path.Combine(
                repositoryRoot,
                "TeknoParrotUi.Avalonia.Android",
                "AdbGameSessionControlReceiver.cs");
            var adbControlReceiver = File.ReadAllText(adbControlReceiverPath);
            True(adbControlReceiver.Contains(
                    "Permission = \"android.permission.DUMP\"",
                    StringComparison.Ordinal) &&
                adbControlReceiver.Contains(
                    "Exported = true",
                    StringComparison.Ordinal) &&
                adbControlReceiver.Contains(
                    "GameSessionService.StopAction",
                    StringComparison.Ordinal),
                "ADB session control receiver is shell-only and stops the managed service");
        }

        private static void VerifyAndroidPhysicalScannerFallback(string recipeDirectory)
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(recipeDirectory, "..", ".."));
            var scannerPath = Path.Combine(
                repositoryRoot, "TeknoParrotUi.Avalonia", "Views", "GameScannerView.axaml.cs");
            var source = File.ReadAllText(scannerPath);
            True(source.Contains("Directory.GetDirectories(rootPhysicalPath)",
                    StringComparison.Ordinal),
                "Android scanner enumerates physical child folders when SAF is stale");
            True(source.Contains("CapturePhysicalAndroidCandidateFiles(",
                    StringComparison.Ordinal),
                "Android scanner captures executable candidates from physical storage");
        }

        private static void VerifyAndroidDocumentPathResolution()
        {
            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/document/" +
                    "primary%3ADownload%2FTeknoParrotGames%2FSR3%2FRally.exe",
                    out var primaryPath),
                "Android primary-storage document URI resolves");
            Equal(
                "/storage/emulated/0/Download/TeknoParrotGames/SR3/Rally.exe",
                primaryPath,
                "Android primary-storage document path");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/document/" +
                    "primary%3AArcade%2FFighters%2Fgame.exe",
                    out var arbitraryPrimaryPath),
                "Android arbitrary primary-storage game URI resolves");
            var arbitraryPrimaryLocation = AndroidWinlatorGamePath.Resolve(
                arbitraryPrimaryPath,
                "/storage/emulated/0/Download");
            Equal(@"I:\game.exe", arbitraryPrimaryLocation.DosPath,
                "Android Browse selection maps to the exact-folder I drive");
            Equal("/storage/emulated/0/Arcade/Fighters",
                arbitraryPrimaryLocation.ScopedGameDirectory,
                "Android Browse selection preserves the chosen executable folder");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/document/" +
                    "primary%3AAndroid%2Fdata%2Fcom.teknogods.rpcs3x6%2Ffiles%2F" +
                    "TeknoParrot%2Farcade%2FRazingStorm%2Fdev_hdd0%2Fgame%2F" +
                    "SCEEXE000%2FUSRDIR%2FEBOOT.BIN",
                    out var razingStormEboot),
                "Android RPCS3X6 EBOOT document URI resolves");
            Equal(
                "/storage/emulated/0/Android/data/com.teknogods.rpcs3x6/files/" +
                "TeknoParrot/arcade/RazingStorm/dev_hdd0/game/SCEEXE000/" +
                "USRDIR/EBOOT.BIN",
                razingStormEboot,
                "Android Razing Storm EBOOT document path");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.teknogods.rpcs3x6.documents/document/" +
                    "root%2FTeknoParrot%2Farcade%2FRazingStorm%2Fdev_hdd0%2F" +
                    "game%2FSCEEXE000%2FUSRDIR%2FEBOOT.BIN",
                    out var rpcs3x6ProviderEboot),
                "RPCS3X6 provider EBOOT document URI resolves");
            Equal(
                "/storage/emulated/0/Android/data/com.teknogods.rpcs3x6/files/" +
                "TeknoParrot/arcade/RazingStorm/dev_hdd0/game/SCEEXE000/" +
                "USRDIR/EBOOT.BIN",
                rpcs3x6ProviderEboot,
                "RPCS3X6 provider Razing Storm EBOOT path");
            False(AndroidDocumentPathResolver.TryResolve(
                    "content://com.teknogods.rpcs3x6.documents/document/" +
                    "root%2F..%2Foutside%2FEBOOT.BIN",
                    out _),
                "RPCS3X6 provider traversal is rejected");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/document/" +
                    "1234-ABCD%3AArcade%2Fgame.exe",
                    out var removablePath),
                "Android removable-storage document URI resolves");
            Equal(
                "/storage/1234-ABCD/Arcade/game.exe",
                removablePath,
                "Android removable-storage document path");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/tree/" +
                    "1234-ABCD%3ATeknoParrotGames",
                    out var removableTreePath),
                "Android removable-storage tree URI resolves");
            Equal(
                "/storage/1234-ABCD/TeknoParrotGames",
                removableTreePath,
                "Android removable-storage tree path");

            True(AndroidDocumentPathResolver.TryResolve(
                    "content://vendor.documents/root/storage/emulated/0/" +
                    "Download/Arcade/game.exe",
                    out var vendorPath),
                "Android vendor document URI with physical path resolves");
            Equal(
                "/storage/emulated/0/Download/Arcade/game.exe",
                vendorPath,
                "Android vendor document path");

            False(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.externalstorage.documents/document/" +
                    "primary%3ADownload%2F..%2Fsecret.exe",
                    out _),
                "Android document traversal is rejected");
            False(AndroidDocumentPathResolver.TryResolve(
                    "content://com.android.providers.downloads.documents/" +
                    "document/msf%3A123",
                    out _),
                "opaque Android download-provider URI is rejected");
        }

        private static string FindRecipeDirectory()
        {
            var directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory, "TeknoParrotUi.Common", AndroidLaunchRecipeCatalog.DirectoryName);
                if (Directory.Exists(candidate))
                    return candidate;
                directory = Path.GetDirectoryName(directory);
            }
            throw new DirectoryNotFoundException("AndroidLaunchRecipes directory was not found.");
        }

        private static string FindWinlatorRoot(string repositoryRoot)
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("TEKNOPARROT_WINLATOR_SOURCE"),
                Path.Combine(
                    Directory.GetParent(repositoryRoot)?.FullName ?? repositoryRoot,
                    "winlator"),
                Path.Combine(repositoryRoot, "WinlatorFork")
            };
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    File.Exists(Path.Combine(
                        candidate, "app", "app", "build.gradle")))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            throw new DirectoryNotFoundException(
                "The standalone TeknoParrot Winlator checkout was not found.");
        }

        private static void True(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name + " was false");
        }

        private static void False(bool value, string name) => True(!value, name);

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    $"{name}: expected '{expected}', got '{actual}'");
        }

        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(name + " did not throw " + typeof(T).Name);
        }
    }
}
