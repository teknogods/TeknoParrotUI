param(
    [Parameter(Mandatory = $true)]
    [string]$DeviceSerial,

    [Parameter(Mandatory = $true)]
    [string]$SourceFile,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [ValidateRange(16, 1024)]
    [int]$ChunkSizeMiB = 256,

    [ValidateRange(1, 20)]
    [int]$RetryCount = 5,

    [string]$AdbPath
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
if (-not $adb) {
    throw 'adb.exe was not found in any configured Android SDK.'
}

$source = Get-Item -LiteralPath $SourceFile
if ($source.PSIsContainer) {
    throw "SourceFile must be a file: $SourceFile"
}

function Quote-Remote([string]$Value) {
    $quote = [string][char]39
    $escapedQuote = [string]::Concat([char]39, [char]92, [char]39, [char]39)
    return $quote + $Value.Replace($quote, $escapedQuote) + $quote
}

function Invoke-AdbText([string[]]$Arguments) {
    $output = & $adb -s $DeviceSerial @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ADB failed: $($Arguments -join ' ')"
    }
    return ($output -join "`n").Trim()
}

function Get-RemoteLength {
    $quoted = Quote-Remote $DestinationPath
    $value = Invoke-AdbText @('shell', "if [ -f $quoted ]; then stat -c %s $quoted; else echo 0; fi")
    $length = 0L
    if (-not [long]::TryParse($value, [ref]$length)) {
        throw "Unable to read remote file length: $value"
    }
    return $length
}

function Send-Chunk([long]$Offset, [long]$Length) {
    $seekMiB = [long]($Offset / 1MB)
    $quoted = Quote-Remote $DestinationPath
    $remoteCommand = "dd of=$quoted bs=1048576 seek=$seekMiB conv=notrunc"

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $adb
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-s', $DeviceSerial, 'exec-in', 'sh', '-c', $remoteCommand)) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::Start($startInfo)
    try {
        $input = $process.StandardInput.BaseStream
        $file = [IO.File]::OpenRead($source.FullName)
        try {
            $file.Position = $Offset
            $remaining = $Length
            $buffer = [byte[]]::new(4MB)
            while ($remaining -gt 0) {
                $requested = [int][Math]::Min($buffer.Length, $remaining)
                $read = $file.Read($buffer, 0, $requested)
                if ($read -le 0) {
                    throw "Unexpected end of source at offset $($file.Position)."
                }
                $input.Write($buffer, 0, $read)
                $remaining -= $read
            }
            $input.Flush()
        }
        finally {
            $file.Dispose()
            $process.StandardInput.Close()
        }

        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "ADB chunk write failed with exit code $($process.ExitCode): $standardError $standardOutput"
        }
    }
    finally {
        $process.Dispose()
    }
}

Invoke-AdbText @('wait-for-device') | Out-Null
Invoke-AdbText @('shell', "mkdir -p $(Quote-Remote ([IO.Path]::GetDirectoryName($DestinationPath).Replace('\', '/')))") | Out-Null

$chunkSize = [long]$ChunkSizeMiB * 1MB
$remoteLength = Get-RemoteLength
if ($remoteLength -gt $source.Length) {
    throw "Remote file is larger than the source ($remoteLength > $($source.Length))."
}

# A failed stream may leave the final range incomplete. Resume from the last
# complete chunk boundary and overwrite that range safely.
$offset = [long]([Math]::Floor($remoteLength / $chunkSize) * $chunkSize)
Write-Output (
    "Resuming {0} at {1:N2}/{2:N2} GiB using {3} MiB chunks." -f
    $source.Name, ($offset / 1GB), ($source.Length / 1GB), $ChunkSizeMiB)

while ($offset -lt $source.Length) {
    $length = [Math]::Min($chunkSize, $source.Length - $offset)
    $attempt = 0
    while ($true) {
        $attempt++
        try {
            Send-Chunk $offset $length
            $completedOffset = $offset + $length
            $remoteLength = Get-RemoteLength
            if ($remoteLength -lt $completedOffset) {
                throw "Remote length $remoteLength did not reach completed offset $completedOffset."
            }
            break
        }
        catch {
            if ($attempt -ge $RetryCount) {
                throw
            }
            Write-Warning "Chunk at $offset failed (attempt $attempt/$RetryCount): $($_.Exception.Message)"
            if ($DeviceSerial.Contains(':')) {
                & $adb connect $DeviceSerial | Out-Null
            }
            & $adb -s $DeviceSerial wait-for-device
        }
    }

    $offset += $length
    $percent = 100.0 * $offset / $source.Length
    Write-Output ("{0,6:N2}%  {1:N2}/{2:N2} GiB" -f $percent, ($offset / 1GB), ($source.Length / 1GB))
}

$finalLength = Get-RemoteLength
if ($finalLength -ne $source.Length) {
    throw "Remote length mismatch: expected $($source.Length), got $finalLength."
}

Write-Output "Resumable file transfer complete: $DestinationPath ($finalLength bytes)."
