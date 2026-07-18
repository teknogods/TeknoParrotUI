param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
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

foreach ($required in @($dotnet, $sdk, $jdk, $adb)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing Android build component: $required"
    }
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$env:DOTNET_ROOT;$jdk\bin;$env:PATH"
$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk
$env:JAVA_HOME = $jdk

$state = (& $adb get-state 2>$null | Out-String).Trim()
$boot = if ($state -eq "device") {
    (& $adb shell getprop sys.boot_completed 2>$null | Out-String).Trim()
} else {
    ""
}
if ($boot -ne "1") {
    throw "No fully booted Android device. Run .\run-emulator.ps1 -Headless first."
}

$hostProject = Join-Path $repoRoot "TeknoParrotUi.Avalonia.Android\TeknoParrotUi.Avalonia.Android.csproj"
$probeProject = Join-Path $repoRoot "TeknoParrotUi.BridgeProbe.Android\TeknoParrotUi.BridgeProbe.Android.csproj"
$buildDirectory = if (Test-Path -LiteralPath $selector) { $selector } else { $repoRoot }

function Build-InstallableApk([string]$Project) {
    & $dotnet build $Project -c $Configuration -f net10.0-android -r android-x64 `
        -p:EmbedAssembliesIntoApk=true `
        "-p:AndroidSdkDirectory=$sdk" `
        "-p:JavaSdkDirectory=$jdk" `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Android build failed for $Project."
    }
}

Push-Location $buildDirectory
try {
    Write-Host "Building the TeknoParrot bridge host..."
    Build-InstallableApk $hostProject
    Write-Host "Building the cross-process bridge probe..."
    Build-InstallableApk $probeProject
}
finally {
    Pop-Location
}

$hostApk = Join-Path $repoRoot "TeknoParrotUi.Avalonia.Android\bin\$Configuration\net10.0-android\android-x64\com.teknoparrot.ui-Signed.apk"
$probeApk = Join-Path $repoRoot "TeknoParrotUi.BridgeProbe.Android\bin\$Configuration\net10.0-android\android-x64\com.teknoparrot.bridgeprobe-Signed.apk"

foreach ($apk in @($hostApk, $probeApk)) {
    if (-not (Test-Path -LiteralPath $apk)) {
        throw "Expected signed APK was not produced: $apk"
    }
    Write-Host "Installing $(Split-Path -Leaf $apk)..."
    & $adb install -r $apk
    if ($LASTEXITCODE -ne 0) {
        throw "adb install failed for $apk."
    }
}

& $adb shell am force-stop com.teknoparrot.bridgeprobe | Out-Null
& $adb shell am force-stop com.teknoparrot.ui | Out-Null
$resolved = & $adb shell cmd package resolve-activity --brief com.teknoparrot.bridgeprobe
$activity = ($resolved | Where-Object { $_ -match "/" } | Select-Object -Last 1).Trim()
if ([string]::IsNullOrWhiteSpace($activity)) {
    throw "Could not resolve the bridge-probe launcher Activity."
}

Write-Host "Launching $activity..."
& $adb shell am start -W -n $activity | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "The bridge-probe Activity could not be launched."
}

for ($attempt = 1; $attempt -le 30; $attempt++) {
    Start-Sleep -Seconds 1
    $dump = (& $adb exec-out uiautomator dump /dev/tty 2>$null | Out-String)
    if ($dump.Contains("BRIDGE SMOKE TEST PASSED", [StringComparison]::Ordinal)) {
        $match = [regex]::Match($dump, 'text="(BRIDGE SMOKE TEST PASSED.*?)"')
        $report = if ($match.Success) {
            [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value).Replace("&#10;", "`n")
        } else {
            "BRIDGE SMOKE TEST PASSED"
        }
        Write-Host "`n$report"
        exit 0
    }
    if ($dump.Contains("BRIDGE SMOKE TEST FAILED", [StringComparison]::Ordinal)) {
        throw "The on-device bridge probe reported failure: $dump"
    }
}

throw "Timed out waiting for the bridge-probe result. Inspect adb logcat and the emulator screen."
