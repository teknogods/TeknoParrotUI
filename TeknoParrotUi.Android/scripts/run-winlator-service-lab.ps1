[CmdletBinding()]
param(
    [string]$DeviceSerial,
    [ValidateSet("android-x64", "android-arm64")]
    [string]$RuntimeIdentifier = "android-x64",
    [ValidateSet("Stub", "Full")]
    [string]$WinlatorVariant = "Stub",
    [switch]$GuestDiagnostic,
    [int]$ContainerId = 1,
    [string]$OpenParrotRuntime = $env:TEKNOPARROT_OPENPARROT_WIN32,
    [string]$TeknoParrotCoreRuntime = $env:TEKNOPARROT_CORE_WIN32,
    [switch]$SkipBuild,
    [switch]$SkipInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$toolchain = Join-Path $env:USERPROFILE "android-toolchain"
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
$selector = Join-Path $toolchain "dotnet-10-select"
$sdk = Join-Path $toolchain "sdk"
$jdk = Join-Path $toolchain "jdk-17"
$adb = Join-Path $sdk "platform-tools\adb.exe"
$apksigner = Join-Path $sdk "build-tools\34.0.0\apksigner.bat"
$zipalign = Join-Path $sdk "build-tools\36.0.0\zipalign.exe"
$readelf = Join-Path $sdk "ndk\24.0.8215888\toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-readelf.exe"
$keystore = Join-Path $env:LOCALAPPDATA "Xamarin\Mono for Android\debug.keystore"
$winlatorApp = Join-Path $repoRoot "WinlatorFork\app"

foreach ($required in @($dotnet, $sdk, $jdk, $adb, $apksigner, $zipalign, $readelf, $keystore,
        (Join-Path $winlatorApp "gradlew.bat"))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing Winlator bridge-lab component: $required"
    }
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:JAVA_HOME = $jdk
$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk
$env:TEKNOPARROT_DEBUG_KEYSTORE = $keystore
$env:PATH = "$env:DOTNET_ROOT;$jdk\bin;$env:PATH"

$connectedDevices = @(
    & $adb devices | ForEach-Object {
        if ($_ -match '^(\S+)\s+device(?:\s|$)') {
            $Matches[1]
        }
    }
)
if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
    if ($connectedDevices.Count -ne 1) {
        $deviceList = if ($connectedDevices.Count -eq 0) {
            "none"
        }
        else {
            $connectedDevices -join ", "
        }
        throw "Expected exactly one authorized Android device, found: $deviceList. Pass -DeviceSerial explicitly."
    }
    $DeviceSerial = $connectedDevices[0]
}
elseif ($DeviceSerial -notin $connectedDevices) {
    throw "Android device '$DeviceSerial' is not connected and authorized."
}

$adbTarget = @("-s", $DeviceSerial)
$state = (& $adb @adbTarget get-state 2>$null | Out-String).Trim()
$boot = if ($state -eq "device") {
    (& $adb @adbTarget shell getprop sys.boot_completed 2>$null | Out-String).Trim()
} else {
    ""
}
if ($boot -ne "1") {
    throw "Android device '$DeviceSerial' is not fully booted."
}

$deviceAbis = (& $adb @adbTarget shell getprop ro.product.cpu.abilist | Out-String).Trim()
$requiredAbi = if ($RuntimeIdentifier -eq "android-arm64") { "arm64-v8a" } else { "x86_64" }
if (($deviceAbis -split ',') -notcontains $requiredAbi) {
    throw "Device '$DeviceSerial' reports ABI '$deviceAbis', which does not support $RuntimeIdentifier."
}
if ($WinlatorVariant -eq "Full" -and $RuntimeIdentifier -ne "android-arm64") {
    throw "The full pinned Winlator APK is ARM64-only. Use -RuntimeIdentifier android-arm64."
}
if ($GuestDiagnostic -and ($WinlatorVariant -ne "Full" -or $RuntimeIdentifier -ne "android-arm64")) {
    throw "The service-controlled Windows guest diagnostic requires full ARM64 Winlator."
}
if ($GuestDiagnostic -and $ContainerId -le 0) {
    throw "The service-controlled Windows guest diagnostic requires a positive container id."
}

if ($WinlatorVariant -eq "Full" -and -not $SkipBuild) {
    if ([string]::IsNullOrWhiteSpace($OpenParrotRuntime) -or
        -not (Test-Path -LiteralPath $OpenParrotRuntime -PathType Container)) {
        throw "Full Winlator builds require -OpenParrotRuntime (or TEKNOPARROT_OPENPARROT_WIN32)."
    }
    if ([string]::IsNullOrWhiteSpace($TeknoParrotCoreRuntime) -or
        -not (Test-Path -LiteralPath $TeknoParrotCoreRuntime -PathType Container)) {
        throw "Full Winlator builds require -TeknoParrotCoreRuntime (or TEKNOPARROT_CORE_WIN32)."
    }
    foreach ($file in @(
            (Join-Path $OpenParrotRuntime "OpenParrotLoader.exe"),
            (Join-Path $OpenParrotRuntime "OpenParrot.dll"),
            (Join-Path $TeknoParrotCoreRuntime "BudgieLoader.exe"),
            (Join-Path $TeknoParrotCoreRuntime "TeknoParrot.dll"))) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Full Winlator runtime file is missing: $file"
        }
    }
    $env:TEKNOPARROT_OPENPARROT_WIN32 = (Resolve-Path -LiteralPath $OpenParrotRuntime).Path
    $env:TEKNOPARROT_CORE_WIN32 = (Resolve-Path -LiteralPath $TeknoParrotCoreRuntime).Path
}

Write-Host "Target device: $DeviceSerial ($deviceAbis)"
Write-Host "Lab mode: $RuntimeIdentifier with Winlator $WinlatorVariant"

$hostProject = Join-Path $repoRoot "TeknoParrotUi.Avalonia.Android\TeknoParrotUi.Avalonia.Android.csproj"
$buildDirectory = if (Test-Path -LiteralPath $selector) { $selector } else { $repoRoot }

if (-not $SkipBuild) {
    Write-Host "Building the $RuntimeIdentifier TeknoParrot Android host..."
    Push-Location $buildDirectory
    try {
        & $dotnet build $hostProject -c Debug -f net10.0-android -r $RuntimeIdentifier `
            -p:EmbedAssembliesIntoApk=true `
            "-p:AndroidSdkDirectory=$sdk" `
            "-p:JavaSdkDirectory=$jdk" `
            --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "TeknoParrot Android host build failed."
        }
    }
    finally {
        Pop-Location
    }

    $gradleTask = if ($WinlatorVariant -eq "Full") {
        ":app:assembleDebug"
    }
    else {
        ":bridge-stub:assembleDebug"
    }
    Write-Host "Building Winlator $WinlatorVariant through $gradleTask..."
    Push-Location $winlatorApp
    try {
        & .\gradlew.bat $gradleTask --no-daemon
        if ($LASTEXITCODE -ne 0) {
            throw "Winlator $WinlatorVariant build failed."
        }
    }
    finally {
        Pop-Location
    }
}

$hostApk = Join-Path $repoRoot `
    "TeknoParrotUi.Avalonia.Android\bin\Debug\net10.0-android\$RuntimeIdentifier\com.teknoparrot.ui-Signed.apk"
$winlatorApk = if ($WinlatorVariant -eq "Full") {
    Join-Path $winlatorApp "app\build\outputs\apk\debug\app-debug.apk"
}
else {
    Join-Path $winlatorApp "bridge-stub\build\outputs\apk\debug\bridge-stub-debug.apk"
}

foreach ($apk in @($hostApk, $winlatorApk)) {
    if (-not (Test-Path -LiteralPath $apk)) {
        throw "Expected signed APK was not produced: $apk"
    }
}

if ($WinlatorVariant -eq "Full") {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($winlatorApk)
    try {
        $entries = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]($archive.Entries.FullName),
            [System.StringComparer]::Ordinal)
        foreach ($requiredAsset in @(
                "assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotLoader.exe",
                "assets/teknoparrot/runtime/OpenParrotWin32/OpenParrot.dll",
                "assets/teknoparrot/runtime/TeknoParrot/BudgieLoader.exe",
                "assets/teknoparrot/runtime/TeknoParrot/TeknoParrot.dll")) {
            if (-not $entries.Contains($requiredAsset)) {
                throw "Full Winlator APK is missing the managed runtime asset: $requiredAsset"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    & $zipalign -c -P 16 4 $winlatorApk
    if ($LASTEXITCODE -ne 0) {
        throw "The full Winlator APK does not satisfy 16 KB ZIP alignment: $winlatorApk"
    }

    $nativeDirectory = Join-Path $winlatorApp `
        "app\build\intermediates\stripped_native_libs\debug\out\lib\arm64-v8a"
    if (Test-Path -LiteralPath $nativeDirectory) {
        $nativeLibraries = @(Get-ChildItem -LiteralPath $nativeDirectory -Filter "*.so" -File)
        $badAlignment = [System.Collections.Generic.List[string]]::new()
        foreach ($library in $nativeLibraries) {
            $loadSegments = @(& $readelf -lW $library.FullName |
                Where-Object { $_ -match '^\s*LOAD\s' })
            $alignments = @($loadSegments | ForEach-Object {
                    ($_ -split '\s+')[-1].ToLowerInvariant()
                } | Sort-Object -Unique)
            $alignmentBytes = @($alignments | ForEach-Object {
                    [Convert]::ToInt64($_.Substring(2), 16)
                })
            if ($alignmentBytes.Count -eq 0 -or
                ($alignmentBytes | Measure-Object -Minimum).Minimum -lt 16384) {
                $badAlignment.Add($library.Name)
            }
        }

        $compatibleCount = $nativeLibraries.Count - $badAlignment.Count
        Write-Host "Winlator 16 KB ELF audit: $compatibleCount/$($nativeLibraries.Count) compatible."
        if ($badAlignment.Count -gt 0) {
            Write-Warning ("Native libraries below 16 KB PT_LOAD alignment: " +
                ($badAlignment -join ", "))
        }
    }
}

function Get-SigningDigest([string]$Apk) {
    $certificate = (& $apksigner verify --print-certs $Apk | Out-String)
    $match = [regex]::Match($certificate, 'SHA-256 digest:\s*([0-9a-f]+)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        throw "Could not read signing certificate from $Apk."
    }
    return $match.Groups[1].Value.ToLowerInvariant()
}

$hostDigest = Get-SigningDigest $hostApk
$winlatorDigest = Get-SigningDigest $winlatorApk
if ($hostDigest -ne $winlatorDigest) {
    throw "Signature permission cannot work: host and Winlator certificates differ."
}
Write-Host "Shared signing certificate: $hostDigest"

if (-not $SkipInstall) {
    foreach ($apk in @($winlatorApk, $hostApk)) {
        Write-Host "Installing $(Split-Path -Leaf $apk) on $DeviceSerial..."
        & $adb @adbTarget install --no-streaming -r $apk
        if ($LASTEXITCODE -ne 0) {
            throw "adb install failed for $apk on $DeviceSerial."
        }
    }
}

& $adb @adbTarget shell am force-stop com.teknoparrot.ui | Out-Null
$resolved = & $adb @adbTarget shell cmd package resolve-activity --brief `
    -a android.intent.action.MAIN `
    -c android.intent.category.LAUNCHER `
    com.teknoparrot.ui
$activity = ($resolved | Where-Object { $_ -match "/" } | Select-Object -Last 1).Trim()
if ([string]::IsNullOrWhiteSpace($activity)) {
    throw "Could not resolve the TeknoParrot launcher Activity."
}

Write-Host "Launching the private debug probe through $activity..."
if ($GuestDiagnostic) {
    & $adb @adbTarget shell am start -n $activity `
        --ez com.teknoparrot.ui.RUN_WINLATOR_GUEST_BRIDGE_PROBE true `
        --ei com.teknoparrot.ui.WINLATOR_GUEST_CONTAINER_ID $ContainerId | Out-Host
}
else {
    & $adb @adbTarget shell am start -n $activity `
        --ez com.teknoparrot.ui.RUN_WINLATOR_BRIDGE_PROBE true | Out-Host
}
if ($LASTEXITCODE -ne 0) {
    throw "The Winlator bridge probe could not be launched."
}

$passMarker = if ($GuestDiagnostic) {
    "WINLATOR WINDOWS GUEST SERVICE TEST PASSED"
}
else {
    "WINLATOR SERVICE SMOKE TEST PASSED"
}
$failureMarker = if ($GuestDiagnostic) {
    "WINLATOR WINDOWS GUEST SERVICE TEST FAILED"
}
else {
    "WINLATOR SERVICE SMOKE TEST FAILED"
}

for ($attempt = 1; $attempt -le 45; $attempt++) {
    Start-Sleep -Seconds 1
    $dump = (& $adb @adbTarget exec-out uiautomator dump /dev/tty 2>$null | Out-String)
    if ($dump.Contains($passMarker, [StringComparison]::Ordinal)) {
        $match = [regex]::Match($dump, ('text="(' + [regex]::Escape($passMarker) + '.*?)"'))
        $report = if ($match.Success) {
            [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value).Replace("&#10;", "`n")
        } else {
            $passMarker
        }
        Write-Host "`n$report"

        if ($WinlatorVariant -eq "Full" -and -not $GuestDiagnostic) {
            $sessionDirectories = @(
                & $adb @adbTarget shell run-as com.teknoparrot.winlator find `
                    files/teknoparrot/sessions -mindepth 1 -maxdepth 1 -type d 2>$null
            )
            if ($LASTEXITCODE -ne 0) {
                throw "Could not inspect Winlator's private bridge session directory."
            }
            if ($sessionDirectories.Count -ne 0) {
                throw "The 100-cycle bridge probe left $($sessionDirectories.Count) Winlator session director$(if ($sessionDirectories.Count -eq 1) { 'y' } else { 'ies' })."
            }
            Write-Host "Winlator session directory cleanup: PASS"
        }

        exit 0
    }
    if ($dump.Contains($failureMarker, [StringComparison]::Ordinal)) {
        throw "The on-device Winlator service probe reported failure: $dump"
    }
}

throw "Timed out waiting for the Winlator service result on $DeviceSerial. Inspect adb logcat and the device screen."
