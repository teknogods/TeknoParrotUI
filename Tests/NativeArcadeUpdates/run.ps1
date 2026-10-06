[CmdletBinding()]
param(
    [string]$PatcherAssembly = '',
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $PatcherAssembly) { $PatcherAssembly = Join-Path $root 'bin\x86\Release\ParrotPatcher.exe' }
if (-not (Test-Path -LiteralPath $PatcherAssembly)) { throw 'Build ParrotPatcher Release first or supply -PatcherAssembly.' }
$output = Join-Path $PSScriptRoot 'bin'
[IO.Directory]::CreateDirectory($output) | Out-Null
$patcher = Join-Path $output 'ParrotPatcher.exe'
Copy-Item -LiteralPath $PatcherAssembly -Destination $patcher
$exe = Join-Path $output 'NativeArcadeUpdates.exe'
& $Compiler /nologo /langversion:9 /target:exe /platform:x86 "/reference:$patcher" "/out:$exe" `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Windows.Forms.dll' `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Drawing.dll' `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.IO.Compression.dll' `
    (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Native arcade update test compilation failed.' }
if (Test-Path -LiteralPath "$PatcherAssembly.config") {
    Copy-Item -LiteralPath "$PatcherAssembly.config" -Destination "$exe.config"
}
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Native arcade install/update checks failed.' }
