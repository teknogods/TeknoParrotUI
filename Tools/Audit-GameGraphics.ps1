[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Root,

    [string] $OutputJson,

    [string] $DumpBinPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-DumpBin {
    param([string] $RequestedPath)

    if ($RequestedPath) {
        $resolved = Resolve-Path -LiteralPath $RequestedPath -ErrorAction Stop
        return $resolved.Path
    }

    $command = Get-Command dumpbin.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $roots = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($visualStudioRoot in $roots) {
        $candidate = Get-ChildItem -LiteralPath $visualStudioRoot -Filter dumpbin.exe -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\Hostx64\\x64\\dumpbin\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw 'dumpbin.exe was not found. Install the Visual Studio C++ build tools or pass -DumpBinPath.'
}

function Get-PeArchitecture {
    param([string] $Path)

    $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try {
        if ($stream.Length -lt 64) {
            return 'unknown'
        }

        $reader = [System.IO.BinaryReader]::new($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) {
            return 'not-pe'
        }

        $stream.Position = 0x3C
        $peOffset = $reader.ReadUInt32()
        if ($peOffset + 6 -gt $stream.Length) {
            return 'unknown'
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            return 'unknown'
        }

        switch ($reader.ReadUInt16()) {
            0x014C { return 'x86' }
            0x8664 { return 'x64' }
            0x01C0 { return 'arm' }
            0x01C4 { return 'armv7' }
            0xAA64 { return 'arm64' }
            default { return ('machine-0x{0:X4}' -f $_) }
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-ImportedLibraries {
    param(
        [string] $Path,
        [string] $DumpBin
    )

    $output = & $DumpBin /nologo /dependents $Path 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }

    return @($output |
        ForEach-Object {
            if ($_ -match '^\s+([^\s]+\.dll)\s*$') {
                $matches[1].ToLowerInvariant()
            }
        } |
        Where-Object { $_ } |
        Sort-Object -Unique)
}

function Get-GraphicsSignals {
    param([string[]] $Imports)

    $signals = [System.Collections.Generic.List[string]]::new()
    foreach ($library in $Imports) {
        switch -Regex ($library) {
            '^d3d12\.dll$' { $signals.Add('Direct3D 12'); break }
            '^d3d11\.dll$' { $signals.Add('Direct3D 11'); break }
            '^d3d10(_1)?\.dll$' { $signals.Add('Direct3D 10'); break }
            '^d3d9\.dll$|^d3dx9_\d+\.dll$' { $signals.Add('Direct3D 9'); break }
            '^d3d8\.dll$' { $signals.Add('Direct3D 8'); break }
            '^ddraw\.dll$' { $signals.Add('DirectDraw'); break }
            '^opengl32\.dll$|^glu32\.dll$' { $signals.Add('OpenGL'); break }
            '^vulkan-1\.dll$' { $signals.Add('Vulkan'); break }
            '^dxgi\.dll$' { $signals.Add('DXGI'); break }
        }
    }

    return @($signals | Sort-Object -Unique)
}

function Get-EmbeddedGraphicsStrings {
    param([string] $Path)

    # Import tables do not expose runtime LoadLibrary calls or engine renderer
    # names. Scan ASCII strings as secondary evidence, in bounded chunks so a
    # very large game DLL is not copied into memory in one allocation.
    $patterns = [ordered]@{
        'Direct3D 12' = '(?i)(d3d12(?:\.dll)?|D3D12CreateDevice)'
        'Direct3D 11' = '(?i)(d3d11(?:\.dll)?|D3D11CreateDevice)'
        'Direct3D 10' = '(?i)(d3d10_1(?:\.dll)?|d3d10(?:\.dll)?|D3D10CreateDevice)'
        'Direct3D 9' = '(?i)(d3dx9_\d+\.dll|d3d9\.dll|Direct3DCreate9Ex?|D3D9)'
        'Direct3D 8' = '(?i)(d3d8\.dll|Direct3DCreate8|D3D8)'
        'DirectDraw' = '(?i)(ddraw\.dll|DirectDrawCreate(?:Ex)?)'
        'OpenGL' = '(?i)(opengl32\.dll|glu32\.dll|wglCreateContext)'
        'Vulkan' = '(?i)(vulkan-1\.dll|vkCreateInstance)'
        'DXGI' = '(?i)(dxgi\.dll|CreateDXGIFactory)'
    }

    $found = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $buffer = [byte[]]::new(1024 * 1024)
    $tail = ''
    $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try {
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $text = $tail + [System.Text.Encoding]::ASCII.GetString($buffer, 0, $read)
            foreach ($entry in $patterns.GetEnumerator()) {
                if (-not $found.Contains($entry.Key) -and $text -match $entry.Value) {
                    [void]$found.Add($entry.Key)
                }
            }

            if ($found.Count -eq $patterns.Count) {
                break
            }
            $tail = if ($text.Length -gt 128) { $text.Substring($text.Length - 128) } else { $text }
        }
    }
    finally {
        $stream.Dispose()
    }

    return @($found | Sort-Object)
}

function Get-LocalGraphicsRole {
    param([string] $FileName)

    switch -Regex ($FileName.ToLowerInvariant()) {
        '^d3d8\.dll$' { return 'Direct3D 8 override/wrapper' }
        '^d3d9\.dll$' { return 'Direct3D 9 override/wrapper' }
        '^d3d10(_1)?\.dll$' { return 'Direct3D 10 override/wrapper' }
        '^d3d11\.dll$' { return 'Direct3D 11 override/wrapper' }
        '^d3d12\.dll$' { return 'Direct3D 12 override/wrapper' }
        '^ddraw\.dll$' { return 'DirectDraw override/wrapper' }
        '^dxgi\.dll$' { return 'DXGI override/wrapper' }
        '^opengl32\.dll$' { return 'OpenGL override/wrapper' }
        '^vulkan-1\.dll$' { return 'Vulkan loader override' }
        default { return $null }
    }
}

$rootPath = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).Path.TrimEnd('\', '/')
$dumpBin = Resolve-DumpBin -RequestedPath $DumpBinPath
$candidates = @(Get-ChildItem -LiteralPath $rootPath -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -ieq '.exe' -or $_.Extension -ieq '.dll' } |
    Sort-Object FullName)

$files = [System.Collections.Generic.List[object]]::new()
$overrides = [System.Collections.Generic.List[object]]::new()
$index = 0

foreach ($candidate in $candidates) {
    $index++
    Write-Progress -Activity 'Auditing PE graphics dependencies' -Status $candidate.Name `
        -PercentComplete (($index / [Math]::Max(1, $candidates.Count)) * 100)

    $relativePath = [System.IO.Path]::GetRelativePath($rootPath, $candidate.FullName).Replace('\', '/')
    $architecture = Get-PeArchitecture -Path $candidate.FullName
    if ($architecture -eq 'not-pe') {
        continue
    }

    $imports = @(Get-ImportedLibraries -Path $candidate.FullName -DumpBin $dumpBin)
    $signals = @(Get-GraphicsSignals -Imports $imports)
    $embeddedSignals = @(Get-EmbeddedGraphicsStrings -Path $candidate.FullName)
    $files.Add([pscustomobject]@{
        path = $relativePath
        architecture = $architecture
        importedLibraries = $imports
        graphicsSignals = $signals
        embeddedGraphicsStrings = $embeddedSignals
    })

    $localRole = Get-LocalGraphicsRole -FileName $candidate.Name
    if ($localRole) {
        $overrides.Add([pscustomobject]@{
            path = $relativePath
            role = $localRole
            architecture = $architecture
            importedLibraries = $imports
            graphicsSignals = $signals
            embeddedGraphicsStrings = $embeddedSignals
        })
    }
}

Write-Progress -Activity 'Auditing PE graphics dependencies' -Completed

$signalSummary = @($files |
    ForEach-Object { $_.graphicsSignals } |
    Group-Object |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{ name = $_.Name; binaryCount = $_.Count }
    })

$architectureSummary = @($files |
    Group-Object architecture |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{ name = $_.Name; binaryCount = $_.Count }
    })

$result = [pscustomobject]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    root = $rootPath
    scanner = 'PE import table via dumpbin /dependents plus bounded embedded-string scan; EXE and DLL candidates are both included'
    candidateCount = $candidates.Count
    peCount = $files.Count
    architectureSummary = $architectureSummary
    graphicsSignalSummary = $signalSummary
    localGraphicsOverrides = $overrides
    files = $files
}

if ($OutputJson) {
    $outputPath = [System.IO.Path]::GetFullPath($OutputJson, (Get-Location).Path)
    $outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
    if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
    Write-Host "Graphics audit JSON: $outputPath"
}

Write-Host "Root: $rootPath"
Write-Host "PE files audited: $($files.Count) of $($candidates.Count) EXE/DLL candidates"
Write-Host "Local graphics overrides: $($overrides.Count)"
$architectureSummary | Format-Table -AutoSize
$signalSummary | Format-Table -AutoSize

if (-not $OutputJson) {
    $result
}
