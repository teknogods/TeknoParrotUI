[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'chase_fpu_helper.cpp'
$outputDirectory = Join-Path $PSScriptRoot 'bin\Release'
$output = Join-Path $outputDirectory 'ChaseFpuHelper.dll'
$object = Join-Path $outputDirectory 'chase_fpu_helper.obj'
$vswhere = Join-Path ${env:ProgramFiles(x86)} `
    'Microsoft Visual Studio\Installer\vswhere.exe'

if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw 'Visual Studio Installer (vswhere.exe) was not found.'
}

$installation = & $vswhere -latest -products '*' `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
if (-not $installation) {
    throw 'Install the Visual Studio C++ x86/x64 build tools before building ChaseFpuHelper.'
}

$vsDevCmd = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path -LiteralPath $vsDevCmd -PathType Leaf)) {
    throw "VsDevCmd.bat was not found below $installation."
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$command = ('call "{0}" -no_logo -arch=x86 -host_arch=x64 && ' +
    'cl.exe /nologo /W4 /WX /O2 /MT /EHsc /D_CRT_SECURE_NO_WARNINGS ' +
    '/Fo:"{2}" "{1}" /link /dll /out:"{3}"') -f `
    $vsDevCmd, $source, $object, $output

& $env:ComSpec /d /c $command
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw 'The Win32 ChaseFpuHelper build failed.'
}

$dumpbin = Get-ChildItem -LiteralPath (Join-Path $installation 'VC\Tools\MSVC') `
    -Filter dumpbin.exe -File -Recurse |
    Where-Object FullName -Match 'Hostx64\\x86\\dumpbin\.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin -or
    -not (& $dumpbin /nologo /headers $output |
        Select-String -Pattern 'machine \(x86\)')) {
    throw 'ChaseFpuHelper.dll is not an x86 PE image.'
}

Write-Host "Built $output"
Write-Host "SHA256 $((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash)"
