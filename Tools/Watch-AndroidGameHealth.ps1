[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9._:-]*$')]
    [string] $DeviceSerial,
    [string] $AdbPath,
    [ValidatePattern('^\d*$')]
    [string] $DisplayId,
    [string] $PackageName = 'com.teknoparrot.winlator',
    [ValidateRange(5, 3600)]
    [int] $DurationSeconds = 120,
    [ValidateRange(1, 60)]
    [int] $IntervalSeconds = 5,
    [string] $OutputDirectory = $env:TEKNOPARROT_ANDROID_EVIDENCE_ROOT,
    [string] $Label = 'android-game-health',
    [bool] $CaptureScreenshots = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        (Split-Path -Parent $PSScriptRoot) 'artifacts\android-screenshots'
}

$adbCandidates = @(
    $AdbPath,
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk-platform37\platform-tools\adb.exe'),
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$adb = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $adb) {
    throw "adb.exe was not found in any configured Android SDK."
}

if (-not $DeviceSerial) {
    $devices = @(& $adb devices | Select-Object -Skip 1 | ForEach-Object {
        if ($_ -match '^(\S+)\s+device$') { $matches[1] }
    })
    if ($devices.Count -ne 1) {
        throw "Specify -DeviceSerial when exactly one ADB device is not connected (found $($devices.Count))."
    }
    $DeviceSerial = $devices[0]
}

function Test-TransientAdbFailure([string] $Message) {
    return $Message -match '(?i)device offline|device .* not found|no devices/emulators found|closed|cannot connect'
}

function Repair-AdbTransport {
    $reconnectOutput = (& $adb reconnect offline 2>&1) -join [Environment]::NewLine
    if ($LASTEXITCODE -ne 0 -or
        $reconnectOutput -match '(?i)no devices/emulators found|cannot connect') {
        & $adb kill-server 2>&1 | Out-Null
        & $adb start-server 2>&1 | Out-Null
    }
    Start-Sleep -Seconds 2
    $state = (& $adb -s $DeviceSerial get-state 2>&1) -join ''
    if ($LASTEXITCODE -ne 0 -or $state.Trim() -ne 'device') {
        & $adb kill-server 2>&1 | Out-Null
        & $adb start-server 2>&1 | Out-Null
        Start-Sleep -Seconds 2
    }
}

function Invoke-Adb([string[]] $Arguments) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $output = & $adb -s $DeviceSerial @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            return $output
        }

        $message = $output -join [Environment]::NewLine
        if ($attempt -lt 3 -and (Test-TransientAdbFailure $message)) {
            Write-Warning "ADB transport dropped; reconnecting before retry $($attempt + 1)/3."
            Repair-AdbTransport
            continue
        }

        throw "adb failed ($exitCode): $message"
    }
}

function Resolve-ScreenshotDisplayId {
    if (-not [string]::IsNullOrWhiteSpace($DisplayId)) {
        return $DisplayId
    }

    $displayState = (Invoke-Adb -Arguments @('shell', 'dumpsys', 'display')) -join "`n"
    if ($displayState -match "isActive=true,\s*displayId=\d+,\s*uniqueId='local:(\d+)'" ) {
        return $matches[1]
    }

    return $null
}

function Save-DeviceScreenshot([string] $Path) {
    $screenshotDisplayId = Resolve-ScreenshotDisplayId
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $adb
        $startInfo.ArgumentList.Add('-s')
        $startInfo.ArgumentList.Add($DeviceSerial)
        $startInfo.ArgumentList.Add('exec-out')
        $startInfo.ArgumentList.Add('screencap')
        if ($screenshotDisplayId) {
            $startInfo.ArgumentList.Add('-d')
            $startInfo.ArgumentList.Add($screenshotDisplayId)
        }
        $startInfo.ArgumentList.Add('-p')
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw "Could not start adb screenshot capture."
        }
        $errorTask = $process.StandardError.ReadToEndAsync()
        $stream = [IO.File]::Create($Path)
        try {
            $process.StandardOutput.BaseStream.CopyTo($stream)
        }
        finally {
            $stream.Dispose()
        }
        $process.WaitForExit()
        $errorText = $errorTask.GetAwaiter().GetResult()
        $signature = [IO.File]::ReadAllBytes($Path)
        $validPng = $signature.Length -ge 8 -and
            $signature[0] -eq 0x89 -and $signature[1] -eq 0x50 -and
            $signature[2] -eq 0x4e -and $signature[3] -eq 0x47
        if ($process.ExitCode -eq 0 -and $validPng) {
            Write-Host "Screenshot: $Path"
            return
        }

        if ($attempt -lt 3 -and (Test-TransientAdbFailure $errorText)) {
            Write-Warning "ADB dropped during screenshot capture; reconnecting before retry $($attempt + 1)/3."
            Repair-AdbTransport
            $screenshotDisplayId = Resolve-ScreenshotDisplayId
            continue
        }

        if ($process.ExitCode -ne 0) {
            throw "adb screenshot failed ($($process.ExitCode)): $errorText"
        }
        throw "adb screenshot did not produce a valid PNG at $Path."
    }
}

function Get-ThermalSample {
    $thermal = Invoke-Adb -Arguments @('shell', 'dumpsys', 'thermalservice')
    $status = 0
    $battery = [double]::NaN
    $skin = [double]::NaN
    foreach ($line in $thermal) {
        if ($line -match '^Thermal Status:\s*(\d+)') {
            $status = [int]$matches[1]
        }
        elseif ($line -match 'mValue=([0-9.]+).*mName=BAT,') {
            $battery = [double]::Parse($matches[1], [Globalization.CultureInfo]::InvariantCulture)
        }
        elseif ($line -match 'mValue=([0-9.]+).*mName=SKIN,') {
            $skin = [double]::Parse($matches[1], [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    return [pscustomobject]@{ Status = $status; BatteryC = $battery; SkinC = $skin }
}

function Get-PackageUid {
    $line = Invoke-Adb -Arguments @('shell', 'cmd', 'package', 'list', 'packages', '-U', $PackageName) |
        Select-Object -First 1
    if ($line -notmatch 'uid:(\d+)') {
        throw "Could not resolve Android UID for $PackageName."
    }
    return [int]$matches[1]
}

function Get-AppProcesses([int] $Uid) {
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($line in (Invoke-Adb -Arguments @('shell', 'ps', '-A', '-o', 'UID,PID,PPID,STAT,NAME'))) {
        if ($line -match '^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(.+?)\s*$' -and
            [int]$matches[1] -eq $Uid -and
            -not $matches[4].StartsWith('Z', [StringComparison]::Ordinal)) {
            $rows.Add([pscustomobject]@{
                Pid = [int]$matches[2]
                ParentPid = [int]$matches[3]
                Name = $matches[5]
            })
        }
    }
    return $rows
}

function Get-ProcessHealth([int] $ProcessId) {
    $mapsOutput = (Invoke-Adb -Arguments @(
        'shell', 'run-as', $PackageName, 'wc', '-l', "/proc/$ProcessId/maps")) -join ''
    $status = Invoke-Adb -Arguments @(
        'shell', 'run-as', $PackageName, 'cat', "/proc/$ProcessId/status")
    $rss = $status | Where-Object { $_ -match '^VmRSS:' } | Select-Object -First 1
    $threads = $status | Where-Object { $_ -match '^Threads:' } | Select-Object -First 1
    return [pscustomobject]@{
        Maps = if ($mapsOutput -match '^(\d+)') { [int]$matches[1] } else { 0 }
        RssKiB = if ($rss -match '^VmRSS:\s*(\d+)') { [int64]$matches[1] } else { 0 }
        Threads = if ($threads -match '^Threads:\s*(\d+)') { [int]$matches[1] } else { 0 }
    }
}

function Get-SystemMemorySample {
    $values = @{}
    foreach ($line in (Invoke-Adb -Arguments @('shell', 'cat', '/proc/meminfo'))) {
        if ($line -match '^(MemAvailable|SwapTotal|SwapFree):\s*(\d+)\s+kB$') {
            $values[$matches[1]] = [int64]$matches[2]
        }
    }
    return [pscustomobject]@{
        AvailableMiB = [math]::Round(([double]($values['MemAvailable'])) / 1024, 1)
        SwapTotalMiB = [math]::Round(([double]($values['SwapTotal'])) / 1024, 1)
        SwapUsedMiB = [math]::Round(
            ([double]($values['SwapTotal'] - $values['SwapFree'])) / 1024, 1)
    }
}

function Read-DeviceNumber([string] $Path) {
    try {
        $text = (Invoke-Adb -Arguments @('shell', 'cat', $Path)) -join ''
    }
    catch {
        if (Test-TransientAdbFailure $_.Exception.Message) {
            throw
        }
        return [double]::NaN
    }
    if ($text -match '(-?[0-9]+(?:\.[0-9]+)?)') {
        return [double]::Parse($matches[1], [Globalization.CultureInfo]::InvariantCulture)
    }
    return [double]::NaN
}

function Get-GpuSample {
    $busy = Read-DeviceNumber '/sys/class/kgsl/kgsl-3d0/gpu_busy_percentage'
    $currentHz = Read-DeviceNumber '/sys/class/kgsl/kgsl-3d0/devfreq/cur_freq'
    $maximumHz = Read-DeviceNumber '/sys/class/kgsl/kgsl-3d0/devfreq/max_freq'
    return [pscustomobject]@{
        BusyPercent = $busy
        CurrentMHz = if ([double]::IsNaN($currentHz)) {
            [double]::NaN
        } else { [math]::Round($currentHz / 1000000, 1) }
        MaxMHz = if ([double]::IsNaN($maximumHz)) {
            [double]::NaN
        } else { [math]::Round($maximumHz / 1000000, 1) }
    }
}

function Get-SurfaceFrameSample {
    try {
        $layerLine = Invoke-Adb -Arguments @('shell', 'dumpsys', 'SurfaceFlinger', '--list') |
            Where-Object {
                $_ -match [regex]::Escape($PackageName) -and $_ -match '\(BLAST\)'
            } |
            Select-Object -First 1
        if (-not $layerLine -or
            $layerLine -notmatch '^RequestedLayerState\{(.+?) parentId=') {
            return [pscustomobject]@{
                Fps = [double]::NaN
                MedianFrameMs = [double]::NaN
                FrameCount = 0
            }
        }

        $layer = $matches[1]
        $latency = Invoke-Adb -Arguments @(
            'shell', "dumpsys SurfaceFlinger --latency '$layer'")
        $timestamps = [Collections.Generic.List[uint64]]::new()
        foreach ($line in ($latency | Select-Object -Skip 1)) {
            $first = (($line -split '\s+') | Where-Object { $_ } | Select-Object -First 1)
            [uint64] $value = 0
            if ([uint64]::TryParse($first, [ref]$value) -and
                $value -gt 0 -and $value -lt 9000000000000000000) {
                $timestamps.Add($value)
            }
        }
        if ($timestamps.Count -lt 2 -or $timestamps[$timestamps.Count - 1] -le $timestamps[0]) {
            return [pscustomobject]@{
                Fps = [double]::NaN
                MedianFrameMs = [double]::NaN
                FrameCount = $timestamps.Count
            }
        }

        $deltas = [Collections.Generic.List[double]]::new()
        for ($index = 1; $index -lt $timestamps.Count; $index++) {
            if ($timestamps[$index] -gt $timestamps[$index - 1]) {
                $deltas.Add(($timestamps[$index] - $timestamps[$index - 1]) / 1000000.0)
            }
        }
        $sortedDeltas = @($deltas | Sort-Object)
        $spanSeconds =
            ($timestamps[$timestamps.Count - 1] - $timestamps[0]) / 1000000000.0
        return [pscustomobject]@{
            Fps = [math]::Round(($timestamps.Count - 1) / $spanSeconds, 2)
            MedianFrameMs = if ($sortedDeltas.Count -gt 0) {
                [math]::Round($sortedDeltas[[math]::Floor($sortedDeltas.Count / 2)], 2)
            } else { [double]::NaN }
            FrameCount = $timestamps.Count
        }
    }
    catch {
        if (Test-TransientAdbFailure $_.Exception.Message) {
            throw
        }
        return [pscustomobject]@{
            Fps = [double]::NaN
            MedianFrameMs = [double]::NaN
            FrameCount = 0
        }
    }
}

$uid = Get-PackageUid
$mapLimit = 65530
$mapLimitOutput = (& $adb -s $DeviceSerial shell cat /proc/sys/vm/max_map_count 2>$null) -join ''
if ($LASTEXITCODE -eq 0 -and $mapLimitOutput -match '^(\d+)') {
    $mapLimit = [int]$matches[1]
}
$mapWarningThreshold = [math]::Floor($mapLimit * 0.9)
$safeLabel = $Label -replace '[^A-Za-z0-9._-]', '-'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$csvPath = Join-Path $OutputDirectory "$safeLabel-$timestamp.csv"
$startScreenshotPath = Join-Path $OutputDirectory "$safeLabel-$timestamp-start.png"
$endScreenshotPath = Join-Path $OutputDirectory "$safeLabel-$timestamp-end.png"
$samples = [Collections.Generic.List[object]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
$seenProcess = $false

Write-Host (
    "Monitoring $PackageName (uid $uid) on $DeviceSerial for $DurationSeconds seconds; " +
    "map limit $mapLimit")
Write-Host "Evidence: $csvPath"
if ($CaptureScreenshots) {
    try {
        Save-DeviceScreenshot $startScreenshotPath
    }
    catch {
        Write-Warning "Start screenshot was unavailable: $($_.Exception.Message)"
    }
}

do {
    $now = Get-Date
    try {
    $thermal = Get-ThermalSample
    $gpu = Get-GpuSample
    $surface = Get-SurfaceFrameSample
    $systemMemory = Get-SystemMemorySample
    $processes = @(Get-AppProcesses $uid)
    $seenProcess = $seenProcess -or $processes.Count -gt 0
    $health = @(foreach ($process in $processes) {
        try {
            $value = Get-ProcessHealth $process.Pid
        }
        catch {
            # Wine helpers are intentionally short-lived during startup. A PID
            # can disappear after ps but before /proc is sampled; skip only that
            # row instead of losing the complete physical-device measurement.
            continue
        }
        [pscustomobject]@{
            Pid = $process.Pid
            Name = $process.Name
            Maps = $value.Maps
            RssKiB = $value.RssKiB
            Threads = $value.Threads
        }
    })
    $largest = $health | Sort-Object Maps -Descending | Select-Object -First 1
    $totalRss = 0
    $totalThreads = 0
    if ($health.Count -gt 0) {
        $totalRss = ($health | Measure-Object RssKiB -Sum).Sum
        $totalThreads = ($health | Measure-Object Threads -Sum).Sum
    }
    $sample = [pscustomobject]@{
        Timestamp = $now.ToString('o')
        ThermalStatus = $thermal.Status
        BatteryC = $thermal.BatteryC
        SkinC = $thermal.SkinC
        GpuBusyPercent = $gpu.BusyPercent
        GpuCurrentMHz = $gpu.CurrentMHz
        GpuMaxMHz = $gpu.MaxMHz
        SurfaceFps = $surface.Fps
        MedianFrameMs = $surface.MedianFrameMs
        SurfaceFrameCount = $surface.FrameCount
        ProcessCount = $processes.Count
        MaxMapPid = if ($largest) { $largest.Pid } else { 0 }
        MaxMapProcess = if ($largest) { $largest.Name } else { '' }
        MaxMaps = if ($largest) { $largest.Maps } else { 0 }
        MapLimit = $mapLimit
        TotalRssMiB = [math]::Round(([double]$totalRss) / 1024, 1)
        TotalThreads = [int]$totalThreads
        SystemAvailableMiB = $systemMemory.AvailableMiB
        SwapTotalMiB = $systemMemory.SwapTotalMiB
        SwapUsedMiB = $systemMemory.SwapUsedMiB
        SwapUsedPercent = if ($systemMemory.SwapTotalMiB -gt 0) {
            [math]::Round(100 * $systemMemory.SwapUsedMiB / $systemMemory.SwapTotalMiB, 1)
        } else { 0 }
        AdbConnected = $true
    }
    }
    catch {
        $failure = $_.Exception.Message
        if (-not (Test-TransientAdbFailure $failure)) {
            throw
        }
        $sample = [pscustomobject]@{
            Timestamp = $now.ToString('o')
            ThermalStatus = -1
            BatteryC = [double]::NaN
            SkinC = [double]::NaN
            GpuBusyPercent = [double]::NaN
            GpuCurrentMHz = [double]::NaN
            GpuMaxMHz = [double]::NaN
            SurfaceFps = [double]::NaN
            MedianFrameMs = [double]::NaN
            SurfaceFrameCount = 0
            ProcessCount = -1
            MaxMapPid = 0
            MaxMapProcess = 'ADB disconnected'
            MaxMaps = 0
            MapLimit = $mapLimit
            TotalRssMiB = [double]::NaN
            TotalThreads = 0
            SystemAvailableMiB = [double]::NaN
            SwapTotalMiB = [double]::NaN
            SwapUsedMiB = [double]::NaN
            SwapUsedPercent = [double]::NaN
            AdbConnected = $false
        }
        Write-Warning "ADB remains unavailable; preserving the run and retrying at the next interval."
    }
    $samples.Add($sample)
    $sample | Format-Table -AutoSize -Property @(
        'Timestamp', 'ThermalStatus', 'SkinC', 'GpuBusyPercent',
        'GpuCurrentMHz', 'GpuMaxMHz', 'SurfaceFps', 'MedianFrameMs', 'ProcessCount',
        'MaxMapProcess', 'MaxMaps', 'TotalRssMiB', 'TotalThreads',
        'SystemAvailableMiB', 'SwapUsedMiB', 'SwapUsedPercent')
    $samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation

    if ($sample.AdbConnected -and $sample.ThermalStatus -ge 3) {
        Write-Warning (
            "Android thermal status is $($thermal.Status); the device is thermally throttled, " +
            "so do not judge game performance from this sample.")
    }
    if ($sample.GpuBusyPercent -ge 95) {
        Write-Warning (
            "Android GPU utilization is $($sample.GpuBusyPercent)% at " +
            "$($sample.GpuCurrentMHz)/$($sample.GpuMaxMHz) MHz; the rendered scene is GPU-bound.")
    }
    if ($sample.MaxMaps -ge $mapWarningThreshold) {
        Write-Warning (
            "$($sample.MaxMapProcess) is near Android's mapping limit " +
            "($($sample.MaxMaps)/$mapLimit mappings).")
    }
    if ($sample.SystemAvailableMiB -lt 512) {
        Write-Warning (
            "Android has only $($sample.SystemAvailableMiB) MiB readily available; " +
            "whole-device stalls and an LMKD foreground kill are now plausible.")
    }
    if ($sample.SwapUsedPercent -ge 75) {
        Write-Warning (
            "Android swap is $($sample.SwapUsedPercent)% full " +
            "($($sample.SwapUsedMiB)/$($sample.SwapTotalMiB) MiB); " +
            "do not attribute slow motion or an I/O timeout to the game alone.")
    }
    if ($sample.AdbConnected -and $seenProcess -and $processes.Count -eq 0) {
        Write-Warning "$PackageName exited during the monitored test."
        break
    }

    if ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $IntervalSeconds
    }
} while ([DateTime]::UtcNow -lt $deadline)

if ($CaptureScreenshots) {
    try {
        Save-DeviceScreenshot $endScreenshotPath
    }
    catch {
        Write-Warning "End screenshot was unavailable: $($_.Exception.Message)"
    }
}
Write-Host "Saved $($samples.Count) samples to $csvPath"
