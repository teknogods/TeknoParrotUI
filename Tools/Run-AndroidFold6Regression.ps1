[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._:-]+$')]
    [string] $DeviceSerial,
    [ValidateRange(0, 17)]
    [int] $StartAt = 0,
    [ValidateRange(1, 18)]
    [int] $Count = 18,
    [string] $AdbPath,
    [string] $OutputDirectory = $env:TEKNOPARROT_ANDROID_EVIDENCE_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        (Split-Path -Parent $PSScriptRoot) 'artifacts\android-screenshots'
}

$adbCandidates = @(
    $AdbPath,
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe'),
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk-platform37\platform-tools\adb.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$adb = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $adb) {
    throw 'adb.exe was not found in any configured Android SDK.'
}

$windowPolicy = (& $adb -s $DeviceSerial shell dumpsys window policy 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Could not query Android lock state: $windowPolicy"
}
if ($windowPolicy -match 'mInputRestricted=true') {
    throw 'The Android device is securely locked; unlock it before starting the Fold6 regression batch.'
}

$manifestPath = Join-Path $PSScriptRoot 'AndroidFold6Regression.json'
$manifest = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
$lastIndex = [math]::Min($manifest.Count, $StartAt + $Count) - 1
if ($StartAt -gt $lastIndex) {
    throw "StartAt $StartAt is outside the $($manifest.Count)-game manifest."
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$summaryPath = Join-Path $OutputDirectory "s26-fold6-batch-$timestamp.csv"
$runner = Join-Path $PSScriptRoot 'Run-AndroidGameRegression.ps1'
$results = [Collections.Generic.List[object]]::new()

for ($index = $StartAt; $index -le $lastIndex; $index++) {
    $game = $manifest[$index]
    Write-Host "[$($index + 1)/$($manifest.Count)] $($game.profile)"
    $started = Get-Date
    try {
        $arguments = @{
            DeviceSerial = $DeviceSerial
            AdbPath = $adb
            SearchText = $game.search
            Label = $game.label
            ExpectedTitlePattern = $game.expectedTitlePattern
            DurationSeconds = [int]$game.durationSeconds
            OutputDirectory = $OutputDirectory
        }
        & $runner @arguments
        $status = 'captured-for-review'
        $errorText = ''
    }
    catch {
        $status = 'automation-error'
        $errorText = $_.Exception.Message
        Write-Warning "$($game.profile): $errorText"
        & $adb -s $DeviceSerial shell am force-stop com.teknoparrot.winlator 2>&1 | Out-Null
        & $adb -s $DeviceSerial shell am force-stop com.teknoparrot.ui 2>&1 | Out-Null
    }

    $results.Add([pscustomobject]@{
        Index = $index
        Profile = $game.profile
        Label = $game.label
        Status = $status
        Started = $started.ToString('o')
        Finished = (Get-Date).ToString('o')
        Error = $errorText
    })
    $results | Export-Csv -LiteralPath $summaryPath -NoTypeInformation
}

Write-Host "Batch capture summary: $summaryPath"
