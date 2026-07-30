[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9._:-]*$')]
    [string] $DeviceSerial,
    [string] $AdbPath,
    [string] $CompanionApk,
    [string] $UiApk,
    [switch] $SkipCompanion,
    [switch] $SkipUi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $CompanionApk) {
    $CompanionApk = Join-Path $repositoryRoot (
        'WinlatorFork\app\app\build\outputs\apk\debug\app-debug.apk')
}
if (-not $UiApk) {
    $UiApk = Join-Path $repositoryRoot (
        'TeknoParrotUi.Avalonia.Android\bin\Release\net10.0-android\android-arm64\' +
        'com.teknoparrot.ui-Signed.apk')
}

$adbCandidates = @(
    $AdbPath,
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk-platform37\platform-tools\adb.exe'),
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$adb = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $adb) {
    throw 'adb.exe was not found in any configured Android SDK.'
}

function Invoke-Adb([string[]] $Arguments, [string] $Purpose) {
    $output = @(& $adb @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "$Purpose failed with exit code $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

$deviceLines = @(Invoke-Adb -Arguments @('devices', '-l') -Purpose 'ADB device query')
$onlineDevices = @($deviceLines | ForEach-Object {
    if ($_ -match '^(\S+)\s+device(?:\s|$)') { $matches[1] }
})
if (-not $DeviceSerial) {
    if ($onlineDevices.Count -ne 1) {
        throw "Exactly one online ADB device is required; found $($onlineDevices.Count)."
    }
    $DeviceSerial = $onlineDevices[0]
}
elseif ($onlineDevices -notcontains $DeviceSerial) {
    throw "ADB device '$DeviceSerial' is not online."
}

function Get-AndroidProcessIds([string] $ProcessName) {
    $output = @(& $adb -s $DeviceSerial shell pidof $ProcessName 2>$null)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and $exitCode -ne 1) {
        throw "Could not query Android process '$ProcessName' (exit code $exitCode)."
    }
    return @(
        ($output -join ' ').Trim() -split '\s+' |
            Where-Object { $_ -match '^\d+$' })
}

# Updating either APK terminates its Android process. TPUI owns the input
# service while a game is active, and the companion owns Wine/XServer state;
# replacing either one mid-session can strand helpers or lose controls. Keep
# Tekno2x6 sessions protected for the same reason. Keep the legacy ARMSX2
# process in the gate as well so an older installation cannot be interrupted
# while the uniquely named companion is installed alongside it.
$protectedProcesses = @(
    [pscustomobject]@{ Name = 'com.teknogods.tekno2x6'; Purpose = 'Tekno2x6' },
    [pscustomobject]@{ Name = 'com.armsx2'; Purpose = 'Legacy ARMSX2/PCSX2X6' },
    [pscustomobject]@{ Name = 'com.teknoparrot.ui'; Purpose = 'TeknoParrotUI' },
    [pscustomobject]@{
        Name = 'com.teknoparrot.winlator'
        Purpose = 'TeknoParrot Winlator companion'
    }
)
$activeProcesses = @(
    foreach ($process in $protectedProcesses) {
        $processIds = @(Get-AndroidProcessIds $process.Name)
        if ($processIds.Count -ne 0) {
            [pscustomobject]@{
                Name = $process.Name
                Purpose = $process.Purpose
                ProcessIds = $processIds -join ','
            }
        }
    })
if ($activeProcesses.Count -ne 0) {
    $details = $activeProcesses |
        ForEach-Object { "$($_.Purpose) ($($_.Name)): PID $($_.ProcessIds)" }
    throw (
        "Refusing to update Android packages while protected session " +
        "processes are active. Close them normally and retry.`n" +
        ($details -join [Environment]::NewLine))
}

function Get-PackageSnapshot([string] $PackageName) {
    # Android 16's `pm path` exits with code 1 when a package is absent. Check
    # the package list first so a normal first installation is not an error.
    $packageList = @(Invoke-Adb -Arguments @(
        '-s', $DeviceSerial, 'shell', 'pm', 'list', 'packages', '--user', '0',
        $PackageName) -Purpose "$PackageName package-list lookup")
    if (-not ($packageList | Where-Object { $_.Trim() -eq "package:$PackageName" })) {
        return [pscustomobject]@{
            Installed = $false
            FirstInstallTime = $null
            LastUpdateTime = $null
            VersionCode = $null
        }
    }

    $pathOutput = @(Invoke-Adb -Arguments @(
        '-s', $DeviceSerial, 'shell', 'pm', 'path', $PackageName) `
        -Purpose "$PackageName package lookup")
    if (-not ($pathOutput | Where-Object { $_ -like 'package:*' })) {
        return [pscustomobject]@{
            Installed = $false
            FirstInstallTime = $null
            LastUpdateTime = $null
            VersionCode = $null
        }
    }

    $dump = (Invoke-Adb -Arguments @(
        '-s', $DeviceSerial, 'shell', 'dumpsys', 'package', $PackageName) `
        -Purpose "$PackageName package metadata") -join "`n"
    $firstInstallTime = if ($dump -match '(?m)^\s*firstInstallTime=(.+)$') {
        $matches[1].Trim()
    } else { $null }
    $lastUpdateTime = if ($dump -match '(?m)^\s*lastUpdateTime=(.+)$') {
        $matches[1].Trim()
    } else { $null }
    $versionCode = if ($dump -match '(?m)^\s*versionCode=(\d+)') {
        $matches[1]
    } else { $null }

    return [pscustomobject]@{
        Installed = $true
        FirstInstallTime = $firstInstallTime
        LastUpdateTime = $lastUpdateTime
        VersionCode = $versionCode
    }
}

function Install-Package(
    [string] $PackageName,
    [string] $ApkPath,
    [string] $Purpose) {
    if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) {
        throw "$Purpose APK is missing: $ApkPath"
    }

    $before = Get-PackageSnapshot $PackageName
    Write-Host "$Purpose APK: $ApkPath"
    Write-Host "$Purpose SHA256: $((Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash)"
    $installOutput = @(Invoke-Adb -Arguments @(
        '-s', $DeviceSerial, 'install', '-r', $ApkPath) -Purpose "$Purpose update")
    if (-not ($installOutput | Where-Object { $_.Trim() -eq 'Success' })) {
        throw "$Purpose update did not report Success.`n$($installOutput -join [Environment]::NewLine)"
    }

    $after = Get-PackageSnapshot $PackageName
    if (-not $after.Installed) {
        throw "$Purpose is not installed after adb install -r."
    }
    if ($before.Installed -and
        $before.FirstInstallTime -and
        $after.FirstInstallTime -ne $before.FirstInstallTime) {
        throw "$Purpose first-install timestamp changed; the update did not preserve installation identity."
    }

    $preservation = if ($before.Installed) {
        "preserved (firstInstallTime $($after.FirstInstallTime))"
    } else {
        'new installation'
    }
    Write-Host "$Purpose installed: versionCode $($after.VersionCode), data identity $preservation"
    Write-Host "$Purpose lastUpdateTime: $($after.LastUpdateTime)"
}

Write-Host "ADB device: $DeviceSerial"
# Install the companion first so TPUI never launches against an older bridge/runtime.
if (-not $SkipCompanion) {
    Install-Package 'com.teknoparrot.winlator' $CompanionApk 'Winlator companion'
}
if (-not $SkipUi) {
    Install-Package 'com.teknoparrot.ui' $UiApk 'TeknoParrotUI'
}

Write-Host 'Android package update completed without uninstalling or clearing app data.'
