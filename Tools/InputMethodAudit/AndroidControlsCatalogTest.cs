using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TeknoParrotUi.Common.Android;

namespace InputMethodAudit
{
    internal static class AndroidControlsCatalogTest
    {
        private static readonly IReadOnlyDictionary<int, string> GameplayBindings =
            new Dictionary<int, string>
            {
                [1] = "GAMEPAD_BUTTON_A",
                [2] = "GAMEPAD_BUTTON_B",
                [3] = "GAMEPAD_BUTTON_X",
                [4] = "GAMEPAD_BUTTON_Y",
                [5] = "GAMEPAD_BUTTON_L1",
                [6] = "GAMEPAD_BUTTON_R1"
            };

        private static readonly HashSet<string> DrivingProtocols = new(StringComparer.Ordinal)
        {
            AndroidLaunchRecipe.InputProtocolSegaRally,
            AndroidLaunchRecipe.InputProtocolJvsBattleGear,
            AndroidLaunchRecipe.InputProtocolJvsChaseHq2,
            AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit,
            AndroidLaunchRecipe.InputProtocolJvsWackyRaces,
            AndroidLaunchRecipe.InputProtocolJvsWmmt,
            AndroidLaunchRecipe.InputProtocolJvsMkdx,
            AndroidLaunchRecipe.InputProtocolJvsInitialD,
            AndroidLaunchRecipe.InputProtocolJvsSegaRacingClassic,
            AndroidLaunchRecipe.InputProtocolJvsSegaSonic,
            AndroidLaunchRecipe.InputProtocolAllsIdta,
            AndroidLaunchRecipe.InputProtocolSharedRawThrills,
            AndroidLaunchRecipe.InputProtocolSharedRawThrillsSuperBikes,
            AndroidLaunchRecipe.InputProtocolSharedRawThrillsH2O,
            AndroidLaunchRecipe.InputProtocolSharedDeadHeat,
            AndroidLaunchRecipe.InputProtocolSharedFrenzyExpress,
            AndroidLaunchRecipe.InputProtocolSharedGrid,
            AndroidLaunchRecipe.InputProtocolSharedGtiClub3,
            AndroidLaunchRecipe.InputProtocolSharedGaelco,
            AndroidLaunchRecipe.InputProtocolSharedCxbxrDriving,
            AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun,
            AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt
        };

        private static readonly HashSet<string> FlightProtocols = new(StringComparer.Ordinal)
        {
            AndroidLaunchRecipe.InputProtocolJvsMachStorm
        };

        private static readonly HashSet<string> GunProtocols = new(StringComparer.Ordinal)
        {
            AndroidLaunchRecipe.InputProtocolJvsSegaDreamRaiders,
            AndroidLaunchRecipe.InputProtocolJvsSegaGoldenGun,
            AndroidLaunchRecipe.InputProtocolJvsSegaLetsGoIsland,
            AndroidLaunchRecipe.InputProtocolSharedCxbxrGun
        };

        private static readonly HashSet<int> ExactArcadeControlIds = new()
        {
            9002, 9003, 9004, 9005, 9013, 9014, 9015, 9016, 9020, 9021, 9029,
            9030, 9031, 9033,
            9041, 9042, 9043
        };

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
            ExactSemanticBindings =
                new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
                {
                    ["SR3"] = Labels(
                        ("GAMEPAD_BUTTON_A", "SHIFT +"),
                        ("GAMEPAD_BUTTON_B", "SHIFT -"),
                        ("GAMEPAD_BUTTON_X", "HANDBRAKE"),
                        ("GAMEPAD_BUTTON_Y", "VIEW")),
                    ["BBHWorld"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "RELOAD")),
                    ["GhostBusters"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "RELOAD")),
                    ["WalkingDead"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "RELOAD")),
                    ["CrossfirePaintball"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "RELOAD")),
                    ["BBHHome"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_X", "PUMP RELOAD")),
                    ["BBHPro"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_X", "PUMP RELOAD")),
                    ["TargetTerrorGold"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_X", "RELOAD")),
                    ["JurassicPark"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "GRENADE"),
                        ("GAMEPAD_BUTTON_X", "MENU UP"),
                        ("GAMEPAD_BUTTON_Y", "MENU DOWN")),
                    ["Terminator"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "GRENADE"),
                        ("GAMEPAD_BUTTON_X", "RELOAD")),
                    ["AngryBirds"] = Labels(
                        ("GAMEPAD_BUTTON_A", "BALL TRAY"),
                        ("GAMEPAD_BUTTON_B", "PLUNGER"),
                        ("GAMEPAD_BUTTON_L1", "VOLUME +"),
                        ("GAMEPAD_BUTTON_R1", "VOLUME -")),
                    ["CrazySpeed"] = Labels(
                        ("GAMEPAD_BUTTON_A", "VIEW"),
                        ("GAMEPAD_BUTTON_B", "SHIFT +"),
                        ("GAMEPAD_BUTTON_X", "SHIFT -")),
                    ["GtiClub3"] = Labels(
                        ("GAMEPAD_BUTTON_A", "ACTION"),
                        ("GAMEPAD_BUTTON_L1", "SHIFT +"),
                        ("GAMEPAD_BUTTON_R1", "SHIFT -")),
                    ["VirtuaRLimit"] = Labels(
                        ("GAMEPAD_BUTTON_A", "NITRO"),
                        ("GAMEPAD_BUTTON_B", "VIEW"),
                        ("GAMEPAD_BUTTON_X", "SIDE BRAKE"),
                        ("GAMEPAD_BUTTON_Y", "SHIFT +"),
                        ("GAMEPAD_BUTTON_L1", "SHIFT -")),
                    ["EADP"] = Labels(
                        ("GAMEPAD_BUTTON_A", "GUN BUTTON"),
                        ("GAMEPAD_BUTTON_B", "SELECT"),
                        ("GAMEPAD_BUTTON_X", "TRIGGER"),
                        ("GAMEPAD_BUTTON_L1", "VOLUME +"),
                        ("GAMEPAD_BUTTON_R1", "VOLUME -"),
                        ("GAMEPAD_BUTTON_START", "ENTER")),
                    ["Friction"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "RELOAD"),
                        ("GAMEPAD_BUTTON_X", "MENU SELECT")),
                    ["GaiaAttack4"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_L1", "VOLUME +"),
                        ("GAMEPAD_BUTTON_R1", "VOLUME -")),
                    ["MusicGunGun2"] = Labels(
                        ("GAMEPAD_BUTTON_A", "TRIGGER"),
                        ("GAMEPAD_BUTTON_B", "SELECT"),
                        ("GAMEPAD_BUTTON_X", "GUN BUTTON"),
                        ("GAMEPAD_BUTTON_Y", "ENTER"),
                        ("GAMEPAD_BUTTON_L1", "VOLUME +"),
                        ("GAMEPAD_BUTTON_R1", "VOLUME -")),
                    ["IDTAS5"] = Labels(
                        ("GAMEPAD_BUTTON_A", "VIEW"),
                        ("GAMEPAD_BUTTON_B", "SHIFT +"),
                        ("GAMEPAD_BUTTON_X", "SHIFT -"),
                        ("GAMEPAD_BUTTON_Y", "AIME CARD")),
                    ["Theatrhythm"] = Labels(
                        ("GAMEPAD_BUTTON_A", "RIGHT STICK"),
                        ("GAMEPAD_BUTTON_B", "RIGHT STICK"),
                        ("GAMEPAD_BUTTON_X", "RIGHT STICK"),
                        ("GAMEPAD_BUTTON_Y", "RIGHT STICK"),
                        ("GAMEPAD_BUTTON_L1", "RIGHT BUTTON"),
                        ("GAMEPAD_BUTTON_L2", "SELECT"),
                        ("GAMEPAD_BUTTON_R2", "LEFT BUTTON"))
                };

        private static readonly IReadOnlyDictionary<string, HashSet<string>> ProtocolsByEmulationProfile =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["APM3"] = Set(AndroidLaunchRecipe.InputProtocolApm3),
                ["APM3Direct"] = Set(AndroidLaunchRecipe.InputProtocolApm3),
                ["ALLSIDTA"] = Set(AndroidLaunchRecipe.InputProtocolAllsIdta),
                ["cxbxr"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrDriving,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrGun,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrOllie,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrGundam,
                    AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf),
                ["ChaseHq2"] = Set(AndroidLaunchRecipe.InputProtocolJvsChaseHq2),
                ["EADP"] = Set(AndroidLaunchRecipe.InputProtocolSharedEadp),
                ["WonderlandWars"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedWonderlandWars),
                ["EuropaRSegaRally3"] = Set(AndroidLaunchRecipe.InputProtocolSegaRally),
                ["ExBoard"] = Set(AndroidLaunchRecipe.InputProtocolSharedExBoard),
                ["FastIo"] = Set(AndroidLaunchRecipe.InputProtocolFastIo),
                ["FrenzyExpress"] = Set(AndroidLaunchRecipe.InputProtocolSharedFrenzyExpress),
                ["Friction"] = Set(AndroidLaunchRecipe.InputProtocolSharedFriction),
                ["GaiaAttack4"] = Set(AndroidLaunchRecipe.InputProtocolSharedTaitoGun),
                ["GHA"] = Set(AndroidLaunchRecipe.InputProtocolSharedGha),
                ["GRID"] = Set(AndroidLaunchRecipe.InputProtocolSharedGrid),
                ["GtiClub3"] = Set(AndroidLaunchRecipe.InputProtocolSharedGtiClub3),
                ["GuiltyGearAPM3"] = Set(AndroidLaunchRecipe.InputProtocolApm3),
                ["HauntedMuseum"] = Set(AndroidLaunchRecipe.InputProtocolSharedTaitoGun),
                ["HauntedMuseum2"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedTaitoGunHauntedMuseum2),
                ["MusicGunGun2"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedTaitoGunMusic),
                ["NamcoMachStorm"] = Set(AndroidLaunchRecipe.InputProtocolJvsMachStorm),
                ["NamcoMkdx"] = Set(AndroidLaunchRecipe.InputProtocolJvsMkdx),
                ["NamcoMkdxUsa"] = Set(AndroidLaunchRecipe.InputProtocolJvsMkdx),
                ["NamcoWmmt3"] = Set(AndroidLaunchRecipe.InputProtocolJvsWmmt),
                ["NamcoWmmt6RR"] = Set(AndroidLaunchRecipe.InputProtocolJvsWmmt),
                ["NamcoWmmt5"] = Set(AndroidLaunchRecipe.InputProtocolJvsWmmt),
                ["LuigisMansion"] = Set(AndroidLaunchRecipe.InputProtocolSharedLuigiMansion),
                ["RadikalBikers"] = Set(AndroidLaunchRecipe.InputProtocolSharedGaelco),
                ["RawThrillsFNF"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedRawThrills,
                    AndroidLaunchRecipe.InputProtocolSharedRawThrillsSuperBikes,
                    AndroidLaunchRecipe.InputProtocolSharedJusticeLeague),
                ["RawThrillsFNFH2O"] = Set(AndroidLaunchRecipe.InputProtocolSharedRawThrillsH2O),
                ["RawThrillsGUN"] = Set(
                    AndroidLaunchRecipe.InputProtocolSharedRawThrillsGun,
                    AndroidLaunchRecipe.InputProtocolSharedRawThrillsGoGoStrike),
                ["WartranTroopers"] = Set(AndroidLaunchRecipe.InputProtocolSharedWartran),
                ["DeadHeat"] = Set(AndroidLaunchRecipe.InputProtocolSharedDeadHeat),
                ["SegaJvs"] = Set(AndroidLaunchRecipe.InputProtocolJvs),
                ["SegaInitialD"] = Set(AndroidLaunchRecipe.InputProtocolJvsInitialD),
                ["SegaRacingClassic"] = Set(AndroidLaunchRecipe.InputProtocolJvsSegaRacingClassic),
                ["SegaSonicAllStarsRacing"] = Set(AndroidLaunchRecipe.InputProtocolJvsSegaSonic),
                ["SegaJvsDreamRaiders"] = Set(AndroidLaunchRecipe.InputProtocolJvsSegaDreamRaiders),
                ["SegaJvsGoldenGun"] = Set(AndroidLaunchRecipe.InputProtocolJvsSegaGoldenGun),
                ["SegaJvsLetsGoIsland"] = Set(AndroidLaunchRecipe.InputProtocolJvsSegaLetsGoIsland),
                ["ShiningForceCrossRaid"] = Set(AndroidLaunchRecipe.InputProtocolJvs),
                ["Taiko"] = Set(AndroidLaunchRecipe.InputProtocolSharedTaiko),
                ["TaitoTypeXBattleGear"] = Set(AndroidLaunchRecipe.InputProtocolJvsBattleGear),
                ["TaitoTypeXGeneric"] = Set(AndroidLaunchRecipe.InputProtocolJvs),
                ["Theatrhythm"] = Set(AndroidLaunchRecipe.InputProtocolFastIoTheatrhythm),
                ["VirtuaRLimit"] = Set(AndroidLaunchRecipe.InputProtocolJvsVirtuaRLimit),
                ["WackyRaces"] = Set(AndroidLaunchRecipe.InputProtocolJvsWackyRaces)
            };

        public static int Run()
        {
            try
            {
                var root = FindRepositoryRoot();
                var recipeDirectory = Path.Combine(
                    root, "TeknoParrotUi.Common", AndroidLaunchRecipeCatalog.DirectoryName);
                var profileDirectory = Path.Combine(
                    root, "TeknoParrotUi.Common", "GameProfiles");
                var controlsDirectory = FindControlsDirectory(root);

                var recipes = AndroidLaunchRecipeCatalog.LoadAll(recipeDirectory);
                var controls = LoadTeknoParrotControls(controlsDirectory);
                if (controls.Count < 12)
                    throw new InvalidOperationException(
                        $"TeknoParrot control-profile count regressed to {controls.Count}.");
                ValidateForwarderCoverage(root, controls.Values);

                foreach (var recipe in recipes)
                {
                    if (!controls.TryGetValue(recipe.ControlsProfileId, out var control))
                        throw new InvalidOperationException(
                            $"{recipe.ProfileName} references missing controls-{recipe.ControlsProfileId}.icp.");

                    RequireBinding(control, "GAMEPAD_BUTTON_SELECT", recipe.ProfileName, "coin");
                    RequireBinding(control, "GAMEPAD_BUTTON_START", recipe.ProfileName, "start");
                    // Raw Thrills exposes cabinet test/service as P1 buttons 3/4,
                    // not keyboard F1/F2. Their exact layouts intentionally send
                    // the shared-state bits OpenParrot consumes.
                    if (control.Id is not 9030 and not 9031)
                    {
                        RequireBinding(control, "KEY_F1", recipe.ProfileName, "test");
                        RequireBinding(control, "KEY_F2", recipe.ProfileName, "service");
                    }

                    if (DrivingProtocols.Contains(recipe.InputProtocol))
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "steer left");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "steer right");
                        RequireBinding(control, "GAMEPAD_BUTTON_L2", recipe.ProfileName, "brake");
                        RequireBinding(control, "GAMEPAD_BUTTON_R2", recipe.ProfileName, "accelerator");
                        if (recipe.InputProtocol ==
                            AndroidLaunchRecipe.InputProtocolJvsBattleGear)
                            RequireBinding(control, "KEY_RIGHT", recipe.ProfileName, "cabinet key");
                    }
                    else if (FlightProtocols.Contains(recipe.InputProtocol))
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName, "aim up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "aim right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName, "aim down");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "aim left");
                        RequireBinding(control, "GAMEPAD_BUTTON_L2", recipe.ProfileName, "throttle brake");
                        RequireBinding(control, "GAMEPAD_BUTTON_R2", recipe.ProfileName, "throttle");
                    }
                    else if (GunProtocols.Contains(recipe.InputProtocol))
                    {
                        // The game surface itself is the absolute light-gun
                        // field. Keep virtual sticks and D-pads off that field;
                        // physical controller sticks are still forwarded by TPI1.
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_LEFT", recipe.ProfileName);
                        RequireBinding(control, "GAMEPAD_BUTTON_A", recipe.ProfileName, "gun trigger");
                    }
                    else if (recipe.InputProtocol ==
                             AndroidLaunchRecipe.InputProtocolSharedJusticeLeague)
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName, "move up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "move right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName, "move down");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "move left");
                    }
                    else if (recipe.InputProtocol ==
                             AndroidLaunchRecipe.InputProtocolSharedWonderlandWars)
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName, "move up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "move right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName, "move down");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "move left");
                        RequireBinding(control, "GAMEPAD_BUTTON_Y", recipe.ProfileName, "Aime");
                    }
                    else if (recipe.InputProtocol ==
                             AndroidLaunchRecipe.InputProtocolSharedLuigiMansion)
                    {
                        // Luigi uses direct absolute touchscreen pointers. Keep
                        // the overlay clear of a redundant aim stick; physical
                        // controllers are forwarded independently by the
                        // companion activity bridge.
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName);
                    }
                    else if (recipe.InputProtocol ==
                             AndroidLaunchRecipe.InputProtocolSharedCxbxrGundam)
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP",
                            recipe.ProfileName, "left stick up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT",
                            recipe.ProfileName, "left stick right");
                        RequireBinding(control, "GAMEPAD_RIGHT_THUMB_UP",
                            recipe.ProfileName, "right stick up");
                        RequireBinding(control, "GAMEPAD_RIGHT_THUMB_RIGHT",
                            recipe.ProfileName, "right stick right");
                        RequireBinding(control, "GAMEPAD_BUTTON_Y",
                            recipe.ProfileName, "right fire 1");
                        RequireBinding(control, "GAMEPAD_BUTTON_L1",
                            recipe.ProfileName, "right fire 2");
                        RequireBinding(control, "GAMEPAD_BUTTON_X",
                            recipe.ProfileName, "card insert");
                        RequireBinding(control, "GAMEPAD_BUTTON_R2",
                            recipe.ProfileName, "analog pedal");
                    }
                    else if (recipe.InputProtocol ==
                             AndroidLaunchRecipe.InputProtocolSharedCxbxrGolf)
                    {
                        // Golf uses its own absolute touch-panel emulation for
                        // menus and a single TP Analog0 swing channel.
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_DPAD_LEFT", recipe.ProfileName);
                        RequireBinding(control, "GAMEPAD_BUTTON_R2",
                            recipe.ProfileName, "golf swing");
                    }
                    else if (recipe.ProfileName is "GoGoStrike" or "TippinBloks")
                    {
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName, "analog up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "analog right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName, "analog down");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "analog left");
                        if (recipe.ProfileName == "GoGoStrike")
                            RequireBinding(control, "GAMEPAD_BUTTON_B", recipe.ProfileName, "cabinet setup");
                    }
                    else if (recipe.ProfileName == "DoodleJump")
                    {
                        // Doodle Jump's Wartran profile exposes a horizontal
                        // control bar as Analog0. It is not a four-way arcade
                        // stick and has no trigger/reload gameplay buttons.
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "control bar right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "control bar left");
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                    }
                    else if (recipe.ProfileName == "HauntedMuseum")
                    {
                        // The cabinet XML assigns the P1 gun trigger to
                        // P2Button1. Its dedicated Android layout must label A
                        // as Trigger and must not advertise unused generic gun
                        // actions from the shared 9025 layout.
                        RequireBinding(control, "GAMEPAD_BUTTON_A",
                            recipe.ProfileName, "P1 gun trigger");
                        ForbidBinding(control, "GAMEPAD_BUTTON_B", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_BUTTON_X", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_BUTTON_Y", recipe.ProfileName);
                        RequireBinding(control, "GAMEPAD_DPAD_UP", recipe.ProfileName, "menu up");
                        RequireBinding(control, "GAMEPAD_DPAD_RIGHT", recipe.ProfileName, "menu enter");
                    }
                    else if (recipe.ProfileName == "AngryBirds")
                    {
                        // The game surface is the slingshot. The overlay only
                        // carries its two cabinet switches and must not cover
                        // the playfield with a redundant movement stick.
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName);
                    }
                    else if (control.Id is >= 9062 and <= 9066 ||
                             control.Id is >= 9071 and <= 9074 ||
                             recipe.ProfileName == "Terminator")
                    {
                        // These cabinets use absolute touch/gun coordinates.
                        // Direction buttons, where present, are discrete menu
                        // or volume switches rather than an aiming control.
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName);
                        ForbidBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName);
                        RequireBinding(control, "GAMEPAD_BUTTON_A", recipe.ProfileName, "primary cabinet action");
                    }
                    else if (recipe.ProfileName.StartsWith(
                                 "ShiningForceCross", StringComparison.Ordinal))
                    {
                        // Shining uses the JVS analog channels for its movement
                        // stick and a separate Wine cursor for cabinet touch.
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_UP", recipe.ProfileName, "move up");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_RIGHT", recipe.ProfileName, "move right");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_DOWN", recipe.ProfileName, "move down");
                        RequireBinding(control, "GAMEPAD_LEFT_THUMB_LEFT", recipe.ProfileName, "move left");
                    }
                    else
                    {
                        RequireBinding(control, "GAMEPAD_DPAD_UP", recipe.ProfileName, "up");
                        RequireBinding(control, "GAMEPAD_DPAD_RIGHT", recipe.ProfileName, "right");
                        RequireBinding(control, "GAMEPAD_DPAD_DOWN", recipe.ProfileName, "down");
                        RequireBinding(control, "GAMEPAD_DPAD_LEFT", recipe.ProfileName, "left");
                    }

                    ValidateXmlButtonCoverage(profileDirectory, recipe, control);
                    ValidateExactArcadeButtons(profileDirectory, recipe, control);
                    ValidateApm3ExtensionButtons(profileDirectory, recipe, control);
                    ValidateExactSemanticBindings(recipe, control);
                    ValidateInputProtocol(profileDirectory, recipe);
                    ValidateProfileArguments(profileDirectory, recipe);
                }

                ValidateWmmtLayout(controls[9012]);
                ValidateSegaRallyProfileLabels(profileDirectory);
                Console.WriteLine(
                    $"Android controls catalog: PASS ({controls.Count} TeknoParrot layouts, " +
                    $"{recipes.Count} launch recipes)");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Android controls catalog test failed: " + error.Message);
                return 1;
            }
        }

        private static Dictionary<int, ControlProfile> LoadTeknoParrotControls(string directory)
        {
            var result = new Dictionary<int, ControlProfile>();
            foreach (var path in Directory.GetFiles(directory, "controls-9*.icp"))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var id = root.GetProperty("id").GetInt32();
                var expectedFileName = $"controls-{id}.icp";
                if (!string.Equals(Path.GetFileName(path), expectedFileName,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Control profile {id} has mismatched filename {Path.GetFileName(path)}.");
                if (!result.TryAdd(id, ReadControlProfile(root)))
                    throw new InvalidOperationException($"Control profile id {id} is duplicated.");
            }
            return result;
        }

        private static void ValidateForwarderCoverage(
            string repositoryRoot,
            IEnumerable<ControlProfile> controls)
        {
            var configuredSource =
                Environment.GetEnvironmentVariable("TEKNOPARROT_CONTROLS_SOURCE");
            var sourceRoot = string.IsNullOrWhiteSpace(configuredSource)
                ? Path.Combine(repositoryRoot, "WinlatorFork")
                : configuredSource;
            var nestedPath = Path.Combine(
                sourceRoot, "app", "app", "src", "main", "java", "com", "winlator",
                "XServerDisplayActivity.java");
            var appPath = Path.Combine(
                sourceRoot, "app", "src", "main", "java", "com", "winlator",
                "XServerDisplayActivity.java");
            var sourcePath = File.Exists(nestedPath) ? nestedPath : appPath;
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    "The Winlator forwarded-input implementation was not found.", sourcePath);

            var source = File.ReadAllText(sourcePath);
            foreach (var binding in controls
                         .SelectMany(control => control.Bindings)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!Regex.IsMatch(
                        source,
                        $@"case\s+{Regex.Escape(binding)}\s*:",
                        RegexOptions.CultureInvariant))
                    throw new InvalidOperationException(
                        $"TeknoParrot control binding {binding} is swallowed by the exclusive " +
                        "forwarding path because XServerDisplayActivity has no case for it.");
            }
        }

        private static ControlProfile ReadControlProfile(JsonElement root)
        {
            var id = root.GetProperty("id").GetInt32();
            var name = root.GetProperty("name").GetString() ?? string.Empty;
            if (!name.StartsWith("TeknoParrot ", StringComparison.Ordinal))
                throw new InvalidOperationException($"Control profile {id} has a non-TeknoParrot name.");

            var bindings = new HashSet<string>(StringComparer.Ordinal);
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            var elements = root.GetProperty("elements");
            if (elements.GetArrayLength() == 0)
                throw new InvalidOperationException($"Control profile {id} has no elements.");
            foreach (var element in elements.EnumerateArray())
            {
                var x = element.GetProperty("x").GetDouble();
                var y = element.GetProperty("y").GetDouble();
                var scale = element.GetProperty("scale").GetDouble();
                if (x < 0 || x > 1 || y < 0 || y > 1 || scale <= 0 || scale > 3)
                    throw new InvalidOperationException(
                        $"Control profile {id} contains out-of-bounds element geometry.");
                var elementBindings = element.GetProperty("bindings");
                if (elementBindings.GetArrayLength() != 4)
                    throw new InvalidOperationException(
                        $"Control profile {id} contains an element without four bindings.");
                foreach (var binding in elementBindings.EnumerateArray())
                {
                    var value = binding.GetString() ?? string.Empty;
                    if (value == "NONE")
                        continue;
                    bindings.Add(value);
                    var text = element.TryGetProperty("text", out var textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    if (labels.TryGetValue(value, out var existingText) &&
                        !string.Equals(existingText, text, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Control profile {id} labels {value} as both '{existingText}' and '{text}'.");
                    labels[value] = text;
                }
            }
            return new ControlProfile(id, name, bindings, labels);
        }

        private static void ValidateExactSemanticBindings(
            AndroidLaunchRecipe recipe,
            ControlProfile control)
        {
            if (!ExactSemanticBindings.TryGetValue(recipe.ProfileName, out var expected))
                return;

            foreach (var pair in expected)
            {
                RequireBinding(control, pair.Key, recipe.ProfileName, pair.Value);
                if (!control.Labels.TryGetValue(pair.Key, out var actualLabel) ||
                    !string.Equals(actualLabel, pair.Value, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"{recipe.ProfileName} layout {control.Id} labels {pair.Key} " +
                        $"as '{actualLabel}', expected '{pair.Value}'.");
            }

            var expectedGameplay = expected.Keys
                .Where(GameplayBindings.Values.Contains)
                .ToHashSet(StringComparer.Ordinal);
            var actualGameplay = control.Bindings
                .Where(GameplayBindings.Values.Contains)
                .ToHashSet(StringComparer.Ordinal);
            if (!actualGameplay.SetEquals(expectedGameplay))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} layout {control.Id} exposes unexpected gameplay buttons " +
                    $"(expected {string.Join(",", expectedGameplay)}, got {string.Join(",", actualGameplay)})." );
        }

        private static void ValidateXmlButtonCoverage(
            string profileDirectory,
            AndroidLaunchRecipe recipe,
            ControlProfile control)
        {
            var path = Path.Combine(profileDirectory, recipe.ProfileName + ".xml");
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} recipe has no matching GameProfiles XML.");

            // TGM3 exposes a fourth diagnostic XML mapping, but the actual cabinet
            // and the device-tested overlay use three gameplay buttons.
            if (recipe.ProfileName == "TetrisTheGrandMaster3TerrorInstinct")
                return;
            // These title-specific layouts are checked below against an exact
            // binding/label contract. Their XMLs include cabinet-only or P2
            // switches that a single-player touch overlay intentionally does
            // not expose as ordinary sequential P1 face buttons.
            if (ExactSemanticBindings.ContainsKey(recipe.ProfileName))
                return;
            // Radikal Bikers forwards its accelerator/brake as analog trigger
            // axes and derives the legacy button bits inside the shared encoder.
            if (recipe.InputProtocol == AndroidLaunchRecipe.InputProtocolSharedGaelco)
                return;
            // MKDX translates the four face buttons to the cabinet's sparse
            // Item/Enter/Mario/Banapass assignments before publishing JVS.
            if (recipe.InputProtocol == AndroidLaunchRecipe.InputProtocolJvsMkdx)
            {
                RequireBinding(control, "GAMEPAD_BUTTON_A", recipe.ProfileName, "item");
                RequireBinding(control, "GAMEPAD_BUTTON_B", recipe.ProfileName, "enter");
                RequireBinding(control, "GAMEPAD_BUTTON_X", recipe.ProfileName, "Mario button");
                RequireBinding(control, "GAMEPAD_BUTTON_Y", recipe.ProfileName, "Banapass");
                return;
            }
            // Initial D's View button is the only ordinary P1 gameplay switch;
            // sequential shifting is deliberately translated to P2 directions.
            if (recipe.InputProtocol == AndroidLaunchRecipe.InputProtocolJvsInitialD)
            {
                RequireBinding(control, "GAMEPAD_BUTTON_A", recipe.ProfileName, "view change");
                RequireBinding(control, "GAMEPAD_BUTTON_B", recipe.ProfileName, "shift up");
                RequireBinding(control, "GAMEPAD_BUTTON_X", recipe.ProfileName, "shift down");
                return;
            }
            if (recipe.InputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta)
            {
                RequireBinding(control, "GAMEPAD_BUTTON_A", recipe.ProfileName, "view change");
                RequireBinding(control, "GAMEPAD_BUTTON_B", recipe.ProfileName, "shift up");
                RequireBinding(control, "GAMEPAD_BUTTON_X", recipe.ProfileName, "shift down");
                RequireBinding(control, "GAMEPAD_BUTTON_Y", recipe.ProfileName, "Aime card");
                return;
            }
            if (recipe.InputProtocol ==
                AndroidLaunchRecipe.InputProtocolSharedCxbxrOutrun)
            {
                RequireBinding(control, "GAMEPAD_BUTTON_A",
                    recipe.ProfileName, "view change");
                RequireBinding(control, "GAMEPAD_BUTTON_B",
                    recipe.ProfileName, "shift down");
                RequireBinding(control, "GAMEPAD_BUTTON_X",
                    recipe.ProfileName, "shift up");
                return;
            }

            var document = XDocument.Load(path, LoadOptions.None);
            var mappings = document.Descendants("InputMapping")
                .Select(element => element.Value.Trim());
            foreach (var mapping in mappings)
            {
                var match = Regex.Match(mapping, "^P1Button([1-6])$", RegexOptions.CultureInvariant);
                if (!match.Success) continue;
                var button = int.Parse(match.Groups[1].Value);
                RequireBinding(
                    control,
                    GameplayBindings[button],
                    recipe.ProfileName,
                    $"P1 button {button}");
            }
        }

        private static void ValidateWmmtLayout(ControlProfile control)
        {
            foreach (var binding in new[]
                     {
                         "GAMEPAD_BUTTON_A", "GAMEPAD_BUTTON_B",
                         "GAMEPAD_BUTTON_X", "GAMEPAD_BUTTON_Y",
                         "GAMEPAD_DPAD_UP", "GAMEPAD_DPAD_DOWN",
                         "GAMEPAD_BUTTON_L1"
                     })
                RequireBinding(control, binding, "WMMT", binding);
        }

        private static void ValidateSegaRallyProfileLabels(string profileDirectory)
        {
            var document = XDocument.Load(
                Path.Combine(profileDirectory, "SR3.xml"), LoadOptions.None);
            var labels = document.Descendants("JoystickButtons")
                .Select(entry => new
                {
                    Mapping = entry.Element("InputMapping")?.Value.Trim(),
                    Label = entry.Element("ButtonName")?.Value.Trim()
                })
                .Where(entry => entry.Mapping is "P1Button3" or "P1Button4")
                .ToDictionary(
                    entry => entry.Mapping!, entry => entry.Label ?? string.Empty,
                    StringComparer.Ordinal);
            if (!labels.TryGetValue("P1Button3", out var button3) ||
                !string.Equals(button3, "Handbrake", StringComparison.Ordinal) ||
                !labels.TryGetValue("P1Button4", out var button4) ||
                !string.Equals(button4, "View Change", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "SR3 profile labels do not match SegaRallyPipe: " +
                    "P1Button3 must be Handbrake and P1Button4 must be View Change.");
        }

        private static void ValidateExactArcadeButtons(
            string profileDirectory,
            AndroidLaunchRecipe recipe,
            ControlProfile control)
        {
            if (!ExactArcadeControlIds.Contains(control.Id))
                return;

            var document = XDocument.Load(
                Path.Combine(profileDirectory, recipe.ProfileName + ".xml"),
                LoadOptions.None);
            var expected = document.Descendants("InputMapping")
                .Select(element => Regex.Match(
                    element.Value.Trim(), "^P1Button([1-6])$",
                    RegexOptions.CultureInvariant))
                .Where(match => match.Success)
                .Select(match => GameplayBindings[int.Parse(match.Groups[1].Value)])
                .ToHashSet(StringComparer.Ordinal);
            var actual = control.Bindings
                .Where(GameplayBindings.Values.Contains)
                .ToHashSet(StringComparer.Ordinal);
            if (!actual.SetEquals(expected))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} layout {control.Id} gameplay buttons do not exactly match XML " +
                    $"(expected {string.Join(",", expected)}, got {string.Join(",", actual)})." );
        }

        private static void ValidateApm3ExtensionButtons(
            string profileDirectory,
            AndroidLaunchRecipe recipe,
            ControlProfile control)
        {
            if (recipe.InputProtocol != AndroidLaunchRecipe.InputProtocolApm3)
                return;

            var document = XDocument.Load(
                Path.Combine(profileDirectory, recipe.ProfileName + ".xml"),
                LoadOptions.None);
            var mappings = document.Descendants("InputMapping")
                .Select(element => element.Value.Trim())
                .ToHashSet(StringComparer.Ordinal);
            if (mappings.Contains("ExtensionOne1"))
                RequireBinding(control, "GAMEPAD_BUTTON_L2", recipe.ProfileName, "APM3 button 7");
            if (mappings.Contains("ExtensionOne2"))
                RequireBinding(control, "GAMEPAD_BUTTON_R2", recipe.ProfileName, "APM3 button 8");
        }

        private static void RequireBinding(
            ControlProfile profile,
            string binding,
            string recipe,
            string purpose)
        {
            if (!profile.Bindings.Contains(binding))
                throw new InvalidOperationException(
                    $"{recipe} layout {profile.Id} is missing {purpose} binding {binding}.");
        }

        private static void ForbidBinding(
            ControlProfile profile,
            string binding,
            string recipe)
        {
            if (profile.Bindings.Contains(binding))
                throw new InvalidOperationException(
                    $"{recipe} layout {profile.Id} has redundant touch-aim binding {binding}.");
        }

        private static void ValidateInputProtocol(
            string profileDirectory,
            AndroidLaunchRecipe recipe)
        {
            var document = XDocument.Load(
                Path.Combine(profileDirectory, recipe.ProfileName + ".xml"),
                LoadOptions.None);
            var emulationProfile = document.Root?.Element("EmulationProfile")?.Value.Trim();
            if (string.IsNullOrEmpty(emulationProfile) ||
                !ProtocolsByEmulationProfile.TryGetValue(emulationProfile, out var protocols))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} has unmapped XML emulation profile '{emulationProfile}'.");
            if (!protocols.Contains(recipe.InputProtocol))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} uses {recipe.InputProtocol}, but XML emulation profile " +
                    $"{emulationProfile} requires {string.Join(" or ", protocols)}.");
        }

        private static void ValidateProfileArguments(
            string profileDirectory,
            AndroidLaunchRecipe recipe)
        {
            var document = XDocument.Load(
                Path.Combine(profileDirectory, recipe.ProfileName + ".xml"),
                LoadOptions.None);
            var customArguments = document.Root?.Element("CustomArguments")?.Value ?? "";
            var extraParameters = document.Root?.Element("ExtraParameters")?.Value ?? "";
            if (!recipe.HandlesProfileArguments(customArguments, extraParameters))
                throw new InvalidOperationException(
                    $"{recipe.ProfileName} recipe does not match its XML custom/extra arguments.");
        }

        private static HashSet<string> Set(params string[] values) =>
            new(values, StringComparer.Ordinal);

        private static IReadOnlyDictionary<string, string> Labels(
            params (string Binding, string Label)[] values) =>
            values.ToDictionary(
                value => value.Binding,
                value => value.Label,
                StringComparer.Ordinal);

        private static string FindRepositoryRoot()
        {
            var directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory, "TeknoParrotUi.Common")) &&
                    Directory.Exists(Path.Combine(directory, "WinlatorFork")))
                    return directory;
                directory = Path.GetDirectoryName(directory);
            }
            throw new DirectoryNotFoundException("TeknoParrotUI repository root was not found.");
        }

        private static string FindControlsDirectory(string repositoryRoot)
        {
            var configuredSource =
                Environment.GetEnvironmentVariable("TEKNOPARROT_CONTROLS_SOURCE");
            var sourceRoot = string.IsNullOrWhiteSpace(configuredSource)
                ? Path.Combine(repositoryRoot, "WinlatorFork")
                : configuredSource;
            var nestedDirectory = Path.Combine(
                sourceRoot, "app", "app", "src", "main", "assets",
                "inputcontrols", "profiles");
            if (Directory.Exists(nestedDirectory))
                return nestedDirectory;
            var appDirectory = Path.Combine(
                sourceRoot, "app", "src", "main", "assets",
                "inputcontrols", "profiles");
            if (Directory.Exists(appDirectory))
                return appDirectory;
            throw new DirectoryNotFoundException(
                "The TeknoParrot Winlator controls directory was not found under: " +
                sourceRoot);
        }

        private sealed record ControlProfile(
            int Id,
            string Name,
            HashSet<string> Bindings,
            Dictionary<string, string> Labels);
    }
}
