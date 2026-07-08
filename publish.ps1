<#
.SYNOPSIS
    Builds a distributable release of TeknoParrotUI (.NET 8, Avalonia).
.DESCRIPTION
    Publishes TeknoParrotUi (Avalonia, win-x64, framework-dependent) and
    ParrotPatcher into a single output folder.
    Users need the .NET 8 Desktop Runtime installed.
.PARAMETER OutputDir
    Destination folder. Default: .\publish\TeknoParrotUi
.PARAMETER Zip
    Also produce a zip archive next to the output folder.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot 'publish\TeknoParrotUi'),
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

Write-Host "Publishing TeknoParrotUi (Avalonia)..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'TeknoParrotUi.Avalonia\TeknoParrotUi.Avalonia.csproj') -c Release -r win-x64 --self-contained false -o $OutputDir --nologo
if ($LASTEXITCODE -ne 0) { throw "TeknoParrotUi publish failed" }

Write-Host "Publishing ParrotPatcher..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'ParrotPatcher\ParrotPatcher.csproj') -c Release -r win-x64 --self-contained false -o $OutputDir --nologo
if ($LASTEXITCODE -ne 0) { throw "ParrotPatcher publish failed" }

# ---------------------------------------------------------------------------
# Move dependency assemblies into libs\ so the root folder stays clean.
# The deps.json files are rewritten so the .NET host resolves them from there.
# ---------------------------------------------------------------------------
Write-Host "Moving dependencies into libs\..." -ForegroundColor Cyan
$libsDir = Join-Path $OutputDir 'libs'
New-Item -ItemType Directory -Force $libsDir | Out-Null

# Files that must stay at the root (apphosts + their host config files)
$keepAtRoot = @(
    'TeknoParrotUi.exe', 'TeknoParrotUi.dll', 'TeknoParrotUi.runtimeconfig.json',
    'ParrotPatcher.exe', 'ParrotPatcher.dll', 'ParrotPatcher.runtimeconfig.json'
)

$moved = @()
foreach ($file in Get-ChildItem $OutputDir -File) {
    if ($keepAtRoot -notcontains $file.Name) {
        Move-Item $file.FullName (Join-Path $libsDir $file.Name) -Force
        $moved += $file.Name
    }
}

# Translation satellite assemblies (fi-FI\, de-DE\, ...) also go under libs\
foreach ($dir in Get-ChildItem $OutputDir -Directory) {
    if ($dir.Name -match '^[a-z]{2}(-[A-Za-z]{2,4})?$') {
        Move-Item $dir.FullName (Join-Path $libsDir $dir.Name) -Force
        $moved += "$($dir.Name)\"
    }
}

# Remove the deps.json manifests: without them the host probes the app folder
# and the in-app LibsResolver handles everything that lives in libs\.
Remove-Item (Join-Path $libsDir 'TeknoParrotUi.deps.json'), (Join-Path $libsDir 'ParrotPatcher.deps.json') -ErrorAction SilentlyContinue

# No debug symbols in the distributable (the native Skia PDBs alone are 100 MB)
Get-ChildItem $OutputDir -Recurse -Filter '*.pdb' | Remove-Item -Force

# RID-specific publishes flatten native libraries; drop any leftover runtimes tree
if (Test-Path (Join-Path $OutputDir 'runtimes')) {
    Remove-Item (Join-Path $OutputDir 'runtimes') -Recurse -Force
}

Write-Host "Moved $($moved.Count) dependency file(s) into libs\" -ForegroundColor Green

$exe = Join-Path $OutputDir 'TeknoParrotUi.exe'
$version = (Get-Item $exe).VersionInfo.FileVersion
$size = '{0:N1} MB' -f ((Get-ChildItem $OutputDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Published TeknoParrotUi $version to $OutputDir ($size)" -ForegroundColor Green

if ($Zip) {
    $zipPath = Join-Path (Split-Path $OutputDir -Parent) "TeknoParrotUi-$version-win-x64.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Write-Host "Creating $zipPath..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $OutputDir '*') -DestinationPath $zipPath
    Write-Host "Created $zipPath ($('{0:N1} MB' -f ((Get-Item $zipPath).Length / 1MB)))" -ForegroundColor Green
}
