<#
.SYNOPSIS
    Inspects a Windows Global System Media Transport Controls (GSMTC)
    session without sending playback commands.

.DESCRIPTION
    Samples the media properties, playback info, enabled controls, and
    timeline published by a media app. This is a diagnostic for A7/A8:
    it distinguishes "the app did not publish a usable timeline" from
    "the app publishes a timeline, but the Windows shell surface does not
    render a seek bar or timestamps".

    The script is read-only. It never calls Play, Pause, Next, Previous,
    FastForward, Rewind, or TryChangePlaybackPositionAsync, and its output
    is not by itself an acceptance PASS for direct Quick Settings use.

    Run it with Windows PowerShell 5.1 (powershell.exe), because that host
    can load the WinRT types and System.Runtime.WindowsRuntime used below.

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Inspect-SmtcSession.ps1 `
        -SourceFilter Mimi -Samples 12 -IntervalMilliseconds 1000

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Inspect-SmtcSession.ps1 `
        -JsonPath .\artifacts\acceptance\smtc-timeline.json
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$SourceFilter = 'Mimi',

    [ValidateRange(1, 120)]
    [int]$Samples = 12,

    [ValidateRange(100, 10000)]
    [int]$IntervalMilliseconds = 1000,

    [ValidateRange(0, 60)]
    [int]$WaitSeconds = 10,

    [string]$JsonPath,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this diagnostic with Windows PowerShell 5.1 (powershell.exe), not PowerShell Core.'
}

$script:SessionManagerType = [type]::GetType(
    'Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime')
$script:MediaPropertiesType = [type]::GetType(
    'Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType=WindowsRuntime')
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

function Get-SessionMatches {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manager,

        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    $sessionMatches = @()
    foreach ($session in $Manager.GetSessions()) {
        if ($session.SourceAppUserModelId -like "*$Filter*") {
            $sessionMatches += $session
        }
    }

    return $sessionMatches
}

function Select-Session {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manager,

        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    $sessionMatches = @(Get-SessionMatches -Manager $Manager -Filter $Filter)
    if ($sessionMatches.Count -eq 0) {
        return $null
    }

    if ($sessionMatches.Count -eq 1) {
        return $sessionMatches[0]
    }

    $current = $Manager.GetCurrentSession()
    if ($null -ne $current) {
        foreach ($candidate in $sessionMatches) {
            if ($candidate.SourceAppUserModelId -eq $current.SourceAppUserModelId) {
                return $candidate
            }
        }
    }

    foreach ($candidate in $sessionMatches) {
        $playbackInfo = $candidate.GetPlaybackInfo()
        if ([string]$playbackInfo.PlaybackStatus -eq 'Playing') {
            return $candidate
        }
    }

    return $sessionMatches[0]
}

function Get-AvailableSourceIds {
    param([Parameter(Mandatory = $true)][object]$Manager)

    $sourceIds = @()
    foreach ($session in $Manager.GetSessions()) {
        $sourceIds += [string]$session.SourceAppUserModelId
    }

    return $sourceIds
}

$manager = Wait-WinRtOperation `
    -Operation ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) `
    -ResultType ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($WaitSeconds)
$selectedSession = Select-Session -Manager $manager -Filter $SourceFilter
while ($null -eq $selectedSession -and [DateTimeOffset]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 250
    $selectedSession = Select-Session -Manager $manager -Filter $SourceFilter
}

if ($null -eq $selectedSession) {
    $available = @(Get-AvailableSourceIds -Manager $manager)
    $availableText = if ($available.Count -eq 0) {
        '(no GSMTC sessions)'
    }
    else {
        $available -join '; '
    }

    throw "No GSMTC session matched filter '$SourceFilter'. Available sessions: $availableText"
}

$sampleRows = @()
for ($sampleIndex = 1; $sampleIndex -le $Samples; $sampleIndex++) {
    $session = Select-Session -Manager $manager -Filter $SourceFilter
    if ($null -eq $session) {
        throw "The GSMTC session matching '$SourceFilter' disappeared during sampling."
    }

    $playbackInfo = $session.GetPlaybackInfo()
    $controls = $playbackInfo.Controls
    $timeline = $session.GetTimelineProperties()
    $sampledAtUtc = [DateTimeOffset]::UtcNow

    $mediaProperties = $null
    $mediaPropertiesError = $null
    try {
        $mediaProperties = Wait-WinRtOperation `
            -Operation ($session.TryGetMediaPropertiesAsync()) `
            -ResultType $script:MediaPropertiesType
    }
    catch {
        $mediaPropertiesError = $_.Exception.Message
    }

    $currentSession = $manager.GetCurrentSession()
    $isCurrentSession = $false
    if ($null -ne $currentSession) {
        $isCurrentSession = `
            $session.SourceAppUserModelId -eq $currentSession.SourceAppUserModelId
    }

    $genres = @()
    $thumbnailPresent = $false
    if ($null -ne $mediaProperties) {
        $thumbnailPresent = $null -ne $mediaProperties.Thumbnail
        if ($null -ne $mediaProperties.Genres) {
            foreach ($genre in $mediaProperties.Genres) {
                $genres += [string]$genre
            }
        }
    }

    $startSeconds = $timeline.StartTime.TotalSeconds
    $endSeconds = $timeline.EndTime.TotalSeconds
    $positionSeconds = $timeline.Position.TotalSeconds
    $relativePositionSeconds = $positionSeconds - $startSeconds
    $durationSeconds = $endSeconds - $startSeconds
    $lastUpdatedTime = [DateTimeOffset]$timeline.LastUpdatedTime
    $timelineAgeSeconds = $null
    $extrapolatedPositionSeconds = $relativePositionSeconds
    $playbackStatus = [string]$playbackInfo.PlaybackStatus
    $playbackRate = ConvertTo-DoubleOrNull $playbackInfo.PlaybackRate

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
                $extrapolatedPositionSeconds = [Math]::Min(
                    $extrapolatedPositionSeconds,
                    $durationSeconds)
            }
            $extrapolatedPositionSeconds = [Math]::Max(
                0,
                $extrapolatedPositionSeconds)
        }
    }

    $progressPercent = $null
    if ($durationSeconds -gt 0) {
        $progressPercent = [Math]::Round(
            100.0 * $extrapolatedPositionSeconds / $durationSeconds,
            2)
    }

    $sample = [pscustomobject][ordered]@{
        Sample = $sampleIndex
        SampledAtUtc = $sampledAtUtc.ToString('o')
        SourceAppUserModelId = [string]$session.SourceAppUserModelId
        IsCurrentSession = $isCurrentSession
        Title = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.Title } else { $null }
        Artist = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.Artist } else { $null }
        AlbumArtist = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.AlbumArtist } else { $null }
        AlbumTitle = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.AlbumTitle } else { $null }
        Subtitle = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.Subtitle } else { $null }
        TrackNumber = if ($null -ne $mediaProperties) { $mediaProperties.TrackNumber } else { $null }
        AlbumTrackCount = if ($null -ne $mediaProperties) { $mediaProperties.AlbumTrackCount } else { $null }
        Genres = $genres
        MediaPlaybackType = if ($null -ne $mediaProperties) { ConvertTo-TextOrNull $mediaProperties.PlaybackType } else { $null }
        ThumbnailPresent = $thumbnailPresent
        MediaPropertiesError = $mediaPropertiesError
        PlaybackStatus = $playbackStatus
        SessionPlaybackType = ConvertTo-TextOrNull $playbackInfo.PlaybackType
        PlaybackRate = $playbackRate
        IsShuffleActive = ConvertTo-TextOrNull $playbackInfo.IsShuffleActive
        AutoRepeatMode = ConvertTo-TextOrNull $playbackInfo.AutoRepeatMode
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
            LastUpdatedTimeUtc = $lastUpdatedTime.ToString('o')
            LastUpdatedAgeSeconds = $timelineAgeSeconds
            ProgressPercent = $progressPercent
        }
    }

    $sampleRows += $sample

    if ($sampleIndex -lt $Samples) {
        Start-Sleep -Milliseconds $IntervalMilliseconds
    }
}

$firstSample = $sampleRows[0]
$lastSample = $sampleRows[$sampleRows.Count - 1]
$sameTitleRun = $firstSample.Title -eq $lastSample.Title
$rawPositionDeltaSeconds = $null
$lastUpdatedDeltaSeconds = $null
$wallClockDeltaSeconds = $null
if ($sameTitleRun) {
    $rawPositionDeltaSeconds = `
        $lastSample.Timeline.RelativePositionSeconds -
        $firstSample.Timeline.RelativePositionSeconds
    $firstUpdated = [DateTimeOffset]::Parse($firstSample.Timeline.LastUpdatedTimeUtc)
    $lastUpdated = [DateTimeOffset]::Parse($lastSample.Timeline.LastUpdatedTimeUtc)
    $lastUpdatedDeltaSeconds = ($lastUpdated - $firstUpdated).TotalSeconds
    $firstSampled = [DateTimeOffset]::Parse($firstSample.SampledAtUtc)
    $lastSampled = [DateTimeOffset]::Parse($lastSample.SampledAtUtc)
    $wallClockDeltaSeconds = ($lastSampled - $firstSampled).TotalSeconds
}

$timelinePublished = $false
foreach ($sample in $sampleRows) {
    $updated = [DateTimeOffset]::Parse($sample.Timeline.LastUpdatedTimeUtc)
    if ($sample.Timeline.DurationSeconds -gt 0 -and $updated.Year -gt 1601) {
        $timelinePublished = $true
        break
    }
}

$hints = @()
if (-not $timelinePublished) {
    $hints += 'timeline-not-published: EndTime/StartTime do not form a positive duration, or LastUpdatedTime is the 1601 sentinel.'
}
elseif ($lastSample.PlaybackStatus -eq 'Playing' -and
        $null -ne $rawPositionDeltaSeconds -and $rawPositionDeltaSeconds -gt 0.5) {
    $hints += 'timeline-published-and-advancing: if Quick Settings shows no seek bar, separate app publishing from shell rendering.'
}
elseif ($lastSample.PlaybackStatus -eq 'Playing' -and
        $null -ne $lastUpdatedDeltaSeconds -and $lastUpdatedDeltaSeconds -gt 0.5 -and
        $null -ne $rawPositionDeltaSeconds -and [Math]::Abs($rawPositionDeltaSeconds) -le 0.5) {
    $hints += 'timeline-timestamp-advances-but-position-is-frozen: inspect the app position source.'
}
elseif ($lastSample.PlaybackStatus -eq 'Playing') {
    $hints += 'timeline-may-be-stale: compare LastUpdatedTime age, position, and the in-app clock.'
}
else {
    $hints += 'not-playing-at-final-sample: a frozen raw position can be correct while paused.'
}

$summary = [pscustomobject][ordered]@{
    SourceFilter = $SourceFilter
    SourceAppUserModelId = $lastSample.SourceAppUserModelId
    Samples = $sampleRows.Count
    SameTitleForFirstAndLastSample = $sameTitleRun
    TimelinePublished = $timelinePublished
    WallClockDeltaSeconds = $wallClockDeltaSeconds
    RawPositionDeltaSeconds = $rawPositionDeltaSeconds
    LastUpdatedDeltaSeconds = $lastUpdatedDeltaSeconds
    FinalPlaybackStatus = $lastSample.PlaybackStatus
    FinalTitle = $lastSample.Title
    FinalDurationSeconds = $lastSample.Timeline.DurationSeconds
    FinalPositionSeconds = $lastSample.Timeline.RelativePositionSeconds
    FinalExtrapolatedPositionSeconds = $lastSample.Timeline.ExtrapolatedPositionSeconds
    IsPlaybackPositionEnabled = $lastSample.Controls.IsPlaybackPositionEnabled
    IsFastForwardEnabled = $lastSample.Controls.IsFastForwardEnabled
    IsRewindEnabled = $lastSample.Controls.IsRewindEnabled
    DiagnosticHints = $hints
}

$result = [pscustomobject][ordered]@{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Summary = $summary
    Samples = $sampleRows
}

if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
    $jsonDirectory = Split-Path -Parent $JsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDirectory)) {
        [void](New-Item -ItemType Directory -Force -Path $jsonDirectory)
    }

    $result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $JsonPath -Encoding UTF8
}

Write-Host 'GSMTC session samples (read-only diagnostic; not an acceptance PASS):'
$displayProperties = @(
    'Sample',
    'PlaybackStatus',
    'Title',
    @{ Name = 'PositionSec'; Expression = { [Math]::Round($_.Timeline.RelativePositionSeconds, 3) } },
    @{ Name = 'DurationSec'; Expression = { [Math]::Round($_.Timeline.DurationSeconds, 3) } },
    @{ Name = 'UpdatedAgeSec'; Expression = { $_.Timeline.LastUpdatedAgeSeconds } },
    @{ Name = 'SeekEnabled'; Expression = { $_.Controls.IsPlaybackPositionEnabled } },
    @{ Name = 'FF'; Expression = { $_.Controls.IsFastForwardEnabled } },
    @{ Name = 'RW'; Expression = { $_.Controls.IsRewindEnabled } }
)
$displaySamples = $sampleRows | Select-Object -Property $displayProperties
$displaySamples | Format-Table -AutoSize | Out-String | Write-Host

Write-Host 'Summary:'
$summary | Format-List | Out-String | Write-Host

if ($PassThru) {
    return $result
}
