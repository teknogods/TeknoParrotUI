param(
    [Parameter(Mandatory=$true)][string]$StagedUiRoot,
    [Parameter(Mandatory=$true)][string]$RomRoot,
    [Parameter(Mandatory=$true)][string]$ChdRoot,
    [ValidateSet('batlgear','raizpin','landhigh','landhigha','dendego3')][string]$Game = 'batlgear',
    [string]$Csc = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $repo 'bin\typezero-contracts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$common = Join-Path $repo 'TeknoParrotUi.Common\obj\x86\Debug\TeknoParrotUi.Common.dll'
if (!(Test-Path -LiteralPath $common)) { throw 'Build Debug|x86 first.' }
Copy-Item -LiteralPath $common -Destination $output -Force
$exe = Join-Path $output 'LiveLaunch.exe'
& $Csc /nologo /platform:x86 /langversion:9.0 "/out:$exe" "/reference:$common" (Join-Path $PSScriptRoot 'LiveLaunch.cs') (Join-Path $repo 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoTZeroLauncher.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $exe $repo $StagedUiRoot $RomRoot $ChdRoot $Game
exit $LASTEXITCODE
