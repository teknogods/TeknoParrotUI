[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._:-]+$')]
    [string] $DeviceSerial,
    [string] $AdbPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
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

$hierarchyDirectory = Join-Path $repoRoot 'cache\android-import'
New-Item -ItemType Directory -Force -Path $hierarchyDirectory | Out-Null

function Invoke-Adb([string[]] $Arguments) {
    $output = & $adb -s $DeviceSerial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "adb failed ($LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return @($output)
}

function Get-Hierarchy([string] $Stage) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $fileName = "managed-import-$stamp-$Stage.xml"
    $remotePath = '/sdcard/Download/tp-managed-import-window.xml'
    $localPath = Join-Path $hierarchyDirectory $fileName
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-Adb @('shell', 'uiautomator', 'dump', $remotePath) | Out-Null
            Invoke-Adb @('pull', $remotePath, $localPath) | Out-Null
            return [xml](Get-Content -LiteralPath $localPath -Raw)
        }
        catch {
            if ($attempt -eq 3) {
                throw
            }
            Start-Sleep -Seconds 1
        }
    }
}

function Get-BoundsCenter($Node) {
    if ($null -eq $Node -or
        $Node.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
        throw 'The requested Android UI element was not found or had invalid bounds.'
    }
    return [pscustomobject]@{
        X = [math]::Floor(([int]$matches[1] + [int]$matches[3]) / 2)
        Y = [math]::Floor(([int]$matches[2] + [int]$matches[4]) / 2)
    }
}

function Invoke-TapNode($Node) {
    $center = Get-BoundsCenter $Node
    Invoke-Adb @('shell', 'input', 'tap', $center.X.ToString(), $center.Y.ToString()) |
        Out-Null
}

function Wait-ForNode([scriptblock] $Selector, [string] $Stage, [int] $Attempts = 20) {
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $hierarchy = Get-Hierarchy "$Stage-$attempt"
        $node = & $Selector $hierarchy
        if ($null -ne $node) {
            return [pscustomobject]@{ Hierarchy = $hierarchy; Node = $node }
        }
        Start-Sleep -Seconds 2
    }
    throw "Android UI element did not become ready: $Stage"
}

$powerState = (Invoke-Adb @('shell', 'dumpsys', 'power')) -join "`n"
if ($powerState -notmatch 'mWakefulness=Awake') {
    Invoke-Adb @('shell', 'input', 'keyevent', '224') | Out-Null
    Start-Sleep -Seconds 2
}
Invoke-Adb @('shell', 'wm', 'dismiss-keyguard') | Out-Null

Invoke-Adb @(
    'shell', 'monkey', '-p', 'com.teknoparrot.ui',
    '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null

try {
    $startState = Wait-ForNode -Stage 'start-state' -Attempts 8 -Selector {
        param($hierarchy)
        $hierarchy.SelectSingleNode(
            '//node[@class="TextBox" and @content-desc="Search games..."] | ' +
            '//node[@class="Button" and @text="Back" and @enabled="true"]')
    }
}
catch {
    # One controlled cold start is sufficient for the rare Android/Mono
    # startup fault seen after a long automated regression sequence. Normal
    # imports reuse the healthy TPUI process and avoid unnecessary churn.
    Write-Warning 'TeknoParrot did not expose its library; performing one controlled cold start.'
    Invoke-Adb @('shell', 'am', 'force-stop', 'com.teknoparrot.ui') | Out-Null
    Start-Sleep -Seconds 2
    Invoke-Adb @(
        'shell', 'monkey', '-p', 'com.teknoparrot.ui',
        '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null
    $startState = Wait-ForNode -Stage 'cold-start' -Attempts 12 -Selector {
        param($hierarchy)
        $hierarchy.SelectSingleNode(
            '//node[@class="TextBox" and @content-desc="Search games..."] | ' +
            '//node[@class="Button" and @text="Back" and @enabled="true"]')
    }
}

if ($startState.Node.class -eq 'Button') {
    Invoke-TapNode $startState.Node
    $library = Wait-ForNode -Stage 'library-after-session' -Selector {
        param($hierarchy)
        $hierarchy.SelectSingleNode(
            '//node[@class="TextBox" and @content-desc="Search games..."]')
    }
}
else {
    $library = $startState
}

$scannerNode = $null
for ($attempt = 1; $attempt -le 4; $attempt++) {
    Invoke-Adb @('shell', 'input', 'swipe', '1800', '600', '1800', '200', '700') |
        Out-Null
    Start-Sleep -Seconds 1
    $hierarchy = Get-Hierarchy "library-scrolled-$attempt"
    $candidate = $hierarchy.SelectSingleNode(
        '//node[@class="Button" and (@text="Rom Scanner" or @text="Game Scanner")]')
    if ($null -ne $candidate -and (Get-BoundsCenter $candidate).Y -lt 620) {
        $scannerNode = $candidate
        break
    }
}
if ($null -eq $scannerNode) {
    throw 'Game Scanner could not be positioned above the fixed launch controls.'
}
Invoke-TapNode $scannerNode

$scan = Wait-ForNode -Stage 'scanner' -Selector {
    param($hierarchy)
    $hierarchy.SelectSingleNode(
        '//node[@class="Button" and @text="Scan Launchable Games" and @enabled="true"]')
}
Invoke-TapNode $scan.Node

$import = Wait-ForNode -Stage 'scan-complete' -Attempts 30 -Selector {
    param($hierarchy)
    $hierarchy.SelectSingleNode(
        '//node[@class="Button" and @text="Import Found Games" and @enabled="true"]')
}
Invoke-TapNode $import.Node

$done = Wait-ForNode -Stage 'import-complete' -Attempts 30 -Selector {
    param($hierarchy)
    $hierarchy.SelectSingleNode(
        '//node[@class="TextBlock" and contains(@text,"Done — added")]')
}
Write-Host $done.Node.text
