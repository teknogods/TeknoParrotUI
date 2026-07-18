[CmdletBinding()]
param(
    [string] $CoreWin32 = $env:TEKNOPARROT_CORE_WIN32,
    [string] $OpenParrotWin32 = $env:TEKNOPARROT_OPENPARROT_WIN32,
    [string] $OpenParrotWin32Legacy = $env:TEKNOPARROT_OPENPARROT_WIN32_LEGACY,
    [string] $OpenParrotWin64 = $env:TEKNOPARROT_OPENPARROT_WIN64,
    [string] $OpenParrotWin64Idmac = $env:TEKNOPARROT_OPENPARROT_WIN64_IDMAC,
    [string] $ElfLdr2Runtime = $env:TEKNOPARROT_ELFLDR2_RUNTIME,
    [string] $CxbxrRuntime = $env:TEKNOPARROT_CXBXR_RUNTIME,
    [string] $WinlatorSource = $env:TEKNOPARROT_WINLATOR_SOURCE,
    [string] $ReleaseKeystore = $env:TEKNOPARROT_RELEASE_KEYSTORE,
    [string] $ReleaseStorePassword = $env:TEKNOPARROT_RELEASE_STORE_PASSWORD,
    [string] $ReleaseKeyAlias = $env:TEKNOPARROT_RELEASE_KEY_ALIAS,
    [string] $ReleaseKeyPassword = $env:TEKNOPARROT_RELEASE_KEY_PASSWORD,
    [string] $AndroidVersionName = $env:TEKNOPARROT_ANDROID_VERSION_NAME,
    [string] $AndroidVersionCode = $env:TEKNOPARROT_ANDROID_VERSION_CODE,
    [string] $WinlatorSourceSha256 = $env:TEKNOPARROT_WINLATOR_SOURCE_SHA256,
    [switch] $ReleaseBuild,
    [switch] $EmbedRuntime,
    [switch] $SkipTests,
    [switch] $SkipCompanion,
    [switch] $SkipUi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$userHome = [Environment]::GetFolderPath('UserProfile')
$runningOnWindows = $env:OS -eq 'Windows_NT'
$companionApk = $null
$uiApk = $null
$cxbxrRuntimeWasExplicit =
    -not [string]::IsNullOrWhiteSpace($CxbxrRuntime)
$requiredOpenParrotWin32Files = @(
    'bngrw.dll',
    'iDmacDrv32.dll',
    'OpenParrot.dll',
    'OpenParrotBG4.dll',
    'OpenParrotAquapazza.dll',
    'OpenParrotCrazySpeed.dll',
    'OpenParrotEADP.dll',
    'OpenParrotDirty.dll',
    'OpenParrotFNFSB.dll',
    'OpenParrotChaseHQ2.dll',
    'ChaseFpuHelper.dll',
    'OpenParrotKonamiLoader.exe',
    'OpenParrotLoader.exe'
)

if ($ReleaseBuild) {
    if ($SkipCompanion -or $SkipUi) {
        throw 'A distributable Android release must build and validate both packages.'
    }
    if ($EmbedRuntime) {
        throw (
            'Distributable Android APKs must not embed TeknoParrot, OpenParrot, ' +
            'ElfLoader2, CXBXR, or PCSX2X6 runtime packages. The updater service ' +
            'provisions platform-specific packages after installation.')
    }

    if ([string]::IsNullOrWhiteSpace($WinlatorSource)) {
        throw (
            'Distributable Android builds require an explicit, source-only ' +
            'Winlator companion checkout.')
    }

    $requiredReleaseInputs = [ordered]@{
        ReleaseKeystore = $ReleaseKeystore
        ReleaseStorePassword = $ReleaseStorePassword
        ReleaseKeyAlias = $ReleaseKeyAlias
        ReleaseKeyPassword = $ReleaseKeyPassword
        AndroidVersionName = $AndroidVersionName
        AndroidVersionCode = $AndroidVersionCode
        WinlatorSourceSha256 = $WinlatorSourceSha256
    }
    foreach ($required in $requiredReleaseInputs.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([string]$required.Value)) {
            throw "Distributable Android build input is missing: $($required.Key)."
        }
    }
    if ($AndroidVersionName -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "AndroidVersionName must be a four-part release version: $AndroidVersionName"
    }
    if ($WinlatorSourceSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw (
            'WinlatorSourceSha256 must be the verified SHA256 of the immutable, ' +
            'source-only Winlator companion bundle.')
    }
    $parsedVersionCode = 0L
    if (-not [int64]::TryParse($AndroidVersionCode, [ref]$parsedVersionCode) -or
        $parsedVersionCode -le 0 -or
        $parsedVersionCode -gt [int]::MaxValue) {
        throw "AndroidVersionCode must be a positive 32-bit integer: $AndroidVersionCode"
    }
}

if ($EmbedRuntime -and -not $CoreWin32) {
    $candidate = Join-Path $repositoryRoot 'cache\teknoparrot-debug-phone-full'
    if (Test-Path -LiteralPath $candidate) { $CoreWin32 = $candidate }
}
if ($EmbedRuntime -and -not $OpenParrotWin32) {
    # The original experimental loader regressed x86 startup under Wine because
    # kernel32.dll was not enumerable while the target process was suspended.
    # Prefer the repaired pair validated on physical Samsung hardware, retain
    # the Fold6-qualified pair as a fallback, and keep the unfixed binaries only
    # for diagnosis. A directory is eligible only when the complete packaged
    # runtime is present; choosing a partially-populated cache makes Gradle
    # fail later and can accidentally mix unrelated title-specific DLLs.
    foreach ($relativeCandidate in @(
        'cache\openparrot-win32-package-qualified-20260725',
        'cache\openparrot-win32-x86-wine-fix',
        'cache\openparrot-win32-fold6-known-good',
        'cache\openparrot-win32-dirty')) {
        $candidate = Join-Path $repositoryRoot $relativeCandidate
        $complete = (Test-Path -LiteralPath $candidate -PathType Container) -and
            -not ($requiredOpenParrotWin32Files | Where-Object {
                -not (Test-Path -LiteralPath (Join-Path $candidate $_) -PathType Leaf)
            })
        if ($complete) {
            $OpenParrotWin32 = $candidate
            break
        }
    }
}
if ($EmbedRuntime -and -not $OpenParrotWin32Legacy) {
    $candidate = Join-Path $repositoryRoot `
        'cache\openparrot-win32-fold6-known-good\OpenParrot.dll'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $OpenParrotWin32Legacy = $candidate
    }
}
if ($EmbedRuntime -and -not $OpenParrotWin64) {
    $candidate = Join-Path (Split-Path $repositoryRoot -Parent) 'OpenParrot\build\bin\android-x64'
    if (Test-Path -LiteralPath $candidate) { $OpenParrotWin64 = $candidate }
}
if ($EmbedRuntime -and -not $OpenParrotWin64Idmac -and $OpenParrotWin64) {
    $candidate = Join-Path $OpenParrotWin64 'iDmacDrv64.dll'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $OpenParrotWin64Idmac = $candidate
    }
}
if ($EmbedRuntime -and -not $OpenParrotWin64Idmac) {
    # The Android-qualified OpenParrot64/loader pair can be staged in its own
    # directory while the architecture-neutral iDmac project still emits to
    # the normal Release directory. Keep that dependency explicit instead of
    # copying a DLL into or replacing the curated runtime directory.
    $candidate = Join-Path (Split-Path $repositoryRoot -Parent) (
        'OpenParrot\build\bin\release\iDmacDrv64.dll')
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $OpenParrotWin64Idmac = $candidate
    }
}
if ($EmbedRuntime -and -not $ElfLdr2Runtime) {
    $candidate = Join-Path $repositoryRoot 'cache\elfldr2-debug-phone'
    if (Test-Path -LiteralPath $candidate) { $ElfLdr2Runtime = $candidate }
}
if ($EmbedRuntime -and -not $CxbxrRuntime) {
    $cxbxrStages = Join-Path $repositoryRoot 'artifacts\android-cxbxr-runtime'
    if (Test-Path -LiteralPath $cxbxrStages -PathType Container) {
        $CxbxrRuntime = Get-ChildItem -LiteralPath $cxbxrStages -Directory |
            Sort-Object Name -Descending |
            Where-Object {
                Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.json') -PathType Leaf
            } |
            Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $WinlatorSource) {
    $candidate = Join-Path $repositoryRoot 'WinlatorFork'
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $WinlatorSource = $candidate
    }
}

function Require-File([string] $Path, [string] $Purpose) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Purpose is missing: $Path"
    }
}

function Require-Directory([string] $Path, [string] $Purpose) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Purpose is missing: $Path"
    }
}

function Get-NormalizedRelativePath(
    [string] $Root,
    [string] $Path) {
    $rootFull = [IO.Path]::GetFullPath($Root)
    $pathFull = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $rootFull.TrimEnd([char[]]@('\', '/')) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith(
            $rootPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected root: $pathFull"
    }

    return $pathFull.Substring($rootPrefix.Length).Replace('\', '/')
}

function Read-JsonArrayCompat([string] $Path) {
    $parsed = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $parsed) {
        return
    }

    # Windows PowerShell 5.1 emits a top-level JSON array as one pipeline
    # object, while PowerShell 7 enumerates it. Normalize both behaviors so a
    # manifest entry can never become one object with array-valued properties.
    if ($parsed -is [Array]) {
        $parsed | ForEach-Object { Write-Output $_ }
        return
    }

    Write-Output $parsed
}

function Require-ImmutableManifestDirectory([string] $Path, [string] $Purpose) {
    Require-Directory $Path $Purpose

    $manifestPath = Join-Path $Path 'manifest.json'
    Require-File $manifestPath "$Purpose manifest"
    $entries = @(Read-JsonArrayCompat $manifestPath)
    if ($entries.Count -eq 0) {
        throw "$Purpose manifest is empty: $manifestPath"
    }

    $root = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $root.TrimEnd([char[]]@('\', '/')) +
        [IO.Path]::DirectorySeparatorChar
    $expected = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)

    foreach ($entry in $entries) {
        $relative = [string]$entry.path
        if ([string]::IsNullOrWhiteSpace($relative) -or
            [IO.Path]::IsPathRooted($relative)) {
            throw "$Purpose manifest contains an invalid path: '$relative'"
        }

        $normalized = $relative.Replace('\', '/')
        if (-not $expected.Add($normalized)) {
            throw "$Purpose manifest contains a duplicate path: '$normalized'"
        }

        $fullPath = [IO.Path]::GetFullPath(
            (Join-Path $root $normalized.Replace('/', [IO.Path]::DirectorySeparatorChar)))
        if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Purpose manifest path escapes its stage: '$normalized'"
        }

        Require-File $fullPath "$Purpose manifest payload '$normalized'"
        $file = Get-Item -LiteralPath $fullPath
        if ($file.Length -ne [int64]$entry.size) {
            throw "$Purpose manifest size mismatch: '$normalized'"
        }
        $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        if ($actualHash -ne [string]$entry.sha256) {
            throw "$Purpose manifest hash mismatch: '$normalized'"
        }
    }

    $unexpected = @(
        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object { $_.FullName -ne $manifestPath } |
            ForEach-Object {
                Get-NormalizedRelativePath $root $_.FullName
            } |
            Where-Object { -not $expected.Contains($_) })
    if ($unexpected.Count -ne 0) {
        $sample = ($unexpected | Select-Object -First 5) -join ', '
        throw "$Purpose contains $($unexpected.Count) file(s) outside its immutable manifest: $sample"
    }
}

function Require-ApkPayloadMatchesManifest(
    [string] $ApkPath,
    [string] $ManifestDirectory,
    [string] $AssetPrefix,
    [string] $Purpose) {
    Require-ImmutableManifestDirectory $ManifestDirectory "$Purpose source stage"
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $manifestPath = Join-Path $ManifestDirectory 'manifest.json'
    $manifest = @(Read-JsonArrayCompat $manifestPath)
    $expected = [Collections.Generic.Dictionary[string, object]]::new(
        [StringComparer]::Ordinal)
    foreach ($entry in $manifest) {
        $expected.Add(([string]$entry.path).Replace('\', '/'), $entry)
    }

    $seen = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    $unexpected = [Collections.Generic.List[string]]::new()
    $mismatched = [Collections.Generic.List[string]]::new()
    $archive = [IO.Compression.ZipFile]::OpenRead($ApkPath)
    try {
        $entries = @($archive.Entries | Where-Object {
            $_.FullName.StartsWith($AssetPrefix, [StringComparison]::Ordinal) -and
            -not $_.FullName.EndsWith('/', [StringComparison]::Ordinal)
        })
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            foreach ($zipEntry in $entries) {
                $relative = $zipEntry.FullName.Substring($AssetPrefix.Length)
                if (-not $expected.ContainsKey($relative)) {
                    $unexpected.Add($relative)
                    continue
                }
                $seen.Add($relative) | Out-Null
                $manifestEntry = $expected[$relative]
                $stream = $zipEntry.Open()
                try {
                    $actualHash = ([BitConverter]::ToString(
                            $sha256.ComputeHash($stream))).Replace('-', '')
                }
                finally {
                    $stream.Dispose()
                }
                if ($zipEntry.Length -ne [int64]$manifestEntry.size -or
                    $actualHash -ne [string]$manifestEntry.sha256) {
                    $mismatched.Add($relative)
                }
            }
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }

    $missing = @($expected.Keys | Where-Object { -not $seen.Contains($_) })
    if ($unexpected.Count -ne 0 -or
        $missing.Count -ne 0 -or
        $mismatched.Count -ne 0) {
        throw (
            ("$Purpose immutable payload mismatch: unexpected={0}, missing={1}, " +
            "hash-or-size={2}.") -f
            $unexpected.Count, $missing.Count, $mismatched.Count)
    }
    Write-Host (
        "$Purpose immutable payload: PASS; files=$($expected.Count); " +
        "manifest=$((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash)")
}

function Resolve-RepositoryInputPath([string] $Path) {
    if (-not $Path) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

function Require-ReleaseRuntimeInput([string] $Path, [string] $Purpose) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath -match '(?i)[\\/](?:cache|artifacts)[\\/]' -or
        $fullPath -match '(?i)(?:^|[\\/_-])(?:debug|dirty|phone)(?:$|[\\/_-])') {
        throw "$Purpose points at a private/debug staging path: $fullPath"
    }

    $forbiddenFiles = if (Test-Path -LiteralPath $fullPath -PathType Container) {
        @(Get-ChildItem -LiteralPath $fullPath -Recurse -File |
            Where-Object {
                $_.Extension -in @('.pdb', '.ilk', '.iobj', '.ipdb') -or
                $_.Name -match '(?i)(?:^|[._-])debug(?:[._-]|$)'
            })
    }
    else {
        $item = Get-Item -LiteralPath $fullPath
        if ($item.Extension -in @('.pdb', '.ilk', '.iobj', '.ipdb') -or
            $item.Name -match '(?i)(?:^|[._-])debug(?:[._-]|$)') {
            @($item)
        }
        else {
            @()
        }
    }
    if ($forbiddenFiles.Count -ne 0) {
        $sample = ($forbiddenFiles |
            Select-Object -First 5 -ExpandProperty FullName) -join ', '
        throw "$Purpose contains debug-only build artifacts: $sample"
    }
}

function Require-BinaryMarkerAbsent(
    [string] $Path,
    [string] $Marker,
    [string] $Purpose) {
    Require-File $Path $Purpose
    $binaryText = [Text.Encoding]::ASCII.GetString(
        [IO.File]::ReadAllBytes($Path))
    if ($binaryText.Contains($Marker)) {
        throw (
            "$Purpose contains the private diagnostic marker '$Marker'. " +
            'Replace it with a clean release build.')
    }
}

function Require-SourceOnlyWinlatorInput([string] $Path) {
    $root = [IO.Path]::GetFullPath($Path)
    $forbidden = @(
        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object {
                $relative = [IO.Path]::GetRelativePath(
                    $root,
                    $_.FullName).Replace('\', '/')
                $name = $_.Name
                $relative -match '(?i)(?:^|/)(?:build|\\.gradle)(?:/|$)' -or
                $relative -match '(?i)(?:^|/)assets/teknoparrot/runtime(?:/|$)' -or
                $name -match '(?i)^(?:OpenParrot.*\.(?:dll|exe)|' +
                    'TeknoParrot(?:64)?\.dll|TeknoDraw(?:64)?\.dll|' +
                    'ScoreSubmission(?:64)?\.dll|BudgieLoader\.exe|' +
                    'cxbxr-(?:ldr\.exe|emu\.dll)|' +
                    'pcsx2.*\.(?:apk|exe|dll|so)|.*\.apk)$' -or
                ($name -match '(?i)\.(?:zip|7z|tar|tar\.gz|tgz)$' -and
                    $name -match '(?i)(?:OpenParrot|TeknoParrot|' +
                        'TeknoParrotElfLdr2|cxbxr|pcsx2x6)')
            })
    if ($forbidden.Count -ne 0) {
        $sample = @($forbidden |
            Select-Object -First 10 |
            ForEach-Object {
                [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
            })
        throw (
            'The Winlator release input is not source-only. Remove build output ' +
            'and emulator/core payloads before publishing the pinned source bundle: ' +
            ($sample -join ', '))
    }
}

function Invoke-Checked([scriptblock] $Operation, [string] $Purpose) {
    & $Operation
    if ($LASTEXITCODE -ne 0) { throw "$Purpose failed with exit code $LASTEXITCODE." }
}

function Read-ApkPackageIdentity([string[]] $Badging, [string] $Purpose) {
    $packageLine = $Badging | Where-Object { $_ -like 'package: name=*' } | Select-Object -First 1
    if (-not $packageLine -or
        $packageLine -notmatch "^package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'" ) {
        throw "Could not read the $Purpose package identity."
    }
    [PSCustomObject]@{
        Name = $Matches[1]
        VersionCode = $Matches[2]
        VersionName = $Matches[3]
    }
}

function Read-ApkComponentBlock(
    [string] $ManifestText,
    [string] $ComponentName,
    [string] $Purpose) {
    $blocks = [regex]::Matches(
        $ManifestText,
        '(?ms)^      E: (?:activity|service|receiver|provider) \([^\r\n]*\).*?(?=^      E: (?:activity|service|receiver|provider) |\z)')
    $matching = @($blocks | Where-Object {
        $_.Value.Contains('="' + $ComponentName + '"')
    })
    if ($matching.Count -ne 1) {
        throw "$Purpose component lookup found $($matching.Count) matches for $ComponentName."
    }
    return $matching[0].Value
}

function Require-ProtectedExportedComponent(
    [string] $ManifestText,
    [string] $ComponentName,
    [string] $Purpose) {
    $block = Read-ApkComponentBlock $ManifestText $ComponentName $Purpose
    if ($block -notmatch 'android:exported[^\r\n]*0xffffffff') {
        throw "$Purpose must be exported: $ComponentName."
    }
    if (-not $block.Contains(
            'android:permission(0x01010006)="com.teknoparrot.permission.BIND_BRIDGE"')) {
        throw "$Purpose is not protected by the TeknoParrot signature permission: $ComponentName."
    }
}

function Require-PrivateComponent(
    [string] $ManifestText,
    [string] $ComponentName,
    [string] $Purpose) {
    $block = Read-ApkComponentBlock $ManifestText $ComponentName $Purpose
    if ($block -notmatch 'android:exported[^\r\n]*0x0') {
        throw "$Purpose must remain non-exported: $ComponentName."
    }
}

if (-not $SkipTests) {
    Push-Location $repositoryRoot
    try {
        Invoke-Checked {
            dotnet run --no-restore --project Tools/InputMethodAudit/InputMethodAudit.csproj -- android-test
        } 'Android host-only test suite'
    }
    finally { Pop-Location }
}

if (-not $SkipCompanion) {
    $WinlatorSource = Resolve-RepositoryInputPath $WinlatorSource
    if ($ReleaseBuild) {
        $ReleaseKeystore = Resolve-RepositoryInputPath $ReleaseKeystore
        Require-File $ReleaseKeystore 'Android release keystore'
    }
    if ($EmbedRuntime) {
        $CoreWin32 = Resolve-RepositoryInputPath $CoreWin32
        $OpenParrotWin32 = Resolve-RepositoryInputPath $OpenParrotWin32
        $OpenParrotWin32Legacy = Resolve-RepositoryInputPath $OpenParrotWin32Legacy
        $OpenParrotWin64 = Resolve-RepositoryInputPath $OpenParrotWin64
        $OpenParrotWin64Idmac = Resolve-RepositoryInputPath $OpenParrotWin64Idmac
        $ElfLdr2Runtime = Resolve-RepositoryInputPath $ElfLdr2Runtime
        $CxbxrRuntime = Resolve-RepositoryInputPath $CxbxrRuntime
        Require-Directory $CoreWin32 'TeknoParrot core runtime'
        foreach ($relativePath in @(
            'BudgieLoader.exe',
            'TeknoParrot.dll',
            'TeknoParrot64.dll',
            'TeknoDraw.dll',
            'TeknoDraw64.dll',
            'ScoreSubmission.dll',
            'ScoreSubmission64.dll',
            'cg.dll',
            'cgGL.dll',
            'FAudio.dll')) {
            Require-File (Join-Path $CoreWin32 $relativePath) `
                "TeknoParrot runtime payload '$relativePath'"
        }
        Require-Directory $OpenParrotWin32 'OpenParrot x86 runtime'
        foreach ($relativePath in $requiredOpenParrotWin32Files) {
            Require-File (Join-Path $OpenParrotWin32 $relativePath) `
                "OpenParrot x86 runtime payload '$relativePath'"
        }
        Require-File $OpenParrotWin32Legacy `
            'Fold6-qualified legacy OpenParrot x86 runtime'
        Require-Directory $OpenParrotWin64 'OpenParrot x64 runtime'
        Require-File $OpenParrotWin64Idmac 'OpenParrot x64 iDmac runtime'
        Require-Directory $ElfLdr2Runtime 'private ElfLoader2 runtime'
        foreach ($relativePath in @(
            'elfloader.exe',
            'TeknoParrot.dll',
            'msys-2.0.dll',
            'msys-gcc_s-1.dll',
            'msys-stdc++-6.dll',
            'hints.dat',
            'env\dev\fd',
            'libs\libGLU.so.1',
            'YACardEmu\YACardEmu.exe',
            'YACardEmu\config.ini',
            'YACardEmu\license.txt',
            'YACardEmu\public\index.html')) {
            Require-File (Join-Path $ElfLdr2Runtime $relativePath) `
                "ElfLoader2 runtime payload '$relativePath'"
        }
        Require-ImmutableManifestDirectory $CxbxrRuntime 'private CXBXR runtime stage'
        foreach ($variant in @('cxbxr-export', 'cxbxr-japan')) {
            foreach ($relativePath in @(
                'cxbxr-ldr.exe',
                'cxbxr-emu.dll',
                'SDL2.dll',
                'glew32.dll',
                'subhook.dll',
                'hlsl\FixedFunctionPixelShader.hlsl',
                'YACardEmu\YACardEmu.exe',
                'YACardEmu\config.ini',
                'YACardEmu\license.txt',
                'YACardEmu\public\index.html',
                'YACardEmu\public\blah.js',
                'TeknoParrot\settings.ini',
                'TeknoParrot\EmuMediaBoard\fpr21042_m29w160et.bin',
                'TeknoParrot\EmuMediaBoard\Chihiro\ic10_g24lc64.bin',
                'TeknoParrot\EmuMediaBoard\Chihiro\pc20_g24lc64.bin',
                'TeknoParrot\EmuMediaBoard\Chihiro\ic11_24lc024.bin')) {
                Require-File (Join-Path $CxbxrRuntime "$variant\$relativePath") `
                    "CXBXR $variant runtime payload '$relativePath'"
            }
        }
    }
    Require-Directory $WinlatorSource 'Winlator companion source'
    Require-Directory (Join-Path $WinlatorSource 'android_alsa') `
        'Winlator companion Android ALSA source'
    Require-File (Join-Path $WinlatorSource 'app\gradlew') `
        'Winlator companion Gradle wrapper'
    Require-File (Join-Path $WinlatorSource 'app\app\build.gradle') `
        'Winlator companion application build'
    if ($ReleaseBuild) {
        Require-SourceOnlyWinlatorInput $WinlatorSource
    }

    if (-not $env:JAVA_HOME) {
        $env:JAVA_HOME = Join-Path $userHome 'android-toolchain\jdk-17'
    }
    if (-not $env:ANDROID_HOME) {
        $env:ANDROID_HOME = Join-Path $userHome 'android-toolchain\sdk'
    }
    if ($ReleaseBuild) {
        $env:TEKNOPARROT_RELEASE_KEYSTORE = $ReleaseKeystore
        $env:TEKNOPARROT_RELEASE_STORE_PASSWORD = $ReleaseStorePassword
        $env:TEKNOPARROT_RELEASE_KEY_ALIAS = $ReleaseKeyAlias
        $env:TEKNOPARROT_RELEASE_KEY_PASSWORD = $ReleaseKeyPassword
        $env:TEKNOPARROT_ANDROID_VERSION_NAME = $AndroidVersionName
        $env:TEKNOPARROT_ANDROID_VERSION_CODE = $AndroidVersionCode
        $env:TEKNOPARROT_ASSET_BUILD_KEY = 'thin-release-v1'
    }
    else {
        $env:TEKNOPARROT_DEBUG_KEYSTORE =
            Join-Path $userHome 'AppData\Local\Xamarin\Mono for Android\debug.keystore'
    }
    $env:TEKNOPARROT_EMBED_RUNTIME = if ($EmbedRuntime) { '1' } else { '0' }
    if ($EmbedRuntime) {
        $env:TEKNOPARROT_CORE_WIN32 = $CoreWin32
        $env:TEKNOPARROT_OPENPARROT_WIN32 = $OpenParrotWin32
        $env:TEKNOPARROT_OPENPARROT_WIN32_LEGACY = $OpenParrotWin32Legacy
        $env:TEKNOPARROT_OPENPARROT_WIN64 = $OpenParrotWin64
        $env:TEKNOPARROT_OPENPARROT_WIN64_IDMAC = $OpenParrotWin64Idmac
        $env:TEKNOPARROT_ELFLDR2_RUNTIME = $ElfLdr2Runtime
        $env:TEKNOPARROT_CXBXR_RUNTIME = $CxbxrRuntime
    }
    $env:TEKNOPARROT_REPOSITORY_ROOT = $repositoryRoot

    $javaExecutable = Join-Path $env:JAVA_HOME $(
        if ($runningOnWindows) { 'bin\java.exe' } else { 'bin/java' })
    Require-File $javaExecutable 'JDK 17'
    Require-Directory $env:ANDROID_HOME 'Android SDK'
    if (-not $ReleaseBuild) {
        Require-File $env:TEKNOPARROT_DEBUG_KEYSTORE 'Android debug keystore'
    }

    $companionRoot = Join-Path $WinlatorSource 'app'
    Push-Location $companionRoot
    try {
        $gradleWrapper = if ($runningOnWindows) {
            Join-Path $companionRoot 'gradlew.bat'
        }
        else {
            Join-Path $companionRoot 'gradlew'
        }
        Require-File $gradleWrapper 'Gradle wrapper'
        if (-not $runningOnWindows) {
            & chmod +x $gradleWrapper
        }
        $gradleTask = if ($ReleaseBuild) {
            ':app:assembleRelease'
        }
        else {
            ':app:assembleDebug'
        }
        Invoke-Checked {
            & $gradleWrapper $gradleTask --no-daemon
        } 'Winlator companion APK build'
    }
    finally { Pop-Location }

    $companionApk = if ($ReleaseBuild) {
        Join-Path $companionRoot 'app\build\outputs\apk\release\app-release.apk'
    }
    else {
        Join-Path $companionRoot 'app\build\outputs\apk\debug\app-debug.apk'
    }
    Require-File $companionApk 'Winlator companion APK'
    Write-Host "Companion APK: $companionApk"
    Write-Host "Companion SHA256: $((Get-FileHash -LiteralPath $companionApk -Algorithm SHA256).Hash)"
}

if (-not $SkipUi) {
    $localDotnetRoot = Join-Path $userHome '.dotnet'
    $localDotnet = Join-Path $localDotnetRoot $(
        if ($runningOnWindows) { 'dotnet.exe' } else { 'dotnet' })
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    # Developer machines may have an older system-wide SDK before the
    # user-local .NET 10 installation on PATH. Prefer the known local host when
    # it exists; clean CI runners use the setup-dotnet host below.
    $dotnet = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
        $localDotnet
    }
    elseif ($dotnetCommand) {
        $dotnetCommand.Source
    }
    else {
        throw '.NET SDK host is missing.'
    }
    $selectorCandidate = Join-Path $userHome 'android-toolchain\dotnet-10-select'
    $selector = if (Test-Path -LiteralPath $selectorCandidate -PathType Container) {
        $selectorCandidate
    }
    else {
        $repositoryRoot
    }
    $sdk = if ($env:ANDROID_HOME) {
        $env:ANDROID_HOME
    }
    else {
        Join-Path $userHome 'android-toolchain\sdk'
    }
    $jdk = if ($env:JAVA_HOME) {
        $env:JAVA_HOME
    }
    else {
        Join-Path $userHome 'android-toolchain\jdk-17'
    }
    $project = Join-Path $repositoryRoot 'TeknoParrotUi.Avalonia.Android\TeknoParrotUi.Avalonia.Android.csproj'
    Require-File $dotnet '.NET SDK host'
    Require-Directory $selector '.NET 10 Android SDK selector'

    if (-not $env:DOTNET_ROOT -and
        (Test-Path -LiteralPath $localDotnet -PathType Leaf)) {
        $env:DOTNET_ROOT = $localDotnetRoot
        $env:PATH = "$localDotnetRoot$([IO.Path]::PathSeparator)$env:PATH"
    }
    Push-Location $selector
    try {
        Invoke-Checked {
            $arguments = @(
                'build', $project,
                '-c', 'Release',
                '-f', 'net10.0-android',
                '-r', 'android-arm64',
                '-p:EmbedAssembliesIntoApk=true',
                '-p:_DisableParallelAot=true',
                "-p:AndroidSdkDirectory=$sdk",
                "-p:JavaSdkDirectory=$jdk",
                '--nologo')
            if ($ReleaseBuild) {
                $arguments += @(
                    "-p:ApplicationVersion=$AndroidVersionCode",
                    "-p:ApplicationDisplayVersion=$AndroidVersionName",
                    '-p:AndroidKeyStore=true',
                    "-p:AndroidSigningKeyStore=$ReleaseKeystore",
                    "-p:AndroidSigningStorePass=$ReleaseStorePassword",
                    "-p:AndroidSigningKeyAlias=$ReleaseKeyAlias",
                    "-p:AndroidSigningKeyPass=$ReleaseKeyPassword")
            }
            & $dotnet @arguments
        } 'TeknoParrotUI Android APK build'
    }
    finally { Pop-Location }

    $uiApk = Join-Path $repositoryRoot (
        'TeknoParrotUi.Avalonia.Android\bin\Release\net10.0-android\android-arm64\' +
        'com.teknoparrot.ui-Signed.apk')
    Require-File $uiApk 'TeknoParrotUI Android APK'
    Write-Host "TPUI APK: $uiApk"
    Write-Host "TPUI SHA256: $((Get-FileHash -LiteralPath $uiApk -Algorithm SHA256).Hash)"
}

# A partial rebuild should still validate the complete installable pair when
# the skipped artifact already exists. This also makes -SkipCompanion -SkipUi
# a fast package-contract check instead of silently bypassing every APK audit.
if (-not $companionApk) {
    $existingCompanionRoot = if ($WinlatorSource) {
        Join-Path (Resolve-RepositoryInputPath $WinlatorSource) 'app'
    }
    else {
        Join-Path $repositoryRoot 'WinlatorFork\app'
    }
    $existingCompanionRelative = if ($ReleaseBuild) {
        'app\build\outputs\apk\release\app-release.apk'
    }
    else {
        'app\build\outputs\apk\debug\app-debug.apk'
    }
    $existingCompanionApk = Join-Path $existingCompanionRoot $existingCompanionRelative
    if (Test-Path -LiteralPath $existingCompanionApk -PathType Leaf) {
        $companionApk = $existingCompanionApk
    }
}
if (-not $uiApk) {
    $existingUiApk = Join-Path $repositoryRoot (
        'TeknoParrotUi.Avalonia.Android\bin\Release\net10.0-android\android-arm64\' +
        'com.teknoparrot.ui-Signed.apk')
    if (Test-Path -LiteralPath $existingUiApk -PathType Leaf) {
        $uiApk = $existingUiApk
    }
}

if ($companionApk -and $uiApk) {
    $androidSdkRoot = if ($env:ANDROID_HOME) {
        $env:ANDROID_HOME
    }
    else {
        Join-Path $userHome 'android-toolchain\sdk'
    }
    $javaRoot = if ($env:JAVA_HOME) {
        $env:JAVA_HOME
    }
    else {
        Join-Path $userHome 'android-toolchain\jdk-17'
    }
    $buildTools = Get-ChildItem -LiteralPath (Join-Path $androidSdkRoot 'build-tools') -Directory |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    $apkSigner = Join-Path $buildTools $(
        if ($runningOnWindows) { 'apksigner.bat' } else { 'apksigner' })
    $aapt = Join-Path $buildTools $(
        if ($runningOnWindows) { 'aapt.exe' } else { 'aapt' })
    $zipalign = Join-Path $buildTools $(
        if ($runningOnWindows) { 'zipalign.exe' } else { 'zipalign' })
    $jar = Join-Path $javaRoot $(
        if ($runningOnWindows) { 'bin\jar.exe' } else { 'bin/jar' })
    Require-File $apkSigner 'Android APK signer'
    Require-File $aapt 'Android asset packaging tool'
    Require-File $zipalign 'Android ZIP alignment tool'
    Require-File $jar 'JDK archive tool'

    foreach ($package in @(
        @{ Path = $companionApk; Purpose = 'Winlator companion' },
        @{ Path = $uiApk; Purpose = 'TeknoParrotUI' })) {
        & $zipalign -c -P 16 4 $package.Path
        if ($LASTEXITCODE -ne 0) {
            throw "$($package.Purpose) APK does not satisfy 16 KB ZIP alignment."
        }
    }

    $companionCertificate = & $apkSigner verify --print-certs $companionApk
    if ($LASTEXITCODE -ne 0) { throw 'Winlator companion APK signature verification failed.' }
    $uiCertificate = & $apkSigner verify --print-certs $uiApk
    if ($LASTEXITCODE -ne 0) { throw 'TeknoParrotUI APK signature verification failed.' }
    # Build-tools 37 prefixes the same signer record with "V2 Signer:" or
    # "V3.0 Signer:" while older apksigner versions use "Signer #1". Accept
    # both formats without weakening the exact digest comparison below.
    $certificatePattern =
        '(?:Signer #1|V[23](?:\.\d+)? Signer):?\s+certificate SHA-256 digest:\s*([0-9a-fA-F]+)'
    $companionCertificateText = $companionCertificate -join "`n"
    $uiCertificateText = $uiCertificate -join "`n"
    if ($companionCertificateText -notmatch $certificatePattern) {
        throw 'Could not read the Winlator companion signing-certificate digest.'
    }
    $companionCertificateDigest = $Matches[1].ToUpperInvariant()
    if ($uiCertificateText -notmatch $certificatePattern) {
        throw 'Could not read the TeknoParrotUI signing-certificate digest.'
    }
    $uiCertificateDigest = $Matches[1].ToUpperInvariant()
    if ($companionCertificateDigest -ne $uiCertificateDigest) {
        throw 'The Android packages use different certificates; signature-protected IPC will fail.'
    }

    $companionBadging = @(& $aapt dump badging $companionApk)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Winlator companion manifest.' }
    $uiBadging = @(& $aapt dump badging $uiApk)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the TeknoParrotUI manifest.' }
    $companionIdentity = Read-ApkPackageIdentity $companionBadging 'Winlator companion'
    $uiIdentity = Read-ApkPackageIdentity $uiBadging 'TeknoParrotUI'
    if ($companionIdentity.Name -ne 'com.teknoparrot.winlator') {
        throw "Unexpected Winlator companion package: $($companionIdentity.Name)."
    }
    if ($uiIdentity.Name -ne 'com.teknoparrot.ui') {
        throw "Unexpected TeknoParrotUI package: $($uiIdentity.Name)."
    }
    $companionLaunchers = @($companionBadging | Where-Object { $_ -like 'launchable-activity:*' })
    $uiLaunchers = @($uiBadging | Where-Object { $_ -like 'launchable-activity:*' })
    if ($companionLaunchers.Count -ne 0) {
        throw 'The Winlator companion unexpectedly publishes a launcher icon.'
    }
    if ($uiLaunchers.Count -ne 1) {
        throw "TeknoParrotUI must publish exactly one launcher Activity; found $($uiLaunchers.Count)."
    }

    $companionManifest = @(& $aapt dump xmltree $companionApk AndroidManifest.xml)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Winlator companion manifest tree.' }
    $uiManifest = @(& $aapt dump xmltree $uiApk AndroidManifest.xml)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the TeknoParrotUI manifest tree.' }
    $companionManifestText = $companionManifest -join "`n"
    $uiManifestText = $uiManifest -join "`n"
    if ($companionManifestText -notmatch 'android:allowBackup[^\r\n]*0x0') {
        throw 'The Winlator companion must disable Android backup for its Wine registry and prefix.'
    }
    if ($uiManifestText -notmatch 'android:allowBackup[^\r\n]*0x0') {
        throw 'TeknoParrotUI must disable Android backup for private account and session state.'
    }
    if ($uiManifestText -notmatch (
            '(?ms)E: permission[^\r\n]*\r?\n' +
            '\s+A: android:name[^\r\n]*="com\.teknoparrot\.permission\.BIND_BRIDGE"[^\r\n]*\r?\n' +
            '\s+A: android:protectionLevel[^\r\n]*0x2')) {
        throw 'TeknoParrotUI must declare BIND_BRIDGE as a signature permission.'
    }
    foreach ($componentName in @(
        'com.winlator.MainActivity',
        'com.winlator.TeknoParrotProvisioningActivity',
        'com.winlator.teknoparrot.TeknoParrotBridgeService')) {
        Require-ProtectedExportedComponent `
            $companionManifestText $componentName 'Winlator companion bridge'
    }
    foreach ($componentName in @(
        'com.winlator.XServerDisplayActivity',
        'com.winlator.ControlsEditorActivity')) {
        Require-PrivateComponent `
            $companionManifestText $componentName 'Winlator game/editor'
    }
    Require-ProtectedExportedComponent `
        $uiManifestText `
        'com.teknoparrot.bridge.ArcadeSessionService' `
        'TeknoParrotUI bridge'
    foreach ($componentName in @(
        'com.teknoparrot.session.GameSessionService',
        'com.teknoparrot.session.GameSessionLauncherActivity')) {
        Require-PrivateComponent `
            $uiManifestText $componentName 'TeknoParrotUI session'
    }

    $companionEntries = @(& $jar tf $companionApk)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Winlator companion APK payload.' }
    $uiEntries = @(& $jar tf $uiApk)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the TeknoParrotUI APK payload.' }
    if (-not $EmbedRuntime) {
        $forbiddenRuntimeEntryPattern = (
            '(?i)(?:^|/)(?:' +
            'OpenParrot[^/]*\.(?:dll|exe)|' +
            'TeknoParrot(?:64)?\.dll|' +
            'TeknoDraw(?:64)?\.dll|' +
            'ScoreSubmission(?:64)?\.dll|' +
            'BudgieLoader\.exe|' +
            'cxbxr-(?:ldr\.exe|emu\.dll)|' +
            'pcsx2[^/]*\.(?:apk|exe|dll|so)|' +
            '(?:OpenParrot|TeknoParrot|TeknoParrotElfLdr2|cxbxr|pcsx2x6)' +
                '[^/]*\.(?:zip|7z|tar|tar\.gz|tgz)' +
            ')$')
        $forbiddenCompanionEntries = @(
            $companionEntries | Where-Object {
                $_ -match $forbiddenRuntimeEntryPattern
            })
        $forbiddenUiEntries = @(
            $uiEntries | Where-Object {
                $_ -match $forbiddenRuntimeEntryPattern
            })
        if ($forbiddenCompanionEntries.Count -ne 0) {
            throw (
                'The thin Winlator companion APK contains forbidden emulator/core ' +
                "payloads: $($forbiddenCompanionEntries -join ', ')")
        }
        if ($forbiddenUiEntries.Count -ne 0) {
            throw (
                'The TeknoParrotUI APK contains forbidden emulator/core payloads: ' +
                ($forbiddenUiEntries -join ', '))
        }
    }
    $companionAbis = @($companionEntries | ForEach-Object {
        if ($_ -match '^lib/([^/]+)/') { $Matches[1] }
    } | Sort-Object -Unique)
    $uiAbis = @($uiEntries | ForEach-Object {
        if ($_ -match '^lib/([^/]+)/') { $Matches[1] }
    } | Sort-Object -Unique)
    if ($companionAbis.Count -ne 1 -or $companionAbis[0] -ne 'arm64-v8a') {
        throw "Winlator companion ABI contract mismatch: $($companionAbis -join ', ')."
    }
    if ($uiAbis.Count -ne 1 -or $uiAbis[0] -ne 'arm64-v8a') {
        throw "TeknoParrotUI ABI contract mismatch: $($uiAbis -join ', ')."
    }
    # An existing APK may have been built from an older immutable stage than
    # the newest local fallback. Require an exact source-stage match whenever
    # the caller selected that stage or this invocation rebuilt the companion.
    # Validation-only runs still enforce the complete APK entry/ABI/manifest
    # contracts below without comparing against an unrelated local stage.
    if ($EmbedRuntime -and $CxbxrRuntime -and
        ($cxbxrRuntimeWasExplicit -or -not $SkipCompanion)) {
        Require-ApkPayloadMatchesManifest `
            $companionApk `
            (Resolve-RepositoryInputPath $CxbxrRuntime) `
            'assets/teknoparrot/runtime/Cxbxr/' `
            'Winlator companion CXBXR'
    }
    elseif ($EmbedRuntime -and $CxbxrRuntime) {
        Write-Host (
            'Winlator companion CXBXR immutable payload: exact source-stage ' +
            'comparison skipped because the existing APK was not rebuilt and ' +
            'no CXBXR stage was explicitly selected.')
    }
    $requiredRuntimeEntries = @(
        'assets/teknoparrot/runtime/OpenParrotWin32/bngrw.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/iDmacDrv32.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrot.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotBG4.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotAquapazza.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotCrazySpeed.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotEADP.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotDirty.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotFNFSB.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotChaseHQ2.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/ChaseFpuHelper.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotKonamiLoader.exe',
        'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotLoader.exe',
        'assets/teknoparrot/runtime/OpenParrotWin32Legacy/bngrw.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32Legacy/iDmacDrv32.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32Legacy/OpenParrot.dll',
        'assets/teknoparrot/runtime/OpenParrotWin32Legacy/OpenParrotKonamiLoader.exe',
        'assets/teknoparrot/runtime/OpenParrotWin32Legacy/OpenParrotLoader.exe',
        'assets/teknoparrot/runtime/OpenParrotWin64/iDmacDrv64.dll',
        'assets/teknoparrot/runtime/OpenParrotWin64/OpenParrot64.dll',
        'assets/teknoparrot/runtime/OpenParrotWin64/OpenParrotLoader64.exe',
        'assets/teknoparrot/runtime/TeknoParrot/BudgieLoader.exe',
        'assets/teknoparrot/runtime/TeknoParrot/TeknoParrot.dll',
        'assets/teknoparrot/runtime/TeknoParrot/TeknoParrot64.dll',
        'assets/teknoparrot/runtime/TeknoParrot/TeknoDraw.dll',
        'assets/teknoparrot/runtime/TeknoParrot/TeknoDraw64.dll',
        'assets/teknoparrot/runtime/TeknoParrot/ScoreSubmission.dll',
        'assets/teknoparrot/runtime/TeknoParrot/ScoreSubmission64.dll',
        'assets/teknoparrot/runtime/TeknoParrot/cg.dll',
        'assets/teknoparrot/runtime/TeknoParrot/cgGL.dll',
        'assets/teknoparrot/runtime/TeknoParrot/FAudio.dll',
        'assets/teknoparrot/runtime/ElfLdr2/elfloader.exe',
        'assets/teknoparrot/runtime/ElfLdr2/TeknoParrot.dll',
        'assets/teknoparrot/runtime/ElfLdr2/msys-2.0.dll',
        'assets/teknoparrot/runtime/ElfLdr2/msys-gcc_s-1.dll',
        'assets/teknoparrot/runtime/ElfLdr2/msys-stdc++-6.dll',
        'assets/teknoparrot/runtime/ElfLdr2/hints.dat',
        'assets/teknoparrot/runtime/ElfLdr2/env/dev/fd',
        'assets/teknoparrot/runtime/ElfLdr2/libs/libGLU.so.1',
        'assets/teknoparrot/runtime/ElfLdr2/YACardEmu/YACardEmu.exe',
        'assets/teknoparrot/runtime/ElfLdr2/YACardEmu/config.ini',
        'assets/teknoparrot/runtime/ElfLdr2/YACardEmu/license.txt',
        'assets/teknoparrot/runtime/ElfLdr2/YACardEmu/public/index.html',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/cxbxr-ldr.exe',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/cxbxr-emu.dll',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/YACardEmu/YACardEmu.exe',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/YACardEmu/config.ini',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/TeknoParrot/settings.ini',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-export/TeknoParrot/EmuMediaBoard/Chihiro/ic10_g24lc64.bin',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/cxbxr-ldr.exe',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/cxbxr-emu.dll',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/YACardEmu/YACardEmu.exe',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/YACardEmu/config.ini',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/TeknoParrot/settings.ini',
        'assets/teknoparrot/runtime/Cxbxr/cxbxr-japan/TeknoParrot/EmuMediaBoard/Chihiro/ic10_g24lc64.bin'
    )
    $packagedRuntimeEntries = @($companionEntries | Where-Object {
        $_.StartsWith(
            'assets/teknoparrot/runtime/',
            [StringComparison]::Ordinal)
    })
    if ($EmbedRuntime) {
        $missingRuntimeEntries = @(
            $requiredRuntimeEntries | Where-Object { $_ -notin $companionEntries })
        if ($missingRuntimeEntries.Count -ne 0) {
            throw "The private Winlator diagnostic APK is missing runtime assets: $($missingRuntimeEntries -join ', ')"
        }
        if ($companionEntries -contains `
                'assets/teknoparrot/runtime/OpenParrotWin32/OpenParrotLegacy.dll') {
            throw 'The Winlator companion contains the rejected renamed legacy DLL payload.'
        }
    }
    elseif ($packagedRuntimeEntries.Count -ne 0) {
        throw (
            'The Winlator companion APK embeds forbidden emulator/core runtime ' +
            "payloads: $($packagedRuntimeEntries -join ', ')")
    }

    $catalogCounts = @{}
    foreach ($catalogName in @(
        'GameProfiles',
        'Metadata',
        'GameSetup',
        'AndroidLaunchRecipes',
        'InputProfiles')) {
        $sourceRoot = Join-Path $repositoryRoot "TeknoParrotUi.Common\$catalogName"
        $expectedCatalogEntries = @(
            Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
                ForEach-Object {
                    $relativePath = Get-NormalizedRelativePath `
                        $sourceRoot `
                        $_.FullName
                    "assets/$catalogName/$relativePath"
                })
        $packagedCatalogEntries = @($uiEntries | Where-Object {
            $_.StartsWith("assets/$catalogName/", [StringComparison]::Ordinal) -and
            -not $_.EndsWith('/', [StringComparison]::Ordinal)
        })
        $duplicateCatalogEntries = @(
            $packagedCatalogEntries | Group-Object | Where-Object Count -gt 1)
        if ($duplicateCatalogEntries.Count -ne 0) {
            throw "$catalogName contains duplicate APK entries: $($duplicateCatalogEntries.Name -join ', ')."
        }
        $catalogEntryDifference = @(
            Compare-Object `
                $expectedCatalogEntries `
                $packagedCatalogEntries `
                -CaseSensitive)
        if ($catalogEntryDifference.Count -ne 0) {
            throw "$catalogName packaging mismatch: $($catalogEntryDifference -join ', ')."
        }
        $catalogCounts[$catalogName] = $packagedCatalogEntries.Count
    }
    $packagedRecipes = @($uiEntries | Where-Object {
        $_ -match '^assets/AndroidLaunchRecipes/[^/]+\.json$'
    })
    $packagedCatalogCount = ($catalogCounts.Values | Measure-Object -Sum).Sum

    $sourceLayouts = @(Get-ChildItem -LiteralPath (
        Join-Path $repositoryRoot 'WinlatorFork\app\app\src\main\assets\inputcontrols\profiles') `
        -Filter 'controls-9???.icp' -File)
    $packagedLayouts = @($companionEntries | Where-Object {
        $_ -match '^assets/inputcontrols/profiles/controls-9[0-9]{3}\.icp$'
    })
    $expectedLayoutEntries = @($sourceLayouts | ForEach-Object {
        'assets/inputcontrols/profiles/' + $_.Name
    })
    $layoutEntryDifference = @(
        Compare-Object $expectedLayoutEntries $packagedLayouts -CaseSensitive)
    if ($layoutEntryDifference.Count -ne 0) {
        throw "Control-layout packaging mismatch: $($layoutEntryDifference -join ', ')."
    }

    Write-Host (
        "Android package contract: PASS; certificate=$companionCertificateDigest; " +
        "companion=$($companionIdentity.VersionName)/$($companionIdentity.VersionCode); " +
        "ui=$($uiIdentity.VersionName)/$($uiIdentity.VersionCode); " +
        "catalog=$packagedCatalogCount; recipes=$($packagedRecipes.Count); " +
        "controls=$($packagedLayouts.Count); " +
        'abi=arm64-v8a; zipalign=16K; backup=disabled; launcher=TeknoParrot only')
}

Write-Host 'Android package gate: PASS'
