[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $DeviceSerial,
    [int] $ContainerId = 1,
    [string] $WinlatorPackage = 'com.teknoparrot.winlator',
    [int] $FileTapX = 306,
    [int] $FileTapY = 607,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$toolsDir = Join-Path $repoRoot 'Tools\ProtonPipeHelper'
$adb = Join-Path $env:USERPROFILE 'android-toolchain\sdk\platform-tools\adb.exe'
$guestRelativeDir = "files/rootfs/home/xuser-$ContainerId/.wine/drive_c/teknoparrot-diagnostics"
$guestUnixDir = "/data/user/0/$WinlatorPackage/$guestRelativeDir"

if (-not (Test-Path -LiteralPath $adb)) {
    throw "adb was not found at $adb."
}

function Invoke-Adb {
    param([Parameter(ValueFromRemainingArguments)] [string[]] $Arguments)

    $output = & $adb -s $DeviceSerial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "adb $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

function Start-AdbStreamProcess {
    param([Parameter(Mandatory)] [string[]] $Arguments)

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $adb
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-s', $DeviceSerial) + $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Failed to start adb.'
    }
    return $process
}

function Write-PrivateBytes {
    param(
        [Parameter(Mandatory)] [byte[]] $Bytes,
        [Parameter(Mandatory)] [string] $RelativePath
    )

    $process = Start-AdbStreamProcess @(
        'shell', 'run-as', $WinlatorPackage, 'tee', $RelativePath
    )
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.StandardInput.BaseStream.Write($Bytes, 0, $Bytes.Length)
    $process.StandardInput.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Writing $RelativePath failed: $($stderr.Result)"
    }
    [void] $stdout.Result
}

function Write-PrivateFile {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $RelativePath
    )

    # `adb shell` is text-oriented on Windows and can truncate PE files at a
    # byte such as Ctrl-Z. Push to shell-owned storage first, then copy through
    # run-as so staging remains byte-for-byte exact.
    $temporaryName = '/data/local/tmp/teknoparrot-' + [IO.Path]::GetFileName($RelativePath)
    [void] (Invoke-Adb -Arguments @('push', $Source, $temporaryName))
    [void] (Invoke-Adb -Arguments @(
        'shell', 'run-as', $WinlatorPackage, 'cp', $temporaryName, $RelativePath
    ))
}

function Read-PrivateBytes {
    param([Parameter(Mandatory)] [string] $RelativePath)

    $process = Start-AdbStreamProcess @(
        'exec-out', 'run-as', $WinlatorPackage, 'cat', $RelativePath
    )
    $stderr = $process.StandardError.ReadToEndAsync()
    $memory = [IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($memory)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Reading $RelativePath failed: $($stderr.Result)"
    }
    return $memory.ToArray()
}

function Read-Exact {
    param(
        [Parameter(Mandatory)] [IO.Stream] $Stream,
        [Parameter(Mandatory)] [int] $Count
    )

    $buffer = [byte[]]::new($Count)
    $offset = 0
    while ($offset -lt $Count) {
        $read = $Stream.Read($buffer, $offset, $Count - $offset)
        if ($read -le 0) {
            throw "The guest pipe closed after $offset of $Count bytes."
        }
        $offset += $read
    }
    return $buffer
}

function Wait-ForPrivateMarker {
    param(
        [Parameter(Mandatory)] [string] $RelativePath,
        [Parameter(Mandatory)] [string] $Marker,
        [int] $TimeoutSeconds = 45
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $text = (& $adb -s $DeviceSerial shell run-as $WinlatorPackage cat $RelativePath 2>$null) -join "`n"
        if ($LASTEXITCODE -eq 0 -and $text.Contains($Marker)) {
            return $text
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Marker in $RelativePath."
}

if (-not $SkipBuild) {
    & (Join-Path $toolsDir 'build-winlator-bridge-guest.ps1')
}

$fixtures = @(
    @{ Source = 'android-winlator-bridge-diagnostics.bat'; Target = 'android-winlator-bridge-diagnostics.bat' },
    @{ Source = 'pipehelper.exe'; Target = 'pipehelper64.exe' },
    @{ Source = 'pipehelper32.exe'; Target = 'pipehelper32.exe' },
    @{ Source = 'bridgeguest64.exe'; Target = 'bridgeguest64.exe' },
    @{ Source = 'bridgeguest32.exe'; Target = 'bridgeguest32.exe' }
)
foreach ($fixture in $fixtures) {
    if (-not (Test-Path -LiteralPath (Join-Path $toolsDir $fixture.Source))) {
        throw "Missing fixture: $($fixture.Source)"
    }
}

$deviceState = (Invoke-Adb -Arguments @('get-state')) -join ''
if ($deviceState.Trim() -ne 'device') {
    throw "ADB target $DeviceSerial is not ready."
}
$abi = ((Invoke-Adb -Arguments @('shell', 'getprop', 'ro.product.cpu.abi')) -join '').Trim()
if ($abi -ne 'arm64-v8a') {
    throw "The full Winlator guest lab requires arm64-v8a; $DeviceSerial reports $abi."
}

[void] (Invoke-Adb -Arguments @(
    'shell', 'run-as', $WinlatorPackage, 'mkdir', '-p', $guestRelativeDir
))
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint] $listener.LocalEndpoint).Port

foreach ($fixture in $fixtures) {
    $source = Join-Path $toolsDir $fixture.Source
    $target = "$guestRelativeDir/$($fixture.Target)"
    if ($fixture.Target -eq 'android-winlator-bridge-diagnostics.bat') {
        $batch = [IO.File]::ReadAllText($source).Replace('__BRIDGE_PORT__', [string] $port)
        Write-PrivateBytes -Bytes ([Text.Encoding]::ASCII.GetBytes($batch)) -RelativePath $target
    }
    else {
        Write-PrivateFile -Source $source -RelativePath $target
    }
}

$page = [byte[]]::new(64)
for ($index = 0; $index -lt 16; $index++) {
    $page[$index] = 0xa0 + $index
}
Write-PrivateBytes -Bytes $page -RelativePath "$guestRelativeDir/shared-page.bin"

[void] (Invoke-Adb -Arguments @('reverse', "tcp:$port", "tcp:$port"))
[void] (Invoke-Adb -Arguments @('shell', 'am', 'force-stop', $WinlatorPackage))
[void] (Invoke-Adb -Arguments @(
    'shell', 'am', 'start', '-n', "$WinlatorPackage/com.winlator.MainActivity",
    '--ei', 'container_id', $ContainerId,
    '--es', 'start_path', $guestUnixDir
))
Start-Sleep -Milliseconds 1800
[void] (Invoke-Adb -Arguments @('logcat', '-c'))
[void] (Invoke-Adb -Arguments @('shell', 'input', 'tap', $FileTapX, $FileTapY))

try {
    foreach ($expectedArchitecture in @(64, 32)) {
        $accept = $listener.AcceptTcpClientAsync()
        if (-not $accept.Wait([TimeSpan]::FromSeconds(30))) {
            throw "Timed out waiting for the $expectedArchitecture-bit pipehelper connection."
        }

        $client = $accept.Result
        try {
            $client.ReceiveTimeout = 10000
            $client.SendTimeout = 10000
            $stream = $client.GetStream()
            $request = Read-Exact -Stream $stream -Count 16
            $expectedRequest = [byte[]] @(
                0x54, 0x50, 0x47, 0x31, $expectedArchitecture,
                0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16,
                0x17, 0x18, 0x19, 0x1a
            )
            if ([Convert]::ToHexString($request) -ne [Convert]::ToHexString($expectedRequest)) {
                throw "The $expectedArchitecture-bit named-pipe request did not match the fixture vector."
            }

            $response = [byte[]] @(
                0x54, 0x50, 0x52, 0x31, $expectedArchitecture,
                0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
                0x27, 0x28, 0x29, 0x2a
            )
            $stream.Write($response, 0, $response.Length)
            $stream.Flush()
            Write-Host "$expectedArchitecture-bit named-pipe round trip: PASS"
        }
        finally {
            $client.Dispose()
        }
    }

    $result = Wait-ForPrivateMarker `
        -RelativePath "$guestRelativeDir/bridge-result.txt" -Marker 'COMPLETE=1'
    $mirroredPage = Read-PrivateBytes "$guestRelativeDir/shared-page.bin"
    for ($index = 0; $index -lt 16; $index++) {
        if ($mirroredPage[32 + $index] -ne 0xd0 + $index) {
            throw "The guest-to-host shared-page marker failed at offset $($index + 32)."
        }
    }
    if ($mirroredPage[48] -ne 32) {
        throw "The final shared-page architecture marker was $($mirroredPage[48]), expected 32."
    }

    $guest64 = Wait-ForPrivateMarker `
        -RelativePath "$guestRelativeDir/bridgeguest64.log" -Marker 'COMPLETE=1'
    $guest32 = Wait-ForPrivateMarker `
        -RelativePath "$guestRelativeDir/bridgeguest32.log" -Marker 'COMPLETE=1'

    Write-Host ''
    Write-Host 'WINLATOR WINDOWS GUEST BRIDGE LAB PASSED'
    Write-Host $result
    Write-Host '64-bit guest:'
    Write-Host $guest64
    Write-Host '32-bit guest:'
    Write-Host $guest32
    Write-Host 'Shared page: PASS (Android -> Windows mapping -> Android)'
}
finally {
    $listener.Stop()
}
