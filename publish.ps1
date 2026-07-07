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
dotnet publish (Join-Path $PSScriptRoot 'ParrotPatcher\ParrotPatcher.csproj') -c Release -o $OutputDir --nologo
if ($LASTEXITCODE -ne 0) { throw "ParrotPatcher publish failed" }

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
