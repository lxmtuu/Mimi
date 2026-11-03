<#
.SYNOPSIS
    Sends commands to a Windows Global System Media Transport Controls
    (GSMTC) session and verifies the published state after each command.

.DESCRIPTION
    This is a command-path acceptance harness for situations where the
    Windows Quick Settings card does not render every control an app
    publishes. It can exercise playback position, FastForward/Rewind,
    Shuffle, and AutoRepeat requests that the shell would normally send,
    then polls GSMTC to verify that the app state actually changed.

    This is not a visual Quick Settings PASS. It proves the app receives
    and applies GSMTC commands and publishes the resulting state; it does
    not prove that a particular Windows shell build draws a seek bar,
    timestamps, or Shuffle/Repeat buttons.

    By default the script targets only the exact Mimi.App session. Run it
    with Windows PowerShell 5.1 (powershell.exe). Position commands are
    easiest to interpret while playback is paused, although the verifier
    also extrapolates the timeline while Playing.

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Invoke-SmtcSessionCommand.ps1 `
        -Command Snapshot

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Invoke-SmtcSessionCommand.ps1 `
        -Command ShuffleOn,RepeatList,RepeatTrack,RepeatNone,ShuffleOff `
        -JsonPath .\artifacts\acceptance\gsmtc-modes.json

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Invoke-SmtcSessionCommand.ps1 `
        -Command FastForward,Rewind,Seek -SeekPositionSeconds 15
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        'Snapshot',
        'Play',
        'Pause',
        'TogglePlayPause',
        'Next',
        'Previous',
        'FastForward',
        'Rewind',
        'Seek',
        'SeekForward',
        'SeekBackward',
        'ShuffleOn',
        'ShuffleOff',
        'RepeatNone',
        'RepeatList',
        'RepeatTrack')]
    [string[]]$Command,

    [string]$SourceAppUserModelId = 'Mimi.App',

    [ValidateRange(0, 86400)]
    [double]$SeekPositionSeconds = 15,

    [ValidateRange(0.1, 3600)]
    [double]$SeekDeltaSeconds = 10,

    [ValidateRange(0.1, 60)]
    [double]$PositionToleranceSeconds = 3,

    [ValidateRange(0, 60)]
    [int]$WaitSeconds = 10,

    [ValidateRange(250, 30000)]
    [int]$VerifyTimeoutMilliseconds = 4000,

    [ValidateRange(50, 2000)]
    [int]$VerifyPollMilliseconds = 250,

    [switch]$RestoreInitialModes,

    [string]$JsonPath,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this harness with Windows PowerShell 5.1 (powershell.exe), not PowerShell Core.'
}

$script:SessionManagerType = [type]::GetType(
    'Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime')
$script:MediaPropertiesType = [type]::GetType(
    'Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType=WindowsRuntime')
$script:AutoRepeatModeType = [type]::GetType(
    'Windows.Media.MediaPlaybackAutoRepeatMode, Windows.Media, ContentType=WindowsRuntime')

if ($null -eq $script:SessionManagerType -or $null -eq $script:MediaPropertiesType) {
    throw 'Could not load the Windows.Media.Control GSMTC WinRT types.'
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime

$script:AsTaskOperationMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and
        $_.IsGenericMethodDefinition -and
        $_.GetGenericArguments().Count -eq 1 -and
        $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } |
    Select-Object -First 1

if ($null -eq $script:AsTaskOperationMethod) {
    throw 'Could not locate WindowsRuntimeSystemExtensions.AsTask(IAsyncOperation[T]).'
}

function Wait-WinRtOperation {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Operation,

        [Parameter(Mandatory = $true)]
        [type]$ResultType
    )

    $method = $script:AsTaskOperationMethod.MakeGenericMethod(@($ResultType))
    $task = $method.Invoke($null, @($Operation))
    [void]$task.Wait()
    return $task.Result
}

function ConvertTo-TextOrNull {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    return [string]$Value
}

function ConvertTo-BoolOrNull {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [bool]) {
        return $Value
    }

    try {
        return [bool]::Parse([string]$Value)
    }
    catch {
        try {
            return [bool]$Value
        }
        catch {
            return $null
        }
    }
}

function ConvertTo-DoubleOrNull {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    try {
        return [double]$Value
    }
    catch {
        return $null
    }
}

function Limit-Position {
    param(
        [double]$Value,
        [double]$Minimum,
        [double]$Maximum
    )

    if ($Maximum -lt $Minimum) {
        return $Minimum
    }

    return [Math]::Max($Minimum, [Math]::Min($Maximum, $Value))
}

function Get-SessionMatches {
    param([Parameter(Mandatory = $true)][object]$Manager)

    $matches = @()
    foreach ($session in $Manager.GetSessions()) {
        if ($session.SourceAppUserModelId -eq $SourceAppUserModelId) {
            $matches += $session
        }
    }

    return $matches
}

function Select-Session {
    param([Parameter(Mandatory = $true)][object]$Manager)

    $matches = @(Get-SessionMatches -Manager $Manager)
    if ($matches.Count -eq 0) {
        return $null
    }

    if ($matches.Count -eq 1) {
        return $matches[0]
    }

    $current = $Manager.GetCurrentSession()
    if ($null -ne $current) {
        foreach ($candidate in $matches) {
            if ($candidate.SourceAppUserModelId -eq $current.SourceAppUserModelId) {
                return $candidate
            }
        }
    }

    foreach ($candidate in $matches) {
        if ([string]$candidate.GetPlaybackInfo().PlaybackStatus -eq 'Playing') {
            return $candidate
        }
    }

    return $matches[0]
}

function Get-AvailableSourceIds {
    param([Parameter(Mandatory = $true)][object]$Manager)

    $sourceIds = @()
    foreach ($session in $Manager.GetSessions()) {
        $sourceIds += [string]$session.SourceAppUserModelId
    }

    return $sourceIds
}

function Get-SessionSnapshot {
    param(
        [Parameter(Mandatory = $true)][object]$Session,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $playbackInfo = $Session.GetPlaybackInfo()
    $controls = $playbackInfo.Controls
    $timeline = $Session.GetTimelineProperties()
    $sampledAtUtc = [DateTimeOffset]::UtcNow

    $mediaProperties = $null
    $mediaPropertiesError = $null
    try {
        $mediaProperties = Wait-WinRtOperation `
            -Operation ($Session.TryGetMediaPropertiesAsync()) `
            -ResultType $script:MediaPropertiesType
    }
    catch {
        $mediaPropertiesError = $_.Exception.Message
    }

    $startSeconds = $timeline.StartTime.TotalSeconds
    $endSeconds = $timeline.EndTime.TotalSeconds
    $positionSeconds = $timeline.Position.TotalSeconds
    $relativePositionSeconds = $positionSeconds - $startSeconds
    $durationSeconds = $endSeconds - $startSeconds
    $minSeekRelativeSeconds = $timeline.MinSeekTime.TotalSeconds - $startSeconds
    $maxSeekRelativeSeconds = $timeline.MaxSeekTime.TotalSeconds - $startSeconds
    $lastUpdatedTime = [DateTimeOffset]$timeline.LastUpdatedTime
    $playbackStatus = [string]$playbackInfo.PlaybackStatus
    $playbackRate = ConvertTo-DoubleOrNull $playbackInfo.PlaybackRate

    $timelineAgeSeconds = $null
    $extrapolatedPositionSeconds = $relativePositionSeconds
    if ($lastUpdatedTime.Year -gt 1601) {
        $timelineAgeSeconds = ($sampledAtUtc - $lastUpdatedTime).TotalSeconds
        if ($playbackStatus -eq 'Playing' -and
            $timelineAgeSeconds -ge 0 -and $timelineAgeSeconds -lt 600) {
            $effectiveRate = 1.0
            if ($null -ne $playbackRate -and $playbackRate -gt 0) {
                $effectiveRate = $playbackRate
            }

            $extrapolatedPositionSeconds = `
                $relativePositionSeconds + ($timelineAgeSeconds * $effectiveRate)
            if ($durationSeconds -gt 0) {
                $extrapolatedPositionSeconds = Limit-Position `
                    -Value $extrapolatedPositionSeconds `
                    -Minimum 0 `
                    -Maximum $durationSeconds
            }
            else {
                $extrapolatedPositionSeconds = [Math]::Max(
                    0,
                    $extrapolatedPositionSeconds)
            }
        }
    }

    return [pscustomobject][ordered]@{
        Label = $Label
        SampledAtUtc = $sampledAtUtc.ToString('o')
        SourceAppUserModelId = [string]$Session.SourceAppUserModelId
        Title = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.Title } else { $null }
        Artist = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.Artist } else { $null }
        PlaybackStatus = $playbackStatus
        PlaybackRate = $playbackRate
        IsShuffleActive = ConvertTo-BoolOrNull $playbackInfo.IsShuffleActive
        IsShuffleActiveRaw = ConvertTo-TextOrNull $playbackInfo.IsShuffleActive
        AutoRepeatMode = ConvertTo-TextOrNull $playbackInfo.AutoRepeatMode
        MediaPropertiesError = $mediaPropertiesError
        Controls = [pscustomobject][ordered]@{
            IsPlayEnabled = [bool]$controls.IsPlayEnabled
            IsPauseEnabled = [bool]$controls.IsPauseEnabled
            IsPlayPauseToggleEnabled = [bool]$controls.IsPlayPauseToggleEnabled
            IsNextEnabled = [bool]$controls.IsNextEnabled
            IsPreviousEnabled = [bool]$controls.IsPreviousEnabled
            IsFastForwardEnabled = [bool]$controls.IsFastForwardEnabled
            IsRewindEnabled = [bool]$controls.IsRewindEnabled
            IsPlaybackPositionEnabled = [bool]$controls.IsPlaybackPositionEnabled
            IsPlaybackRateEnabled = [bool]$controls.IsPlaybackRateEnabled
            IsRepeatEnabled = [bool]$controls.IsRepeatEnabled
            IsShuffleEnabled = [bool]$controls.IsShuffleEnabled
            IsStopEnabled = [bool]$controls.IsStopEnabled
        }
        Timeline = [pscustomobject][ordered]@{
            StartTimeSeconds = $startSeconds
            EndTimeSeconds = $endSeconds
            PositionSeconds = $positionSeconds
            RelativePositionSeconds = $relativePositionSeconds
            ExtrapolatedPositionSeconds = $extrapolatedPositionSeconds
            DurationSeconds = $durationSeconds
            MinSeekTimeSeconds = $timeline.MinSeekTime.TotalSeconds
            MaxSeekTimeSeconds = $timeline.MaxSeekTime.TotalSeconds
            MinSeekRelativeSeconds = $minSeekRelativeSeconds
            MaxSeekRelativeSeconds = $maxSeekRelativeSeconds
            LastUpdatedTimeUtc = $lastUpdatedTime.ToString('o')
            LastUpdatedAgeSeconds = $timelineAgeSeconds
        }
    }
}

function Invoke-SessionBooleanOperation {
    param([Parameter(Mandatory = $true)][object]$Operation)

    return [bool](Wait-WinRtOperation -Operation $Operation -ResultType ([bool]))
}

function Get-AutoRepeatModeValue {
    param(
        [Parameter(Mandatory = $true)][object]$Session,
        [Parameter(Mandatory = $true)][string]$Mode
    )

    if ($null -eq $script:AutoRepeatModeType) {
        $currentMode = $Session.GetPlaybackInfo().AutoRepeatMode
        if ($null -eq $currentMode) {
            throw 'Could not load or infer Windows.Media.MediaPlaybackAutoRepeatMode.'
        }

        $script:AutoRepeatModeType = $currentMode.GetType()
    }

    return [Enum]::Parse($script:AutoRepeatModeType, $Mode)
}

function Invoke-ShuffleChange {
    param(
        [Parameter(Mandatory = $true)][object]$Session,
        [Parameter(Mandatory = $true)][bool]$Enabled
    )

    return Invoke-SessionBooleanOperation `
        -Operation ($Session.TryChangeShuffleActiveAsync($Enabled))
}

function Invoke-RepeatChange {
    param(
        [Parameter(Mandatory = $true)][object]$Session,
        [Parameter(Mandatory = $true)][string]$Mode
    )

    $value = Get-AutoRepeatModeValue -Session $Session -Mode $Mode
    return Invoke-SessionBooleanOperation `
        -Operation ($Session.TryChangeAutoRepeatModeAsync($value))
}

function Invoke-SeekChange {
    param(
        [Parameter(Mandatory = $true)][object]$Session,
        [Parameter(Mandatory = $true)][double]$AbsolutePositionSeconds
    )

    $ticks = [TimeSpan]::FromSeconds($AbsolutePositionSeconds).Ticks
    return Invoke-SessionBooleanOperation `
        -Operation ($Session.TryChangePlaybackPositionAsync([long]$ticks))
}

function Wait-ForVerification {
    param(
        [Parameter(Mandatory = $true)][object]$Manager,
        [Parameter(Mandatory = $true)][scriptblock]$Predicate
    )

    $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds(
        $VerifyTimeoutMilliseconds)
    $lastSnapshot = $null

    do {
        $session = Select-Session -Manager $Manager
        if ($null -eq $session) {
            throw "The GSMTC session '$SourceAppUserModelId' disappeared during verification."
        }

        $lastSnapshot = Get-SessionSnapshot -Session $session -Label 'poll'
        if ([bool](& $Predicate $lastSnapshot)) {
            return @{
                Verified = $true
                Snapshot = $lastSnapshot
            }
        }

        if ([DateTimeOffset]::UtcNow -ge $deadline) {
            break
        }

        Start-Sleep -Milliseconds $VerifyPollMilliseconds
    } while ($true)

    return @{
        Verified = $false
        Snapshot = $lastSnapshot
    }
}

$manager = Wait-WinRtOperation `
    -Operation ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) `
    -ResultType ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($WaitSeconds)
$initialSession = Select-Session -Manager $manager
while ($null -eq $initialSession -and [DateTimeOffset]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 250
    $initialSession = Select-Session -Manager $manager
}

if ($null -eq $initialSession) {
    $available = @(Get-AvailableSourceIds -Manager $manager)
    $availableText = if ($available.Count -eq 0) {
        '(no GSMTC sessions)'
    }
    else {
        $available -join '; '
    }

    throw "No GSMTC session exactly matches SourceAppUserModelId '$SourceAppUserModelId'. Available sessions: $availableText"
}

$initialSnapshot = Get-SessionSnapshot -Session $initialSession -Label 'initial'
$results = @()
$overallSucceeded = $true

for ($index = 0; $index -lt $Command.Count; $index++) {
    $commandName = $Command[$index]
    $session = Select-Session -Manager $manager
    if ($null -eq $session) {
        throw "The GSMTC session '$SourceAppUserModelId' disappeared before command '$commandName'."
    }

    $before = Get-SessionSnapshot -Session $session -Label 'before'
    $after = $before
    $operationAccepted = $false
    $verificationStatus = 'Fail'
    $verificationDetail = $null
    $targetPositionSeconds = $null
    $errorMessage = $null
    $commandStartedUtc = [DateTimeOffset]::UtcNow

    try {
        $predicate = $null

        switch ($commandName) {
            'Snapshot' {
                $operationAccepted = $true
                $verificationStatus = 'Pass'
                $verificationDetail = 'Snapshot only; no command was sent.'
            }

            'Play' {
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TryPlayAsync())
                $predicate = {
                    param($snapshot)
                    $snapshot.PlaybackStatus -eq 'Playing'
                }
                $verificationDetail = 'Expected PlaybackStatus=Playing.'
            }

            'Pause' {
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TryPauseAsync())
                $predicate = {
                    param($snapshot)
                    $snapshot.PlaybackStatus -eq 'Paused'
                }
                $verificationDetail = 'Expected PlaybackStatus=Paused.'
            }

            'TogglePlayPause' {
                $beforeStatus = $before.PlaybackStatus
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TryTogglePlayPauseAsync())
                $predicate = {
                    param($snapshot)
                    $snapshot.PlaybackStatus -ne $beforeStatus -and
                    $snapshot.PlaybackStatus -in @('Playing', 'Paused')
                }
                $verificationDetail = "Expected PlaybackStatus to change from $beforeStatus."
            }

            'Next' {
                $beforeTitle = $before.Title
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TrySkipNextAsync())
                $predicate = {
                    param($snapshot)
                    ($snapshot.Title -ne $beforeTitle) -or
                    (($beforePosition -gt 3) -and
                     ($snapshot.Timeline.ExtrapolatedPositionSeconds -lt ($beforePosition - 2)))
                }
                $verificationDetail = 'Expected a title change or a clear position reset.'
            }

            'Previous' {
                $beforeTitle = $before.Title
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TrySkipPreviousAsync())
                $predicate = {
                    param($snapshot)
                    ($snapshot.Title -ne $beforeTitle) -or
                    (($beforePosition -gt 3) -and
                     ($snapshot.Timeline.ExtrapolatedPositionSeconds -lt ($beforePosition - 2)))
                }
                $verificationDetail = 'Expected a title change or restart near the beginning of the track.'
            }

            'FastForward' {
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $targetPositionSeconds = Limit-Position `
                    -Value ($beforePosition + 10) `
                    -Minimum $before.Timeline.MinSeekRelativeSeconds `
                    -Maximum $before.Timeline.MaxSeekRelativeSeconds
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TryFastForwardAsync())
                $predicate = {
                    param($snapshot)
                    $elapsed = ([DateTimeOffset]::Parse($snapshot.SampledAtUtc) - $commandStartedUtc).TotalSeconds
                    $expected = $targetPositionSeconds
                    if ($snapshot.PlaybackStatus -eq 'Playing') {
                        $expected += [Math]::Max(0, $elapsed)
                    }
                    [Math]::Abs($snapshot.Timeline.ExtrapolatedPositionSeconds - $expected) -le $PositionToleranceSeconds
                }
                $verificationDetail = "Expected Mimi's FastForward mapping to seek about +10 seconds to $targetPositionSeconds."
            }

            'Rewind' {
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $targetPositionSeconds = Limit-Position `
                    -Value ($beforePosition - 10) `
                    -Minimum $before.Timeline.MinSeekRelativeSeconds `
                    -Maximum $before.Timeline.MaxSeekRelativeSeconds
                $operationAccepted = Invoke-SessionBooleanOperation `
                    -Operation ($session.TryRewindAsync())
                $predicate = {
                    param($snapshot)
                    $elapsed = ([DateTimeOffset]::Parse($snapshot.SampledAtUtc) - $commandStartedUtc).TotalSeconds
                    $expected = $targetPositionSeconds
                    if ($snapshot.PlaybackStatus -eq 'Playing') {
                        $expected += [Math]::Max(0, $elapsed)
                    }
                    [Math]::Abs($snapshot.Timeline.ExtrapolatedPositionSeconds - $expected) -le $PositionToleranceSeconds
                }
                $verificationDetail = "Expected Mimi's Rewind mapping to seek about -10 seconds to $targetPositionSeconds."
            }

            'Seek' {
                $targetPositionSeconds = Limit-Position `
                    -Value $SeekPositionSeconds `
                    -Minimum $before.Timeline.MinSeekRelativeSeconds `
                    -Maximum $before.Timeline.MaxSeekRelativeSeconds
                $operationAccepted = Invoke-SeekChange `
                    -Session $session `
                    -AbsolutePositionSeconds ($before.Timeline.StartTimeSeconds + $targetPositionSeconds)
                $predicate = {
                    param($snapshot)
                    $elapsed = ([DateTimeOffset]::Parse($snapshot.SampledAtUtc) - $commandStartedUtc).TotalSeconds
                    $expected = $targetPositionSeconds
                    if ($snapshot.PlaybackStatus -eq 'Playing') {
                        $expected += [Math]::Max(0, $elapsed)
                    }
                    [Math]::Abs($snapshot.Timeline.ExtrapolatedPositionSeconds - $expected) -le $PositionToleranceSeconds
                }
                $verificationDetail = "Expected relative position near $targetPositionSeconds seconds."
            }

            'SeekForward' {
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $targetPositionSeconds = Limit-Position `
                    -Value ($beforePosition + $SeekDeltaSeconds) `
                    -Minimum $before.Timeline.MinSeekRelativeSeconds `
                    -Maximum $before.Timeline.MaxSeekRelativeSeconds
                $operationAccepted = Invoke-SeekChange `
                    -Session $session `
                    -AbsolutePositionSeconds ($before.Timeline.StartTimeSeconds + $targetPositionSeconds)
                $predicate = {
                    param($snapshot)
                    $elapsed = ([DateTimeOffset]::Parse($snapshot.SampledAtUtc) - $commandStartedUtc).TotalSeconds
                    $expected = $targetPositionSeconds
                    if ($snapshot.PlaybackStatus -eq 'Playing') {
                        $expected += [Math]::Max(0, $elapsed)
                    }
                    [Math]::Abs($snapshot.Timeline.ExtrapolatedPositionSeconds - $expected) -le $PositionToleranceSeconds
                }
                $verificationDetail = "Expected relative position near $targetPositionSeconds seconds."
            }

            'SeekBackward' {
                $beforePosition = $before.Timeline.ExtrapolatedPositionSeconds
                $targetPositionSeconds = Limit-Position `
                    -Value ($beforePosition - $SeekDeltaSeconds) `
                    -Minimum $before.Timeline.MinSeekRelativeSeconds `
                    -Maximum $before.Timeline.MaxSeekRelativeSeconds
                $operationAccepted = Invoke-SeekChange `
                    -Session $session `
                    -AbsolutePositionSeconds ($before.Timeline.StartTimeSeconds + $targetPositionSeconds)
                $predicate = {
                    param($snapshot)
                    $elapsed = ([DateTimeOffset]::Parse($snapshot.SampledAtUtc) - $commandStartedUtc).TotalSeconds
                    $expected = $targetPositionSeconds
                    if ($snapshot.PlaybackStatus -eq 'Playing') {
                        $expected += [Math]::Max(0, $elapsed)
                    }
                    [Math]::Abs($snapshot.Timeline.ExtrapolatedPositionSeconds - $expected) -le $PositionToleranceSeconds
                }
                $verificationDetail = "Expected relative position near $targetPositionSeconds seconds."
            }

            'ShuffleOn' {
                $operationAccepted = Invoke-ShuffleChange -Session $session -Enabled $true
                $predicate = {
                    param($snapshot)
                    $snapshot.IsShuffleActive -eq $true
                }
                $verificationDetail = 'Expected IsShuffleActive=True.'
            }

            'ShuffleOff' {
                $operationAccepted = Invoke-ShuffleChange -Session $session -Enabled $false
                $predicate = {
                    param($snapshot)
                    $snapshot.IsShuffleActive -eq $false
                }
                $verificationDetail = 'Expected IsShuffleActive=False.'
            }

            'RepeatNone' {
                $expectedRepeatMode = 'None'
                $operationAccepted = Invoke-RepeatChange -Session $session -Mode $expectedRepeatMode
                $predicate = {
                    param($snapshot)
                    $snapshot.AutoRepeatMode -eq $expectedRepeatMode
                }
                $verificationDetail = 'Expected AutoRepeatMode=None.'
            }

            'RepeatList' {
                $expectedRepeatMode = 'List'
                $operationAccepted = Invoke-RepeatChange -Session $session -Mode $expectedRepeatMode
                $predicate = {
                    param($snapshot)
                    $snapshot.AutoRepeatMode -eq $expectedRepeatMode
                }
                $verificationDetail = 'Expected AutoRepeatMode=List (Mimi Repeat All).'
            }

            'RepeatTrack' {
                $expectedRepeatMode = 'Track'
                $operationAccepted = Invoke-RepeatChange -Session $session -Mode $expectedRepeatMode
                $predicate = {
                    param($snapshot)
                    $snapshot.AutoRepeatMode -eq $expectedRepeatMode
                }
                $verificationDetail = 'Expected AutoRepeatMode=Track (Mimi Repeat One).'
            }
        }

        if ($operationAccepted -and $null -ne $predicate) {
            $verification = Wait-ForVerification -Manager $manager -Predicate $predicate
            $after = $verification.Snapshot
            if ([bool]$verification.Verified) {
                $verificationStatus = 'Pass'
            }
            else {
                $verificationStatus = 'Fail'
            }
        }
        elseif (-not $operationAccepted) {
            $afterSession = Select-Session -Manager $manager
            if ($null -ne $afterSession) {
                $after = Get-SessionSnapshot -Session $afterSession -Label 'after'
            }
        }
    }
    catch {
        # $operationAccepted remains false if the API call itself threw. If
        # the API already accepted the command but verification threw, keep
        # that distinction in the evidence instead of rewriting it.
        $verificationStatus = 'Fail'
        $errorMessage = $_.Exception.Message
        try {
            $afterSession = Select-Session -Manager $manager
            if ($null -ne $afterSession) {
                $after = Get-SessionSnapshot -Session $afterSession -Label 'after-error'
            }
        }
        catch {
            $errorMessage = "$errorMessage; after-snapshot failed: $($_.Exception.Message)"
        }
    }

    if ($commandName -eq 'Snapshot') {
        $after = $before
    }

    $commandSucceeded = $operationAccepted -and $verificationStatus -eq 'Pass'
    if (-not $commandSucceeded) {
        $overallSucceeded = $false
    }

    $results += [pscustomobject][ordered]@{
        Index = $index + 1
        Command = $commandName
        StartedAtUtc = $commandStartedUtc.ToString('o')
        CommandAccepted = $operationAccepted
        VerificationStatus = $verificationStatus
        VerificationDetail = $verificationDetail
        TargetRelativePositionSeconds = $targetPositionSeconds
        Error = $errorMessage
        Before = $before
        After = $after
    }

    Write-Host ("[{0}] {1}: accepted={2}; verification={3}; title='{4}'; status={5}; shuffle={6}; repeat={7}; position={8:N2}s" -f `
        ($index + 1), $commandName, $operationAccepted, $verificationStatus,
        $after.Title, $after.PlaybackStatus, $after.IsShuffleActiveRaw,
        $after.AutoRepeatMode, $after.Timeline.ExtrapolatedPositionSeconds)

    if (-not $commandSucceeded) {
        break
    }
}

$modeRestore = $null
if ($RestoreInitialModes) {
    $restoreAccepted = $true
    $restoreError = $null
    $restoreSnapshot = $null

    try {
        $restoreSession = Select-Session -Manager $manager
        if ($null -eq $restoreSession) {
            throw "The GSMTC session '$SourceAppUserModelId' disappeared before mode restore."
        }

        if ($null -ne $initialSnapshot.IsShuffleActive) {
            $shuffleAccepted = Invoke-ShuffleChange `
                -Session $restoreSession `
                -Enabled ([bool]$initialSnapshot.IsShuffleActive)
            $restoreAccepted = $restoreAccepted -and $shuffleAccepted
        }

        if ($initialSnapshot.AutoRepeatMode -in @('None', 'List', 'Track')) {
            $restoreSession = Select-Session -Manager $manager
            $repeatAccepted = Invoke-RepeatChange `
                -Session $restoreSession `
                -Mode $initialSnapshot.AutoRepeatMode
            $restoreAccepted = $restoreAccepted -and $repeatAccepted
        }

        $restoreSession = Select-Session -Manager $manager
        if ($null -ne $restoreSession) {
            $restoreSnapshot = Get-SessionSnapshot -Session $restoreSession -Label 'restore'
        }
    }
    catch {
        $restoreAccepted = $false
        $restoreError = $_.Exception.Message
    }

    if (-not $restoreAccepted) {
        $overallSucceeded = $false
    }

    $modeRestore = [pscustomobject][ordered]@{
        Requested = $true
        Accepted = $restoreAccepted
        InitialIsShuffleActive = $initialSnapshot.IsShuffleActive
        InitialAutoRepeatMode = $initialSnapshot.AutoRepeatMode
        Error = $restoreError
        After = $restoreSnapshot
    }
}

$finalSession = Select-Session -Manager $manager
$finalSnapshot = if ($null -ne $finalSession) {
    Get-SessionSnapshot -Session $finalSession -Label 'final'
}
else {
    $null
}

$result = [pscustomobject][ordered]@{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    SourceAppUserModelId = $SourceAppUserModelId
    CommandsRequested = @($Command)
    CommandsExecuted = $results.Count
    OverallSucceeded = $overallSucceeded
    AcceptanceBoundary = 'GSMTC command-path verification only; not a visual Quick Settings PASS.'
    Initial = $initialSnapshot
    Results = $results
    ModeRestore = $modeRestore
    Final = $finalSnapshot
}

if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
    $jsonDirectory = Split-Path -Parent $JsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDirectory)) {
        [void](New-Item -ItemType Directory -Force -Path $jsonDirectory)
    }

    $result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $JsonPath -Encoding UTF8
}

Write-Host ''
Write-Host "OverallSucceeded: $overallSucceeded"
Write-Host 'Acceptance boundary: GSMTC command-path verification only; not a visual Quick Settings PASS.'

if ($PassThru) {
    $result
}

if (-not $overallSucceeded) {
    exit 1
}
