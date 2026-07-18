[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string] $ProfileName,

    [Parameter(Mandatory)]
    [string] $GamePath,

    [Parameter(Mandatory)]
    [string] $DeviceSerial,

    [string] $AdbPath = "$env:USERPROFILE\android-toolchain\sdk\platform-tools\adb.exe",

    [bool] $DebugLogging = $false,

    [hashtable] $ConfigOverrides = @{}
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$stockPath = Join-Path $repositoryRoot "TeknoParrotUi.Common\GameProfiles\$ProfileName.xml"
if (-not (Test-Path -LiteralPath $stockPath -PathType Leaf)) {
    throw "Stock profile not found: $stockPath"
}
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) {
    throw "ADB not found: $AdbPath"
}
if (-not $GamePath.StartsWith('/storage/emulated/0/Download/', [StringComparison]::Ordinal) -and
    -not $GamePath.StartsWith('/sdcard/Download/', [StringComparison]::Ordinal)) {
    throw 'The Android game path must be under the shared Download directory.'
}

$document = [System.Xml.XmlDocument]::new()
$document.PreserveWhitespace = $true
$document.Load($stockPath)
$root = $document.DocumentElement
if ($null -eq $root) {
    throw "Profile has no XML root: $stockPath"
}

$gamePathNode = $root.SelectSingleNode('GamePath')
if ($null -eq $gamePathNode) {
    throw "Profile has no GamePath element: $stockPath"
}
$gamePathNode.InnerText = $GamePath

$debugNode = $root.SelectSingleNode('AndroidDebugLogging')
if ($null -eq $debugNode) {
    $debugNode = $document.CreateElement('AndroidDebugLogging')
    [void] $root.AppendChild($debugNode)
}
$debugNode.InnerText = $DebugLogging.ToString().ToLowerInvariant()

foreach ($entry in $ConfigOverrides.GetEnumerator()) {
    $field = $root.SelectNodes('ConfigValues/FieldInformation') |
        Where-Object { $_.SelectSingleNode('FieldName')?.InnerText -eq [string] $entry.Key } |
        Select-Object -First 1
    if ($null -eq $field) {
        throw "Profile $ProfileName has no config field named '$($entry.Key)'."
    }
    $value = $field.SelectSingleNode('FieldValue')
    if ($null -eq $value) {
        throw "Config field '$($entry.Key)' has no FieldValue element."
    }
    $value.InnerText = [string] $entry.Value
}

$artifactDirectory = Join-Path $repositoryRoot 'cache\android-profile-deploy'
[void] (New-Item -ItemType Directory -Path $artifactDirectory -Force)
$artifactPath = Join-Path $artifactDirectory "$ProfileName.xml"
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create($artifactPath, $settings)
try {
    $document.Save($writer)
}
finally {
    $writer.Dispose()
}

$remoteStagingPath = "/data/local/tmp/teknoparrot-$ProfileName.xml"
& $AdbPath -s $DeviceSerial push $artifactPath $remoteStagingPath
if ($LASTEXITCODE -ne 0) {
    throw "ADB could not stage $ProfileName."
}
& $AdbPath -s $DeviceSerial shell run-as com.teknoparrot.ui `
    cp $remoteStagingPath "files/UserProfiles/$ProfileName.xml"
if ($LASTEXITCODE -ne 0) {
    throw "ADB could not install the private $ProfileName profile."
}

Write-Output "Installed $ProfileName -> $GamePath (debug logging: $DebugLogging)"
foreach ($entry in $ConfigOverrides.GetEnumerator()) {
    Write-Output "  $($entry.Key)=$($entry.Value)"
}
