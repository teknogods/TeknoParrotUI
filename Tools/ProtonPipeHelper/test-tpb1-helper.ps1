[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Stop-TestProcess {
    param([Diagnostics.Process] $Process)
    if ($null -ne $Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force
        $Process.WaitForExit(5000) | Out-Null
    }
}

function Start-NativeHost {
    param(
        [Parameter(Mandatory)] [string] $SessionId,
        [Parameter(Mandatory)] [byte[]] $Token,
        [Parameter(Mandatory)] [string] $PipeName,
        [Parameter(Mandatory)] [ValidateSet(32, 64)] [int] $Architecture,
        [Parameter(Mandatory)] [ValidateSet('accept', 'reject')] [string] $Mode
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = Join-Path $PSScriptRoot 'tpb1host.exe'
    $startInfo.WorkingDirectory = $PSScriptRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @(
        $SessionId,
        [Convert]::ToHexString($Token),
        $PipeName,
        $Architecture,
        $Mode)) {
        $startInfo.ArgumentList.Add([string]$argument)
    }

    $process = [Diagnostics.Process]::Start($startInfo)
    $portLine = $process.StandardOutput.ReadLine()
    if ($portLine -notmatch '^PORT=(\d+)$') {
        $errorText = $process.StandardError.ReadToEnd()
        Stop-TestProcess $process
        throw "Native TPB1 host did not publish a port: $portLine $errorText"
    }
    return [pscustomobject]@{ Process = $process; Port = [int]$Matches[1] }
}

function Invoke-AuthenticatedRoundTrip {
    param(
        [Parameter(Mandatory)] [string] $HelperName,
        [Parameter(Mandatory)] [string] $GuestName,
        [Parameter(Mandatory)] [string] $Architecture
    )

    $helperPath = Join-Path $PSScriptRoot $HelperName
    $guestPath = Join-Path $PSScriptRoot $GuestName
    $sessionId = [Guid]::NewGuid().ToString('N')
    $token = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $pipeName = "TPB1Local${Architecture}_$([Guid]::NewGuid().ToString('N'))"
    $mappingName = "TPB1LocalMap${Architecture}_$([Guid]::NewGuid().ToString('N'))"
    $architectureByte = if ($Architecture -eq 'x64') { [byte]64 } else { [byte]32 }
    $hostInfo = $null
    $process = $null
    $guest = $null
    $mapping = $null
    $accessor = $null
    try {
        $mapping = [IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew(
            $mappingName, 4096, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::ReadWrite)
        $accessor = $mapping.CreateViewAccessor()
        $hostPrefix = [byte[]]::new(16)
        for ($index = 0; $index -lt $hostPrefix.Length; $index++) {
            $hostPrefix[$index] = [byte](0xA0 + $index)
        }
        $accessor.WriteArray(0, $hostPrefix, 0, $hostPrefix.Length) | Out-Null

        $hostInfo = Start-NativeHost -SessionId $sessionId -Token $token `
            -PipeName $pipeName -Architecture $architectureByte -Mode accept
        $arguments = @(
            'pipe', '--name', $pipeName, '--host', '127.0.0.1', '--port', $hostInfo.Port,
            '--session', $sessionId, '--token', [Convert]::ToHexString($token))
        $errorLog = Join-Path $PSScriptRoot "tpb1-helper-$Architecture.log"
        $process = Start-Process -FilePath $helperPath -ArgumentList $arguments `
            -WindowStyle Hidden -RedirectStandardError $errorLog -PassThru
        $guestOut = Join-Path $PSScriptRoot "tpb1-guest-$Architecture.out.log"
        $guestError = Join-Path $PSScriptRoot "tpb1-guest-$Architecture.err.log"
        $guest = Start-Process -FilePath $guestPath `
            -ArgumentList @($pipeName, $mappingName, 4096) -WindowStyle Hidden `
            -RedirectStandardOutput $guestOut -RedirectStandardError $guestError -PassThru

        if (-not $hostInfo.Process.WaitForExit(15000)) {
            Stop-TestProcess $hostInfo.Process
            $hostOutput = $hostInfo.Process.StandardOutput.ReadToEnd()
            $hostError = $hostInfo.Process.StandardError.ReadToEnd()
            throw "$Architecture native TPB1 host did not finish: $hostOutput $hostError"
        }
        $hostOutput = $hostInfo.Process.StandardOutput.ReadToEnd()
        $hostError = $hostInfo.Process.StandardError.ReadToEnd()
        if ($hostInfo.Process.ExitCode -ne 0 -or
            $hostOutput -notmatch 'TPB1_NATIVE_ROUND_TRIP=PASS' -or
            $hostOutput -notmatch 'RANDOMIZED_BYTES_EACH_DIRECTION=1048576') {
            throw "$Architecture native TPB1 host failed: $hostOutput $hostError"
        }
        if (-not $guest.WaitForExit(15000)) {
            throw "$Architecture native guest did not exit after its pipe response."
        }
        if ($guest.ExitCode -ne 0) {
            throw "$Architecture native guest failed with exit code $($guest.ExitCode)."
        }
        $marker = [byte[]]::new(16)
        $accessor.ReadArray(32, $marker, 0, $marker.Length) | Out-Null
        $expectedMarker = [byte[]](
            0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7,
            0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF)
        if ([Convert]::ToHexString($marker) -ne [Convert]::ToHexString($expectedMarker) -or
            $accessor.ReadByte(48) -ne $architectureByte) {
            throw "$Architecture native guest did not update the shared mapping."
        }
        Write-Host "$Architecture authenticated native guest pipe/page + 1 MiB/dir: PASS"
    }
    finally {
        Stop-TestProcess $guest
        Stop-TestProcess $process
        if ($null -ne $hostInfo) { Stop-TestProcess $hostInfo.Process }
        if ($null -ne $accessor) { $accessor.Dispose() }
        if ($null -ne $mapping) { $mapping.Dispose() }
    }
}

function Invoke-WrongTokenRejection {
    $helperPath = Join-Path $PSScriptRoot 'pipehelper.exe'
    $guestPath = Join-Path $PSScriptRoot 'bridgeguest64.exe'
    $sessionId = [Guid]::NewGuid().ToString('N')
    $expectedToken = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $sentToken = [byte[]]$expectedToken.Clone()
    $sentToken[0] = $sentToken[0] -bxor 0x80
    $pipeName = "TPB1Reject_$([Guid]::NewGuid().ToString('N'))"
    $mappingName = "TPB1RejectMap_$([Guid]::NewGuid().ToString('N'))"
    $hostInfo = $null
    $process = $null
    $guest = $null
    $mapping = $null
    $accessor = $null
    try {
        $mapping = [IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew(
            $mappingName, 4096, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::ReadWrite)
        $accessor = $mapping.CreateViewAccessor()
        $hostPrefix = [byte[]]::new(16)
        for ($index = 0; $index -lt $hostPrefix.Length; $index++) {
            $hostPrefix[$index] = [byte](0xA0 + $index)
        }
        $accessor.WriteArray(0, $hostPrefix, 0, $hostPrefix.Length) | Out-Null

        $hostInfo = Start-NativeHost -SessionId $sessionId -Token $expectedToken `
            -PipeName $pipeName -Architecture 64 -Mode reject
        $arguments = @(
            'pipe', '--name', $pipeName, '--host', '127.0.0.1', '--port', $hostInfo.Port,
            '--session', $sessionId, '--token', [Convert]::ToHexString($sentToken))
        $process = Start-Process -FilePath $helperPath -ArgumentList $arguments `
            -WindowStyle Hidden -PassThru
        $guest = Start-Process -FilePath $guestPath `
            -ArgumentList @($pipeName, $mappingName, 4096) -WindowStyle Hidden -PassThru
        if (-not $hostInfo.Process.WaitForExit(10000)) {
            throw 'The wrong-token native TPB1 host did not finish.'
        }
        $hostOutput = $hostInfo.Process.StandardOutput.ReadToEnd()
        $hostError = $hostInfo.Process.StandardError.ReadToEnd()
        if ($hostInfo.Process.ExitCode -ne 0 -or
            $hostOutput -notmatch 'WRONG_TOKEN_REJECTED=1') {
            throw "The wrong-token native TPB1 host failed: $hostOutput $hostError"
        }
        if (-not $guest.WaitForExit(10000)) {
            throw 'The wrong-token native guest did not disconnect after rejection.'
        }
        if ($guest.ExitCode -eq 0) {
            throw 'The wrong-token native guest unexpectedly completed.'
        }
        Write-Host 'Wrong-token native guest receives no TPB1 acknowledgement: PASS'
    }
    finally {
        Stop-TestProcess $guest
        Stop-TestProcess $process
        if ($null -ne $hostInfo) { Stop-TestProcess $hostInfo.Process }
        if ($null -ne $accessor) { $accessor.Dispose() }
        if ($null -ne $mapping) { $mapping.Dispose() }
    }
}

Invoke-AuthenticatedRoundTrip -HelperName 'pipehelper.exe' `
    -GuestName 'bridgeguest64.exe' -Architecture 'x64'
Invoke-AuthenticatedRoundTrip -HelperName 'pipehelper32.exe' `
    -GuestName 'bridgeguest32.exe' -Architecture 'x86'
Invoke-WrongTokenRejection
