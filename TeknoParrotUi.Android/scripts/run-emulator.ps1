param(
    [switch]$Headless,
    [string]$AvdName = "tp_bridge_api34"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolchain = Join-Path $env:USERPROFILE "android-toolchain"
$sdk = Join-Path $toolchain "sdk"
$jdk = Join-Path $toolchain "jdk-17"
$adb = Join-Path $sdk "platform-tools\adb.exe"
$emulator = Join-Path $sdk "emulator\emulator.exe"
$avdManager = Join-Path $sdk "cmdline-tools\latest\bin\avdmanager.bat"
$systemImage = Join-Path $sdk "system-images\android-34\google_apis\x86_64"
$avdDirectory = Join-Path $env:USERPROFILE ".android\avd\$AvdName.avd"

foreach ($required in @($adb, $emulator, $avdManager, $systemImage, $jdk)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing Android toolchain component: $required"
    }
}

$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk
$env:JAVA_HOME = $jdk
$env:PATH = "$jdk\bin;$env:PATH"

if (-not (Test-Path -LiteralPath $avdDirectory)) {
    Write-Host "Creating AVD '$AvdName' (Pixel 5, API 34, x86_64)..."
    "no" | & $avdManager create avd -n $AvdName `
        -k "system-images;android-34;google_apis;x86_64" --device "pixel_5"
    if ($LASTEXITCODE -ne 0) {
        throw "avdmanager failed with exit code $LASTEXITCODE."
    }
}

$state = (& $adb get-state 2>$null | Out-String).Trim()
$boot = if ($state -eq "device") {
    (& $adb shell getprop sys.boot_completed 2>$null | Out-String).Trim()
} else {
    ""
}

if ($boot -eq "1") {
    Write-Host "An Android device is already booted ($state); reusing it."
    exit 0
}

$arguments = @(
    "-avd", $AvdName,
    "-no-audio",
    "-no-boot-anim",
    "-no-snapshot",
    "-no-metrics",
    "-gpu", "swiftshader_indirect"
)

if ($Headless) {
    $arguments += "-no-window"
    $process = Start-Process -FilePath $emulator -ArgumentList $arguments `
        -WindowStyle Hidden -PassThru
} else {
    $process = Start-Process -FilePath $emulator -ArgumentList $arguments -PassThru
}

Write-Host "Started $AvdName (PID $($process.Id)); waiting for Android first boot..."
for ($attempt = 1; $attempt -le 120; $attempt++) {
    $state = (& $adb get-state 2>$null | Out-String).Trim()
    $boot = if ($state -eq "device") {
        (& $adb shell getprop sys.boot_completed 2>$null | Out-String).Trim()
    } else {
        ""
    }

    if ($boot -eq "1") {
        Write-Host "Android boot completed: $state"
        exit 0
    }

    if (($attempt % 10) -eq 0) {
        Write-Host "Still waiting (attempt $attempt/120, state '$state')..."
    }
    Start-Sleep -Seconds 2
}

throw "The emulator did not finish booting within four minutes."
