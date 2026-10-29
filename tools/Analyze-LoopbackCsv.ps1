<#
.SYNOPSIS
    Summarizes Windows loopback-capture CSV files for Mimi audio A/B tests.

.DESCRIPTION
    Detects common column names for timestamp, callback gap, RMS, peak, and
    buffer size. For each input file the script reports signal coverage,
    time to the first above-threshold buffer, callback-gap percentiles,
    large gaps, and silent runs after the first signal buffer.

    These are digital loopback measurements. Time to first signal is not an
    end-to-end acoustic latency measurement from a Play command to a
    loudspeaker. If a capture includes pre-roll before the test trigger,
    pass -TriggerOffsetMilliseconds with the known trigger position on the
    CSV timeline so the report can distinguish first signal from CSV start
    from first signal after that trigger.

.EXAMPLE
    .\tools\Analyze-LoopbackCsv.ps1 -Path '.\wasapi.csv','.\waveout.csv'

.EXAMPLE
    .\tools\Analyze-LoopbackCsv.ps1 -Path '.\wasapi.csv' `
        -TriggerOffsetMilliseconds 2000 `
        -MarkdownPath '.\artifacts\acceptance\loopback-summary.md' `
        -JsonPath '.\artifacts\acceptance\loopback-summary.json'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,

    [double]$SignalThreshold = 0.001,

    [double]$LargeGapMilliseconds = 100,

    [int]$TopGapCount = 10,

    [double]$TriggerOffsetMilliseconds = 0,

    [ValidateSet('Auto', 'Seconds', 'Milliseconds', 'Microseconds', 'Minutes', 'Ticks')]
    [string]$TimestampUnit = 'Auto',

    [ValidateSet('Auto', 'Seconds', 'Milliseconds', 'Microseconds', 'Minutes', 'Ticks')]
    [string]$GapUnit = 'Auto',

    [char]$Delimiter = ',',

    [string]$MarkdownPath,

    [string]$JsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($TriggerOffsetMilliseconds -lt 0) {
    throw 'TriggerOffsetMilliseconds cannot be negative.'
}

function Normalize-ColumnName {
    param([string]$Name)

    if ($null -eq $Name) {
        return ''
    }

    return ([regex]::Replace($Name.ToLowerInvariant(), '[^a-z0-9]', ''))
}

function Find-Column {
    param(
        [object]$FirstRow,
        [string[]]$Aliases
    )

    $columnsByNormalizedName = @{}
    foreach ($property in $FirstRow.PSObject.Properties) {
        $normalized = Normalize-ColumnName $property.Name
        if (-not $columnsByNormalizedName.ContainsKey($normalized)) {
            $columnsByNormalizedName[$normalized] = $property.Name
        }
    }

    foreach ($alias in $Aliases) {
        $normalizedAlias = Normalize-ColumnName $alias
        if ($columnsByNormalizedName.ContainsKey($normalizedAlias)) {
            return $columnsByNormalizedName[$normalizedAlias]
        }
    }

    return $null
}

function Get-CellValue {
    param(
        [object]$Row,
        [string]$ColumnName
    )

    if ([string]::IsNullOrWhiteSpace($ColumnName)) {
        return $null
    }

    $property = $Row.PSObject.Properties[$ColumnName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function ConvertTo-DoubleOrNull {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [byte] -or $Value -is [int] -or $Value -is [long] -or
        $Value -is [float] -or $Value -is [double] -or $Value -is [decimal]) {
        return [double]$Value
    }

    $text = ([string]$Value).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $parsed = 0.0
    $styles = [System.Globalization.NumberStyles]::Float
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    if ([double]::TryParse($text, $styles, $culture, [ref]$parsed)) {
        return $parsed
    }

    # Tolerate a decimal comma when the file does not also use a decimal dot.
    if ($text.Contains(',') -and -not $text.Contains('.')) {
        $normalized = $text.Replace(',', '.')
        if ([double]::TryParse($normalized, $styles, $culture, [ref]$parsed)) {
            return $parsed
        }
    }

    return $null
}

function Get-Percentile {
    param(
        [object[]]$Values,
        [double]$Percentile
    )

    $numbers = @()
    foreach ($value in $Values) {
        if ($null -ne $value) {
            $numbers += [double]$value
        }
    }

    if ($numbers.Count -eq 0) {
        return $null
    }

    $sorted = @($numbers | Sort-Object)
    if ($sorted.Count -eq 1) {
        return [double]$sorted[0]
    }

    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $lowerIndex = [int][Math]::Floor($rank)
    $upperIndex = [int][Math]::Ceiling($rank)
    if ($lowerIndex -eq $upperIndex) {
        return [double]$sorted[$lowerIndex]
    }

    $fraction = $rank - $lowerIndex
    $lowerValue = [double]$sorted[$lowerIndex]
    $upperValue = [double]$sorted[$upperIndex]
    return $lowerValue + (($upperValue - $lowerValue) * $fraction)
}

function Get-Median {
    param([object[]]$Values)

    return Get-Percentile -Values $Values -Percentile 50
}

function Get-TimeConversion {
    param(
        [object[]]$Values,
        [string]$RequestedUnit,
        [switch]$ValuesAreDurations
    )

    $factor = 1.0
    $unitUsed = $RequestedUnit

    if ($RequestedUnit -ne 'Auto') {
        switch ($RequestedUnit) {
            'Seconds' { $factor = 1000.0 }
            'Milliseconds' { $factor = 1.0 }
            'Microseconds' { $factor = 0.001 }
            'Minutes' { $factor = 60000.0 }
            'Ticks' { $factor = 0.0001 }
        }

        return [pscustomobject]@{
            Factor = $factor
            Unit = $unitUsed
        }
    }

    $positiveDeltas = @()
    if ($ValuesAreDurations) {
        # A gap column already contains durations. Infer its unit from the
        # magnitudes themselves, never from differences between successive
        # gap values.
        foreach ($value in $Values) {
            if ($null -ne $value -and [double]$value -gt 0) {
                $positiveDeltas += [double]$value
            }
        }
    }
    else {
        for ($i = 1; $i -lt $Values.Count; $i++) {
            if ($null -ne $Values[$i] -and $null -ne $Values[$i - 1]) {
                $delta = [double]$Values[$i] - [double]$Values[$i - 1]
                if ($delta -gt 0) {
                    $positiveDeltas += $delta
                }
            }
        }
    }

    $median = Get-Median $positiveDeltas
    if ($null -eq $median -or $median -le 0) {
        $unitUsed = 'Milliseconds (assumed)'
        $factor = 1.0
    }
    elseif ($median -lt 2) {
        $unitUsed = 'Seconds (inferred)'
        $factor = 1000.0
    }
    elseif ($median -lt 10000) {
        $unitUsed = 'Milliseconds (inferred)'
        $factor = 1.0
    }
    elseif ($median -lt 200000) {
        $unitUsed = 'Microseconds (inferred)'
        $factor = 0.001
    }
    else {
        $unitUsed = 'Ticks (inferred)'
        $factor = 0.0001
    }

    return [pscustomobject]@{
        Factor = $factor
        Unit = $unitUsed
    }
}

function Format-NullableNumber {
    param(
        [object]$Value,
        [int]$DecimalPlaces = 2
    )

    if ($null -eq $Value) {
        return 'n/a'
    }

    $format = 'F' + $DecimalPlaces
    return ([double]$Value).ToString(
        $format,
        [System.Globalization.CultureInfo]::InvariantCulture)
}

function Escape-MarkdownCell {
    param([object]$Value)

    if ($null -eq $Value) {
        return 'n/a'
    }

    return ([string]$Value).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

$results = New-Object System.Collections.ArrayList

foreach ($inputPath in $Path) {
    $resolvedPaths = @(Resolve-Path -LiteralPath $inputPath)

    foreach ($resolvedPath in $resolvedPaths) {
        $filePath = $resolvedPath.Path
        $rows = @(Import-Csv -LiteralPath $filePath -Delimiter $Delimiter)
        if ($rows.Count -eq 0) {
            throw "CSV file contains no data rows: $filePath"
        }

        $firstRow = $rows[0]
        $availableColumns = @($firstRow.PSObject.Properties | ForEach-Object { $_.Name })

        $timestampColumn = Find-Column $firstRow @(
            'timestamp', 'timestampms', 'time', 'timems', 'elapsed',
            'elapsedtime', 'elapsedms', 'elapsedmilliseconds',
            'callbacktime', 'callbacktimestamp', 'buffertime',
            'sampletime', 't'
        )
        $gapColumn = Find-Column $firstRow @(
            'gap', 'gapms', 'gapmilliseconds', 'callbackgap',
            'callbackgapms', 'callbackinterval', 'callbackintervalms',
            'interval', 'intervalms', 'intervalmilliseconds', 'delta',
            'deltatime', 'deltatimems', 'timedelta', 'timedeltams',
            'sinceprevious', 'sincepreviousms'
        )
        $rmsColumn = Find-Column $firstRow @(
            'rms', 'rootmeansquare', 'bufferrms', 'averagerms',
            'rmslevel', 'levelrms', 'signalrms'
        )
        $peakColumn = Find-Column $firstRow @(
            'peak', 'peakamplitude', 'peaklevel', 'maxamplitude',
            'maximumamplitude', 'maxsample', 'maxabs', 'maxabsolute'
        )
        $sizeColumn = Find-Column $firstRow @(
            'frames', 'framecount', 'framesreceived', 'sampleframes',
            'samples', 'samplecount', 'recordedframes', 'bytes',
            'bytecount', 'bytesreceived', 'bytesrecorded',
            'recordedbytes', 'buffersize', 'buffersizebytes',
            'bufferbytes', 'lengthbytes'
        )

        if ($null -eq $rmsColumn -and $null -eq $peakColumn) {
            throw (
                "No recognized RMS or peak column in $filePath. " +
                "Available columns: $($availableColumns -join ', ')"
            )
        }

        $signalColumn = $rmsColumn
        $signalMetricName = 'RMS'
        if ($null -eq $signalColumn) {
            $signalColumn = $peakColumn
            $signalMetricName = 'Peak'
        }

        $timestampKinds = New-Object System.Collections.ArrayList
        $timestampRawValues = New-Object System.Collections.ArrayList
        $gapRawValues = New-Object System.Collections.ArrayList
        $records = New-Object System.Collections.ArrayList

        for ($i = 0; $i -lt $rows.Count; $i++) {
            $row = $rows[$i]
            $timestampKind = $null
            $timestampRaw = $null

            if ($null -ne $timestampColumn) {
                $timestampCell = Get-CellValue $row $timestampColumn
                $timestampNumber = ConvertTo-DoubleOrNull $timestampCell
                if ($null -ne $timestampNumber) {
                    $timestampKind = 'Number'
                    $timestampRaw = $timestampNumber
                }
                elseif ($null -ne $timestampCell) {
                    $timestampText = ([string]$timestampCell).Trim()
                    $timeSpan = [System.TimeSpan]::Zero
                    $dateTime = [System.DateTime]::MinValue
                    if ([System.TimeSpan]::TryParse($timestampText, [ref]$timeSpan)) {
                        $timestampKind = 'TimeSpan'
                        $timestampRaw = $timeSpan
                    }
                    elseif ([System.DateTime]::TryParse(
                            $timestampText,
                            [System.Globalization.CultureInfo]::InvariantCulture,
                            [System.Globalization.DateTimeStyles]::AllowWhiteSpaces,
                            [ref]$dateTime)) {
                        $timestampKind = 'DateTime'
                        $timestampRaw = $dateTime
                    }
                }
            }

            [void]$timestampKinds.Add($timestampKind)
            [void]$timestampRawValues.Add($timestampRaw)

            $gapRaw = $null
            if ($null -ne $gapColumn) {
                $gapRaw = ConvertTo-DoubleOrNull (Get-CellValue $row $gapColumn)
            }
            [void]$gapRawValues.Add($gapRaw)

            $rms = $null
            if ($null -ne $rmsColumn) {
                $rms = ConvertTo-DoubleOrNull (Get-CellValue $row $rmsColumn)
            }

            $peak = $null
            if ($null -ne $peakColumn) {
                $peak = ConvertTo-DoubleOrNull (Get-CellValue $row $peakColumn)
            }

            $metric = $rms
            if ($null -eq $metric) {
                $metric = $peak
            }

            $bufferSize = $null
            if ($null -ne $sizeColumn) {
                $bufferSize = ConvertTo-DoubleOrNull (Get-CellValue $row $sizeColumn)
            }

            $hasSignal = $false
            if ($null -ne $metric) {
                $hasSignal = [Math]::Abs([double]$metric) -gt $SignalThreshold
            }

            [void]$records.Add([pscustomobject]@{
                Index = $i
                Row = $i + 1
                TimeMs = $null
                GapMs = $null
                Rms = $rms
                Peak = $peak
                SignalMetric = $metric
                BufferSize = $bufferSize
                HasSignal = $hasSignal
            })
        }

        $timestampUnitUsed = 'n/a'
        $hasTimestamps = $false
        $nonNullTimestampKinds = @(
            $timestampKinds | Where-Object { $null -ne $_ } | Select-Object -Unique
        )

        if ($nonNullTimestampKinds.Count -gt 1) {
            throw "Timestamp column mixes incompatible value kinds in $filePath."
        }

        if ($nonNullTimestampKinds.Count -eq 1) {
            $hasTimestamps = $true
            $kind = $nonNullTimestampKinds[0]

            if ($kind -eq 'DateTime') {
                $firstTimestamp = $null
                foreach ($candidate in $timestampRawValues) {
                    if ($null -ne $candidate) {
                        $firstTimestamp = [System.DateTime]$candidate
                        break
                    }
                }

                for ($i = 0; $i -lt $records.Count; $i++) {
                    if ($null -ne $timestampRawValues[$i] -and $null -ne $firstTimestamp) {
                        $records[$i].TimeMs =
                            ([System.DateTime]$timestampRawValues[$i] - $firstTimestamp).TotalMilliseconds
                    }
                }
                $timestampUnitUsed = 'DateTime'
            }
            elseif ($kind -eq 'TimeSpan') {
                for ($i = 0; $i -lt $records.Count; $i++) {
                    if ($null -ne $timestampRawValues[$i]) {
                        $records[$i].TimeMs =
                            ([System.TimeSpan]$timestampRawValues[$i]).TotalMilliseconds
                    }
                }
                $timestampUnitUsed = 'TimeSpan'
            }
            else {
                $numericTimestamps = @()
                foreach ($candidate in $timestampRawValues) {
                    if ($null -ne $candidate) {
                        $numericTimestamps += [double]$candidate
                    }
                }

                $conversion = Get-TimeConversion $numericTimestamps $TimestampUnit
                $timestampUnitUsed = $conversion.Unit
                $firstTimestampMs = $null

                for ($i = 0; $i -lt $records.Count; $i++) {
                    if ($null -ne $timestampRawValues[$i]) {
                        $absoluteMs = [double]$timestampRawValues[$i] * $conversion.Factor
                        if ($null -eq $firstTimestampMs) {
                            $firstTimestampMs = $absoluteMs
                        }
                        $records[$i].TimeMs = $absoluteMs - $firstTimestampMs
                    }
                }
            }
        }

        $gapUnitUsed = 'n/a'
        if ($null -ne $gapColumn) {
            $numericGaps = @()
            foreach ($candidate in $gapRawValues) {
                if ($null -ne $candidate) {
                    $numericGaps += [double]$candidate
                }
            }

            $gapConversion = Get-TimeConversion $numericGaps $GapUnit -ValuesAreDurations
            $gapUnitUsed = $gapConversion.Unit
            for ($i = 0; $i -lt $records.Count; $i++) {
                if ($null -ne $gapRawValues[$i]) {
                    $records[$i].GapMs = [double]$gapRawValues[$i] * $gapConversion.Factor
                }
            }
        }
        elseif ($hasTimestamps) {
            $gapUnitUsed = 'Derived from timestamps (ms)'
            for ($i = 1; $i -lt $records.Count; $i++) {
                if ($null -ne $records[$i].TimeMs -and
                    $null -ne $records[$i - 1].TimeMs) {
                    $records[$i].GapMs =
                        [double]$records[$i].TimeMs - [double]$records[$i - 1].TimeMs
                }
            }
        }

        $firstSignalIndex = -1
        for ($i = 0; $i -lt $records.Count; $i++) {
            if ($records[$i].HasSignal) {
                $firstSignalIndex = $i
                break
            }
        }

        $overallSignalCount = @($records | Where-Object { $_.HasSignal }).Count
        $overallSignalPercent = $null
        if ($records.Count -gt 0) {
            $overallSignalPercent = 100.0 * $overallSignalCount / $records.Count
        }

        $durationMs = $null
        if ($hasTimestamps) {
            $timeValues = @(
                $records | ForEach-Object { $_.TimeMs } | Where-Object { $null -ne $_ }
            )
            if ($timeValues.Count -gt 0) {
                $durationMs = [double]($timeValues | Measure-Object -Maximum).Maximum
            }
        }

        $postSignalCount = 0
        $postSignalWithSignal = 0
        $postSignalPercent = $null
        $timeToFirstSignalMs = $null
        $timeToFirstSignalAfterTriggerMs = $null
        $gapValues = @()
        $rmsValues = @()
        $silentRuns = New-Object System.Collections.ArrayList
        $topGaps = @()

        if ($firstSignalIndex -ge 0) {
            $postSignalCount = $records.Count - $firstSignalIndex
            $postSignalWithSignal = @(
                $records[$firstSignalIndex..($records.Count - 1)] |
                    Where-Object { $_.HasSignal }
            ).Count
            if ($postSignalCount -gt 0) {
                $postSignalPercent =
                    100.0 * $postSignalWithSignal / $postSignalCount
            }

            if ($hasTimestamps -and $null -ne $records[$firstSignalIndex].TimeMs) {
                $timeToFirstSignalMs = [double]$records[$firstSignalIndex].TimeMs
                $timeToFirstSignalAfterTriggerMs =
                    $timeToFirstSignalMs - $TriggerOffsetMilliseconds

                if ($timeToFirstSignalAfterTriggerMs -lt 0) {
                    Write-Warning (
                        "First above-threshold buffer occurs before the supplied " +
                        "trigger offset in $filePath. Check pre-roll/trigger alignment."
                    )
                }
            }

            $runStart = -1
            for ($i = $firstSignalIndex; $i -le $records.Count; $i++) {
                $atEnd = $i -eq $records.Count
                $isSilent = $false
                if (-not $atEnd) {
                    $isSilent = -not $records[$i].HasSignal
                }

                if ($isSilent -and $runStart -lt 0) {
                    $runStart = $i
                }

                if (($atEnd -or -not $isSilent) -and $runStart -ge 0) {
                    $runEnd = $i - 1
                    $runDuration = $null
                    if ($hasTimestamps -and $null -ne $records[$runStart].TimeMs) {
                        if ($i -lt $records.Count -and $null -ne $records[$i].TimeMs) {
                            $runDuration =
                                [double]$records[$i].TimeMs -
                                [double]$records[$runStart].TimeMs
                        }
                        elseif ($null -ne $records[$runEnd].TimeMs) {
                            $runDuration =
                                [double]$records[$runEnd].TimeMs -
                                [double]$records[$runStart].TimeMs
                        }
                    }

                    [void]$silentRuns.Add([pscustomobject]@{
                        StartRow = $runStart + 1
                        EndRow = $runEnd + 1
                        BufferCount = $runEnd - $runStart + 1
                        StartTimeMs = $records[$runStart].TimeMs
                        ApproxDurationMs = $runDuration
                    })
                    $runStart = -1
                }

                if (-not $atEnd) {
                    if ($null -ne $records[$i].GapMs -and
                        [double]$records[$i].GapMs -ge 0) {
                        $gapValues += [double]$records[$i].GapMs
                    }
                    if ($null -ne $records[$i].Rms) {
                        $rmsValues += [double]$records[$i].Rms
                    }
                }
            }

            $gapRecords = @(
                $records[$firstSignalIndex..($records.Count - 1)] |
                    Where-Object { $null -ne $_.GapMs } |
                    Sort-Object -Property GapMs -Descending |
                    Select-Object -First $TopGapCount
            )

            foreach ($gapRecord in $gapRecords) {
                $topGaps += [pscustomobject]@{
                    Row = $gapRecord.Row
                    TimeMs = $gapRecord.TimeMs
                    GapMs = $gapRecord.GapMs
                    Rms = $gapRecord.Rms
                    Peak = $gapRecord.Peak
                    BufferSize = $gapRecord.BufferSize
                    HasSignal = $gapRecord.HasSignal
                }
            }
        }
        else {
            Write-Warning (
                "No buffer exceeded signal threshold $SignalThreshold in $filePath. " +
                "Check -SignalThreshold and the selected RMS/peak column."
            )
        }

        $gapP50 = Get-Percentile $gapValues 50
        $gapP95 = Get-Percentile $gapValues 95
        $gapP99 = Get-Percentile $gapValues 99
        $gapMax = Get-Percentile $gapValues 100
        $gapsOverThreshold = @(
            $gapValues | Where-Object { [double]$_ -gt $LargeGapMilliseconds }
        ).Count

        $gapsOver2xMedian = $null
        if ($null -ne $gapP50 -and $gapP50 -gt 0) {
            $doubleMedian = 2.0 * [double]$gapP50
            $gapsOver2xMedian = @(
                $gapValues | Where-Object { [double]$_ -gt $doubleMedian }
            ).Count
        }

        $averageRms = $null
        if ($rmsValues.Count -gt 0) {
            $averageRms = ($rmsValues | Measure-Object -Average).Average
        }

        $longestSilentRun = $null
        foreach ($run in $silentRuns) {
            if ($null -eq $longestSilentRun -or
                $run.BufferCount -gt $longestSilentRun.BufferCount) {
                $longestSilentRun = $run
            }
        }

        $topSilentRuns = @(
            $silentRuns |
                Sort-Object -Property BufferCount -Descending |
                Select-Object -First 20
        )

        $firstSignalRow = $null
        if ($firstSignalIndex -ge 0) {
            $firstSignalRow = $firstSignalIndex + 1
        }

        $longestSilentRunBuffers = $null
        $longestSilentRunMs = $null
        if ($null -ne $longestSilentRun) {
            $longestSilentRunBuffers = $longestSilentRun.BufferCount
            $longestSilentRunMs = $longestSilentRun.ApproxDurationMs
        }

        $summary = [pscustomobject]@{
            File = [System.IO.Path]::GetFileName($filePath)
            FullPath = $filePath
            Rows = $records.Count
            DurationMs = $durationMs
            TimestampColumn = $timestampColumn
            TimestampUnitUsed = $timestampUnitUsed
            GapColumn = $gapColumn
            GapUnitUsed = $gapUnitUsed
            SignalColumn = $signalColumn
            SignalMetric = $signalMetricName
            SignalThreshold = $SignalThreshold
            FirstSignalRow = $firstSignalRow
            TriggerOffsetMs = $TriggerOffsetMilliseconds
            TimeToFirstSignalMs = $timeToFirstSignalMs
            TimeToFirstSignalFromCsvStartMs = $timeToFirstSignalMs
            TimeToFirstSignalAfterTriggerMs = $timeToFirstSignalAfterTriggerMs
            BuffersFromFirstSignal = $postSignalCount
            SignalBuffersFromFirstSignal = $postSignalWithSignal
            SignalPercentFromFirstSignal = $postSignalPercent
            OverallSignalPercent = $overallSignalPercent
            AverageRmsFromFirstSignal = $averageRms
            GapP50Ms = $gapP50
            GapP95Ms = $gapP95
            GapP99Ms = $gapP99
            GapMaxMs = $gapMax
            GapsOverThreshold = $gapsOverThreshold
            LargeGapThresholdMs = $LargeGapMilliseconds
            GapsOver2xMedian = $gapsOver2xMedian
            SilentRunCountFromFirstSignal = $silentRuns.Count
            LongestSilentRunBuffers = $longestSilentRunBuffers
            LongestSilentRunMs = $longestSilentRunMs
            BufferSizeColumn = $sizeColumn
        }

        [void]$results.Add([pscustomobject]@{
            Summary = $summary
            TopGaps = $topGaps
            LongestSilentRuns = $topSilentRuns
        })
    }
}

foreach ($result in $results) {
    Write-Host ""
    Write-Host "=== $($result.Summary.File) ==="
    $result.Summary | Format-List

    if ($result.TopGaps.Count -gt 0) {
        Write-Host "Largest callback gaps after first signal:"
        $result.TopGaps | Format-Table -AutoSize
    }

    if ($result.LongestSilentRuns.Count -gt 0) {
        Write-Host "Longest silent runs after first signal:"
        $result.LongestSilentRuns | Select-Object -First 5 | Format-Table -AutoSize
    }
}

if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
    $jsonDirectory = Split-Path -Parent $JsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDirectory)) {
        [void](New-Item -ItemType Directory -Path $jsonDirectory -Force)
    }
    $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $JsonPath -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add('# Loopback CSV summary')
    [void]$lines.Add('')
    [void]$lines.Add('Digital loopback only: first signal after the supplied trigger offset is a software-side metric, not end-to-end acoustic latency. The JSON/console summary also retains first signal from CSV start.')
    [void]$lines.Add('')
    [void]$lines.Add('| File | Buffers from first signal | Signal buffers | Signal % | First signal after trigger (ms) | Gap p50/p95/p99/max (ms) | Gaps > threshold | Longest silent run | Average RMS |')
    [void]$lines.Add('|---|---:|---:|---:|---:|---|---:|---|---:|')

    foreach ($result in $results) {
        $summary = $result.Summary
        $gapText = '{0}/{1}/{2}/{3}' -f `
            (Format-NullableNumber $summary.GapP50Ms), `
            (Format-NullableNumber $summary.GapP95Ms), `
            (Format-NullableNumber $summary.GapP99Ms), `
            (Format-NullableNumber $summary.GapMaxMs)
        $silentText = '{0} buffers / {1} ms' -f `
            (Format-NullableNumber $summary.LongestSilentRunBuffers 0), `
            (Format-NullableNumber $summary.LongestSilentRunMs)
        $line = '| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |' -f `
            (Escape-MarkdownCell $summary.File), `
            $summary.BuffersFromFirstSignal, `
            $summary.SignalBuffersFromFirstSignal, `
            (Format-NullableNumber $summary.SignalPercentFromFirstSignal), `
            (Format-NullableNumber $summary.TimeToFirstSignalAfterTriggerMs), `
            $gapText, `
            $summary.GapsOverThreshold, `
            $silentText, `
            (Format-NullableNumber $summary.AverageRmsFromFirstSignal 4)
        [void]$lines.Add($line)
    }

    foreach ($result in $results) {
        [void]$lines.Add('')
        [void]$lines.Add('## ' + $result.Summary.File)
        [void]$lines.Add('')
        [void]$lines.Add('- Signal metric: ' + $result.Summary.SignalMetric + ' (`' + $result.Summary.SignalColumn + '`), threshold ' + $result.Summary.SignalThreshold)
        [void]$lines.Add('- Timestamp: `' + $result.Summary.TimestampColumn + '` (' + $result.Summary.TimestampUnitUsed + ')')
        [void]$lines.Add('- Gap: `' + $result.Summary.GapColumn + '` (' + $result.Summary.GapUnitUsed + ')')
        [void]$lines.Add('- Gaps over 2x median: ' + (Format-NullableNumber $result.Summary.GapsOver2xMedian 0))
        [void]$lines.Add('- Trigger offset: ' + (Format-NullableNumber $result.Summary.TriggerOffsetMs) + ' ms; first signal from CSV start: ' + (Format-NullableNumber $result.Summary.TimeToFirstSignalFromCsvStartMs) + ' ms')

        if ($result.TopGaps.Count -gt 0) {
            [void]$lines.Add('')
            [void]$lines.Add('Largest gaps after first signal:')
            [void]$lines.Add('')
            [void]$lines.Add('| Row | Time (ms) | Gap (ms) | RMS | Peak | Buffer size | Has signal |')
            [void]$lines.Add('|---:|---:|---:|---:|---:|---:|---|')
            foreach ($gap in $result.TopGaps) {
                $line = '| {0} | {1} | {2} | {3} | {4} | {5} | {6} |' -f `
                    $gap.Row, `
                    (Format-NullableNumber $gap.TimeMs), `
                    (Format-NullableNumber $gap.GapMs), `
                    (Format-NullableNumber $gap.Rms 4), `
                    (Format-NullableNumber $gap.Peak 4), `
                    (Format-NullableNumber $gap.BufferSize 0), `
                    $gap.HasSignal
                [void]$lines.Add($line)
            }
        }
    }

    $markdownDirectory = Split-Path -Parent $MarkdownPath
    if (-not [string]::IsNullOrWhiteSpace($markdownDirectory)) {
        [void](New-Item -ItemType Directory -Path $markdownDirectory -Force)
    }
    $lines | Set-Content -LiteralPath $MarkdownPath -Encoding UTF8
}
