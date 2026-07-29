using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.AndroidBridge
{
    // WinlatorSessionContract only needs these platform-independent bounds.
    // The Android Binder transaction constants remain covered by the APK build.
    internal static class BridgeProtocol
    {
        public const int PageSize = 4096;
        public const int MaxPipeNameBytes = 128;
    }
}

namespace InputMethodAudit
{
    internal static class AndroidWinlatorContractTest
    {
        public static int Run()
        {
            try
            {
                var repositoryRoot = FindRepositoryRoot();
                var winlatorRoot = FindWinlatorRoot(repositoryRoot);
                var profileDirectory = Path.Combine(
                    repositoryRoot, "TeknoParrotUi.Common", "GameProfiles");
                var profilePath = Path.Combine(profileDirectory, "StarWars.xml");
                Lazydata.ParrotData ??= new ParrotData();
                var previousDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Path.Combine(
                    repositoryRoot, "TeknoParrotUi.Common");
                try
                {
                    var wmmt2jMetadata = JoystickHelper.DeSerializeMetadata(
                        Path.Combine(profileDirectory, "WMMT2j.xml"));
                    if (wmmt2jMetadata?.game_name !=
                        "Wangan Midnight Maximum Tune 2 (Japan)")
                        throw new InvalidOperationException(
                            "Case-insensitive stock metadata fallback did not resolve WMMT2j.");
                }
                finally
                {
                    Environment.CurrentDirectory = previousDirectory;
                }
                var starWarsProfile = JoystickHelper.DeSerializeGameProfile(profilePath, false)
                    ?? throw new InvalidOperationException("Star Wars profile could not be loaded.");
                var profileConfigIni = TeknoParrotIniWriter.BuildConfigIni(starWarsProfile);
                if (!profileConfigIni.Contains("[General]", StringComparison.Ordinal) ||
                    !profileConfigIni.Contains("Input API=DirectInput", StringComparison.Ordinal) ||
                    !profileConfigIni.Contains("Remove Camera Error=1", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "The complete Star Wars profile was not converted to TeknoParrot.ini.");

                var radikalProfilePath = Path.Combine(profileDirectory, "RadikalBikers.xml");
                var radikalProfile = JoystickHelper.DeSerializeGameProfile(
                    radikalProfilePath, false)
                    ?? throw new InvalidOperationException(
                        "Radikal Bikers profile could not be loaded.");
                var recipeDirectory = Path.Combine(
                    repositoryRoot, "TeknoParrotUi.Common",
                    AndroidLaunchRecipeCatalog.DirectoryName);
                if (!AndroidLaunchRecipeCatalog.TryGetValidated(
                        "RadikalBikers", out var radikalRecipe, out var radikalRecipeError,
                        recipeDirectory))
                    throw new InvalidOperationException(radikalRecipeError);
                var radikalConfigIni = radikalRecipe.ApplyProfileConfigOverrides(
                    TeknoParrotIniWriter.BuildConfigIni(radikalProfile));
                if (!radikalConfigIni.Contains("[General]", StringComparison.Ordinal) ||
                    !radikalConfigIni.Contains("Setup Screen=0", StringComparison.Ordinal) ||
                    radikalConfigIni.Contains("Setup Screen=1", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Radikal Bikers did not receive its Android-only setup-screen override.");

                if (!AndroidLaunchRecipeCatalog.TryGetValidated(
                        "GGS", out var striveRecipe, out var striveRecipeError,
                        recipeDirectory))
                    throw new InvalidOperationException(striveRecipeError);
                if (!striveRecipe.HandlesProfileArguments("-language=ja", "") ||
                    striveRecipe.HandlesProfileArguments("-language=en", "") ||
                    !striveRecipe.Resolve("D:\\Games\\GGST-Win64-Shipping.exe")
                        .Arguments.Contains("-language=ja"))
                    throw new InvalidOperationException(
                        "Guilty Gear Strive profile arguments were not converted safely.");

                var managedEnvironment = WinlatorSessionContract.ParseManagedEnvironment(
                    """
                    {
                      "schemaVersion": 1,
                      "state": "ready",
                      "containerId": 1,
                      "containerTemplate": "teknoparrot-x86-v1",
                      "runtimeRoot": "E:\\TeknoParrotRuntime",
                      "cxbxrAvailable": true
                    }
                    """);
                if (!managedEnvironment.IsReady ||
                    managedEnvironment.CxbxrAvailable != true)
                    throw new InvalidOperationException(
                        "Managed CXBXR runtime availability was not preserved.");
                var legacyManagedEnvironment = WinlatorSessionContract.ParseManagedEnvironment(
                    """
                    {
                      "schemaVersion": 1,
                      "state": "ready",
                      "containerId": 1,
                      "containerTemplate": "teknoparrot-x86-v1",
                      "runtimeRoot": "E:\\TeknoParrotRuntime"
                    }
                    """);
                if (legacyManagedEnvironment.CxbxrAvailable != null)
                    throw new InvalidOperationException(
                        "Legacy companion runtime availability must remain unknown.");
                RequireRejected(
                    () => WinlatorSessionContract.ParseManagedEnvironment(
                        """
                        {
                          "schemaVersion": 1,
                          "state": "ready",
                          "containerId": 1,
                          "containerTemplate": "teknoparrot-x86-v1",
                          "runtimeRoot": "E:\\TeknoParrotRuntime",
                          "cxbxrAvailable": "yes"
                        }
                        """),
                    "malformed managed CXBXR runtime availability");

                var invalidOverrideRecipe = new AndroidLaunchRecipe
                {
                    SchemaVersion = AndroidLaunchRecipe.CurrentSchemaVersion,
                    RecipeId = "Invalid.override.v1",
                    ProfileName = "InvalidOverride",
                    Validated = true,
                    ContainerTemplate = "teknoparrot-x86-v1",
                    ContainerId = 1,
                    GuestArchitecture = "x86",
                    RuntimeRoot = "E:\\TeknoParrotRuntime",
                    LoaderExecutable = "OpenParrotWin32\\OpenParrotLoader.exe",
                    WorkingDirectory = ".",
                    LibraryDirectory = "OpenParrotWin32",
                    InputProtocol = AndroidLaunchRecipe.InputProtocolFastIo,
                    ControlsProfileId = 1,
                    Arguments = new() { AndroidLaunchRecipe.GameExecutablePlaceholder },
                    ProfileConfigOverrides = new()
                    {
                        new AndroidProfileConfigOverride
                        {
                            CategoryName = "General",
                            FieldName = "Bad=Field",
                            FieldValue = "1"
                        }
                    },
                    Import = new AndroidGameImportRule
                    {
                        FolderNameHints = new() { "Invalid" },
                        ExecutableCandidates = new() { "invalid.exe" }
                    }
                };
                RequireRejected(
                    invalidOverrideRecipe.Validate,
                    "malformed profile configuration override");

                var request = new WinlatorActivityLaunchRequest(
                    Guid.NewGuid(),
                    1,
                    WinlatorSessionContract.WindowsExecutableLaunchKind,
                    "E:\\TeknoParrotRuntime\\OpenParrotWin32\\OpenParrotLoader.exe",
                    "E:\\TeknoParrotRuntime",
                    new[] { "D:\\TeknoParrotGames\\WackyRaces\\Launcher.exe" },
                    "E:\\TeknoParrotRuntime\\OpenParrotWin32",
                    9010,
                    60,
                    CompatibilityPreset: WinlatorSessionContract.CompatibilityPresetWackyRacesNetwork,
                    ProfileConfigIni: profileConfigIni);

                var activityContractPath = Path.Combine(
                    winlatorRoot, "app", "teknoparrot-bridge", "src",
                    "main", "java", "com", "winlator", "teknoparrot",
                    "ActivityLaunchContract.java");
                var activityContractSource = File.ReadAllText(activityContractPath);
                foreach (var compatibilityPreset in AndroidLaunchRecipeCatalog
                    .LoadAll(recipeDirectory)
                    .Select(recipe => recipe.CompatibilityPreset)
                    .Distinct(StringComparer.Ordinal))
                {
                    WinlatorSessionContract.CreateActivityLaunch(
                        request with { CompatibilityPreset = compatibilityPreset });
                    if (!string.IsNullOrEmpty(compatibilityPreset) &&
                        !activityContractSource.Contains(
                            "\"" + compatibilityPreset + "\"", StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "The companion Activity contract is missing compatibility preset '" +
                            compatibilityPreset + "'.");
                }

                using var envelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                var preset = envelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetWackyRacesNetwork)
                    throw new InvalidOperationException("Wacky Races preset was not serialized.");
                if (envelope.RootElement.GetProperty("profileConfigIni").GetString() != profileConfigIni)
                    throw new InvalidOperationException("The complete profile INI was not serialized losslessly.");

                request = request with
                {
                    CompatibilityPreset = WinlatorSessionContract.CompatibilityPresetChaseHq2
                };
                using var chaseEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = chaseEnvelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetChaseHq2)
                    throw new InvalidOperationException("Chase H.Q. 2 preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset = WinlatorSessionContract.CompatibilityPresetStarWars
                };
                using var starWarsEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = starWarsEnvelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetStarWars)
                    throw new InvalidOperationException("Star Wars preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetTaikoCustomResolution
                };
                using var taikoEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = taikoEnvelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetTaikoCustomResolution)
                    throw new InvalidOperationException("Taiko custom-resolution preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset = WinlatorSessionContract.CompatibilityPresetWmmtNoTerminal
                };
                using var wmmtEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = wmmtEnvelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetWmmtNoTerminal)
                    throw new InvalidOperationException("WMMT no-terminal preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetWmmt3YaCard
                };
                using var wmmt3CardEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = wmmt3CardEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetWmmt3YaCard)
                    throw new InvalidOperationException(
                        "WMMT3 YACardEmu preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetCxbxrWmmtYaCard
                };
                using var cxbxrWmmtCardEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = cxbxrWmmtCardEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetCxbxrWmmtYaCard)
                    throw new InvalidOperationException(
                        "CXBXR WMMT1/2 YACardEmu preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetCxbxrPerformance
                };
                using var cxbxrPerformanceEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = cxbxrPerformanceEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetCxbxrPerformance)
                    throw new InvalidOperationException(
                        "CXBXR performance preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetCxbxrChihiroType3
                };
                using var cxbxrType3Envelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = cxbxrType3Envelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetCxbxrChihiroType3)
                    throw new InvalidOperationException(
                        "CXBXR Chihiro Type-3 preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset = WinlatorSessionContract.CompatibilityPresetInitialD8
                };
                using var initialDEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = initialDEnvelope.RootElement.GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetInitialD8)
                    throw new InvalidOperationException("Initial D8 helper preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetInitialDTheArcade
                };
                using var initialDTheArcadeEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = initialDTheArcadeEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetInitialDTheArcade)
                    throw new InvalidOperationException(
                        "Initial D The Arcade preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetParkedEntrypoint
                };
                using var parkedEntrypointEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = parkedEntrypointEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetParkedEntrypoint)
                    throw new InvalidOperationException(
                        "Parked-entry-point preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetWineD3dParkedEntrypoint
                };
                using var wineD3dParkedEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = wineD3dParkedEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetWineD3dParkedEntrypoint)
                    throw new InvalidOperationException(
                        "WineD3D parked-entry-point preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetWineD3dRemoteThread
                };
                using var wineD3dRemoteThreadEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = wineD3dRemoteThreadEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetWineD3dRemoteThread)
                    throw new InvalidOperationException(
                        "WineD3D remote-thread preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetSharedJvsDualIo
                };
                using var sharedJvsDualIoEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = sharedJvsDualIoEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetSharedJvsDualIo)
                    throw new InvalidOperationException(
                        "Shared-state plus JVS preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetDirectTouchJvs
                };
                using var directTouchJvsEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = directTouchJvsEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset != WinlatorSessionContract.CompatibilityPresetDirectTouchJvs)
                    throw new InvalidOperationException(
                        "Direct-touch plus JVS preset was not serialized.");

                request = request with
                {
                    CompatibilityPreset =
                        WinlatorSessionContract.CompatibilityPresetGameWorkingDirectory
                };
                using var gameWorkingDirectoryEnvelope = JsonDocument.Parse(
                    WinlatorSessionContract.CreateActivityLaunch(request));
                preset = gameWorkingDirectoryEnvelope.RootElement
                    .GetProperty("compatibilityPreset").GetString();
                if (preset !=
                    WinlatorSessionContract.CompatibilityPresetGameWorkingDirectory)
                    throw new InvalidOperationException(
                        "Game-working-directory preset was not serialized.");

                RequireRejected(
                    () => WinlatorSessionContract.CreateActivityLaunch(
                        request with { ProfileConfigIni = string.Empty }),
                    "empty profile configuration");
                RequireRejected(
                    () => WinlatorSessionContract.CreateActivityLaunch(
                        request with { ProfileConfigIni = "[General]\nBad=\0\n" }),
                    "NUL profile configuration");
                RequireRejected(
                    () => WinlatorSessionContract.CreateActivityLaunch(
                        request with
                        {
                            ProfileConfigIni = new string(
                                'A', WinlatorSessionContract.MaximumProfileConfigBytes + 1)
                        }),
                    "oversized profile configuration");
                RequireRejected(
                    () => WinlatorSessionContract.CreateActivityLaunch(request, protocolVersion: 12),
                    "profile configuration downgrade");

                var validatedProfiles = 0;
                var skippedDevProfiles = 0;
                foreach (var candidate in Directory.EnumerateFiles(profileDirectory, "*.xml"))
                {
                    var profile = JoystickHelper.DeSerializeGameProfile(candidate, false);
                    if (profile == null && IsDevOnlyProfile(candidate))
                    {
                        skippedDevProfiles++;
                        continue;
                    }
                    if (profile == null)
                        throw new InvalidOperationException(
                            "Profile could not be loaded: " + Path.GetFileName(candidate));
                    WinlatorSessionContract.ValidateProfileConfigIni(
                        TeknoParrotIniWriter.BuildConfigIni(profile));
                    validatedProfiles++;
                }
                if (validatedProfiles == 0)
                    throw new InvalidOperationException("No stock profile INIs were validated.");

                var displayActivityPath = Path.Combine(
                    winlatorRoot,
                    "app", "app", "src", "main", "java", "com", "winlator",
                    "XServerDisplayActivity.java");
                var displayActivitySource = File.ReadAllText(displayActivityPath);
                var inputControlsManagerSource = File.ReadAllText(Path.Combine(
                    winlatorRoot,
                    "app", "app", "src", "main", "java", "com", "winlator",
                    "inputcontrols", "InputControlsManager.java"));
                var windowsPathBootstrapPath = Path.Combine(
                    repositoryRoot,
                    "Tools", "ProtonPipeHelper", "windows_path_bootstrap.c");
                var windowsPathBootstrapSource = File.ReadAllText(windowsPathBootstrapPath);
                var cxbxrDeploySource = File.ReadAllText(Path.Combine(
                    repositoryRoot, "Tools", "Deploy-AndroidCxbxrRuntime.ps1"));
                var androidPackageBuildSource = File.ReadAllText(Path.Combine(
                    repositoryRoot, "Tools", "Build-AndroidPackages.ps1"));
                var androidPackageInstallSource = File.ReadAllText(Path.Combine(
                    repositoryRoot, "Tools", "Install-AndroidPackages.ps1"));
                var androidUpdaterSource = File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Avalonia.Android",
                    "AndroidAppUpdater.cs"));
                var androidRuntimeUpdaterSource = File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Avalonia.Android",
                    "AndroidRuntimePackageUpdater.cs"));
                var sharedRuntimeAdapterSource = File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Common", "Updater",
                    "SharedOpenParrotArchiveAdapter.cs"));
                var buildWorkflowSource = File.ReadAllText(Path.Combine(
                    repositoryRoot, ".github", "workflows", "build.yml"));
                var winlatorWorkflowSource = File.ReadAllText(Path.Combine(
                    winlatorRoot, ".github", "workflows", "build-android.yml"));
                var androidUiPackageGateSource = File.ReadAllText(Path.Combine(
                    repositoryRoot, "Tools", "Test-AndroidUiPackage.ps1"));
                var winlatorGradleSource = File.ReadAllText(Path.Combine(
                    winlatorRoot, "app", "app", "build.gradle"));
                var diagnosticBackendSource = File.ReadAllText(Path.Combine(
                    winlatorRoot,
                    "app", "app", "src", "main", "java", "com", "winlator",
                    "teknoparrot", "TeknoParrotGuestDiagnosticBackend.java"));
                var bridgeServicePath = Path.Combine(
                    winlatorRoot,
                    "app", "teknoparrot-bridge", "src", "main", "java",
                    "com", "winlator", "teknoparrot", "TeknoParrotBridgeService.java");
                var bridgeServiceSource = File.ReadAllText(bridgeServicePath);
                var runtimePackageInstallerSource = File.ReadAllText(Path.Combine(
                    winlatorRoot,
                    "app", "teknoparrot-bridge", "src", "main", "java",
                    "com", "winlator", "teknoparrot",
                    "TeknoParrotRuntimePackageInstaller.java"));
                RequireContains(
                    displayActivitySource,
                    "if (\"centered\".equals(preparedWindowsLaunch.displayMode))",
                    "centered display-mode gate");
                RequireContains(
                    inputControlsManagerSource,
                    "LEGACY_GUNDAM_PROFILE_SHA256",
                    "known-old Gundam controls migration fingerprint");
                RequireContains(
                    inputControlsManagerSource,
                    "shouldRefreshBundledProfile(",
                    "editable built-in controls migration gate");
                RequireContains(
                    inputControlsManagerSource,
                    "profileId == LEGACY_GUNDAM_PROFILE_ID &&",
                    "Gundam controls migration scope");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_BORDERLESS_WINDOW\", \"1\")",
                    "borderless-window environment flag");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_CENTER_WINDOW\", \"1\")",
                    "center-window environment flag");
                RequireContains(
                    displayActivitySource,
                    "envVars.remove(\"TP_BORDERLESS_WINDOW\")",
                    "borderless-window non-centered cleanup");
                RequireContains(
                    displayActivitySource,
                    "envVars.remove(\"TP_CENTER_WINDOW\")",
                    "center-window non-centered cleanup");
                RequireContains(
                    displayActivitySource,
                    "preparedWindowsLaunch.controlsProfileId == 9008",
                    "Battle Gear 4 menu-removal scope");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_HIDE_WINDOW_MENU\", \"1\")",
                    "Battle Gear 4 menu-removal environment flag");
                RequireContains(
                    displayActivitySource,
                    "monitorPreparedGameProcess();",
                    "detached prepared-game lifetime monitor");
                RequireContains(
                    displayActivitySource,
                    "ProcessHelper.hasLiveGuestProcessName(\n                        immutableProcessMarker)",
                    "UID-scoped prepared-game process observation");
                RequireContains(
                    displayActivitySource,
                    "if (processMarker.length() > 15)",
                    "Linux process-name length normalization");
                RequireContains(
                    displayActivitySource,
                    "if (now - absentSince >= 2_000L) break;",
                    "prepared-game exit debounce");
                RequireDoesNotContain(
                    winlatorGradleSource,
                    "OpenParrotDirty.dll",
                    "embedded Dirty Drivin runtime staging");
                RequireContains(
                    diagnosticBackendSource,
                    "\"OpenParrotDirty.dll\"",
                    "updater-installed Dirty Drivin runtime allowlist");
                RequireContains(
                    displayActivitySource,
                    "isPreparedCxbxrLaunch() &&\n            " +
                    "isPreparedCxbxrPerformanceTitle())",
                    "CXBXR title-scoped performance-preset scope");
                RequireContains(
                    displayActivitySource,
                    "else if (isPreparedCxbxrPerformanceTitle())",
                    "CXBXR guarded performance-preset transition scope");
                RequireContains(
                    displayActivitySource,
                    "\" /dm 2 /df C:\\\\teknoparrot-cxbxr-kernel-debug.txt\"",
                    "CXBXR file-backed kernel diagnostic mode");
                RequireContains(
                    displayActivitySource,
                    "\" /render-trace\"",
                    "CXBXR low-volume renderer diagnostic mode");
                RequireContains(
                    displayActivitySource,
                    "normalized.endsWith(\"\\\\vc3.xbe\") ||\n" +
                    "                normalized.endsWith(\"\\\\vsg.xbe\")",
                    "CXBXR existing-import performance migration fallback");
                RequireContains(
                    displayActivitySource,
                    "\"CxbxDebugMode = \" + debugMode",
                    "CXBXR per-game GUI logging policy");
                RequireContains(
                    displayActivitySource,
                    "\"KrnlDebugMode = \" + debugMode",
                    "CXBXR per-game kernel logging policy");
                RequireContains(
                    displayActivitySource,
                    "\"LoggedModules = 0x00007000\\n\" +",
                    "CXBXR DirectSound/XAPI diagnostic modules");
                RequireContains(
                    displayActivitySource,
                    "\"LoggedModules = 0x00000780\\n\" +",
                    "CXBXR stream/XMO diagnostic modules");
                RequireContains(
                    displayActivitySource,
                    ": \"LoggedModules = 0x0\"",
                    "CXBXR production logging-module reset");
                RequireContains(
                    winlatorGradleSource,
                    "teknoparrot-thin-assets-v1",
                    "thin generated-assets namespace");
                RequireDoesNotContain(
                    winlatorGradleSource,
                    "cxbxr-export",
                    "embedded CXBXR runtime staging");
                RequireContains(
                    displayActivitySource,
                    "\"cxbxr-chihiro-type3\".equals(",
                    "CXBXR title-scoped Chihiro Type-3 identity");
                RequireContains(
                    displayActivitySource,
                    "\"mb_board_type\", boardIdentity",
                    "CXBXR media-board identity update");
                RequireContains(
                    displayActivitySource,
                    "\"mb_dimm_size\", boardIdentity",
                    "CXBXR DIMM-size identity update");
                RequireContains(
                    displayActivitySource,
                    "isPreparedCxbxrVirtuaCop3Title()",
                    "CXBXR VC3 title-scoped timing profile");
                RequireContains(
                    displayActivitySource,
                    "\"cooperative_self_suspend\",",
                    "CXBXR VC3 cooperative self-suspend policy");
                RequireContains(
                    displayActivitySource,
                    "\"ff_nv2a_blend_matrices\",\n" +
                    "                virtuaCop3 ? \"0\" : \"1\"",
                    "CXBXR VC3 title-scoped fixed-function skinning matrices");
                RequireContains(
                    displayActivitySource,
                    "\"scheduler_io_trace\", \"0\"",
                    "CXBXR production scheduler trace reset");
                RequireContains(
                    displayActivitySource,
                    "\"wmmt_device_poll_yield_ms\",\n" +
                    "                cxbxrWmmt ? \"1\" : \"0\"",
                    "CXBXR WMMT title-scoped device-poll yield");
                RequireContains(
                    displayActivitySource,
                    "\"wmmt_gamepad_init_bypass\",\n" +
                    "                cxbxrWmmt ? \"1\" : \"0\"",
                    "CXBXR WMMT title-scoped gamepad-enumeration completion");
                RequireContains(
                    displayActivitySource,
                    "appendPreparedCxbxrDebugMode(bootstrapCommand)",
                    "CXBXR bootstrap kernel-log argument");
                RequireContains(
                    displayActivitySource,
                    "appendPreparedCxbxrDebugMode(preparedCommand)",
                    "CXBXR direct kernel-log argument");
                RequireContains(
                    displayActivitySource,
                    "command.append(\" /dm 0\")",
                    "CXBXR quick-reboot kernel-log command-line policy");
                RequireContains(
                    windowsPathBootstrapSource,
                    "if (!process_in_tree)",
                    "Wine-reparented game window safety boundary");
                RequireContains(
                    displayActivitySource,
                    "\"TP_PRELAUNCH_EXECUTABLE\",\n                        cardDirectory + \"\\\\YACardEmu.exe\"",
                    "YACardEmu executable selection");
                RequireContains(
                    displayActivitySource,
                    "\"cxbxr-wmmt-yacard\".equals(",
                    "CXBXR WMMT1/2 card-service preset");
                RequireContains(
                    displayActivitySource,
                    "FileUtils.getDirname(preparedWindowsLaunch.executable) +",
                    "regional CXBXR YACardEmu directory selection");
                RequireContains(
                    cxbxrDeploySource,
                    "targetdevice = C1231LR",
                    "CXBXR WMMT card-reader model");
                RequireContains(
                    cxbxrDeploySource,
                    "serialbaud = 9600",
                    "CXBXR WMMT card-reader baud rate");
                RequireContains(
                    cxbxrDeploySource,
                    "serialparity = none",
                    "CXBXR WMMT card-reader parity");
                RequireContains(
                    androidPackageBuildSource,
                    "Require-ImmutableManifestDirectory $CxbxrRuntime",
                    "CXBXR immutable source-stage package gate");
                RequireContains(
                    androidPackageBuildSource,
                    "Require-ApkPayloadMatchesManifest",
                    "CXBXR embedded-payload hash gate");
                RequireContains(
                    androidPackageBuildSource,
                    "if ($EmbedRuntime)",
                    "explicit private runtime-embedding switch");
                RequireContains(
                    androidPackageBuildSource,
                    "Distributable Android APKs must not embed TeknoParrot",
                    "release runtime-embedding refusal");
                RequireContains(
                    androidPackageBuildSource,
                    "[string] $WinlatorSource = $env:TEKNOPARROT_WINLATOR_SOURCE",
                    "external Winlator release-source input");
                RequireContains(
                    androidPackageBuildSource,
                    "Require-SourceOnlyWinlatorInput $WinlatorSource",
                    "source-only Winlator release-input gate");
                RequireDoesNotContain(
                    buildWorkflowSource,
                    "ANDROID_WINLATOR_SOURCE",
                    "obsolete coupled Winlator source input");
                RequireContains(
                    winlatorWorkflowSource,
                    "tag=winlator",
                    "standalone Winlator rolling release");
                RequireContains(
                    winlatorWorkflowSource,
                    "TeknoParrotWinlator-${version}-android-arm64.apk",
                    "standalone Winlator versioned asset");
                RequireContains(
                    winlatorWorkflowSource,
                    "version_code=$((1000000 + GITHUB_RUN_NUMBER))",
                    "Winlator monotonic release version-code floor");
                RequireContains(
                    androidUiPackageGateSource,
                    "TeknoParrotUI APK contains forbidden emulator/core payloads",
                    "UI-only APK runtime-payload gate");
                RequireContains(
                    winlatorGradleSource,
                    "TEKNOPARROT_REPOSITORY_ROOT",
                    "external Winlator source repository-root override");
                RequireContains(
                    androidPackageBuildSource,
                    "OpenParrotWin32/OpenParrotDirty.dll",
                    "Dirty Drivin APK payload requirement");
                RequireContains(
                    androidPackageBuildSource,
                    "$cxbxrRuntimeWasExplicit -or -not $SkipCompanion",
                    "CXBXR exact-stage validation scope");
                RequireContains(
                    androidPackageBuildSource,
                    "no CXBXR stage was explicitly selected",
                    "CXBXR validation-only stage-drift diagnostic");
                RequireDoesNotContain(
                    winlatorGradleSource,
                    "TEKNOPARROT_EMBED_RUNTIME",
                    "private runtime embedding opt-in");
                RequireDoesNotContain(
                    winlatorGradleSource,
                    "stageTeknoParrotRuntime",
                    "private runtime staging task");
                RequireContains(
                    androidPackageBuildSource,
                    "'assets/teknoparrot/runtime/'",
                    "APK runtime-payload scan");
                RequireContains(
                    androidPackageBuildSource,
                    "embeds forbidden emulator/core runtime",
                    "thin APK fail-closed package gate");
                RequireContains(
                    androidUpdaterSource,
                    "CreateRuntimeComponent(\"OpenParrotWin32\")",
                    "Android x86 OpenParrot runtime update component");
                RequireContains(
                    androidUpdaterSource,
                    "CreateRuntimeComponent(\"OpenParrotx64\")",
                    "Android x64 OpenParrot runtime update component");
                RequireContains(
                    androidUpdaterSource,
                    "assetNameExact = packageId + \".zip\"",
                    "shared OpenParrot release asset selection");
                RequireDoesNotContain(
                    androidUpdaterSource,
                    "\"-android.zip\"",
                    "obsolete Android-only OpenParrot archive");
                RequireContains(
                    androidRuntimeUpdaterSource,
                    "TryParseSha256(asset.digest",
                    "authoritative Android runtime archive digest");
                RequireContains(
                    androidRuntimeUpdaterSource,
                    "asset.size <= 0",
                    "authoritative Android runtime archive size");
                RequireContains(
                    androidRuntimeUpdaterSource,
                    "SharedOpenParrotArchiveAdapter.CreateInstallEnvelope",
                    "local shared-archive Winlator adaptation");
                RequireContains(
                    sharedRuntimeAdapterSource,
                    "\"payload/\" + contract.RuntimeRoot + \"/\" + file.Name",
                    "private-cache Winlator payload envelope");
                RequireContains(
                    androidRuntimeUpdaterSource,
                    "InstallWinlatorRuntimePackageTransaction",
                    "runtime archive descriptor transfer");
                RequireContains(
                    runtimePackageInstallerSource,
                    "rejectDuplicateOrUnexpectedEntries",
                    "Winlator duplicate/unlisted ZIP-entry rejection");
                RequireContains(
                    runtimePackageInstallerSource,
                    "MessageDigest.isEqual(expectedDigest, hash.digest())",
                    "Winlator outer runtime archive verification");
                RequireContains(
                    runtimePackageInstallerSource,
                    "replaceRoots(runtimeRoot, staging, backup, allowedRoots)",
                    "package-root staging and rollback install");
                RequireContains(
                    runtimePackageInstallerSource,
                    "rollbackRoots(runtimeRoot, backup, allowedRoots, error)",
                    "runtime marker-failure rollback");
                foreach (var protectedProcess in new[]
                {
                    "com.armsx2",
                    "com.teknoparrot.ui",
                    "com.teknoparrot.winlator"
                })
                    RequireContains(
                        androidPackageInstallSource,
                        $"Name = '{protectedProcess}'",
                        $"Android installer protected-process gate for {protectedProcess}");
                RequireContains(
                    androidPackageInstallSource,
                    "Refusing to update Android packages while protected session",
                    "Android installer active-session refusal");
                RequireContains(
                    androidUpdaterSource,
                    "assetNameMarker = \"-android-arm64.apk\"",
                    "Android updater ARM64 package marker");
                RequireContains(
                    androidUpdaterSource,
                    "ReleaseTag = \"TeknoParrotUI-android\"",
                    "independent Android rolling update channel");
                RequireContains(
                    androidUpdaterSource,
                    "\"winlator\",\n                \"ReaverTeknoGods\"",
                    "standalone Winlator updater repository");
                RequireContains(
                    buildWorkflowSource,
                    "\"dist/TeknoParrotUi-$full-android-arm64.apk\"",
                    "versioned Android UI release asset name");
                RequireDoesNotContain(
                    buildWorkflowSource,
                    "dist/TeknoParrotWinlator-",
                    "coupled Android companion release artifact");
                RequireContains(
                    buildWorkflowSource,
                    "name: Remove stale desktop assets",
                    "desktop-only rolling-release cleanup");
                RequireContains(
                    buildWorkflowSource,
                    "name: Remove stale Android assets",
                    "Android-only rolling-release cleanup");
                RequireContains(
                    buildWorkflowSource,
                    "tag: TeknoParrotUI-android",
                    "independent Android rolling release tag");
                RequireContains(
                    buildWorkflowSource,
                    "group: teknoparrotui-android-release",
                    "serialized Android-only release publication");
                RequireContains(
                    buildWorkflowSource,
                    "TeknoParrotUi-*-android-arm64.apk)",
                    "Android UI asset cleanup pattern");
                RequireContains(
                    buildWorkflowSource,
                    "group: teknoparrotui-rolling-release",
                    "serialized rolling-release publication");
                RequireDoesNotContain(
                    buildWorkflowSource,
                    "removeArtifacts: true",
                    "cross-platform rolling-release asset deletion");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_PRELAUNCH_READY_PIPE\", \"\\\\\\\\.\\\\pipe\\\\YACardEmu\")",
                    "WMMT3 YACardEmu readiness pipe");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_PRELAUNCH_TERMINATE_WITH_GAME\", \"1\")",
                    "WMMT3 card-service lifetime scope");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_PRELAUNCH_ARGUMENTS\", \"-t -f\")",
                    "WMMT3 YACardEmu debug-only trace logging");
                RequireContains(
                    windowsPathBootstrapSource,
                    "environment_flag_enabled(L\"TP_PRELAUNCH_DIRECT\")",
                    "direct persistent-helper prelaunch mode");
                RequireContains(
                    windowsPathBootstrapSource,
                    "WaitNamedPipeW(prelaunch_ready_pipe, 100)",
                    "prelaunch named-pipe readiness gate");
                RequireContains(
                    windowsPathBootstrapSource,
                    "environment_flag_enabled(L\"TP_PRELAUNCH_TERMINATE_WITH_GAME\")",
                    "prelaunch helper teardown policy");
                RequireContains(
                    windowsPathBootstrapSource,
                    "TP_PRELAUNCH_ARGUMENTS",
                    "direct prelaunch helper argument forwarding");
                RequireContains(
                    displayActivitySource,
                    "envVars.put(\"TP_HIDE_LAUNCH_CONSOLE\", \"1\")",
                    "production launch-console hiding");
                RequireContains(
                    displayActivitySource,
                    "envVars.remove(\"TP_HIDE_LAUNCH_CONSOLE\")",
                    "debug launch-console restoration");
                RequireContains(
                    bridgeServiceSource,
                    "request.flags == SessionContract.SESSION_FLAG_PRODUCTION",
                    "production-only companion process recycling");
                RequireContains(
                    bridgeServiceSource,
                    "if (productionProcessExitPending)",
                    "process recycling after TPUI unbind");
                RequireContains(
                    bridgeServiceSource,
                    "cancelProductionProcessExitLocked();",
                    "rapid relaunch process-recycle cancellation");
                RequireContains(
                    bridgeServiceSource,
                    "android.os.Process.killProcess(android.os.Process.myPid())",
                    "complete managed Wine graphics-memory reclamation");

                var gameSessionServicePath = Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Avalonia.Android", "GameSessionService.cs");
                var gameSessionServiceSource = File.ReadAllText(gameSessionServicePath);
                RequireContains(
                    gameSessionServiceSource,
                    "connection?.MarkTerminal();",
                    "terminal game-session binding guard");
                RequireContains(
                    gameSessionServiceSource,
                    "if (Volatile.Read(ref _terminal) == 0)",
                    "late disconnect suppression");
                RequireContains(
                    gameSessionServiceSource,
                    "PublishTerminalStatus(\n" +
                    "                            $\"state=ended;detail=Winlator game closed",
                    "clean remote-exit terminal status");

                var androidGameSessionPath = Path.Combine(
                    repositoryRoot,
                    "TeknoParrotUi.Avalonia.Android", "AndroidWinlatorGameSession.cs");
                var androidGameSessionSource = File.ReadAllText(androidGameSessionPath);
                RequireContains(
                    androidGameSessionSource,
                    "else if (status.StartsWith(\"state=stopped\", StringComparison.Ordinal))\n" +
                    "            // state=stopped is emitted only for an explicit user stop.",
                    "notification Stop clean-exit classification");
                RequireContains(
                    androidGameSessionSource,
                    "            Complete(0, \"Game stopped\");",
                    "notification Stop zero exit code");

                var libraryViewPath = Path.Combine(
                    repositoryRoot, "TeknoParrotUi.Avalonia", "Views", "LibraryView.axaml");
                var libraryViewSource = File.ReadAllText(libraryViewPath);
                RequireContains(
                    libraryViewSource,
                    "<Grid Name=\"DetailsPanel\"",
                    "responsive library details panel");
                RequireContains(
                    libraryViewSource,
                    "<StackPanel Name=\"PrimaryActions\" Grid.Row=\"1\"",
                    "always-visible library launch actions");

                var libraryViewCodePath = Path.Combine(
                    repositoryRoot, "TeknoParrotUi.Avalonia", "Views", "LibraryView.axaml.cs");
                var libraryViewCodeSource = File.ReadAllText(libraryViewCodePath);
                RequireContains(
                    libraryViewCodeSource,
                    "Grid.SetColumn(DetailsPanel, 0);",
                    "narrow-screen library details placement");

                Console.WriteLine(
                    $"Android Winlator launch, compatibility, and borderless-window contract: PASS " +
                    $"({validatedProfiles} stock profile INIs, " +
                    $"{skippedDevProfiles} development-only skipped)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Android Winlator launch contract: FAIL");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static bool IsDevOnlyProfile(string profilePath)
        {
            var document = XDocument.Load(profilePath, LoadOptions.None);
            return bool.TryParse(
                       document.Root?.Element("DevOnly")?.Value,
                       out var devOnly) &&
                   devOnly;
        }

        private static void RequireRejected(Action action, string scenario)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }
            throw new InvalidOperationException(
                "The Android Winlator contract accepted " + scenario + '.');
        }

        private static void RequireContains(string source, string expected, string scenario)
        {
            if (!source.ReplaceLineEndings("\n").Contains(
                    expected.ReplaceLineEndings("\n"),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The Android Winlator source is missing the " + scenario + '.');
        }

        private static void RequireDoesNotContain(
            string source,
            string forbidden,
            string scenario)
        {
            if (source.ReplaceLineEndings("\n").Contains(
                    forbidden.ReplaceLineEndings("\n"),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The Android Winlator source still contains the " + scenario + '.');
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "TeknoParrotUI.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("TeknoParrotUI repository root was not found.");
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
                    return Path.GetFullPath(candidate);
            }

            throw new DirectoryNotFoundException(
                "The standalone TeknoParrot Winlator checkout was not found. " +
                "Set TEKNOPARROT_WINLATOR_SOURCE or clone it beside TeknoParrotUI.");
        }
    }
}
