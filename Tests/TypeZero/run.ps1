param(
    [string]$Csc = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$ExportArguments = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $repo 'bin\typezero-contracts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$common = Join-Path $repo 'TeknoParrotUi.Common\obj\x86\Debug\TeknoParrotUi.Common.dll'
if (!(Test-Path -LiteralPath $common)) { throw 'Build Debug|x86 first.' }
Copy-Item -LiteralPath $common -Destination $output -Force
# A complete UI build embeds/removes loose dependencies with Costura.
Copy-Item -LiteralPath (Join-Path $repo 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll') -Destination $output -Force
$exe = Join-Path $output 'Contracts.exe'
$inputReferences = @()
foreach ($library in @('SharpDX', 'SharpDX.DirectInput', 'SharpDX.XInput')) {
    $dll = Join-Path $repo "packages\$library.4.2.0\lib\net45\$library.dll"
    Copy-Item -LiteralPath $dll -Destination $output -Force
    $inputReferences += "/reference:$dll"
}
& $Csc /nologo /platform:x86 /langversion:9.0 "/out:$exe" "/reference:$common" @inputReferences (Join-Path $PSScriptRoot 'Contracts.cs') (Join-Path $PSScriptRoot 'PowerShovelControls.cs') (Join-Path $repo 'TeknoParrotUi\Views\GameRunningCode\ProcessManagement\TeknoTZeroLauncher.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($ExportArguments) { & $exe $repo $ExportArguments }
else { & $exe $repo }
exit $LASTEXITCODE
