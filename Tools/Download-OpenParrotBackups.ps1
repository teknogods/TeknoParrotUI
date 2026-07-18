[CmdletBinding()]
param(
    [Parameter()]
    [string] $Destination = $env:TEKNOPARROT_OPENPARROT_BACKUP_ROOT,

    [Parameter()]
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter()]
    [switch] $ListOnly,

    [Parameter()]
    [switch] $VerifyExisting,

    [Parameter()]
    [switch] $ConfirmAuthorized,

    [Parameter()]
    [ValidateRange(1, 16)]
    [int] $ConcurrentDownloads = 4
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Destination)) {
    throw (
        'Specify -Destination or set ' +
        'TEKNOPARROT_OPENPARROT_BACKUP_ROOT.')
}

# This script only downloads files that correspond to source GameProfiles whose
# EmulatorType is exactly OpenParrot. Archive metadata supplies the authoritative
# remote filename, size, and MD5 checksum.
$archiveItemIds = @(
    'tp__roms_2'
    'tp-roms_1'
    'tp-roms_0'
)

$gameProfilesPath = Join-Path $RepositoryRoot 'TeknoParrotUi.Common\GameProfiles'
$gameMetadataPath = Join-Path $RepositoryRoot 'TeknoParrotUi.Common\Metadata'

if (-not (Test-Path -LiteralPath $gameProfilesPath -PathType Container)) {
    throw "GameProfiles directory not found: $gameProfilesPath"
}

if (-not (Test-Path -LiteralPath $gameMetadataPath -PathType Container)) {
    throw "Metadata directory not found: $gameMetadataPath"
}

if (-not $ListOnly -and -not $ConfirmAuthorized) {
    throw 'Only download files you are authorized to copy. Re-run with -ConfirmAuthorized, or use -ListOnly for an audit.'
}

function ConvertTo-SearchText {
    param([Parameter(Mandatory)][string] $Value)

    $text = $Value.ToLowerInvariant().Normalize([Text.NormalizationForm]::FormD)
    $builder = [Text.StringBuilder]::new()
    foreach ($character in $text.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($character) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void] $builder.Append($character)
        }
    }

    $text = $builder.ToString().Normalize([Text.NormalizationForm]::FormC)
    $text = $text -replace '&', ' and '
    $text = $text -replace '[^a-z0-9]+', ' '
    return ($text -replace '\s+', ' ').Trim()
}

function Get-SearchTitle {
    param(
        [Parameter(Mandatory)][string] $ProfileName,
        [Parameter(Mandatory)][string] $GameName
    )

    $aliases = @{
        Goketsuji                                  = 'Gouketsuji Ichizoku Matsuri Senzo Kuyou'
        KingofFightersMaximumImpactRegulationA    = 'King of Fighters Maximum Impact Regulation A'
        PowerInstinctV                            = 'Gouketsuji Ichizoku Matsuri Senzo Kuyou'
        Taiko                                     = 'Taiko no Tatsujin Nijiro Version'
    }

    if ($aliases.ContainsKey($ProfileName)) {
        return $aliases[$ProfileName]
    }

    $title = $GameName
    $title = $title -replace '\s+for NESiCAxLive$', ''
    $title = $title -replace '\s+APM3 Edition$', ''
    $title = $title -replace '\s+v\d+(?:\.\d+)+$', ''
    $title = $title -replace '\s+\((?:eX-Board|Third-Party Emulator|Taito Type X2)\)$', ''
    return $title
}

function ConvertTo-EncodedPath {
    param([Parameter(Mandatory)][string] $Path)

    return (($Path -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
}

function Get-ExistingState {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][long] $ExpectedSize
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'Missing'
    }

    $length = (Get-Item -LiteralPath $Path).Length
    if ($length -eq $ExpectedSize) {
        return 'Present'
    }
    if ($length -lt $ExpectedSize) {
        return 'Partial'
    }
    return 'SizeMismatch'
}

# Explicit patterns prevent a similarly named platform/version from being used
# when more than one archive member shares a title.
$preferredRemotePatterns = @{
    ArcanaHeart2Exboard                         = '^Arcana Heart 2 .*\[EXAMU eX-BOARD\]'
    BattleFantasia                              = '^Battle Fantasia Offline Edition '
    BattleFantasiaNesica                        = '^Battle Fantasia Network Edition '
    ChaosBreaker                                = '^Chaos Breaker \(2004\)\[Taito Type X\]'
    ChaosCodeNSOC103                            = '^Chaos Code - New Sign of Catastrophe \(1\.03\.00\)'
    DaemonBrideExboard                          = '^Daemon Bride \(2009\).*\[EXAMU eX-BOARD\]'
    DengekiBunkoFightingClimax                  = '^Dengeki Bunko - Fighting Climax \(APM3 Edition\)'
    GGXrdAPM3                                   = '^Guilty Gear Xrd Rev2 \(APM3\)'
    Goketsuji                                   = '^Gouketsuji Ichizoku - Matsuri Senzo Kuyou \(1\.0\.1\).*NESiCAxLive'
    KingofFighters98UltimateMatchFinalEditionNesica = "^The King of Fighters '98 - Ultimate Match Final Edition "
    KingofFighters98UnlimitedMatch              = "^The King of Fighters '98 - Ultimate Match \(2008\)"
    KingofFightersXIII                          = '^The King of Fighters XIII \(2010\)'
    KingofFightersXIIIClimax                    = '^The King of Fighters XIII Climax \(2012\).*Type X2'
    Lupin3                                      = '^Lupin'
    PowerInstinctV                              = '^Gouketsuji Ichizoku - Matsuri Senzo Kuyou \(1\.0\.0\).*Type X2'
    RaidenIII                                   = '^Raiden III .*\[Taito Type X\]'
    RaidenIIINesica                             = '^Raiden III .*NESiCAxLive'
    RaidenIV                                    = '^Raiden IV .*\[Taito Type X\]'
    RaidenIVNesica                              = '^Raiden IV .*NESiCAxLive'
    SenkoNoRondeDuo                             = '^Senko no Ronde DUO - Dis-United Order \(2\.00\).*Type X2'
    SenkoNoRondeDuoNesica                       = '^Senko no Ronde DUO - Dis-United Order \(2\.35\).*NESiCAxLive'
    SpaceInvaders                               = '^Space Invaders '
    StarWars                                    = '^Star Wars Battle Pod \(1\.00\)'
    StreetFighterIV                             = '^Street Fighter IV \(2008\)'
    StreetFighterVTypeArcade                    = '^Street Fighter V Type Arcade \(1\.01\.00\)'
    SuperStreetFighterIVArcadeEdition           = '^Super Street Fighter IV Arcade Edition \(2010-09-14\)'
    SuperStreetFighterIVArcadeEditionEXP        = '^Super Street Fighter IV Arcade Edition \(2010-11-04\)'
    SuperStreetFighterIVArcadeEditionVer2012    = '^Super Street Fighter IV Arcade Edition Ver 2012 '
    SuggoiArcanaHeart2Exboard                   = '^Suggoi! Arcana Heart 2 .*\[EXAMU eX-BOARD\]'
    Taiko                                       = '^Taiko no Tatsujin Nijiro Version '
    TaisenMixParty                              = '^Taisen Mix Party '
    Tekken7                                     = '^Tekken 7 \(1\.2\)'
    Tekken7FR                                   = '^Tekken 7 Fated Retribution \(1\.06\.00\)'
    TroubleWitches                              = '^Trouble Witches AC Episode1.*\(2008\)\[Taito Type X\]'
    TroubleWitchesNesica                        = '^Trouble Witches AC Episode1.*\(1\.12\).*NESiCAxLive'
    UnderNightAPM3                              = '^Under Night In-Birth Exe - Late \[cl-r\] \(APM3 Edition\)'
    VF5FSapm3                                   = '^Virtua Fighter 5 Final Showdown \(APM3\)'
    WMMT5                                       = '^Wangan Midnight Maximum Tune 5 \('
    WMMT5DX                                     = '^Wangan Midnight Maximum Tune 5DX \('
    WMMT5DXPlus                                 = '^Wangan Midnight Maximum Tune 5DX\+ \('
    WMMT6                                       = '^Wangan Midnight Maximum Tune 6 \('
    WMMT6R                                      = '^Wangan Midnight Maximum Tune 6R \('
}

Write-Host 'Reading OpenParrot source profiles...'
$targets = foreach ($profileFile in Get-ChildItem -LiteralPath $gameProfilesPath -Filter '*.xml' -File) {
    [xml] $profileXml = Get-Content -LiteralPath $profileFile.FullName -Raw
    if ([string] $profileXml.GameProfile.EmulatorType -cne 'OpenParrot') {
        continue
    }

    $metadataFile = Join-Path $gameMetadataPath ($profileFile.BaseName + '.json')
    if (-not (Test-Path -LiteralPath $metadataFile -PathType Leaf)) {
        throw "Metadata file not found for $($profileFile.Name): $metadataFile"
    }

    $metadata = Get-Content -LiteralPath $metadataFile -Raw | ConvertFrom-Json
    [pscustomobject]@{
        ProfileName = $profileFile.BaseName
        GameName    = [string] $metadata.game_name
        Platform    = [string] $metadata.platform
        ReleaseYear = [string] $metadata.release_year
        SearchTitle = Get-SearchTitle -ProfileName $profileFile.BaseName -GameName ([string] $metadata.game_name)
    }
}

Write-Host "Found $($targets.Count) OpenParrot profiles. Reading Archive.org metadata..."
$archiveFiles = foreach ($itemId in $archiveItemIds) {
    $metadataUri = "https://archive.org/metadata/$itemId"
    try {
        $archiveMetadata = Invoke-RestMethod -Uri $metadataUri -Method Get
    }
    catch {
        throw "Failed to read $metadataUri. $($_.Exception.Message)"
    }

    foreach ($file in $archiveMetadata.files) {
        $remoteName = [string] $file.name
        if ($remoteName -notlike 'TeknoParrot Collection/*.zip') {
            continue
        }

        $leafName = [IO.Path]::GetFileName($remoteName)
        [pscustomobject]@{
            ItemId     = $itemId
            RemoteName = $remoteName
            LeafName   = $leafName
            SearchText = ConvertTo-SearchText -Value $leafName
            Size       = [long] $file.size
            Md5        = ([string] $file.md5).ToLowerInvariant()
        }
    }
}

$resolved = [Collections.Generic.List[object]]::new()
$unmatched = [Collections.Generic.List[object]]::new()

foreach ($target in $targets) {
    $match = $null
    if ($preferredRemotePatterns.ContainsKey($target.ProfileName)) {
        $pattern = $preferredRemotePatterns[$target.ProfileName]
        $preferred = @($archiveFiles | Where-Object { $_.LeafName -match $pattern })
        if ($preferred.Count -eq 1) {
            $match = $preferred[0]
        }
        elseif ($preferred.Count -gt 1) {
            $unmatched.Add([pscustomobject]@{
                ProfileName = $target.ProfileName
                GameName    = $target.GameName
                Reason      = "Pattern matched $($preferred.Count) files"
            })
            continue
        }
        else {
            $unmatched.Add([pscustomobject]@{
                ProfileName = $target.ProfileName
                GameName    = $target.GameName
                Reason      = 'No matching file in supplied archive items'
            })
            continue
        }
    }
    else {
        $searchText = ConvertTo-SearchText -Value $target.SearchTitle
        $candidates = @($archiveFiles | Where-Object { $_.SearchText.Contains($searchText) })
        if ($candidates.Count -eq 0) {
            $unmatched.Add([pscustomobject]@{
                ProfileName = $target.ProfileName
                GameName    = $target.GameName
                Reason      = 'No matching file in supplied archive items'
            })
            continue
        }

        $ranked = foreach ($candidate in $candidates) {
            $score = 0
            if ($candidate.SearchText.StartsWith($searchText)) {
                $score += 1000
            }
            elseif ($candidate.SearchText.Contains($searchText)) {
                $score += 500
            }

            $platformText = ConvertTo-SearchText -Value $target.Platform
            if ($platformText -and $candidate.SearchText.Contains($platformText)) {
                $score += 100
            }
            if ($target.ReleaseYear -and $candidate.LeafName -match "(?<!\d)$([regex]::Escape($target.ReleaseYear))(?!\d)") {
                $score += 20
            }

            [pscustomobject]@{ File = $candidate; Score = $score }
        }

        $ranked = @($ranked | Sort-Object Score -Descending)
        $top = @($ranked | Where-Object { $_.Score -eq $ranked[0].Score })
        if ($top.Count -ne 1) {
            $unmatched.Add([pscustomobject]@{
                ProfileName = $target.ProfileName
                GameName    = $target.GameName
                Reason      = "Ambiguous match ($($top.Count) equally ranked files)"
            })
            continue
        }
        $match = $top[0].File
    }

    $localPath = Join-Path $Destination $match.LeafName
    $resolved.Add([pscustomobject]@{
        ProfileName = $target.ProfileName
        GameName    = $target.GameName
        ItemId      = $match.ItemId
        RemoteName  = $match.RemoteName
        LeafName    = $match.LeafName
        Size        = $match.Size
        Md5         = $match.Md5
        LocalPath   = $localPath
        State       = Get-ExistingState -Path $localPath -ExpectedSize $match.Size
    })
}

$uniqueResolved = @($resolved | Group-Object LocalPath | ForEach-Object { $_.Group[0] })
$totalGiB = [math]::Round((($uniqueResolved | Measure-Object Size -Sum).Sum / 1GB), 2)
$presentGiB = [math]::Round((($uniqueResolved | Where-Object State -eq 'Present' | Measure-Object Size -Sum).Sum / 1GB), 2)
$remainingBytes = [long] 0
foreach ($entry in $uniqueResolved) {
    $existingLength = if (Test-Path -LiteralPath $entry.LocalPath -PathType Leaf) {
        (Get-Item -LiteralPath $entry.LocalPath).Length
    }
    else {
        0
    }
    $remainingForEntry = [long] $entry.Size - [long] $existingLength
    if ($remainingForEntry -gt 0) {
        $remainingBytes += $remainingForEntry
    }
}
$remainingGiB = [math]::Round(($remainingBytes / 1GB), 2)

Write-Host ''
Write-Host "Resolved: $($resolved.Count) / $($targets.Count) profiles"
Write-Host "Unique archive files: $($uniqueResolved.Count)"
Write-Host "Archive size: $totalGiB GiB; already present: $presentGiB GiB; remaining transfer: $remainingGiB GiB"
$resolved |
    Sort-Object GameName |
    Select-Object GameName, State, @{ Name = 'GiB'; Expression = { [math]::Round($_.Size / 1GB, 2) } }, LeafName |
    Format-Table -AutoSize

if ($unmatched.Count -gt 0) {
    Write-Warning "$($unmatched.Count) profiles have no unambiguous file in the three supplied archive items:"
    $unmatched | Sort-Object GameName | Format-Table GameName, ProfileName, Reason -AutoSize
}

if ($ListOnly) {
    return
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot((Resolve-Path -LiteralPath $Destination).Path))
$reserveBytes = 5GB
if ($drive.AvailableFreeSpace -lt ($remainingBytes + $reserveBytes)) {
    throw "Not enough free space on $($drive.Name). Need approximately $remainingGiB GiB plus a 5 GiB reserve."
}

$curl = (Get-Command curl.exe -ErrorAction Stop).Source
$logPath = Join-Path $Destination 'OpenParrot-download.log'
$statusPath = Join-Path $Destination 'OpenParrot-download-status.json'
$statusTempPath = $statusPath + '.tmp'
$sessionStarted = Get-Date
$totalBytes = [long] (($uniqueResolved | Measure-Object Size -Sum).Sum)
$orderedResolved = @($uniqueResolved | Sort-Object GameName)
$activeJobs = [Collections.Generic.List[object]]::new()
$failedEntries = [Collections.Generic.List[object]]::new()
$pendingEntries = [Collections.Generic.List[object]]::new()
$completedCount = 0
$completedBytes = [long] 0
$latestError = $null

function Get-DownloadedByteCount {
    param([Parameter(Mandatory)][long] $CompletedByteCount)

    $downloaded = $CompletedByteCount
    foreach ($statusEntry in $orderedResolved) {
        $candidatePartialPath = $statusEntry.LocalPath + '.partial'
        if (Test-Path -LiteralPath $candidatePartialPath -PathType Leaf) {
            $partialLength = [long] (Get-Item -LiteralPath $candidatePartialPath).Length
            if ($partialLength -gt $statusEntry.Size) {
                $partialLength = [long] $statusEntry.Size
            }
            $downloaded += $partialLength
        }
    }
    return $downloaded
}

function Write-DownloadStatus {
    param(
        [Parameter(Mandatory)][string] $State,
        [Parameter(Mandatory)][int] $CompletedCount,
        [Parameter(Mandatory)][long] $CompletedBytes,
        [Parameter()][object[]] $ActiveDownloads = @(),
        [Parameter()][AllowNull()][string] $ErrorMessage
    )

    $activeStatus = @(
        foreach ($job in $ActiveDownloads) {
            [ordered]@{
                state          = $job.State
                process_id     = $job.Process.Id
                index          = $job.Index
                game           = $job.Entry.GameName
                file           = $job.Entry.LeafName
                partial_path   = $job.Entry.LocalPath + '.partial'
                expected_bytes = [long] $job.Entry.Size
                started_at     = $job.StartedAt.ToString('o')
            }
        }
    )

    $current = if ($activeStatus.Count -gt 0) { $activeStatus[0] } else { $null }
    $status = [ordered]@{
        state                = $State
        process_id           = $PID
        started_at           = $sessionStarted.ToString('o')
        updated_at           = (Get-Date).ToString('o')
        concurrent_limit     = $ConcurrentDownloads
        active_downloads     = $activeStatus
        total_files          = $orderedResolved.Count
        completed_files      = $CompletedCount
        failed_downloads     = $failedEntries.Count
        current_index        = if ($current) { $current.index } else { $CompletedCount }
        current_game         = if ($current) { $current.game } else { $null }
        current_file         = if ($current) { $current.file } else { $null }
        partial_path         = if ($current) { $current.partial_path } else { $null }
        expected_bytes       = if ($current) { $current.expected_bytes } else { [long] 0 }
        completed_bytes      = $CompletedBytes
        downloaded_bytes     = Get-DownloadedByteCount -CompletedByteCount $CompletedBytes
        total_bytes          = $totalBytes
        unmatched_profiles   = $unmatched.Count
        log_path             = $logPath
        error_message        = $ErrorMessage
    }

    $json = $status | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($statusTempPath, $json, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $statusTempPath -Destination $statusPath -Force
}

function Start-CurlDownload {
    param([Parameter(Mandatory)][object] $Entry)

    $encodedRemoteName = ConvertTo-EncodedPath -Path $Entry.RemoteName
    $downloadUri = "https://archive.org/download/$($Entry.ItemId)/$encodedRemoteName"
    $partialPath = $Entry.LocalPath + '.partial'
    $arguments = @(
        '--location'
        '--fail'
        '--silent'
        '--show-error'
        '--connect-timeout', '30'
        '--speed-limit', '1024'
        '--speed-time', '300'
        '--retry', '20'
        '--retry-all-errors'
        '--retry-delay', '10'
        '--continue-at', '-'
        '--output', $partialPath
        $downloadUri
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $curl
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in $arguments) {
        [void] $startInfo.ArgumentList.Add([string] $argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Failed to start curl for $($Entry.LeafName)"
    }
    return $process
}

"$(Get-Date -Format o) Starting download of $($resolved.Count) resolved OpenParrot backups with $ConcurrentDownloads concurrent transfers." | Add-Content -LiteralPath $logPath
Write-DownloadStatus -State 'Preparing' -CompletedCount 0 -CompletedBytes 0 -ErrorMessage $null

try {
    for ($index = 0; $index -lt $orderedResolved.Count; $index++) {
        $entry = $orderedResolved[$index]
        $finalPath = $entry.LocalPath
        $partialPath = $finalPath + '.partial'

        if (Test-Path -LiteralPath $finalPath -PathType Leaf) {
            $finalLength = [long] (Get-Item -LiteralPath $finalPath).Length
            if ($finalLength -eq $entry.Size) {
                if ($VerifyExisting -and $entry.Md5) {
                    Write-Host "Verifying existing: $($entry.LeafName)"
                    $actualMd5 = (Get-FileHash -LiteralPath $finalPath -Algorithm MD5).Hash.ToLowerInvariant()
                    if ($actualMd5 -ne $entry.Md5) {
                        throw "MD5 mismatch for existing file: $finalPath"
                    }
                }
                $completedCount++
                $completedBytes += [long] $entry.Size
                continue
            }
            if ($finalLength -gt $entry.Size) {
                throw "Existing file is larger than the archive member and was not changed: $finalPath"
            }
            if (Test-Path -LiteralPath $partialPath -PathType Leaf) {
                throw "Both a partial final file and .partial file exist. Resolve them before retrying: $finalPath"
            }
            Move-Item -LiteralPath $finalPath -Destination $partialPath
        }

        $pendingEntries.Add([pscustomobject]@{
            Entry = $entry
            Index = $index + 1
        })
    }

    Write-DownloadStatus -State 'Preparing' -CompletedCount $completedCount -CompletedBytes $completedBytes -ErrorMessage $null
    $nextPendingIndex = 0

    while ($nextPendingIndex -lt $pendingEntries.Count -or $activeJobs.Count -gt 0) {
        while ($activeJobs.Count -lt $ConcurrentDownloads -and $nextPendingIndex -lt $pendingEntries.Count) {
            $pending = $pendingEntries[$nextPendingIndex]
            $nextPendingIndex++
            $process = Start-CurlDownload -Entry $pending.Entry
            $job = [pscustomobject]@{
                Entry     = $pending.Entry
                Index     = $pending.Index
                Process   = $process
                State     = 'Downloading'
                StartedAt = Get-Date
            }
            $activeJobs.Add($job)
            Write-Host "Downloading [$($activeJobs.Count)/$ConcurrentDownloads]: $($pending.Entry.GameName)"
            "$(Get-Date -Format o) Downloading $($pending.Entry.LeafName) from $($pending.Entry.ItemId) (curl PID $($process.Id))." | Add-Content -LiteralPath $logPath
        }

        Write-DownloadStatus -State 'Downloading' -CompletedCount $completedCount -CompletedBytes $completedBytes -ActiveDownloads @($activeJobs) -ErrorMessage $latestError
        if ($activeJobs.Count -eq 0) {
            break
        }

        Start-Sleep -Seconds 1
        $finishedJobs = @($activeJobs | Where-Object { $_.Process.HasExited })
        foreach ($job in $finishedJobs) {
            $entry = $job.Entry
            $partialPath = $entry.LocalPath + '.partial'
            $finalPath = $entry.LocalPath
            $exitCode = $job.Process.ExitCode

            if ($exitCode -ne 0) {
                $latestError = "curl failed with exit code $exitCode for $($entry.LeafName)"
                $failedEntries.Add([pscustomobject]@{ Entry = $entry; Error = $latestError })
                "$(Get-Date -Format o) FAILED: $latestError" | Add-Content -LiteralPath $logPath
                [void] $activeJobs.Remove($job)
                $job.Process.Dispose()
                continue
            }

            try {
                $downloadedSize = [long] (Get-Item -LiteralPath $partialPath).Length
                if ($downloadedSize -ne $entry.Size) {
                    throw "Size mismatch for $partialPath. Expected $($entry.Size), got $downloadedSize."
                }

                $job.State = 'Verifying'
                Write-DownloadStatus -State 'Downloading' -CompletedCount $completedCount -CompletedBytes $completedBytes -ActiveDownloads @($activeJobs) -ErrorMessage $latestError
                if ($entry.Md5) {
                    Write-Host "Verifying MD5: $($entry.LeafName)"
                    $actualMd5 = (Get-FileHash -LiteralPath $partialPath -Algorithm MD5).Hash.ToLowerInvariant()
                    if ($actualMd5 -ne $entry.Md5) {
                        throw "MD5 mismatch for $partialPath. Expected $($entry.Md5), got $actualMd5."
                    }
                }

                Move-Item -LiteralPath $partialPath -Destination $finalPath -Force
                $completedCount++
                $completedBytes += [long] $entry.Size
                "$(Get-Date -Format o) Completed $($entry.LeafName)." | Add-Content -LiteralPath $logPath
            }
            catch {
                $latestError = $_.Exception.Message
                $failedEntries.Add([pscustomobject]@{ Entry = $entry; Error = $latestError })
                "$(Get-Date -Format o) FAILED: $latestError" | Add-Content -LiteralPath $logPath
            }
            finally {
                [void] $activeJobs.Remove($job)
                $job.Process.Dispose()
            }
        }
    }
}
catch {
    $latestError = $_.Exception.Message
    foreach ($job in @($activeJobs)) {
        try {
            if (-not $job.Process.HasExited) {
                $job.Process.Kill($true)
            }
        }
        catch { }
    }
    Write-DownloadStatus -State 'Failed' -CompletedCount $completedCount -CompletedBytes $completedBytes -ActiveDownloads @($activeJobs) -ErrorMessage $latestError
    "$(Get-Date -Format o) FAILED: $latestError" | Add-Content -LiteralPath $logPath
    throw
}

if ($failedEntries.Count -gt 0) {
    $latestError = "$($failedEntries.Count) downloads failed after curl retries; re-run the script to resume them."
    Write-DownloadStatus -State 'Failed' -CompletedCount $completedCount -CompletedBytes $completedBytes -ErrorMessage $latestError
    "$(Get-Date -Format o) FAILED: $latestError" | Add-Content -LiteralPath $logPath
    Write-Error $latestError
    exit 1
}

"$(Get-Date -Format o) All resolved OpenParrot backups are complete." | Add-Content -LiteralPath $logPath
Write-DownloadStatus -State 'Complete' -CompletedCount $completedCount -CompletedBytes $completedBytes -ErrorMessage $null
Write-Host "All $($resolved.Count) resolved OpenParrot backups are complete."
