[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._:-]+$')]
    [string] $DeviceSerial,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $SearchText,
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string] $Label,
    [string] $ExpectedTitlePattern,
    [ValidateRange(15, 600)]
    [int] $DurationSeconds = 50,
    [string] $AdbPath,
    [string] $OutputDirectory = $env:TEKNOPARROT_ANDROID_EVIDENCE_ROOT,
    [ValidateSet('Unchanged', 'Enabled', 'Disabled')]
    [string] $DebugLoggingMode = 'Unchanged',
    [string] $GameExecutablePathOverride,
    [switch] $KeepGameRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\android-screenshots'
}
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

function Test-TransientAdbFailure([string] $Message) {
    return $Message -match '(?i)device offline|device .* not found|no devices/emulators found|closed|cannot connect'
}

function Invoke-Adb([string[]] $Arguments) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $output = & $adb -s $DeviceSerial @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            return @($output)
        }

        $message = $output -join [Environment]::NewLine
        if ($attempt -lt 3 -and (Test-TransientAdbFailure $message)) {
            & $adb reconnect offline 2>&1 | Out-Null
            Start-Sleep -Seconds 2
            continue
        }

        throw "adb failed ($exitCode): $message"
    }
}

function Get-BoundsCenter([string] $Bounds) {
    if ($Bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
        throw "Invalid Android UI bounds: $Bounds"
    }

    return [pscustomobject]@{
        X = [math]::Floor(([int]$matches[1] + [int]$matches[3]) / 2)
        Y = [math]::Floor(([int]$matches[2] + [int]$matches[4]) / 2)
    }
}

function ConvertTo-AdbInputText([string] $Value) {
    if ([string]::IsNullOrEmpty($Value)) {
        throw 'ADB input text cannot be empty.'
    }

    # `adb shell` joins its remaining arguments into a remote shell command.
    # Escape shell metacharacters before Android's input command sees them;
    # otherwise game titles containing parentheses, apostrophes, or ampersands
    # are parsed as shell syntax instead of being typed into TPUI's search box.
    $shellMetacharacters = [char[]] @(
        '(', ')', '<', '>', '|', ';', '&', '*', '?', '~', '$', "'", '"',
        '\', '[', ']', '{', '}', '!')
    $builder = [Text.StringBuilder]::new()
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq ' ') {
            [void] $builder.Append('%s')
        }
        elseif ($character -eq '%') {
            [void] $builder.Append('\%')
        }
        elseif ($shellMetacharacters -contains $character) {
            [void] $builder.Append('\').Append($character)
        }
        else {
            [void] $builder.Append($character)
        }
    }
    return $builder.ToString()
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$safePrefix = "s26-fold6-$Label-$timestamp"
$hierarchyDirectory = Join-Path $repoRoot 'cache\android-regression'
New-Item -ItemType Directory -Force -Path $hierarchyDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Get-Hierarchy([string] $Stage) {
    $fileName = "$safePrefix-$Stage.xml"
    # Reuse one short remote path. On a busy device uiautomator can report
    # success before a stage-specific file becomes visible to adb pull.
    $remotePath = '/sdcard/Download/tp-regression-window.xml'
    $localPath = Join-Path $hierarchyDirectory $fileName
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-Adb -Arguments @('shell', 'uiautomator', 'dump', $remotePath) | Out-Null
            Invoke-Adb -Arguments @('pull', $remotePath, $localPath) | Out-Null
            return [xml](Get-Content -LiteralPath $localPath -Raw)
        }
        catch {
            if ($attempt -eq 3) {
                throw
            }
            Start-Sleep -Seconds 1
        }
    }
}

function Invoke-TapNode($Node) {
    if ($null -eq $Node) {
        throw 'The requested Android UI element was not found.'
    }
    $center = Get-BoundsCenter $Node.bounds
    Invoke-Adb -Arguments @('shell', 'input', 'tap', $center.X.ToString(), $center.Y.ToString()) |
        Out-Null
}

function Set-SelectedGameDebugLogging([xml] $SelectionHierarchy) {
    if ($DebugLoggingMode -eq 'Unchanged' -and
        [string]::IsNullOrWhiteSpace($GameExecutablePathOverride)) {
        return $SelectionHierarchy
    }

    $settingsButton = $SelectionHierarchy.SelectSingleNode(
        '//node[@class="Button" and @text="GAME SETTINGS" and @enabled="true"]')
    Invoke-TapNode $settingsButton
    Start-Sleep -Seconds 2

    $settingsHierarchy = Get-Hierarchy 'settings-initial'

    if (-not [string]::IsNullOrWhiteSpace($GameExecutablePathOverride)) {
        $pathBox = $settingsHierarchy.SelectSingleNode(
            '//node[@class="TextBox" and contains(@text,"/Download/TeknoParrotGames/")]')
        if ($null -eq $pathBox) {
            throw 'The configured Android game-executable field was not found in Game Settings.'
        }
        Invoke-TapNode $pathBox
        Invoke-Adb -Arguments @('shell', 'input', 'keyevent', '123') | Out-Null
        $deleteKeys = @('shell', 'input', 'keyevent') +
            @(1..220 | ForEach-Object { '67' })
        Invoke-Adb -Arguments $deleteKeys | Out-Null
        $encodedPath = ConvertTo-AdbInputText $GameExecutablePathOverride
        Invoke-Adb -Arguments @('shell', 'input', 'text', $encodedPath) | Out-Null
        Start-Sleep -Milliseconds 700
        $settingsHierarchy = Get-Hierarchy 'settings-path-updated'
        $pathBox = $settingsHierarchy.SelectSingleNode(
            "//node[@class=`"TextBox`" and @text=`"$GameExecutablePathOverride`"]")
        if ($null -eq $pathBox) {
            throw 'The Android game-executable path did not update to the requested value.'
        }
        Write-Host "Game executable: $GameExecutablePathOverride"
    }

    if ($DebugLoggingMode -ne 'Unchanged') {
        for ($attempt = 1; $attempt -le 4; $attempt++) {
            $debugLabel = $settingsHierarchy.SelectSingleNode(
                '//node[@class="TextBlock" and @text="Debug logging"]')
            $debugCheck = if ($null -ne $debugLabel) {
                $debugLabel.SelectSingleNode('following::node[@class="CheckBox"][1]')
            }
            else {
                $null
            }

            # The hierarchy can include fields hidden underneath the fixed action
            # footer. Require the switch to be clearly above that footer before
            # tapping it; one Page-down click reliably exposes the diagnostics row
            # on both portrait and landscape Samsung layouts.
            $saveButton = $settingsHierarchy.SelectSingleNode(
                '//node[@class="Button" and translate(@text,"S","s")="save settings"]')
            $switchIsVisible = $false
            if ($null -ne $debugCheck -and $null -ne $saveButton -and
                $debugCheck.bounds -match '^\[\d+,(\d+)\]\[\d+,(\d+)\]$') {
                $switchBottom = [int]$matches[2]
                if ($saveButton.bounds -match '^\[\d+,(\d+)\]\[\d+,\d+\]$') {
                    $footerTop = [int]$matches[1]
                    $switchIsVisible = $switchBottom -lt $footerTop
                }
            }
            if ($switchIsVisible) {
                break
            }

            $pageDown = $settingsHierarchy.SelectSingleNode(
                '//node[@class="RepeatButton" and @text="Page down" and @enabled="true"]')
            if ($null -eq $pageDown) {
                throw 'The Game Settings diagnostics row could not be brought into view.'
            }
            Invoke-TapNode $pageDown
            Start-Sleep -Milliseconds 600
            $settingsHierarchy = Get-Hierarchy "settings-page-$attempt"
        }

        if ($null -eq $debugCheck) {
            throw 'The Debug logging switch was not found in Game Settings.'
        }
        $desiredChecked = $DebugLoggingMode -eq 'Enabled'
        $actualChecked = $debugCheck.checked -eq 'true'
        if ($actualChecked -ne $desiredChecked) {
            Invoke-TapNode $debugCheck
            Start-Sleep -Milliseconds 500
            $settingsHierarchy = Get-Hierarchy 'settings-toggled'
            $debugLabel = $settingsHierarchy.SelectSingleNode(
                '//node[@class="TextBlock" and @text="Debug logging"]')
            $debugCheck = if ($null -ne $debugLabel) {
                $debugLabel.SelectSingleNode('following::node[@class="CheckBox"][1]')
            }
            else {
                $null
            }
            if ($null -eq $debugCheck -or (($debugCheck.checked -eq 'true') -ne $desiredChecked)) {
                throw "Debug logging did not change to $DebugLoggingMode."
            }
        }
    }

    $saveButton = $settingsHierarchy.SelectSingleNode(
        '//node[@class="Button" and translate(@text,"S","s")="save settings" and @enabled="true"]')
    Invoke-TapNode $saveButton
    Start-Sleep -Seconds 2
    if ($DebugLoggingMode -ne 'Unchanged') {
        Write-Host "Debug logging: $DebugLoggingMode"
    }
    return Get-Hierarchy 'selection-after-settings'
}

function Wait-ForLibraryHierarchy([int] $Attempts = 12) {
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $candidate = Get-Hierarchy "initial-$attempt"
        $searchBox = $candidate.SelectSingleNode(
            '//node[@class="TextBox" and @content-desc="Search games..."]')
        $gameRunning = $candidate.SelectSingleNode('//node[@text="Game Running"]')
        $backButton = $candidate.SelectSingleNode(
            '//node[@class="Button" and @text="Back" and @enabled="true"]')
        if ($null -ne $searchBox -or $null -ne $gameRunning -or $null -ne $backButton) {
            return $candidate
        }

        Start-Sleep -Seconds 2
    }

    return $null
}

function Test-PackageProcessRunning([string] $PackageName) {
    $processId = (Invoke-Adb -Arguments @('shell', 'pidof', $PackageName) |
        Select-Object -First 1).Trim()
    return -not [string]::IsNullOrWhiteSpace($processId)
}

function Open-TeknoParrotLibrary {
    # Keep the long-lived UI process when it is healthy. Repeatedly cold
    # starting Mono/Avalonia between every title caused an Android 16 native
    # startup fault under the overnight stress loop and does not improve Wine
    # isolation; the companion process is still recycled independently.
    Invoke-Adb -Arguments @(
        'shell', 'monkey', '-p', 'com.teknoparrot.ui',
        '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null
    Start-Sleep -Seconds 4

    $hierarchy = Wait-ForLibraryHierarchy -Attempts 8
    if ($null -ne $hierarchy) {
        return $hierarchy
    }

    $failureMode = if (Test-PackageProcessRunning 'com.teknoparrot.ui') {
        'the UI stayed alive but did not expose its library'
    }
    else {
        'the UI process terminated during startup'
    }
    Write-Warning "TeknoParrot startup recovery: $failureMode. Performing one controlled cold start."

    Invoke-Adb -Arguments @('shell', 'am', 'force-stop', 'com.teknoparrot.ui') | Out-Null
    Start-Sleep -Seconds 2
    Invoke-Adb -Arguments @(
        'shell', 'monkey', '-p', 'com.teknoparrot.ui',
        '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null
    Start-Sleep -Seconds 7

    $hierarchy = Wait-ForLibraryHierarchy -Attempts 12
    if ($null -eq $hierarchy) {
        throw 'TeknoParrot library did not become ready after a controlled cold-start recovery.'
    }
    return $hierarchy
}

function Test-WinlatorProcessRunning {
    $processes = @(Invoke-Adb -Arguments @('shell', 'ps', '-A'))
    return [bool]($processes | Where-Object {
        $_ -match '\scom\.teknoparrot\.winlator$'
    })
}

function Wait-WinlatorProcessTreeClean {
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500
        $processes = @(Invoke-Adb -Arguments @('shell', 'ps', '-A'))
        if (-not ($processes | Where-Object {
                $_ -match '\s(com\.teknoparrot\.winlator|.*\.exe|wineserver|wine-preloader)$'
            })) {
            return $true
        }
    }
    return $false
}

function Stop-GameSession {
    if (-not (Test-WinlatorProcessRunning)) {
        return
    }

    # Opening Samsung's notification shade can force a memory-hungry Winlator
    # process to allocate another ART/JNI thread. Use the DUMP-protected,
    # shell-only receiver first so stress tests can recycle the exact managed
    # session without foregrounding SystemUI. The visible notification remains
    # the normal user stop path and the fallback exercised below.
    Invoke-Adb -Arguments @(
        'shell', 'am', 'broadcast',
        '-a', 'com.teknoparrot.ui.action.ADB_STOP_GAME_SESSION',
        '-n', 'com.teknoparrot.ui/com.teknoparrot.session.AdbGameSessionControlReceiver') |
        Out-Null
    if (Wait-WinlatorProcessTreeClean) {
        Write-Host 'Session stopped through the protected ADB control receiver.'
        return
    }

    Write-Warning 'Protected ADB stop did not recycle the session; trying the visible notification action.'
    try {
        Invoke-Adb -Arguments @('shell', 'cmd', 'statusbar', 'expand-notifications') | Out-Null
        Start-Sleep -Milliseconds 800
        $notificationHierarchy = Get-Hierarchy 'notification'
        $notificationRow = $notificationHierarchy.SelectSingleNode(
            '//node[@resource-id="com.android.systemui:id/expandableNotificationRow" ' +
            'and .//node[@text="TeknoParrot game session"]]')
        if ($null -eq $notificationRow) {
            # The row may be just below the current notification viewport.
            Invoke-Adb -Arguments @(
                'shell', 'input', 'swipe', '1170', '950', '1170', '450', '400') | Out-Null
            Start-Sleep -Milliseconds 600
            $notificationHierarchy = Get-Hierarchy 'notification-scrolled'
            $notificationRow = $notificationHierarchy.SelectSingleNode(
                '//node[@resource-id="com.android.systemui:id/expandableNotificationRow" ' +
                'and .//node[@text="TeknoParrot game session"]]')
            if ($null -eq $notificationRow) {
                throw 'The TeknoParrot foreground-session notification was not found.'
            }
        }

        $stopAction = $notificationRow.SelectSingleNode('.//node[@text="Stop" and @enabled="true"]')
        if ($null -eq $stopAction) {
            $expandAction = $notificationRow.SelectSingleNode(
                './/node[@resource-id="android:id/expand_button" and @clickable="true"]')
            Invoke-TapNode $expandAction
            Start-Sleep -Milliseconds 800
            $notificationHierarchy = Get-Hierarchy 'notification-expanded'
            $notificationRow = $notificationHierarchy.SelectSingleNode(
                '//node[@resource-id="com.android.systemui:id/expandableNotificationRow" ' +
                'and .//node[@text="TeknoParrot game session"]]')
            $stopAction = if ($null -ne $notificationRow) {
                $notificationRow.SelectSingleNode(
                    './/node[@text="Stop" and @enabled="true"]')
            }
            else {
                $null
            }
        }

        if ($null -eq $stopAction) {
            throw 'The TeknoParrot notification Stop action was not found.'
        }

        # Samsung can expose a partly clipped notification action in the UI
        # hierarchy even though taps on that bottom-inset area are discarded.
        # Reposition only when the resolved Stop action actually touches the
        # hierarchy bottom; scrolling every time can move a top row off-screen.
        $hierarchyBounds = $notificationHierarchy.hierarchy.node.bounds
        if ($hierarchyBounds -match '^\[\d+,\d+\]\[\d+,(\d+)\]$') {
            $hierarchyBottom = [int]$matches[1]
            if ($stopAction.bounds -match '^\[\d+,\d+\]\[\d+,(\d+)\]$' -and
                [int]$matches[1] -ge $hierarchyBottom - 10) {
                Invoke-Adb -Arguments @(
                    'shell', 'input', 'swipe', '1170', '950', '1170', '450', '400') | Out-Null
                Start-Sleep -Milliseconds 600
                $notificationHierarchy = Get-Hierarchy 'notification-action-visible'
                $notificationRow = $notificationHierarchy.SelectSingleNode(
                    '//node[@resource-id="com.android.systemui:id/expandableNotificationRow" ' +
                    'and .//node[@text="TeknoParrot game session"]]')
                $stopAction = if ($null -ne $notificationRow) {
                    $notificationRow.SelectSingleNode(
                        './/node[@text="Stop" and @enabled="true"]')
                }
                else {
                    $null
                }
                if ($null -eq $stopAction -and $null -ne $notificationRow) {
                    $expandAction = $notificationRow.SelectSingleNode(
                        './/node[@resource-id="android:id/expand_button" and @clickable="true"]')
                    Invoke-TapNode $expandAction
                    Start-Sleep -Milliseconds 800
                    $notificationHierarchy = Get-Hierarchy 'notification-action-expanded'
                    $notificationRow = $notificationHierarchy.SelectSingleNode(
                        '//node[@resource-id="com.android.systemui:id/expandableNotificationRow" ' +
                        'and .//node[@text="TeknoParrot game session"]]')
                    $stopAction = if ($null -ne $notificationRow) {
                        $notificationRow.SelectSingleNode(
                            './/node[@text="Stop" and @enabled="true"]')
                    }
                    else {
                        $null
                    }
                }
                if ($null -eq $stopAction) {
                    throw 'The TeknoParrot notification Stop action could not be repositioned.'
                }
            }
        }
        Invoke-TapNode $stopAction

        if (-not (Wait-WinlatorProcessTreeClean)) {
            throw 'The managed Winlator session did not recycle within ten seconds.'
        }
    }
    finally {
        Invoke-Adb -Arguments @('shell', 'cmd', 'statusbar', 'collapse') | Out-Null
    }
}

$powerState = (Invoke-Adb -Arguments @('shell', 'dumpsys', 'power')) -join "`n"
if ($powerState -notmatch 'mWakefulness=Awake') {
    Invoke-Adb -Arguments @('shell', 'input', 'keyevent', '224') | Out-Null
    Start-Sleep -Seconds 2
}
Invoke-Adb -Arguments @('shell', 'wm', 'dismiss-keyguard') | Out-Null
$windowPolicy = (Invoke-Adb -Arguments @('shell', 'dumpsys', 'window', 'policy')) -join "`n"
if ($windowPolicy -match 'mInputRestricted=true') {
    throw 'The Android device is securely locked; unlock it before claiming a physical game regression.'
}

Invoke-Adb -Arguments @('shell', 'am', 'force-stop', 'com.teknoparrot.winlator') | Out-Null
$hierarchy = Open-TeknoParrotLibrary
$searchBox = $hierarchy.SelectSingleNode(
    '//node[@class="TextBox" and @content-desc="Search games..."]')
for ($navigationAttempt = 1; $null -eq $searchBox -and $navigationAttempt -le 4; $navigationAttempt++) {
    $backButton = $hierarchy.SelectSingleNode('//node[@class="Button" and @text="Back" and @enabled="true"]')
    if ($null -ne $backButton) {
        Invoke-TapNode $backButton
    }
    else {
        Invoke-Adb -Arguments @('shell', 'input', 'keyevent', '4') | Out-Null
    }
    Start-Sleep -Seconds 2
    $hierarchy = Get-Hierarchy "library-$navigationAttempt"
    $searchBox = $hierarchy.SelectSingleNode(
        '//node[@class="TextBox" and @content-desc="Search games..."]')
}

if ($null -eq $searchBox) {
    throw 'TeknoParrot could not navigate back to the library without restarting the UI process.'
}
Invoke-TapNode $searchBox
Invoke-Adb -Arguments @('shell', 'input', 'keyevent', '123') | Out-Null
$deleteKeys = @('shell', 'input', 'keyevent') + @(1..100 | ForEach-Object { '67' })
Invoke-Adb -Arguments $deleteKeys | Out-Null
$encodedSearch = ConvertTo-AdbInputText $SearchText
Invoke-Adb -Arguments @('shell', 'input', 'text', $encodedSearch) | Out-Null
Invoke-Adb -Arguments @('shell', 'input', 'keyevent', '4') | Out-Null
Start-Sleep -Seconds 3

$selectionHierarchy = Get-Hierarchy 'selection'
$selectedGame = $selectionHierarchy.SelectSingleNode('//node[@class="ListBoxItem" and @selected="true"]')
if ($null -eq $selectedGame -or [string]::IsNullOrWhiteSpace($selectedGame.text)) {
    throw "No library game was selected after searching for '$SearchText'."
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedTitlePattern) -and
    $selectedGame.text -notmatch $ExpectedTitlePattern) {
    $matchingGames = @($selectionHierarchy.SelectNodes('//node[@class="ListBoxItem"]') |
        Where-Object { $_.text -match $ExpectedTitlePattern })
    if ($matchingGames.Count -ne 1) {
        throw "Search '$SearchText' selected '$($selectedGame.text)' and found $($matchingGames.Count) list items matching '$ExpectedTitlePattern'."
    }

    Invoke-TapNode $matchingGames[0]
    Start-Sleep -Seconds 1
    $selectionHierarchy = Get-Hierarchy 'expected-selection'
    $selectedGame = $selectionHierarchy.SelectSingleNode(
        '//node[@class="ListBoxItem" and @selected="true"]')
    if ($null -eq $selectedGame -or $selectedGame.text -notmatch $ExpectedTitlePattern) {
        throw "Could not select the game matching '$ExpectedTitlePattern' after searching for '$SearchText'."
    }
}

$selectionHierarchy = Set-SelectedGameDebugLogging $selectionHierarchy
$launchButton = $selectionHierarchy.SelectSingleNode(
    '//node[@class="Button" and contains(@text,"LAUNCH GAME") and @enabled="true"]')
Invoke-TapNode $launchButton
Write-Host "Launching: $($selectedGame.text)"
Start-Sleep -Seconds 8

$monitorScript = Join-Path $PSScriptRoot 'Watch-AndroidGameHealth.ps1'
$monitorArguments = @{
    DeviceSerial = $DeviceSerial
    AdbPath = $adb
    DurationSeconds = $DurationSeconds
    IntervalSeconds = 10
    OutputDirectory = $OutputDirectory
    Label = $safePrefix
}
& $monitorScript @monitorArguments

$uidLine = (Invoke-Adb -Arguments @(
    'shell', 'cmd', 'package', 'list', 'packages', '-U', 'com.teknoparrot.winlator') |
    Select-Object -First 1)
if ($uidLine -notmatch 'uid:(\d+)') {
    throw 'Could not resolve the Winlator package UID for the final process snapshot.'
}
$uid = $matches[1]
$processRows = @(Invoke-Adb -Arguments @('shell', 'ps', '-A', '-o', 'UID,PID,PPID,STAT,NAME') |
    Where-Object { $_ -match "^\s*$uid\s+" })
$thermalRows = @(Invoke-Adb -Arguments @('shell', 'dumpsys', 'thermalservice') |
    Where-Object { $_ -match 'Thermal Status:|mType=2' })
$focusRows = @(Invoke-Adb -Arguments @('shell', 'dumpsys', 'window') |
    Where-Object { $_ -match 'mCurrentFocus=' })
$summaryPath = Join-Path $OutputDirectory "$safePrefix-result.txt"
@(
    "SelectedTitle=$($selectedGame.text)"
    "SearchText=$SearchText"
    "DurationSeconds=$DurationSeconds"
    'Processes:'
    $processRows
    'Thermal:'
    $thermalRows
    'Focus:'
    $focusRows
) | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "Final process snapshot:"
foreach ($processRow in $processRows) {
    Write-Host $processRow
}
Write-Host "Result summary: $summaryPath"

if (-not $KeepGameRunning) {
    Stop-GameSession
    Write-Host 'Managed Winlator process tree is clean.'
}
