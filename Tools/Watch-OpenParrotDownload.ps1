[CmdletBinding()]
param(
    [Parameter()]
    [string] $Destination = $env:TEKNOPARROT_OPENPARROT_BACKUP_ROOT,

    [Parameter()]
    [ValidateRange(1, 60)]
    [int] $RefreshSeconds = 2,

    [Parameter()]
    [switch] $Once
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Destination)) {
    throw (
        'Specify -Destination or set ' +
        'TEKNOPARROT_OPENPARROT_BACKUP_ROOT.')
}

$statusPath = Join-Path $Destination 'OpenParrot-download-status.json'
$fallbackLogPath = Join-Path $Destination 'OpenParrot-download.log'
$stderrPath = Join-Path $Destination 'OpenParrot-download.stderr.log'

function Format-ByteCount {
    param([Parameter(Mandatory)][long] $Bytes)

    if ($Bytes -ge 1TB) { return ('{0:N2} TiB' -f ($Bytes / 1TB)) }
    if ($Bytes -ge 1GB) { return ('{0:N2} GiB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N2} MiB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N2} KiB' -f ($Bytes / 1KB)) }
    return "$Bytes B"
}

function Format-Duration {
    param([Parameter(Mandatory)][double] $Seconds)

    if ([double]::IsInfinity($Seconds) -or [double]::IsNaN($Seconds) -or $Seconds -lt 0) {
        return '--'
    }
    $duration = [TimeSpan]::FromSeconds($Seconds)
    if ($duration.TotalDays -ge 1) { return ('{0:%d}d {0:hh\:mm\:ss}' -f $duration) }
    return ('{0:hh\:mm\:ss}' -f $duration)
}

function New-ProgressBar {
    param(
        [Parameter(Mandatory)][double] $Fraction,
        [Parameter()][int] $Width = 42
    )

    $fraction = [math]::Max(0.0, [math]::Min(1.0, $Fraction))
    $filled = [int] [math]::Floor($fraction * $Width)
    return '[' + ('#' * $filled) + ('-' * ($Width - $filled)) + ']'
}

$previousSamples = @{}

try {
    try { [Console]::CursorVisible = $false } catch { }

    while ($true) {
        try { Clear-Host } catch { }
        Write-Host 'OpenParrot Archive.org download status' -ForegroundColor Cyan
        Write-Host "Destination: $Destination"
        Write-Host "Updated:     $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        Write-Host ''

        if (-not (Test-Path -LiteralPath $statusPath -PathType Leaf)) {
            Write-Host 'Waiting for the downloader to create its status file...' -ForegroundColor Yellow
            if ($Once) { break }
            Start-Sleep -Seconds $RefreshSeconds
            continue
        }

        try {
            $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-Host 'Status is being updated; retrying...' -ForegroundColor Yellow
            if ($Once) { break }
            Start-Sleep -Seconds $RefreshSeconds
            continue
        }

        $process = Get-Process -Id ([int] $status.process_id) -ErrorAction SilentlyContinue
        $processState = if ($process) { 'running' } else { 'not running' }
        $concurrentLimit = if ($status.PSObject.Properties.Name -contains 'concurrent_limit') { [int] $status.concurrent_limit } else { 1 }
        $failedDownloads = if ($status.PSObject.Properties.Name -contains 'failed_downloads') { [int] $status.failed_downloads } else { 0 }
        Write-Host ("State:       {0} (PID {1}, {2})" -f $status.state, $status.process_id, $processState)
        Write-Host ("Files:       {0} / {1} complete; {2} failed; {3} unmatched" -f $status.completed_files, $status.total_files, $failedDownloads, $status.unmatched_profiles)

        $activeDownloads = @()
        if ($status.PSObject.Properties.Name -contains 'active_downloads') {
            $activeDownloads = @($status.active_downloads)
        }
        elseif ($status.current_game) {
            $activeDownloads = @([pscustomobject]@{
                state          = $status.state
                process_id     = 0
                index          = $status.current_index
                game           = $status.current_game
                file           = $status.current_file
                partial_path   = $status.partial_path
                expected_bytes = $status.expected_bytes
                started_at     = $status.started_at
            })
        }

        Write-Host ("Transfers:   {0} active / {1} allowed" -f $activeDownloads.Count, $concurrentLimit)
        $now = Get-Date
        $newSamples = @{}
        $totalBytesPerSecond = [double] 0
        $activeStatistics = @(
            foreach ($download in $activeDownloads) {
                $partialPath = [string] $download.partial_path
                $currentBytes = [long] 0
                if ($partialPath -and (Test-Path -LiteralPath $partialPath -PathType Leaf)) {
                    $currentBytes = [long] (Get-Item -LiteralPath $partialPath).Length
                }
                elseif ($download.state -eq 'Verifying') {
                    $finalPath = Join-Path $Destination ([string] $download.file)
                    if (Test-Path -LiteralPath $finalPath -PathType Leaf) {
                        $currentBytes = [long] (Get-Item -LiteralPath $finalPath).Length
                    }
                }

                $bytesPerSecond = [double] 0
                if ($partialPath -and $previousSamples.ContainsKey($partialPath)) {
                    $previous = $previousSamples[$partialPath]
                    $elapsed = ($now - $previous.At).TotalSeconds
                    if ($elapsed -gt 0 -and $currentBytes -ge $previous.Bytes) {
                        $bytesPerSecond = ($currentBytes - $previous.Bytes) / $elapsed
                    }
                }
                if ($partialPath) {
                    $newSamples[$partialPath] = [pscustomobject]@{ Bytes = $currentBytes; At = $now }
                }
                $totalBytesPerSecond += $bytesPerSecond

                $expectedBytes = [long] $download.expected_bytes
                $fileFraction = if ($expectedBytes -gt 0) { $currentBytes / [double] $expectedBytes } else { 0.0 }
                $fileEta = if ($bytesPerSecond -gt 0 -and $expectedBytes -gt $currentBytes) {
                    ($expectedBytes - $currentBytes) / $bytesPerSecond
                }
                else {
                    [double]::PositiveInfinity
                }

                [pscustomobject]@{
                    State          = [string] $download.state
                    Index          = [int] $download.index
                    Game           = [string] $download.game
                    File           = [string] $download.file
                    PartialPath    = $partialPath
                    CurrentBytes   = $currentBytes
                    ExpectedBytes  = $expectedBytes
                    Fraction       = $fileFraction
                    BytesPerSecond = $bytesPerSecond
                    EtaSeconds     = $fileEta
                }
            }
        )
        $previousSamples = $newSamples

        $overallBytes = if ($status.PSObject.Properties.Name -contains 'downloaded_bytes') {
            [long] $status.downloaded_bytes
        }
        else {
            [long] $status.completed_bytes + [long] (($activeStatistics | Measure-Object CurrentBytes -Sum).Sum)
        }
        $totalBytes = [long] $status.total_bytes
        $overallFraction = if ($totalBytes -gt 0) { $overallBytes / [double] $totalBytes } else { 0.0 }
        $overallEta = if ($totalBytesPerSecond -gt 0 -and $totalBytes -gt $overallBytes) {
            ($totalBytes - $overallBytes) / $totalBytesPerSecond
        }
        else {
            [double]::PositiveInfinity
        }

        Write-Host ''
        if ($activeStatistics.Count -gt 0) {
            foreach ($active in $activeStatistics) {
                $stateColor = if ($active.State -eq 'Verifying') { 'Yellow' } else { 'Green' }
                Write-Host ("[{0}] File {1}/{2}: {3}" -f $active.State, $active.Index, $status.total_files, $active.Game) -ForegroundColor $stateColor
                Write-Host ("  Name:   {0}" -f $active.File)
                Write-Host ("  Output: {0}" -f $active.PartialPath)
                Write-Host ("  {0} {1,6:N2}%  {2} / {3}" -f (New-ProgressBar -Fraction $active.Fraction -Width 32), ($active.Fraction * 100), (Format-ByteCount $active.CurrentBytes), (Format-ByteCount $active.ExpectedBytes))
                Write-Host ("  Speed: {0}/s    ETA: {1}" -f (Format-ByteCount ([long] $active.BytesPerSecond)), (Format-Duration $active.EtaSeconds))
                Write-Host ''
            }
        }
        else {
            Write-Host 'Active downloads: --'
            Write-Host ''
        }

        Write-Host ("Combined:     {0}/s    overall ETA: {1}" -f (Format-ByteCount ([long] $totalBytesPerSecond)), (Format-Duration $overallEta))
        Write-Host ("Overall:      {0} {1,6:N2}%  {2} / {3}" -f (New-ProgressBar -Fraction $overallFraction), ($overallFraction * 100), (Format-ByteCount $overallBytes), (Format-ByteCount $totalBytes))

        if ($status.error_message) {
            Write-Host ''
            Write-Host ("ERROR: {0}" -f $status.error_message) -ForegroundColor Red
        }

        $logPath = if ($status.log_path) { [string] $status.log_path } else { $fallbackLogPath }
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            Write-Host ''
            Write-Host 'Recent activity:' -ForegroundColor Cyan
            Get-Content -LiteralPath $logPath -Tail 5 | ForEach-Object { Write-Host "  $_" }
        }

        if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
            $recentErrors = @(Get-Content -LiteralPath $stderrPath -Tail 3)
            if ($recentErrors.Count -gt 0) {
                Write-Host ''
                Write-Host 'Recent downloader errors:' -ForegroundColor Red
                $recentErrors | ForEach-Object { Write-Host "  $_" }
            }
        }

        if ($Once) { break }
        if ($status.state -in @('Complete', 'Failed') -and -not $process) { break }
        Start-Sleep -Seconds $RefreshSeconds
    }
}
finally {
    try { [Console]::CursorVisible = $true } catch { }
}
