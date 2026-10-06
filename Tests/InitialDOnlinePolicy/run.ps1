[CmdletBinding()]
param(
    [string]$UiAssembly = '',
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $UiAssembly) { $UiAssembly = Join-Path $root 'bin\x86\Release\TeknoParrotUi.exe' }
if (-not (Test-Path -LiteralPath $UiAssembly)) { throw 'Build TPUI Release first or supply -UiAssembly.' }
$assemblyRoot = Split-Path -Parent $UiAssembly
$output = Join-Path $PSScriptRoot 'bin'
[IO.Directory]::CreateDirectory($output) | Out-Null
Copy-Item -LiteralPath $UiAssembly -Destination $output
Get-ChildItem -LiteralPath $assemblyRoot -Filter '*.dll' -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $output }
$libraries = Join-Path $output 'libs'
[IO.Directory]::CreateDirectory($libraries) | Out-Null
Get-ChildItem -LiteralPath (Join-Path $assemblyRoot 'libs') -Filter '*.dll' -File |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $libraries
        Copy-Item -LiteralPath $_.FullName -Destination $output
    }
$exe = Join-Path $output 'InitialDOnlinePolicy.exe'
& $Compiler /nologo /langversion:9 /target:exe /platform:x86 "/out:$exe" `
    "/reference:$UiAssembly" "/reference:$output\libs\TeknoParrotUi.Common.dll" `
    "/reference:$output\libs\Newtonsoft.Json.dll" `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\WindowsBase.dll' `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationFramework.dll' `
    '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationCore.dll' `
    (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Initial D policy check compilation failed.' }
Copy-Item -LiteralPath (Join-Path $assemblyRoot 'TeknoParrotUi.exe.config') -Destination "$exe.config"
& $exe $root
if ($LASTEXITCODE -ne 0) { throw 'Initial D policy checks failed.' }
