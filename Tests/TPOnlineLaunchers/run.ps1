[CmdletBinding()]
param(
    [string]$CommonAssembly = '',
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $CommonAssembly) {
    $CommonAssembly = Join-Path $root 'bin\x86\Release\libs\TeknoParrotUi.Common.dll'
    if (-not (Test-Path -LiteralPath $CommonAssembly)) {
        $CommonAssembly = Join-Path $root 'TeknoParrotUi.Common\bin\Release\TeknoParrotUi.Common.dll'
    }
}
if (-not (Test-Path -LiteralPath $CommonAssembly -PathType Leaf)) {
    throw 'Build TeknoParrotUi.Common first, or supply -CommonAssembly.'
}
$output = Join-Path $PSScriptRoot 'bin'
[IO.Directory]::CreateDirectory($output) | Out-Null
Get-ChildItem -LiteralPath (Split-Path -Parent $CommonAssembly) -Filter '*.dll' |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $output }
$exe = Join-Path $output 'TPOnlineLaunchers.exe'
& $Compiler /nologo /langversion:9 /target:exe /platform:x86 "/reference:$CommonAssembly" "/out:$exe" `
    (Join-Path $PSScriptRoot 'Program.cs') `
    (Join-Path $PSScriptRoot 'MvsChecks.cs') `
    (Join-Path $PSScriptRoot 'CpsChecks.cs') `
    (Join-Path $root 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoCPSLauncher.cs') `
    (Join-Path $root 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoMVSLauncher.cs') `
    (Join-Path $root 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoS22Launcher.cs') `
    (Join-Path $root 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoS23Launcher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Launcher check compilation failed.' }
& $exe $root
if ($LASTEXITCODE -ne 0) { throw 'Launcher checks failed.' }
