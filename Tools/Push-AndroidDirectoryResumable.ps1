param(
    [Parameter(Mandatory = $true)]
    [string]$DeviceSerial,

    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory,

    [ValidateRange(64, 4096)]
    [int]$LargeFileThresholdMiB = 1024,

    [ValidateRange(16, 1024)]
    [int]$ChunkSizeMiB = 256,

    [ValidateRange(1, 20)]
    [int]$RetryCount = 5,

    [string]$AdbPath,

    [string[]]$ExcludeTopLevelDirectories = @('[SYSTEM]')
)

$ErrorActionPreference = 'Stop'

$adbCandidates = @(
    $AdbPath,
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe'),
    (Join-Path $env:USERPROFILE 'android-toolchain\sdk-platform37\platform-tools\adb.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$adb = $adbCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
$fileTransfer = Join-Path $PSScriptRoot 'Push-AndroidFileResumable.ps1'
if (-not $adb) {
    throw 'adb.exe was not found in any configured Android SDK.'
}
if (-not (Test-Path -LiteralPath $fileTransfer)) {
    throw "Resumable file-transfer helper was not found at $fileTransfer."
}

$source = Get-Item -LiteralPath $SourceDirectory
if (-not $source.PSIsContainer) {
    throw "SourceDirectory must be a directory: $SourceDirectory"
}

$destination = $DestinationDirectory.Replace('\', '/').TrimEnd('/')
if (-not $destination.StartsWith('/sdcard/Download/TeknoParrotGames/',
        [StringComparison]::Ordinal)) {
    throw 'DestinationDirectory must be below /sdcard/Download/TeknoParrotGames/.'
}

function Quote-Remote([string]$Value) {
    $quote = [string][char]39
    $escapedQuote = [string]::Concat([char]39, [char]92, [char]39, [char]39)
    return $quote + $Value.Replace($quote, $escapedQuote) + $quote
}

function Invoke-AdbText([string[]]$Arguments) {
    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        $output = & $adb -s $DeviceSerial @Arguments
        if ($LASTEXITCODE -eq 0) {
            return @($output)
        }

        if ($attempt -eq $RetryCount) {
            throw "ADB failed after $RetryCount attempt(s): $($Arguments -join ' ')"
        }

        Write-Warning (
            "ADB command was interrupted; restarting the daemon and retrying attempt {0}/{1}." -f
            ($attempt + 1), $RetryCount)
        & $adb start-server 2>&1 | Out-Null
        Start-Sleep -Milliseconds ([Math]::Min(4000, 500 * $attempt))
    }
}

function Invoke-AdbPushWithRetry(
    [string]$LocalPath,
    [string]$RemotePath,
    [switch]$Sync) {
    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        $arguments = @('-s', $DeviceSerial, 'push')
        if ($Sync) {
            $arguments += '--sync'
        }
        $arguments += @($LocalPath, $RemotePath)
        & $adb @arguments
        if ($LASTEXITCODE -eq 0) {
            return
        }

        if ($attempt -eq $RetryCount) {
            throw "ADB push failed after $RetryCount attempt(s): $LocalPath"
        }

        Write-Warning (
            "ADB push was interrupted; retrying attempt {0}/{1}: {2}" -f
            ($attempt + 1), $RetryCount, $LocalPath)
        & $adb start-server 2>&1 | Out-Null
        Start-Sleep -Milliseconds ([Math]::Min(4000, 500 * $attempt))
    }
}

function Get-RemoteManifest {
    $result = @{}
    $quoted = Quote-Remote $destination
    $lines = Invoke-AdbText @(
        'shell',
        "if [ -d $quoted ]; then find $quoted -type f -printf '%P|%s\n'; fi")
    foreach ($line in $lines) {
        if ($line -match '^(.*)\|(\d+)\r?$') {
            $result[$Matches[1]] = [long]$Matches[2]
        }
    }
    return $result
}

function Is-Excluded([string]$RelativePath) {
    $topLevel = ($RelativePath -split '/', 2)[0]
    return $ExcludeTopLevelDirectories -contains $topLevel
}

Invoke-AdbText @('wait-for-device') | Out-Null
Invoke-AdbText @('shell', "mkdir -p $(Quote-Remote $destination)") | Out-Null

$hostFiles = @(
    Get-ChildItem -LiteralPath $source.FullName -Recurse -File |
        ForEach-Object {
            $relative = ([IO.Path]::GetRelativePath(
                    $source.FullName, $_.FullName)).Replace('\', '/')
            if (-not (Is-Excluded $relative)) {
                [pscustomobject]@{
                    File = $_
                    RelativePath = $relative
                }
            }
        })
if ($hostFiles.Count -eq 0) {
    throw "No transferable files were found below $($source.FullName)."
}

$threshold = [long]$LargeFileThresholdMiB * 1MB
$remoteFiles = Get-RemoteManifest
$pending = @(
    $hostFiles | Where-Object {
        -not $remoteFiles.ContainsKey($_.RelativePath) -or
        $remoteFiles[$_.RelativePath] -ne $_.File.Length
    })

# ADB's native directory traversal is dramatically faster for trees containing
# hundreds of small assets. Use it as a bulk first pass only when the tree has
# no large file that needs chunk-level recovery, then fall back to exact
# per-file resume for anything the native pass missed or truncated.
if ($pending.Count -gt 0 -and
    -not ($hostFiles | Where-Object { $_.File.Length -ge $threshold } |
        Select-Object -First 1)) {
    Write-Output (
        "Bulk-syncing small-file tree before exact manifest verification ($($pending.Count) pending).")
    Get-ChildItem -LiteralPath $source.FullName -Force |
        Where-Object { -not (Is-Excluded $_.Name) } |
        ForEach-Object {
            Invoke-AdbPushWithRetry -LocalPath $_.FullName `
                -RemotePath "$destination/" -Sync
        }
    $remoteFiles = Get-RemoteManifest
    $pending = @(
        $hostFiles | Where-Object {
            -not $remoteFiles.ContainsKey($_.RelativePath) -or
            $remoteFiles[$_.RelativePath] -ne $_.File.Length
        })
}

$totalBytes = [long](($hostFiles | ForEach-Object { $_.File.Length } |
            Measure-Object -Sum).Sum)
$pendingBytes = [long](($pending | ForEach-Object { $_.File.Length } |
            Measure-Object -Sum).Sum)
$completeBytes = $totalBytes - $pendingBytes

Write-Output (
    'Directory transfer: {0} file(s), {1:N2} GiB total; {2} pending, {3:N2} GiB already verified.' -f
    $hostFiles.Count, ($totalBytes / 1GB), $pending.Count, ($completeBytes / 1GB))

$index = 0
foreach ($entry in $pending) {
    $index++
    $file = $entry.File
    $remotePath = $destination + '/' + $entry.RelativePath
    $remoteLength = if ($remoteFiles.ContainsKey($entry.RelativePath)) {
        [long]$remoteFiles[$entry.RelativePath]
    } else {
        0L
    }
    $overallPercent = if ($totalBytes -eq 0) {
        100.0
    } else {
        100.0 * $completeBytes / $totalBytes
    }
    Write-Output (
        '[{0}/{1}] {2:N2}% {3} ({4:N2} MiB)' -f
        $index, $pending.Count, $overallPercent, $entry.RelativePath, ($file.Length / 1MB))

    if ($remoteLength -gt $file.Length) {
        throw "Remote file is larger than its source: $remotePath ($remoteLength > $($file.Length))."
    }

    $parent = $remotePath.Substring(0, $remotePath.LastIndexOf('/'))
    Invoke-AdbText @('shell', "mkdir -p $(Quote-Remote $parent)") | Out-Null
    if ($file.Length -ge $threshold -or $remoteLength -gt 0) {
        & $fileTransfer `
            -DeviceSerial $DeviceSerial `
            -SourceFile $file.FullName `
            -DestinationPath $remotePath `
            -ChunkSizeMiB $ChunkSizeMiB `
            -RetryCount $RetryCount `
            -AdbPath $adb
    } else {
        Invoke-AdbPushWithRetry -LocalPath $file.FullName -RemotePath $remotePath
    }
    $completeBytes += $file.Length
}

$finalRemoteFiles = Get-RemoteManifest
$mismatches = @(
    $hostFiles | Where-Object {
        -not $finalRemoteFiles.ContainsKey($_.RelativePath) -or
        $finalRemoteFiles[$_.RelativePath] -ne $_.File.Length
    })
if ($mismatches.Count -ne 0) {
    throw "Directory verification failed for $($mismatches.Count) file(s)."
}

Write-Output (
    'Resumable directory transfer complete: {0} file(s), {1:N2} GiB verified; remote extras preserved.' -f
    $hostFiles.Count, ($totalBytes / 1GB))
