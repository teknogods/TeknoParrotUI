[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ApkPath,

    [Parameter(Mandatory)]
    [string] $ExpectedVersionName,

    [Parameter(Mandatory)]
    [string] $ExpectedVersionCode,

    [switch] $AllowDebugCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-ApkComponentBlock(
    [string] $ManifestText,
    [string] $ComponentName) {
    $blocks = [regex]::Matches(
        $ManifestText,
        '(?ms)^      E: (?:activity|service|receiver|provider) \([^\r\n]*\).*?' +
        '(?=^      E: (?:activity|service|receiver|provider) |\z)')
    $matching = @($blocks | Where-Object {
        $_.Value.Contains('="' + $ComponentName + '"')
    })
    if ($matching.Count -ne 1) {
        throw "Manifest component lookup found $($matching.Count) matches for $ComponentName."
    }
    return $matching[0].Value
}

function Require-ProtectedExportedComponent(
    [string] $ManifestText,
    [string] $ComponentName) {
    $block = Read-ApkComponentBlock $ManifestText $ComponentName
    if ($block -notmatch 'android:exported[^\r\n]*0xffffffff') {
        throw "Bridge component must be exported: $ComponentName."
    }
    if (-not $block.Contains(
            'android:permission(0x01010006)="com.teknoparrot.permission.BIND_BRIDGE"')) {
        throw "Bridge component lacks the signature permission: $ComponentName."
    }
}

function Require-PrivateComponent(
    [string] $ManifestText,
    [string] $ComponentName) {
    $block = Read-ApkComponentBlock $ManifestText $ComponentName
    if ($block -notmatch 'android:exported[^\r\n]*0x0') {
        throw "Session component must remain non-exported: $ComponentName."
    }
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$apk = [IO.Path]::GetFullPath($ApkPath)
if (-not (Test-Path -LiteralPath $apk -PathType Leaf)) {
    throw "TeknoParrotUI APK is missing: $apk"
}
if ($ExpectedVersionName -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "ExpectedVersionName must contain four numeric parts: $ExpectedVersionName"
}
if ($ExpectedVersionCode -notmatch '^[1-9][0-9]*$') {
    throw "ExpectedVersionCode must be a positive integer: $ExpectedVersionCode"
}

$androidSdkRoot = if ($env:ANDROID_HOME) {
    $env:ANDROID_HOME
}
elseif ($env:ANDROID_SDK_ROOT) {
    $env:ANDROID_SDK_ROOT
}
else {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) 'android-toolchain/sdk'
}
$buildTools = Get-ChildItem -LiteralPath (Join-Path $androidSdkRoot 'build-tools') -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$runningOnWindows = $env:OS -eq 'Windows_NT'
$apkSigner = Join-Path $buildTools $(if ($runningOnWindows) { 'apksigner.bat' } else { 'apksigner' })
$aapt = Join-Path $buildTools $(if ($runningOnWindows) { 'aapt.exe' } else { 'aapt' })
$zipalign = Join-Path $buildTools $(if ($runningOnWindows) { 'zipalign.exe' } else { 'zipalign' })
foreach ($tool in @($apkSigner, $aapt, $zipalign)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Android build tool is missing: $tool"
    }
}

& $zipalign -c -P 16 4 $apk
if ($LASTEXITCODE -ne 0) {
    throw 'TeknoParrotUI APK does not satisfy 16 KB ZIP alignment.'
}

$certificate = @(& $apkSigner verify --print-certs $apk)
if ($LASTEXITCODE -ne 0) {
    throw 'TeknoParrotUI APK signature verification failed.'
}
$certificateText = $certificate -join "`n"
if ($certificateText -notmatch
    '(?:Signer #1|V[23](?:\.\d+)? Signer):?\s+certificate SHA-256 digest:\s*([0-9a-fA-F]{64})') {
    throw 'Could not read the TeknoParrotUI signing-certificate digest.'
}
$certificateDigest = $Matches[1].ToUpperInvariant()
if (-not $AllowDebugCertificate -and
    $certificateText -match 'O=Android,\s*CN=Android Debug') {
    throw 'A distributable TeknoParrotUI APK must not use the Android debug certificate.'
}

$badging = @(& $aapt dump badging $apk)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the TeknoParrotUI APK manifest.'
}
$packageLine = $badging | Where-Object { $_ -like 'package:*' } | Select-Object -First 1
if ($packageLine -notmatch
    "name='([^']+)'\s+versionCode='([^']+)'\s+versionName='([^']+)'") {
    throw 'Could not parse the TeknoParrotUI package identity.'
}
if ($Matches[1] -ne 'com.teknoparrot.ui') {
    throw "Unexpected TeknoParrotUI package identity: $($Matches[1])"
}
if ($Matches[2] -ne $ExpectedVersionCode) {
    throw "TeknoParrotUI versionCode mismatch: expected $ExpectedVersionCode, found $($Matches[2])."
}
if ($Matches[3] -ne $ExpectedVersionName) {
    throw "TeknoParrotUI versionName mismatch: expected $ExpectedVersionName, found $($Matches[3])."
}
$nativeCode = @($badging | Where-Object { $_ -like 'native-code:*' })
if ($nativeCode.Count -ne 1 -or $nativeCode[0] -ne "native-code: 'arm64-v8a'") {
    throw "TeknoParrotUI must contain only arm64-v8a native code: $($nativeCode -join ', ')"
}
$launchers = @($badging | Where-Object { $_ -like 'launchable-activity:*' })
if ($launchers.Count -ne 1) {
    throw "TeknoParrotUI must publish exactly one launcher Activity; found $($launchers.Count)."
}

$manifest = @(& $aapt dump xmltree $apk AndroidManifest.xml)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the TeknoParrotUI manifest tree.'
}
$manifestText = $manifest -join "`n"
if ($manifestText -notmatch 'android:allowBackup[^\r\n]*0x0') {
    throw 'TeknoParrotUI must disable Android backup for private account and session state.'
}
if ($manifestText -notmatch (
        '(?ms)E: permission[^\r\n]*\r?\n' +
        '\s+A: android:name[^\r\n]*="com\.teknoparrot\.permission\.BIND_BRIDGE"' +
        '[^\r\n]*\r?\n\s+A: android:protectionLevel[^\r\n]*0x2')) {
    throw 'TeknoParrotUI must declare BIND_BRIDGE as a signature permission.'
}
Require-ProtectedExportedComponent `
    $manifestText `
    'com.teknoparrot.bridge.ArcadeSessionService'
foreach ($componentName in @(
    'com.teknoparrot.session.GameSessionService',
    'com.teknoparrot.session.GameSessionLauncherActivity')) {
    Require-PrivateComponent $manifestText $componentName
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($apk)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
}
finally {
    $archive.Dispose()
}
$forbiddenRuntimeEntryPattern = (
    '(?i)(?:^|/)(?:' +
    'OpenParrot[^/]*\.(?:dll|exe)|' +
    'TeknoParrot(?:64)?\.dll|' +
    'TeknoDraw(?:64)?\.dll|' +
    'ScoreSubmission(?:64)?\.dll|' +
    'BudgieLoader\.exe|' +
    'cxbxr-(?:ldr\.exe|emu\.dll)|' +
    'pcsx2[^/]*\.(?:apk|exe|dll|so)|' +
    '(?:OpenParrot|TeknoParrot|TeknoParrotElfLdr2|cxbxr|pcsx2x6)' +
        '[^/]*\.(?:zip|7z|tar|tar\.gz|tgz)' +
    ')$')
$forbiddenEntries = @($entries | Where-Object { $_ -match $forbiddenRuntimeEntryPattern })
if ($forbiddenEntries.Count -ne 0) {
    throw "TeknoParrotUI APK contains forbidden emulator/core payloads: $($forbiddenEntries -join ', ')"
}

$catalogCounts = @{}
foreach ($catalogName in @(
    'GameProfiles',
    'Metadata',
    'GameSetup',
    'AndroidLaunchRecipes',
    'InputProfiles')) {
    $sourceRoot = Join-Path $repositoryRoot "TeknoParrotUi.Common/$catalogName"
    $expected = @(
        Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
            ForEach-Object {
                $relative = [IO.Path]::GetRelativePath($sourceRoot, $_.FullName).Replace('\', '/')
                "assets/$catalogName/$relative"
            })
    $packaged = @($entries | Where-Object {
        $_.StartsWith("assets/$catalogName/", [StringComparison]::Ordinal) -and
        -not $_.EndsWith('/', [StringComparison]::Ordinal)
    })
    $duplicates = @($packaged | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -ne 0) {
        throw "$catalogName contains duplicate APK entries: $($duplicates.Name -join ', ')"
    }
    $difference = @(Compare-Object $expected $packaged -CaseSensitive)
    if ($difference.Count -ne 0) {
        throw "$catalogName packaging mismatch: $($difference -join ', ')"
    }
    $catalogCounts[$catalogName] = $packaged.Count
}

$catalogCount = ($catalogCounts.Values | Measure-Object -Sum).Sum
Write-Host (
    "TeknoParrotUI Android package: PASS; certificate=$certificateDigest; " +
    "version=$ExpectedVersionName/$ExpectedVersionCode; catalog=$catalogCount; " +
    "recipes=$($catalogCounts.AndroidLaunchRecipes); abi=arm64-v8a; " +
    'zipalign=16K; backup=disabled; bundled-runtimes=0')
