using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TeknoParrotUi.Common.Android
{
    public enum AndroidDisplayMode
    {
        Centered,
        AspectFit,
        Fullscreen
    }

    /// <summary>
    /// Versioned, data-driven contract for launching a Windows game through
    /// TeknoParrot's managed Winlator integration. Recipe files are stock
    /// catalog data: a new game can graduate to Android without adding another
    /// profile-name branch to the Android application.
    /// </summary>
    public sealed class AndroidLaunchRecipe
    {
        public const int CurrentSchemaVersion = 1;
        public const string GameExecutablePlaceholder = "{GameExecutable}";
        public const string InputProtocolJvs = "jvs";
        public const string InputProtocolJvsBattleGear = "jvs-battle-gear";
        public const string InputProtocolJvsChaseHq2 = "jvs-chase-hq2";
        public const string InputProtocolJvsVirtuaRLimit = "jvs-virtua-r-limit";
        public const string InputProtocolJvsWackyRaces = "jvs-wacky-races";
        public const string InputProtocolJvsWmmt = "jvs-wmmt";
        public const string InputProtocolJvsMachStorm = "jvs-mach-storm";
        public const string InputProtocolJvsMkdx = "jvs-mkdx";
        public const string InputProtocolJvsInitialD = "jvs-initial-d";
        public const string InputProtocolJvsSegaRacingClassic = "jvs-sega-racing-classic";
        public const string InputProtocolJvsSegaSonic = "jvs-sega-sonic";
        public const string InputProtocolJvsSegaDreamRaiders = "jvs-sega-dream-raiders";
        public const string InputProtocolJvsSegaGoldenGun = "jvs-sega-golden-gun";
        public const string InputProtocolJvsSegaLetsGoIsland = "jvs-sega-lets-go-island";
        public const string InputProtocolFastIo = "fast-io";
        public const string InputProtocolFastIoTheatrhythm = "fast-io-theatrhythm";
        public const string InputProtocolApm3 = "apm3";
        public const string InputProtocolAllsIdta = "alls-idta";
        public const string InputProtocolSegaRally = "sega-rally";
        public const string InputProtocolSharedExBoard = "shared-exboard";
        public const string InputProtocolSharedRawThrills = "shared-raw-thrills";
        public const string InputProtocolSharedRawThrillsSuperBikes =
            "shared-raw-thrills-super-bikes";
        public const string InputProtocolSharedRawThrillsH2O = "shared-raw-thrills-h2o";
        public const string InputProtocolSharedRawThrillsGun = "shared-raw-thrills-gun";
        public const string InputProtocolSharedRawThrillsGoGoStrike =
            "shared-raw-thrills-gogo-strike";
        public const string InputProtocolSharedWartran = "shared-wartran";
        public const string InputProtocolSharedDeadHeat = "shared-dead-heat";
        public const string InputProtocolSharedFrenzyExpress = "shared-frenzy-express";
        public const string InputProtocolSharedGrid = "shared-grid";
        public const string InputProtocolSharedGtiClub3 = "shared-gti-club3";
        public const string InputProtocolSharedTaiko = "shared-taiko";
        public const string InputProtocolSharedGaelco = "shared-gaelco";
        public const string InputProtocolSharedJusticeLeague = "shared-justice-league";
        public const string InputProtocolSharedEadp = "shared-eadp";
        public const string InputProtocolSharedWonderlandWars =
            "shared-wonderland-wars";
        public const string InputProtocolSharedFriction = "shared-friction";
        public const string InputProtocolSharedTaitoGun = "shared-taito-gun";
        public const string InputProtocolSharedTaitoGunMusic =
            "shared-taito-gun-music";
        public const string InputProtocolSharedTaitoGunHauntedMuseum2 =
            "shared-taito-gun-haunted-museum2";
        public const string InputProtocolSharedGha = "shared-gha";
        public const string InputProtocolSharedLuigiMansion = "shared-luigi-mansion";
        public const string InputProtocolSharedCxbxrDriving = "shared-cxbxr-driving";
        public const string InputProtocolSharedCxbxrOutrun = "shared-cxbxr-outrun";
        public const string InputProtocolSharedCxbxrWmmt = "shared-cxbxr-wmmt";
        public const string InputProtocolSharedCxbxrGun = "shared-cxbxr-gun";
        public const string InputProtocolSharedCxbxrOllie = "shared-cxbxr-ollie";
        public const string InputProtocolSharedCxbxrGundam = "shared-cxbxr-gundam";
        public const string InputProtocolSharedCxbxrGolf = "shared-cxbxr-golf";
        public const string CompatibilityPresetNone = "";
        public const string CompatibilityPresetMediaWmv = "media-wmv";
        public const string CompatibilityPresetWineGStreamer = "wine-gstreamer";
        public const string CompatibilityPresetTaitoLegacySCard = "taito-legacy-scard";
        public const string CompatibilityPresetDirtyDrivingFullscreen = "dirty-driving-fullscreen";
        public const string CompatibilityPresetEnEinsNativeFullscreen =
            "en-eins-native-fullscreen";
        public const string CompatibilityPresetWmmtTerminal = "wmmt-terminal";
        public const string CompatibilityPresetWmmtNoTerminal = "wmmt-no-terminal";
        public const string CompatibilityPresetWmmt3YaCard = "wmmt3-yacard";
        public const string CompatibilityPresetCxbxrWmmtYaCard = "cxbxr-wmmt-yacard";
        public const string CompatibilityPresetCxbxrPerformance =
            "cxbxr-performance";
        public const string CompatibilityPresetCxbxrChihiroType3 =
            "cxbxr-chihiro-type3";
        public const string CompatibilityPresetWackyRacesNetwork = "wacky-races-network";
        public const string CompatibilityPresetPostStartRemoteThread =
            "post-start-remote-thread";
        public const string CompatibilityPresetParkedEntrypoint = "parked-entrypoint";
        public const string CompatibilityPresetWineD3dRemoteThread =
            "wined3d-remote-thread";
        public const string CompatibilityPresetWineD3dParkedEntrypoint =
            "wined3d-parked-entrypoint";
        public const string CompatibilityPresetInitialD8 = "initial-d8";
        public const string CompatibilityPresetInitialDTheArcade = "initial-d-the-arcade";
        public const string CompatibilityPresetChaseHq2 = "chase-hq2";
        public const string CompatibilityPresetStarWars = "star-wars";
        public const string CompatibilityPresetTaikoCustomResolution = "taiko-custom-resolution";
        public const string CompatibilityPresetLargeAddressAware = "large-address-aware";
        public const string CompatibilityPresetLargeAddressAwareDdraw = "large-address-aware-ddraw";
        public const string CompatibilityPresetGameWorkingDirectory = "game-working-directory";
        public const string CompatibilityPresetBuiltinDdraw = "builtin-ddraw";
        public const string CompatibilityPresetXactLocalRegister = "xact-local-register";
        public const string CompatibilityPresetEadpDualIo = "eadp-dual-io";
        public const string CompatibilityPresetSharedJvsDualIo =
            "shared-jvs-dual-io";
        public const string CompatibilityPresetDirectTouchJvs =
            "direct-touch-jvs";
        public const string CompatibilityPresetBox64Interpreter =
            "box64-interpreter";
        public const string CompatibilityPresetPortraitWindowCounterClockwise =
            "portrait-window-counter-clockwise";
        public const string DisplayModeCentered = "centered";
        public const string DisplayModeAspectFit = "aspect-fit";
        public const string DisplayModeFullscreen = "fullscreen";

        public int SchemaVersion { get; set; }
        public string RecipeId { get; set; } = "";
        public string ProfileName { get; set; } = "";
        public bool Validated { get; set; }
        public string ContainerTemplate { get; set; } = "";
        public int ContainerId { get; set; }
        public string GuestArchitecture { get; set; } = "";
        public string RuntimeRoot { get; set; } = "";
        public string LoaderExecutable { get; set; } = "";
        public string WorkingDirectory { get; set; } = ".";
        public string LibraryDirectory { get; set; } = "";
        public string InputProtocol { get; set; } = "";
        public int ControlsProfileId { get; set; }
        public int FrameRateLimit { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        // Keep the guest window native and centered unless a recipe explicitly
        // opts into Winlator's screen-fitting or the game's own fullscreen mode.
        // The screen-fitting renderer transformation has caused otherwise
        // healthy Wine games to terminate on physical Android hardware.
        public string DisplayMode { get; set; } = DisplayModeCentered;
        public string CompatibilityPreset { get; set; } = CompatibilityPresetNone;
        // Android game sessions should be quiet by default. Older recipes omit
        // this optional field; treating omission as diagnostic mode enables
        // Wine, DXVK and Box64 logging and can materially hurt emulation speed.
        // A game's persisted AndroidDebugLogging setting remains the explicit
        // per-title troubleshooting override.
        public bool PerformanceModeDefault { get; set; } = true;
        // These fields record the exact desktop-profile argument strings that
        // were deliberately converted into this recipe's structured Arguments.
        // An empty value means that the corresponding profile field must also
        // be empty, so newly-added desktop arguments cannot be silently lost on
        // Android.
        public string ProfileCustomArguments { get; set; } = "";
        public string ProfileExtraParameters { get; set; } = "";
        public List<string> Arguments { get; set; } = new List<string>();
        public List<AndroidProfileConfigOverride> ProfileConfigOverrides { get; set; } =
            new List<AndroidProfileConfigOverride>();
        public AndroidGameImportRule Import { get; set; } = new AndroidGameImportRule();

        public void Validate()
        {
            if (SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"Android launch recipe '{RecipeId}' uses unsupported schema {SchemaVersion}.");
            RequireIdentifier(RecipeId, nameof(RecipeId));
            RequireIdentifier(ProfileName, nameof(ProfileName));
            RequireIdentifier(ContainerTemplate, nameof(ContainerTemplate));
            if (ContainerId < 1)
                throw new InvalidDataException("Android launch recipe container id must be positive.");
            if (!string.Equals(GuestArchitecture, "x86", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(GuestArchitecture, "x64", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Android launch recipe architecture must be x86 or x64.");

            ValidateDosRoot(RuntimeRoot);
            ValidateRelativeWindowsPath(LoaderExecutable, nameof(LoaderExecutable), allowCurrent: false);
            ValidateRelativeWindowsPath(WorkingDirectory, nameof(WorkingDirectory), allowCurrent: true);
            ValidateRelativeWindowsPath(LibraryDirectory, nameof(LibraryDirectory), allowCurrent: false);
            if (!IsSupportedInputProtocol(InputProtocol))
                throw new InvalidDataException(
                    $"Android launch recipe input protocol '{InputProtocol}' is unsupported.");
            if (ControlsProfileId <= 0 || ControlsProfileId > 1_000_000)
                throw new InvalidDataException(
                    "Android launch recipe controls profile id is invalid.");
            if (FrameRateLimit < 0 || FrameRateLimit > 1_000)
                throw new InvalidDataException(
                    "Android launch recipe frame-rate limit must be between 0 and 1000.");
            if ((ResolutionWidth == 0) != (ResolutionHeight == 0) ||
                ResolutionWidth < 0 || ResolutionHeight < 0 ||
                ResolutionWidth > 8_192 || ResolutionHeight > 8_192 ||
                (ResolutionWidth != 0 && (ResolutionWidth < 320 || ResolutionHeight < 240)))
                throw new InvalidDataException(
                    "Android launch recipe resolution must be omitted or between 320x240 and 8192x8192.");
            if (!IsSupportedDisplayMode(DisplayMode))
                throw new InvalidDataException(
                    $"Android launch recipe display mode '{DisplayMode}' is unsupported.");
            if (!IsSupportedCompatibilityPreset(CompatibilityPreset))
                throw new InvalidDataException(
                    $"Android launch recipe compatibility preset '{CompatibilityPreset}' is unsupported.");

            if (Arguments == null || Arguments.Count == 0)
                throw new InvalidDataException("Android launch recipe arguments are missing.");
            ValidateProfileArgumentContract(
                ProfileCustomArguments, nameof(ProfileCustomArguments));
            ValidateProfileArgumentContract(
                ProfileExtraParameters, nameof(ProfileExtraParameters));
            var placeholders = 0;
            foreach (var argument in Arguments)
            {
                if (string.IsNullOrWhiteSpace(argument) || HasControlCharacters(argument))
                    throw new InvalidDataException("Android launch recipe contains an invalid argument.");
                placeholders += CountOccurrences(argument, GameExecutablePlaceholder);
                var withoutKnownPlaceholder = argument.Replace(
                    GameExecutablePlaceholder, "", StringComparison.Ordinal);
                if (withoutKnownPlaceholder.Contains('{') || withoutKnownPlaceholder.Contains('}'))
                    throw new InvalidDataException("Android launch recipe contains an unknown placeholder.");
            }
            if (placeholders != 1)
                throw new InvalidDataException(
                    "Android launch recipe must contain exactly one {GameExecutable} placeholder.");

            if (ProfileConfigOverrides == null)
                throw new InvalidDataException(
                    "Android launch recipe profile configuration overrides are invalid.");
            var overriddenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var configOverride in ProfileConfigOverrides)
            {
                if (configOverride == null)
                    throw new InvalidDataException(
                        "Android launch recipe contains an invalid profile configuration override.");
                ValidateIniName(configOverride.CategoryName, "configuration category");
                ValidateIniName(configOverride.FieldName, "configuration field");
                if (configOverride.FieldValue == null || configOverride.FieldValue.Length > 2_048 ||
                    HasControlCharacters(configOverride.FieldValue))
                    throw new InvalidDataException(
                        "Android launch recipe contains an invalid profile configuration value.");
                if (!overriddenFields.Add(configOverride.CategoryName + "\0" + configOverride.FieldName))
                    throw new InvalidDataException(
                        "Android launch recipe contains duplicate profile configuration overrides.");
            }

            if (Import == null || Import.FolderNameHints == null ||
                Import.FolderNameHints.Count == 0 || Import.ExecutableCandidates == null ||
                Import.ExecutableCandidates.Count == 0)
                throw new InvalidDataException("Android launch recipe import rules are incomplete.");
            foreach (var hint in Import.FolderNameHints)
            {
                if (string.IsNullOrWhiteSpace(hint) || HasControlCharacters(hint))
                    throw new InvalidDataException("Android launch recipe contains an invalid folder hint.");
            }
            foreach (var candidate in Import.ExecutableCandidates)
                ValidateRelativePortablePath(candidate, "executable candidate");
        }

        public AndroidResolvedLaunch Resolve(string gameExecutable)
        {
            Validate();
            if (string.IsNullOrWhiteSpace(gameExecutable) || HasControlCharacters(gameExecutable) ||
                gameExecutable.Contains('"'))
                throw new InvalidDataException("The mapped Android game executable is invalid.");

            return new AndroidResolvedLaunch(
                ContainerId,
                ContainerTemplate,
                GuestArchitecture.ToLowerInvariant(),
                CombineGuestPath(RuntimeRoot, LoaderExecutable),
                CombineGuestPath(RuntimeRoot, WorkingDirectory),
                Arguments.Select(argument => argument.Replace(
                    GameExecutablePlaceholder, gameExecutable, StringComparison.Ordinal)).ToArray(),
                CombineGuestPath(RuntimeRoot, LibraryDirectory),
                InputProtocol,
                ControlsProfileId,
                FrameRateLimit,
                ResolutionWidth,
                ResolutionHeight,
                DisplayMode,
                CompatibilityPreset,
                PerformanceModeDefault);
        }

        public bool HandlesProfileArguments(string customArguments, string extraParameters)
        {
            Validate();
            return string.Equals(
                       customArguments ?? "", ProfileCustomArguments,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       extraParameters ?? "", ProfileExtraParameters,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// Applies Android-only profile settings to the generated TeknoParrot.ini
        /// without mutating the shared game profile used by Windows and Linux.
        /// </summary>
        public string ApplyProfileConfigOverrides(string profileConfigIni)
        {
            Validate();
            if (profileConfigIni == null)
                throw new ArgumentNullException(nameof(profileConfigIni));
            if (ProfileConfigOverrides.Count == 0)
                return profileConfigIni;

            var normalized = profileConfigIni.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var lines = normalized.Split('\n').ToList();
            while (lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            foreach (var configOverride in ProfileConfigOverrides)
                ApplyProfileConfigOverride(lines, configOverride);

            return string.Join("\n", lines) + "\n";
        }

        private static void ApplyProfileConfigOverride(
            List<string> lines,
            AndroidProfileConfigOverride configOverride)
        {
            var sectionHeader = "[" + configOverride.CategoryName + "]";
            var sectionStart = lines.FindIndex(line =>
                string.Equals(line.Trim(), sectionHeader, StringComparison.OrdinalIgnoreCase));
            if (sectionStart < 0)
            {
                if (lines.Count > 0 && lines[^1].Length != 0)
                    lines.Add("");
                lines.Add(sectionHeader);
                lines.Add(configOverride.FieldName + "=" + configOverride.FieldValue);
                return;
            }

            var sectionEnd = lines.Count;
            for (var index = sectionStart + 1; index < lines.Count; index++)
            {
                var candidate = lines[index].Trim();
                if (candidate.Length >= 2 && candidate[0] == '[' && candidate[^1] == ']')
                {
                    sectionEnd = index;
                    break;
                }

                var equals = candidate.IndexOf('=');
                if (equals >= 0 && string.Equals(
                        candidate[..equals].Trim(),
                        configOverride.FieldName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    lines[index] = configOverride.FieldName + "=" + configOverride.FieldValue;
                    return;
                }
            }

            lines.Insert(sectionEnd, configOverride.FieldName + "=" + configOverride.FieldValue);
        }

        private static string CombineGuestPath(string root, string relative)
        {
            if (relative == ".")
                return root.TrimEnd('\\');
            return root.TrimEnd('\\') + "\\" + relative.Replace('/', '\\').TrimStart('\\');
        }

        private static void RequireIdentifier(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96 ||
                value.Any(character => !char.IsLetterOrDigit(character) &&
                    character != '.' && character != '-' && character != '_'))
                throw new InvalidDataException($"Android launch recipe {name} is invalid.");
        }

        private static void ValidateDosRoot(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 3 ||
                !char.IsLetter(value[0]) || value[1] != ':' || value[2] != '\\' ||
                value.Contains('"') || HasControlCharacters(value))
                throw new InvalidDataException("Android launch recipe runtime root is not an absolute DOS path.");

            var segments = value[3..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment is "." or ".."))
                throw new InvalidDataException("Android launch recipe runtime root contains traversal.");
        }

        private static void ValidateRelativeWindowsPath(string value, string name, bool allowCurrent)
        {
            if (allowCurrent && value == ".")
                return;
            ValidateRelativePortablePath(value, name);
        }

        private static void ValidateRelativePortablePath(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512 ||
                value.StartsWith('/') || value.StartsWith('\\') ||
                (value.Length > 1 && value[1] == ':') || value.Contains('"') ||
                HasControlCharacters(value))
                throw new InvalidDataException($"Android launch recipe {name} is not a safe relative path.");

            var segments = value.Replace('\\', '/').Split('/');
            if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
                throw new InvalidDataException($"Android launch recipe {name} contains traversal.");
        }

        private static void ValidateIniName(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
                HasControlCharacters(value) || value.IndexOfAny(new[] { '[', ']', '=' }) >= 0)
                throw new InvalidDataException(
                    $"Android launch recipe {name} is invalid.");
        }

        private static void ValidateProfileArgumentContract(string value, string name)
        {
            if (value == null || value.Length > 4_096 || HasControlCharacters(value))
                throw new InvalidDataException(
                    $"Android launch recipe {name} is invalid.");
        }

        private static bool HasControlCharacters(string value) =>
            value.Any(character => character < 0x20);

        private static int CountOccurrences(string value, string needle)
        {
            var count = 0;
            var offset = 0;
            while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }
            return count;
        }

        public static bool IsJvsInputProtocol(string value) =>
            value == InputProtocolJvs ||
            value == InputProtocolJvsBattleGear ||
            value == InputProtocolJvsChaseHq2 ||
            value == InputProtocolJvsVirtuaRLimit ||
            value == InputProtocolJvsWackyRaces ||
            value == InputProtocolJvsWmmt ||
            value == InputProtocolJvsMachStorm ||
            value == InputProtocolJvsMkdx ||
            value == InputProtocolJvsInitialD ||
            value == InputProtocolJvsSegaRacingClassic ||
            value == InputProtocolJvsSegaSonic ||
            value == InputProtocolJvsSegaDreamRaiders ||
            value == InputProtocolJvsSegaGoldenGun ||
            value == InputProtocolJvsSegaLetsGoIsland;

        public static bool IsSharedStateInputProtocol(string value) =>
            value == InputProtocolSharedExBoard ||
            value == InputProtocolSharedRawThrills ||
            value == InputProtocolSharedRawThrillsSuperBikes ||
            value == InputProtocolSharedRawThrillsH2O ||
            value == InputProtocolSharedRawThrillsGun ||
            value == InputProtocolSharedRawThrillsGoGoStrike ||
            value == InputProtocolSharedWartran ||
            value == InputProtocolSharedDeadHeat ||
            value == InputProtocolSharedFrenzyExpress ||
            value == InputProtocolSharedGrid ||
            value == InputProtocolSharedGtiClub3 ||
            value == InputProtocolSharedTaiko ||
            value == InputProtocolSharedGaelco ||
            value == InputProtocolSharedJusticeLeague ||
            value == InputProtocolSharedEadp ||
            value == InputProtocolSharedWonderlandWars ||
            value == InputProtocolSharedFriction ||
            value == InputProtocolSharedTaitoGun ||
            value == InputProtocolSharedTaitoGunMusic ||
            value == InputProtocolSharedTaitoGunHauntedMuseum2 ||
            value == InputProtocolSharedGha ||
            value == InputProtocolSharedLuigiMansion ||
            value == InputProtocolSharedCxbxrDriving ||
            value == InputProtocolSharedCxbxrOutrun ||
            value == InputProtocolSharedCxbxrWmmt ||
            value == InputProtocolSharedCxbxrGun ||
            value == InputProtocolSharedCxbxrOllie ||
            value == InputProtocolSharedCxbxrGundam ||
            value == InputProtocolSharedCxbxrGolf;

        public static bool IsSupportedInputProtocol(string value) =>
            IsJvsInputProtocol(value) ||
            IsSharedStateInputProtocol(value) ||
            IsFastIoInputProtocol(value) ||
            value == InputProtocolAllsIdta ||
            value == InputProtocolApm3 ||
            value == InputProtocolSegaRally;

        public static bool IsFastIoInputProtocol(string value) =>
            value == InputProtocolFastIo ||
            value == InputProtocolFastIoTheatrhythm;

        public static bool IsSupportedCompatibilityPreset(string value) =>
            value == CompatibilityPresetNone ||
            value == CompatibilityPresetMediaWmv ||
            value == CompatibilityPresetWineGStreamer ||
            value == CompatibilityPresetTaitoLegacySCard ||
            value == CompatibilityPresetDirtyDrivingFullscreen ||
            value == CompatibilityPresetEnEinsNativeFullscreen ||
            value == CompatibilityPresetWmmtTerminal ||
            value == CompatibilityPresetWmmtNoTerminal ||
            value == CompatibilityPresetWmmt3YaCard ||
            value == CompatibilityPresetCxbxrWmmtYaCard ||
            value == CompatibilityPresetCxbxrPerformance ||
            value == CompatibilityPresetCxbxrChihiroType3 ||
            value == CompatibilityPresetWackyRacesNetwork ||
            value == CompatibilityPresetPostStartRemoteThread ||
            value == CompatibilityPresetParkedEntrypoint ||
            value == CompatibilityPresetWineD3dRemoteThread ||
            value == CompatibilityPresetWineD3dParkedEntrypoint ||
            value == CompatibilityPresetInitialD8 ||
            value == CompatibilityPresetInitialDTheArcade ||
            value == CompatibilityPresetChaseHq2 ||
            value == CompatibilityPresetStarWars ||
            value == CompatibilityPresetTaikoCustomResolution ||
            value == CompatibilityPresetLargeAddressAware ||
            value == CompatibilityPresetLargeAddressAwareDdraw ||
            value == CompatibilityPresetGameWorkingDirectory ||
            value == CompatibilityPresetBuiltinDdraw ||
            value == CompatibilityPresetXactLocalRegister ||
            value == CompatibilityPresetEadpDualIo ||
            value == CompatibilityPresetSharedJvsDualIo ||
            value == CompatibilityPresetDirectTouchJvs ||
            value == CompatibilityPresetBox64Interpreter ||
            value == CompatibilityPresetPortraitWindowCounterClockwise;

        public static bool IsSupportedDisplayMode(string value) =>
            value == DisplayModeCentered ||
            value == DisplayModeAspectFit ||
            value == DisplayModeFullscreen;
    }

    public sealed class AndroidGameImportRule
    {
        public List<string> FolderNameHints { get; set; } = new List<string>();
        public List<string> ExecutableCandidates { get; set; } = new List<string>();
    }

    public sealed class AndroidProfileConfigOverride
    {
        public string CategoryName { get; set; } = "";
        public string FieldName { get; set; } = "";
        public string FieldValue { get; set; } = "";
    }

    public sealed record AndroidResolvedLaunch(
        int ContainerId,
        string ContainerTemplate,
        string GuestArchitecture,
        string LoaderExecutable,
        string WorkingDirectory,
        IReadOnlyList<string> Arguments,
        string LibraryDirectory,
        string InputProtocol,
        int ControlsProfileId,
        int FrameRateLimit,
        int ResolutionWidth,
        int ResolutionHeight,
        string DisplayMode,
        string CompatibilityPreset,
        bool PerformanceModeDefault);

    public static class AndroidLaunchRecipeCatalog
    {
        public const string DirectoryName = "AndroidLaunchRecipes";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow
        };

        public static IReadOnlyList<AndroidLaunchRecipe> LoadAll(string directory = null)
        {
            directory ??= Path.Combine(Environment.CurrentDirectory, DirectoryName);
            if (!Directory.Exists(directory))
                return Array.Empty<AndroidLaunchRecipe>();

            var recipes = new List<AndroidLaunchRecipe>();
            var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(
                         value => value, StringComparer.OrdinalIgnoreCase))
            {
                AndroidLaunchRecipe recipe;
                try
                {
                    recipe = JsonSerializer.Deserialize<AndroidLaunchRecipe>(
                        File.ReadAllText(path), SerializerOptions);
                }
                catch (JsonException error)
                {
                    throw new InvalidDataException(
                        $"Android launch recipe '{Path.GetFileName(path)}' is not valid JSON.", error);
                }

                if (recipe == null)
                    throw new InvalidDataException(
                        $"Android launch recipe '{Path.GetFileName(path)}' is empty.");
                recipe.Validate();
                if (!profiles.Add(recipe.ProfileName))
                    throw new InvalidDataException(
                        $"More than one Android launch recipe targets '{recipe.ProfileName}'.");
                if (!ids.Add(recipe.RecipeId))
                    throw new InvalidDataException(
                        $"Android launch recipe id '{recipe.RecipeId}' is duplicated.");
                recipes.Add(recipe);
            }
            return recipes;
        }

        public static bool TryGetValidated(
            string profileName,
            out AndroidLaunchRecipe recipe,
            out string error,
            string directory = null)
        {
            recipe = null;
            error = "";
            try
            {
                recipe = LoadAll(directory).FirstOrDefault(candidate =>
                    string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
                if (recipe == null)
                {
                    error = "This Android profile does not have a managed Winlator launch recipe yet.";
                    return false;
                }
                if (!recipe.Validated)
                {
                    error = "This Android launch recipe has not graduated from validation yet.";
                    recipe = null;
                    return false;
                }
                return true;
            }
            catch (InvalidDataException exception)
            {
                error = exception.Message;
                recipe = null;
                return false;
            }
        }
    }
}
