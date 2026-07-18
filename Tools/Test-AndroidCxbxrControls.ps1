[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._:-]+$')]
    [string] $DeviceSerial,
    [Parameter(Mandatory)]
    [ValidateSet(9012, 9013, 9053, 9054, 9055, 9057, 9058, 9059)]
    [int] $ControlsProfileId,
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string] $Label,
    [string] $AdbPath,
    [string] $OutputDirectory = $env:TEKNOPARROT_ANDROID_EVIDENCE_ROOT,
    [ValidateRange(500, 5000)]
    [int] $HoldMilliseconds = 1400,
    [ValidateRange(1000, 5000)]
    [int] $SurfaceWidth = 2280,
    [ValidateRange(500, 3000)]
    [int] $SurfaceHeight = 1080,
    [switch] $Full
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        (Split-Path -Parent $PSScriptRoot) 'artifacts\android-screenshots'
}

$adbCandidates = @(
    $AdbPath,
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe'),
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk-platform37\platform-tools\adb.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$adb = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $adb) {
    throw 'adb.exe was not found in any configured Android SDK.'
}

$packageName = 'com.teknoparrot.winlator'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$prefix = "cxbxr-controls-$Label-$timestamp"

function Invoke-Adb([string[]] $Arguments) {
    $output = & $adb -s $DeviceSerial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "adb failed ($LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return @($output)
}

function Get-CurrentPagePath {
    $command =
        "run-as $packageName sh -c 'ls -t ./files/teknoparrot/sessions | head -1'"
    $sessionId = (Invoke-Adb @('shell', $command) | Select-Object -First 1).Trim()
    if ($sessionId -notmatch '^[a-f0-9]{32}$') {
        throw "Could not identify the active TPJ1 session: '$sessionId'"
    }
    return "./files/teknoparrot/sessions/$sessionId/TeknoParrot_JvsState.page"
}

function ConvertTo-Coordinate([double] $X, [double] $Y) {
    return [pscustomobject]@{
        X = [Math]::Round($X * $SurfaceWidth)
        Y = [Math]::Round($Y * $SurfaceHeight)
    }
}

function Read-Page([string] $Name, [string] $PagePath) {
    $path = Join-Path $OutputDirectory "$prefix-$Name.page"
    $process = Start-Process -FilePath $adb -ArgumentList @(
        '-s', $DeviceSerial, 'exec-out', 'run-as', $packageName, 'cat', $PagePath
    ) -RedirectStandardOutput $path -NoNewWindow -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "Could not capture $Name TPJ1 page (exit $($process.ExitCode))."
    }

    $page = [IO.File]::ReadAllBytes($path)
    if ($page.Length -ne 4096 -or
        [Text.Encoding]::ASCII.GetString($page, 64, 4) -ne 'TPJ1') {
        throw "$Name did not capture a valid 4096-byte TPJ1 page."
    }

    return [pscustomobject]@{
        Test = $Name
        Control = '0x{0:X8}' -f [BitConverter]::ToUInt32($page, 8)
        Analog0 = $page[12]
        Analog1 = $page[13]
        Analog2 = $page[14]
        Analog3 = $page[15]
        Coin = [BitConverter]::ToInt32($page, 32)
        HostSequence = [BitConverter]::ToUInt32($page, 76)
        GuestSequence = [BitConverter]::ToUInt32($page, 80)
        Flags = '0x{0:X8}' -f [BitConverter]::ToUInt32($page, 100)
        Page = $path
    }
}

function Invoke-HeldControl(
    [string] $Name,
    [double] $StartX,
    [double] $StartY,
    [double] $EndX,
    [double] $EndY,
    [string] $PagePath) {
    $start = ConvertTo-Coordinate $StartX $StartY
    $end = ConvertTo-Coordinate $EndX $EndY
    $hold = Start-Process -FilePath $adb -ArgumentList @(
        '-s', $DeviceSerial, 'shell', 'input', 'touchscreen', 'swipe',
        $start.X, $start.Y, $end.X, $end.Y, $HoldMilliseconds
    ) -WindowStyle Hidden -PassThru

    Start-Sleep -Milliseconds ([Math]::Max(350, $HoldMilliseconds - 700))
    $held = Read-Page "$Name-held" $PagePath
    $hold.WaitForExit()
    if ($hold.ExitCode -ne 0) {
        throw "ADB held input failed for $Name (exit $($hold.ExitCode))."
    }
    Start-Sleep -Milliseconds 300
    $released = Read-Page "$Name-released" $PagePath
    return @($held, $released)
}

$common = @(
    @{ Name = 'coin'; X = 0.43; Y = 0.92 },
    @{ Name = 'start'; X = 0.55; Y = 0.92 }
)
$service = @(
    @{ Name = 'service'; X = 0.39; Y = 0.10 },
    @{ Name = 'test'; X = 0.53; Y = 0.10 }
)

$tests = switch ($ControlsProfileId) {
    9012 {
        @(
            @{ Name = 'steer-right'; X = 0.14; Y = 0.70; EndX = 0.24; EndY = 0.70 },
            @{ Name = 'gas'; X = 0.91; Y = 0.78 },
            @{ Name = 'brake'; X = 0.80; Y = 0.85 },
            @{ Name = 'shift-up'; X = 0.88; Y = 0.58 },
            @{ Name = 'shift-down'; X = 0.76; Y = 0.68 },
            @{ Name = 'view'; X = 0.72; Y = 0.47 },
            @{ Name = 'interrupt'; X = 0.84; Y = 0.37 }
        )
    }
    9013 {
        @(
            @{ Name = 'board-right'; X = 0.16; Y = 0.70; EndX = 0.26; EndY = 0.70 },
            @{ Name = 'button1'; X = 0.80; Y = 0.68 },
            @{ Name = 'button2'; X = 0.91; Y = 0.54 }
        )
    }
    9053 {
        @(
            @{ Name = 'left-stick-right'; X = 0.14; Y = 0.69; EndX = 0.24; EndY = 0.69 },
            @{ Name = 'right-stick-right'; X = 0.86; Y = 0.69; EndX = 0.96; EndY = 0.69 },
            @{ Name = 'left-fire1'; X = 0.65; Y = 0.53 },
            @{ Name = 'left-fire2'; X = 0.73; Y = 0.43 },
            @{ Name = 'card-in'; X = 0.62; Y = 0.34 },
            @{ Name = 'right-fire1'; X = 0.84; Y = 0.34 },
            @{ Name = 'right-fire2'; X = 0.74; Y = 0.24 },
            @{ Name = 'pedal'; X = 0.70; Y = 0.83 }
        )
    }
    9054 {
        @(
            @{ Name = 'trigger'; X = 0.91; Y = 0.78 },
            @{ Name = 'reload'; X = 0.84; Y = 0.58 },
            @{ Name = 'pointer'; X = 0.35; Y = 0.28 }
        )
    }
    9055 {
        @(
            @{ Name = 'trigger'; X = 0.91; Y = 0.78 },
            @{ Name = 'reload'; X = 0.84; Y = 0.59 },
            @{ Name = 'weapon'; X = 0.75; Y = 0.47 },
            @{ Name = 'special'; X = 0.86; Y = 0.37 },
            @{ Name = 'pointer'; X = 0.35; Y = 0.28 }
        )
    }
    9057 {
        @(
            @{ Name = 'steer-right'; X = 0.14; Y = 0.70; EndX = 0.24; EndY = 0.70 },
            @{ Name = 'gas'; X = 0.91; Y = 0.78 },
            @{ Name = 'brake'; X = 0.80; Y = 0.85 },
            @{ Name = 'drive'; X = 0.10; Y = 0.41 },
            @{ Name = 'reverse'; X = 0.22; Y = 0.41 },
            @{ Name = 'jump1'; X = 0.76; Y = 0.65 },
            @{ Name = 'jump2'; X = 0.88; Y = 0.55 }
        )
    }
    9058 {
        @(
            @{ Name = 'trigger'; X = 0.91; Y = 0.78 },
            @{ Name = 'action'; X = 0.83; Y = 0.58 },
            @{ Name = 'pointer'; X = 0.35; Y = 0.28 }
        )
    }
    9059 {
        @(
            @{ Name = 'steer-right'; X = 0.14; Y = 0.70; EndX = 0.24; EndY = 0.70 },
            @{ Name = 'gas'; X = 0.91; Y = 0.78 },
            @{ Name = 'brake'; X = 0.80; Y = 0.85 },
            @{ Name = 'view'; X = 0.75; Y = 0.61 },
            @{ Name = 'shift-down'; X = 0.87; Y = 0.52 },
            @{ Name = 'shift-up'; X = 0.75; Y = 0.41 }
        )
    }
}

if (-not $Full) {
    $tests = @($tests | Select-Object -First 3)
}
$tests = @($tests) + $common
if ($Full) {
    $tests += $service
}

$processSnapshot = Invoke-Adb @(
    'shell', "ps -A | grep -E 'cxbxr-ldr.exe|com.teknoparrot.winlator'"
)
if (($processSnapshot -join "`n") -notmatch 'cxbxr-ldr\.exe') {
    throw 'CXBXR is not running; launch the title before testing its controls.'
}

$pagePath = Get-CurrentPagePath
$results = [Collections.Generic.List[object]]::new()
$results.Add((Read-Page 'baseline' $pagePath))
foreach ($test in $tests) {
    $endX = if ($test.ContainsKey('EndX')) { $test.EndX } else { $test.X }
    $endY = if ($test.ContainsKey('EndY')) { $test.EndY } else { $test.Y }
    foreach ($sample in Invoke-HeldControl `
            $test.Name $test.X $test.Y $endX $endY $pagePath) {
        $results.Add($sample)
    }
}

$screenshot = Join-Path $OutputDirectory "$prefix-end.png"
$screen = Start-Process -FilePath $adb -ArgumentList @(
    '-s', $DeviceSerial, 'exec-out', 'screencap', '-p'
) -RedirectStandardOutput $screenshot -NoNewWindow -PassThru -Wait
if ($screen.ExitCode -ne 0) {
    throw "Screenshot capture failed (exit $($screen.ExitCode))."
}

$csv = Join-Path $OutputDirectory "$prefix-results.csv"
$results | Export-Csv -LiteralPath $csv -NoTypeInformation
$results | Format-Table Test, Control, Analog0, Analog1, Analog2, Analog3,
    Coin, HostSequence, GuestSequence, Flags -AutoSize
Write-Host "TPJ1 page: $pagePath"
Write-Host "Results: $csv"
Write-Host "Screenshot: $screenshot"
