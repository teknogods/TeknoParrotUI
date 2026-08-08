[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet(
        'OpenParrotWin32',
        'OpenParrotx64',
        'TeknoParrot',
        'TeknoParrotElfLdr2',
        'cxbxr')]
    [string] $PackageId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._+-]+$')]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $SourceRoot,

    [Parameter(Mandatory)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$allowedRoots = @{
    OpenParrotWin32 = @('OpenParrotWin32', 'OpenParrotWin32Legacy')
    OpenParrotx64 = @('OpenParrotWin64')
    TeknoParrot = @('TeknoParrot')
    TeknoParrotElfLdr2 = @('ElfLdr2')
    cxbxr = @('cxbxr-export', 'cxbxr-japan')
}

$source = [IO.Path]::GetFullPath($SourceRoot)
$output = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Runtime package source directory is missing: $source"
}
if (Test-Path -LiteralPath $output) {
    throw "Refusing to replace an existing runtime package: $output"
}

$stageManifestPath = [IO.Path]::GetFullPath(
    (Join-Path $source 'manifest.json'))
$files = @(
    Get-ChildItem -LiteralPath $source -File -Recurse |
        Where-Object {
            -not [string]::Equals(
                [IO.Path]::GetFullPath($_.FullName),
                $stageManifestPath,
                [StringComparison]::OrdinalIgnoreCase)
        } |
        Sort-Object FullName)
if ($files.Count -eq 0) {
    throw 'The runtime package source contains no files.'
}

$manifestFiles = [Collections.Generic.List[object]]::new()
$payloadFiles = [Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Runtime packages cannot contain links or reparse points: $($file.FullName)"
    }
    $relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace('\', '/')
    if ($relative.StartsWith('../', [StringComparison]::Ordinal) -or
        $relative.Contains('/../', [StringComparison]::Ordinal) -or
        $relative.StartsWith('/', [StringComparison]::Ordinal)) {
        throw "Unsafe runtime package path: $relative"
    }
    $separator = $relative.IndexOf('/')
    $root = if ($separator -gt 0) { $relative.Substring(0, $separator) } else { '' }
    if ($root -notin $allowedRoots[$PackageId]) {
        throw (
            "Runtime package '$PackageId' cannot own '$relative'. Allowed roots: " +
            ($allowedRoots[$PackageId] -join ', '))
    }
    $manifestFiles.Add([ordered]@{
        path = $relative
        size = [int64]$file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    })
    $payloadFiles.Add([pscustomobject]@{
        Source = $file.FullName
        Entry = "payload/$relative"
    })
}

$manifest = [ordered]@{
    schemaVersion = 1
    packageId = $PackageId
    platform = 'android'
    version = $Version
    files = $manifestFiles
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5

$parent = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    [void][IO.Directory]::CreateDirectory($parent)
}

Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open(
    $output,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::ReadWrite,
    [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new(
        $stream,
        [IO.Compression.ZipArchiveMode]::Create,
        $true)
    try {
        $timestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $manifestEntry = $archive.CreateEntry(
            'teknoparrot-package.json',
            [IO.Compression.CompressionLevel]::Optimal)
        $manifestEntry.LastWriteTime = $timestamp
        $writer = [IO.StreamWriter]::new(
            $manifestEntry.Open(),
            [Text.UTF8Encoding]::new($false))
        try {
            $writer.Write($manifestJson)
        }
        finally {
            $writer.Dispose()
        }

        foreach ($payload in $payloadFiles) {
            $entry = $archive.CreateEntry(
                $payload.Entry,
                [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $timestamp
            $input = [IO.File]::OpenRead($payload.Source)
            $entryStream = $entry.Open()
            try {
                $input.CopyTo($entryStream)
            }
            finally {
                $entryStream.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $stream.Dispose()
}

$archiveHash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Runtime package: $output"
Write-Host "Size: $((Get-Item -LiteralPath $output).Length)"
Write-Host "Digest: sha256:$archiveHash"
