[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'winlator_bridge_guest.c'
$pipeHelperSource = Join-Path $PSScriptRoot 'pipehelper.c'
$hostTestSource = Join-Path $PSScriptRoot 'tpb1_host_test.c'
$windowsPathBootstrapSource = Join-Path $PSScriptRoot 'windows_path_bootstrap.c'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer (vswhere.exe) was not found.'
}

$installation = & $vswhere -latest -products '*' `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
if (-not $installation) {
    throw 'Install the Visual Studio C++ x86/x64 build tools before building the guest fixtures.'
}

$vsDevCmd = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path -LiteralPath $vsDevCmd)) {
    throw "VsDevCmd.bat was not found below $installation."
}

function Invoke-NativeBuild {
    param(
        [Parameter(Mandatory)] [ValidateSet('x64', 'x86')] [string] $Architecture,
        [Parameter(Mandatory)] [string] $OutputName
    )

    $output = Join-Path $PSScriptRoot $OutputName
    $object = [IO.Path]::ChangeExtension($output, '.obj')
    $command = ('call "{0}" -no_logo -arch={1} -host_arch=x64 && ' +
                'cl.exe /nologo /W4 /WX /O2 /MT /D_CRT_SECURE_NO_WARNINGS ' +
                '/Fo:"{3}" "{2}" /link /subsystem:console /out:"{4}"') -f `
        $vsDevCmd, $Architecture, $source, $object, $output

    & $env:ComSpec /d /c $command
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "The $Architecture Windows guest fixture failed to build."
    }
    Write-Host "Built $output"
}

function Invoke-PipeHelperBuild {
    param(
        [Parameter(Mandatory)] [ValidateSet('x64', 'x86')] [string] $Architecture,
        [Parameter(Mandatory)] [string] $OutputName
    )

    $output = Join-Path $PSScriptRoot $OutputName
    $objectName = if ($Architecture -eq 'x64') { 'pipehelper64.obj' } else { 'pipehelper32.obj' }
    $object = Join-Path $PSScriptRoot $objectName
    $command = ('call "{0}" -no_logo -arch={1} -host_arch=x64 && ' +
                'cl.exe /nologo /W4 /WX /O2 /MT /D_CRT_SECURE_NO_WARNINGS ' +
                '/Fo:"{3}" "{2}" /link ws2_32.lib /subsystem:console /out:"{4}"') -f `
        $vsDevCmd, $Architecture, $pipeHelperSource, $object, $output

    & $env:ComSpec /d /c $command
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "The $Architecture Windows pipe helper failed to build."
    }
    Write-Host "Built $output"
}

function Invoke-TpbHostBuild {
    $output = Join-Path $PSScriptRoot 'tpb1host.exe'
    $object = Join-Path $PSScriptRoot 'tpb1host.obj'
    $command = ('call "{0}" -no_logo -arch=x64 -host_arch=x64 && ' +
                'cl.exe /nologo /W4 /WX /O2 /MT /D_CRT_SECURE_NO_WARNINGS ' +
                '/Fo:"{2}" "{1}" /link ws2_32.lib /subsystem:console /out:"{3}"') -f `
        $vsDevCmd, $hostTestSource, $object, $output

    & $env:ComSpec /d /c $command
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw 'The native TPB1 host test failed to build.'
    }
    Write-Host "Built $output"
}

function Invoke-WindowsPathBootstrapBuild {
    $output = Join-Path $PSScriptRoot 'windows-path-bootstrap.exe'
    $object = Join-Path $PSScriptRoot 'windows-path-bootstrap.obj'
    $command = ('call "{0}" -no_logo -arch=x86 -host_arch=x64 && ' +
                'cl.exe /nologo /W4 /WX /O2 /MT /D_CRT_SECURE_NO_WARNINGS ' +
                '/Fo:"{2}" "{1}" /link user32.lib /subsystem:console /out:"{3}"') -f `
        $vsDevCmd, $windowsPathBootstrapSource, $object, $output

    & $env:ComSpec /d /c $command
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw 'The x86 Windows PATH bootstrap failed to build.'
    }
    & $output --self-test-borderless
    if ($LASTEXITCODE -ne 0) {
        throw "The Windows borderless-window self-test failed with exit code $LASTEXITCODE."
    }
    Write-Host "Built $output"
}

Invoke-NativeBuild -Architecture x64 -OutputName 'bridgeguest64.exe'
Invoke-NativeBuild -Architecture x86 -OutputName 'bridgeguest32.exe'
Invoke-PipeHelperBuild -Architecture x64 -OutputName 'pipehelper.exe'
Invoke-PipeHelperBuild -Architecture x86 -OutputName 'pipehelper32.exe'
Invoke-TpbHostBuild
Invoke-WindowsPathBootstrapBuild
