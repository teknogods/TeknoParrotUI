[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repositoryRoot
try {
    $tracked = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate tracked repository files.'
    }

    $forbidden = [Collections.Generic.List[string]]::new()
    foreach ($path in $tracked) {
        $normalized = $path.Replace('\', '/')
        $fileName = [IO.Path]::GetFileName($normalized)
        $extension = [IO.Path]::GetExtension($fileName)

        $isDiagnostic = $normalized.StartsWith(
            'Tools/ProtonPipeHelper/.diagnostics/',
            [StringComparison]::OrdinalIgnoreCase)
        $isRuntimeArtifact = $normalized.StartsWith(
            'artifacts/runtime-packages/',
            [StringComparison]::OrdinalIgnoreCase)
        $isGitLinkCandidate = $normalized.Equals(
            'WinlatorFork',
            [StringComparison]::OrdinalIgnoreCase)
        $isPackageArchive =
            $extension -match '^(?i:\.(?:zip|7z|tar|gz|xz|tzst))$' -and
            $fileName -match '(?i:TeknoParrot|OpenParrot|cxbxr|pcsx2x6)'
        $isForbiddenBinary =
            $extension.Equals('.apk', [StringComparison]::OrdinalIgnoreCase) -or
            $fileName -match '^(?i:TeknoParrot(?:64)?\.dll)$' -or
            $fileName -match '^(?i:OpenParrot.*\.(?:dll|exe))$' -or
            $fileName -match '^(?i:cxbxr-(?:ldr\.exe|emu\.dll))$' -or
            $fileName -match '^(?i:pcsx2.*\.(?:exe|dll|apk))$'

        if ($isDiagnostic -or $isRuntimeArtifact -or $isGitLinkCandidate -or
            $isPackageArchive -or $isForbiddenBinary) {
            $forbidden.Add($normalized)
        }
    }

    $gitLinks = @(& git ls-files --stage | Where-Object {
        $_ -match '^160000\s'
    })
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect tracked Git links.'
    }
    foreach ($gitLink in $gitLinks) {
        $fields = $gitLink -split '\s+', 4
        if ($fields.Count -eq 4) {
            $forbidden.Add($fields[3])
        }
    }

    if ($forbidden.Count -ne 0) {
        throw (
            'Repository contains forbidden runtime/package or diagnostic payloads: ' +
            (($forbidden | Sort-Object -Unique) -join ', '))
    }

    Write-Host (
        "Repository runtime-payload gate: PASS ($($tracked.Count) tracked files; " +
        'no emulator/core packages, APKs, diagnostic trees, or Git links)')
}
finally {
    Pop-Location
}
