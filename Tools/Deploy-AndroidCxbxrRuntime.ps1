param(
    [string]$CxbxrReleaseDirectory =
        $env:TEKNOPARROT_CXBXR_RELEASE_DIRECTORY,

    [string]$FirmwareDirectory =
        $env:TEKNOPARROT_CXBXR_FIRMWARE_DIRECTORY,

    [string]$YaCardEmuDirectory,

    [string]$AdbPath,

    [string]$DeviceSerial,

    [string]$RemoteTransferRoot = '/sdcard/Download/TeknoParrotRuntime',

    [string]$WinlatorPackage = 'com.teknoparrot.winlator',

    [string]$PrivateRuntimeRoot = 'storage/TeknoParrotRuntime',

    [string]$StageRoot,

    [switch]$StageOnly
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($CxbxrReleaseDirectory)) {
    $CxbxrReleaseDirectory = [IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot '..\Cxbx-Reloaded\build\bin\Release'))
}
if ([string]::IsNullOrWhiteSpace($FirmwareDirectory)) {
    $FirmwareDirectory = Join-Path `
        $repositoryRoot 'bin\x86\Debug_old\cxbxr\TeknoParrot'
}

if ([string]::IsNullOrWhiteSpace($StageRoot)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $StageRoot = Join-Path $PSScriptRoot "..\artifacts\android-cxbxr-runtime\$stamp"
}
$StageRoot = [IO.Path]::GetFullPath($StageRoot)
if ([string]::IsNullOrWhiteSpace($YaCardEmuDirectory)) {
    $YaCardEmuDirectory = [IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot '..\..\YACardEmu\build-win32-static2\Release'))
}
$YaCardEmuDirectory = [IO.Path]::GetFullPath($YaCardEmuDirectory)

$runtimeFiles = @(
    'cxbxr-ldr.exe',
    'cxbxr-emu.dll',
    'SDL2.dll',
    'glew32.dll',
    'subhook.dll',
    'beta.ini'
)
$firmwareFiles = @(
    'EmuMediaBoard\fpr21042_m29w160et.bin',
    'EmuMediaBoard\Chihiro\ic10_g24lc64.bin',
    'EmuMediaBoard\Chihiro\pc20_g24lc64.bin',
    'EmuMediaBoard\Chihiro\ic11_24lc024.bin'
)
$yaCardEmuFiles = @(
    'YACardEmu.exe',
    'license.txt',
    'public\index.html',
    'public\blah.js'
)

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required CXBXR file was not found: $Path"
    }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -eq 0) {
        throw "Required CXBXR file is empty: $Path"
    }
    return $file
}

function Get-NormalizedRelativePath(
    [string]$Root,
    [string]$Path) {
    $rootFull = [IO.Path]::GetFullPath($Root)
    $pathFull = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $rootFull.TrimEnd([char[]]@('\', '/')) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith(
            $rootPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected root: $pathFull"
    }

    return $pathFull.Substring($rootPrefix.Length).Replace('\', '/')
}

foreach ($relative in $runtimeFiles) {
    Require-File (Join-Path $CxbxrReleaseDirectory $relative) | Out-Null
}
foreach ($relative in $firmwareFiles) {
    Require-File (Join-Path $FirmwareDirectory $relative) | Out-Null
}
foreach ($relative in $yaCardEmuFiles) {
    Require-File (Join-Path $YaCardEmuDirectory $relative) | Out-Null
}
if (-not (Test-Path -LiteralPath (Join-Path $CxbxrReleaseDirectory 'hlsl') -PathType Container)) {
    throw "CXBXR HLSL directory was not found below $CxbxrReleaseDirectory."
}
if (Test-Path -LiteralPath $StageRoot) {
    throw "Refusing to replace an existing staging directory: $StageRoot"
}

$sourceFirmwareHashes = @{}
foreach ($relative in $firmwareFiles) {
    $path = Join-Path $FirmwareDirectory $relative
    $sourceFirmwareHashes[$relative] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
$sourceYaCardEmuHashes = @{}
foreach ($relative in $yaCardEmuFiles) {
    $path = Join-Path $YaCardEmuDirectory $relative
    $sourceYaCardEmuHashes[$relative] =
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

$settings = @'
[gui]
CxbxDebugMode = 0x0
CxbxDebugLogFile =
DataStorageToggle = 0x1
DataCustomLocation =
IgnoreInvalidXbeSig = false
IgnoreInvalidXbeSec = false
ConsoleTypeToggle = 0x0

[core]
Revision = 9
FlagsLLE = 0x0
KrnlDebugMode = 0x0
KrnlDebugLogFile =
AllowAdminPrivilege = false
LogLevel = 1
LoggedModules = 0x0
LogPopupTestCase = false

[video]
VideoResolution =
adapter = 0x0
Direct3DDevice = 0x0
VSync = false
FullScreen = false
MaintainAspect = true
RenderResolution = 1

[audio]
adapter = 00000000 0000 0000 0000 000000000000
PCM = true
XADPCM = true
UnknownCodec = true
MuteOnUnfocus = false

[network]
adapter_name =

[input-general]
MouseAxisRange = 10
MouseWheelRange = 80
IgnoreKbMoUnfocus = false

[input-port-0]
Type = -1
DeviceName =
ProfileName = ""
TopSlot = -1
BottomSlot = -1

[input-port-1]
Type = -1
DeviceName =
ProfileName = ""
TopSlot = -1
BottomSlot = -1

[input-port-2]
Type = -1
DeviceName =
ProfileName = ""
TopSlot = -1
BottomSlot = -1

[input-port-3]
Type = -1
DeviceName =
ProfileName = ""
TopSlot = -1
BottomSlot = -1

[overlay]
Build Hash = false
FPS = false
HLE/LLE Stats = false
Title Name = false
File Name = false

[hack]
DisablePixelShaders = false
UseAllCores = false
SkipRdtscPatching = false
'@

# WMMT1/2 use the CRP-1231LR-10NAB serial card reader. Keep this
# configuration separate from ElfLoader2's S31R/38400-even WMMT3 helper.
$yaCardEmuSettings = @'
[config]
serialpath = \\.\pipe\YACardEmu
targetdevice = C1231LR
serialbaud = 9600
serialparity = none
apihost = 0.0.0.0
apiport = 8080
autoselectedcard = card.bin
'@

New-Item -ItemType Directory -Path $StageRoot | Out-Null
$variants = @(
    [pscustomobject]@{ Name = 'cxbxr-export'; Region = 3 },
    [pscustomobject]@{ Name = 'cxbxr-japan'; Region = 1 }
)

foreach ($variant in $variants) {
        $target = Join-Path $StageRoot $variant.Name
    $data = Join-Path $target 'TeknoParrot'
    New-Item -ItemType Directory -Path $data | Out-Null

    foreach ($relative in $runtimeFiles) {
        Copy-Item -LiteralPath (Join-Path $CxbxrReleaseDirectory $relative) `
            -Destination (Join-Path $target $relative)
    }
    Copy-Item -LiteralPath (Join-Path $CxbxrReleaseDirectory 'hlsl') `
        -Destination (Join-Path $target 'hlsl') -Recurse

    $yaCardTarget = Join-Path $target 'YACardEmu'
    New-Item -ItemType Directory -Path (Join-Path $yaCardTarget 'public') |
        Out-Null
    foreach ($relative in $yaCardEmuFiles) {
        $destination = Join-Path $yaCardTarget $relative
        Copy-Item -LiteralPath (Join-Path $YaCardEmuDirectory $relative) `
            -Destination $destination
    }
    [IO.File]::WriteAllText(
        (Join-Path $yaCardTarget 'config.ini'),
        $yaCardEmuSettings.Replace("`n", "`r`n"),
        [Text.UTF8Encoding]::new($false))

    foreach ($relative in $firmwareFiles) {
        $destination = Join-Path $data $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) `
            -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $FirmwareDirectory $relative) `
            -Destination $destination
    }

    $regionPath = Join-Path $data 'EmuMediaBoard\Chihiro\ic10_g24lc64.bin'
    $stream = [IO.File]::Open($regionPath, [IO.FileMode]::Open, [IO.FileAccess]::Write)
    try {
        $stream.Position = 0x1F00
        $stream.WriteByte([byte]$variant.Region)
    }
    finally {
        $stream.Dispose()
    }

    [IO.File]::WriteAllText(
        (Join-Path $data 'settings.ini'),
        $settings.Replace("`n", "`r`n"),
        [Text.UTF8Encoding]::new($false))
}

foreach ($relative in $firmwareFiles) {
    $path = Join-Path $FirmwareDirectory $relative
    $after = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($after -ne $sourceFirmwareHashes[$relative]) {
        throw "Source firmware changed while staging: $relative"
    }
}
foreach ($relative in $yaCardEmuFiles) {
    $path = Join-Path $YaCardEmuDirectory $relative
    $after = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($after -ne $sourceYaCardEmuHashes[$relative]) {
        throw "YACardEmu source changed while staging: $relative"
    }
}

$manifest = @(
    Get-ChildItem -LiteralPath $StageRoot -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                path = Get-NormalizedRelativePath $StageRoot $_.FullName
                size = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        })
$manifest | ConvertTo-Json -Depth 3 |
    Set-Content -LiteralPath (Join-Path $StageRoot 'manifest.json') -Encoding utf8

$total = ($manifest | Measure-Object size -Sum).Sum
Write-Output (
    "Staged clean CXBXR Android runtime: {0} files, {1:N2} MiB at {2}" -f
    $manifest.Count, ($total / 1MB), $StageRoot)
Write-Output 'Excluded: desktop Games, EEPROM, EmuDisk, EmuMu, logs, PDB/MAP files, and shader caches.'

if ($StageOnly) {
    return
}
if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
    throw 'DeviceSerial is required unless -StageOnly is used.'
}
$adbCommand = Get-Command adb.exe -ErrorAction SilentlyContinue
$adbCandidates = @(
    $AdbPath,
    $(if ($env:ANDROID_SDK_ROOT) {
        Join-Path $env:ANDROID_SDK_ROOT 'platform-tools\adb.exe'
    }),
    $(if ($env:ANDROID_HOME) {
        Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
    }),
    $(if ($adbCommand) { $adbCommand.Source })
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$AdbPath = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) {
    throw (
        'ADB was not found. Supply -AdbPath, set ANDROID_SDK_ROOT or ' +
        'ANDROID_HOME, or add adb.exe to PATH.')
}
if (-not $RemoteTransferRoot.StartsWith('/sdcard/Download/TeknoParrotRuntime',
        [StringComparison]::Ordinal)) {
    throw 'RemoteTransferRoot must be /sdcard/Download/TeknoParrotRuntime or a child.'
}
if ($WinlatorPackage -notmatch '^[a-zA-Z0-9._]+$') {
    throw 'WinlatorPackage contains unsupported characters.'
}
if ($PrivateRuntimeRoot -notmatch '^storage/TeknoParrotRuntime(?:/.*)?$') {
    throw 'PrivateRuntimeRoot must be storage/TeknoParrotRuntime or a child.'
}

& $AdbPath -s $DeviceSerial wait-for-device
if ($LASTEXITCODE -ne 0) {
    throw "ADB could not reach $DeviceSerial."
}
& $AdbPath -s $DeviceSerial shell "mkdir -p '$RemoteTransferRoot'"
if ($LASTEXITCODE -ne 0) {
    throw "Could not create the Android transfer directory."
}

foreach ($variant in $variants) {
    $target = Join-Path $StageRoot $variant.Name
    & $AdbPath -s $DeviceSerial push --sync $target "$RemoteTransferRoot/"
    if ($LASTEXITCODE -ne 0) {
        throw "CXBXR runtime transfer failed for $($variant.Name)."
    }

    # Recipes intentionally use E:\TeknoParrotRuntime. In the managed
    # container E: is Winlator's private app storage, while public Downloads is
    # D:. Merge the staged files into E: as the package UID. cp -R source/. is
    # additions/updates only: existing game state, caches, and unrelated
    # runtime files are retained.
    $privateVariant = "$PrivateRuntimeRoot/$($variant.Name)"
    $transferVariant = "$RemoteTransferRoot/$($variant.Name)"
    & $AdbPath -s $DeviceSerial shell run-as $WinlatorPackage `
        mkdir -p $privateVariant
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create Winlator private runtime: $privateVariant"
    }
    & $AdbPath -s $DeviceSerial shell run-as $WinlatorPackage `
        cp -R "$transferVariant/." "$privateVariant/"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not merge $($variant.Name) into Winlator's E: drive."
    }
}

foreach ($variant in $variants) {
    foreach ($relative in @(
            'cxbxr-ldr.exe',
            'cxbxr-emu.dll',
            'beta.ini',
            'YACardEmu/YACardEmu.exe',
            'YACardEmu/config.ini',
            'TeknoParrot/EmuMediaBoard/fpr21042_m29w160et.bin',
            'TeknoParrot/EmuMediaBoard/Chihiro/ic10_g24lc64.bin',
            'TeknoParrot/EmuMediaBoard/Chihiro/pc20_g24lc64.bin',
            'TeknoParrot/EmuMediaBoard/Chihiro/ic11_24lc024.bin')) {
        $remote = "$PrivateRuntimeRoot/$($variant.Name)/$relative"
        & $AdbPath -s $DeviceSerial shell run-as $WinlatorPackage test -s $remote
        if ($LASTEXITCODE -ne 0) {
            throw "Winlator private runtime verification failed: $remote"
        }
    }
}

Write-Output (
    "CXBXR Android runtime merged into Winlator E: without deleting remote extras: {0}" -f
    $PrivateRuntimeRoot)
Write-Output (
    "Public transfer copy retained at: {0}" -f $RemoteTransferRoot)
