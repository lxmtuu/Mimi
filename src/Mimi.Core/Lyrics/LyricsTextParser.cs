using System.Globalization;

namespace Mimi.Lyrics;

/// <summary>
/// Small, dependency-free parser for standard line-timed LRC text and for
/// plain unsynchronized lyrics embedded in audio tags. Enhanced LRC word
/// timestamps are stripped for this line-level display slice.
/// </summary>
public static class LyricsTextParser
{
    private const int MaximumLines = 10000;
    private const long MaximumDocumentOffsetMilliseconds = 10 * 60 * 1000;

    public static LyricsDocument Parse(
        string text,
        LyricsSourceKind source)
    {
        ArgumentNullException.ThrowIfNull(text);

        var timedLines = new List<LyricLine>();
        var plainLines = new List<LyricLine>();
        var documentOffset = TimeSpan.Zero;

        using var reader = new StringReader(text.TrimStart('\uFEFF'));
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            if (timedLines.Count >= MaximumLines &&
                plainLines.Count >= MaximumLines)
            {
                break;
            }

            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var timestamps = new List<TimeSpan>();
            var remainder = line;
            var isMetadataLine = false;

            while (remainder.StartsWith('['))
            {
                var closeIndex = remainder.IndexOf(']');
                if (closeIndex <= 1) break;

                var tagContent = remainder[1..closeIndex].Trim();
                if (TryParseTimestamp(tagContent, out var timestamp))
                {
                    timestamps.Add(timestamp);
                    remainder = remainder[(closeIndex + 1)..].TrimStart();
                    continue;
                }

                if (TryParseOffsetTag(
                        tagContent,
                        out var parsedDocumentOffset))
                {
                    documentOffset = parsedDocumentOffset;
                    isMetadataLine = true;
                    remainder = remainder[(closeIndex + 1)..].TrimStart();
                    continue;
                }

                if (IsMetadataTag(tagContent))
                {
                    isMetadataLine = true;
                    remainder = remainder[(closeIndex + 1)..].TrimStart();
                    continue;
                }

                break;
            }

            if (timestamps.Count > 0)
            {
                var lyricText = StripInlineTimestamps(remainder.Trim());
                foreach (var timestamp in timestamps)
                {
                    if (timedLines.Count >= MaximumLines) break;
                    timedLines.Add(new LyricLine(timestamp, lyricText));
                }

                continue;
            }

            if (isMetadataLine) continue;

            var plainText = StripInlineTimestamps(line);
            if (plainText.Length > 0 && plainLines.Count < MaximumLines)
                plainLines.Add(new LyricLine(null, plainText));
        }

        if (timedLines.Count > 0)
        {
            // Enumerable.OrderBy is stable, so repeated timestamps retain
            // the order in which the source file presented them.
            var ordered = timedLines
                .OrderBy(line => line.Timestamp!.Value)
                .ToArray();
            return new LyricsDocument(source, ordered, documentOffset);
        }

        return new LyricsDocument(source, plainLines, documentOffset);
    }

    private static bool TryParseOffsetTag(
        string tagContent,
        out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        var separatorIndex = tagContent.IndexOf(':');
        if (separatorIndex <= 0) return false;

        var name = tagContent[..separatorIndex].Trim();
        if (!string.Equals(name, "offset", StringComparison.OrdinalIgnoreCase))
            return false;

        var valueText = tagContent[(separatorIndex + 1)..].Trim();
        if (!long.TryParse(
                valueText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var milliseconds))
        {
            return true;
        }

        milliseconds = Math.Clamp(
            milliseconds,
            -MaximumDocumentOffsetMilliseconds,
            MaximumDocumentOffsetMilliseconds);
        offset = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static bool IsMetadataTag(string tagContent)
    {
        var separatorIndex = tagContent.IndexOf(':');
        if (separatorIndex <= 0) return false;

        for (var i = 0; i < separatorIndex; i++)
        {
            var c = tagContent[i];
            if (!char.IsAsciiLetter(c) && c != '#' && c != '_')
                return false;
        }

        return true;
    }

    private static bool TryParseTimestamp(
        string value,
        out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        var parts = value.Trim().Split(':');
        if (parts.Length is not (2 or 3)) return false;

        var secondPart = parts[^1].Trim();
        var fractionSeparator = secondPart.IndexOfAny(['.', ',']);
        var secondText = fractionSeparator >= 0
            ? secondPart[..fractionSeparator]
            : secondPart;
        var fractionText = fractionSeparator >= 0
            ? secondPart[(fractionSeparator + 1)..]
            : string.Empty;

        if (!TryParseNonNegativeInt(parts[0], out var first) ||
            !TryParseNonNegativeInt(secondText, out var seconds) ||
            seconds > 59)
        {
            return false;
        }

        long hours = 0;
        long minutes = first;
        if (parts.Length == 3)
        {
            hours = first;
            if (!TryParseNonNegativeInt(parts[1], out var parsedMinutes) ||
                parsedMinutes > 59)
            {
                return false;
            }

            minutes = parsedMinutes;
        }

        if (!TryParseFractionMilliseconds(
                fractionText,
                out var fractionMilliseconds))
        {
            return false;
        }

        try
        {
            var totalMilliseconds = checked(
                (((hours * 60 + minutes) * 60 + seconds) * 1000) +
                fractionMilliseconds);
            timestamp = TimeSpan.FromMilliseconds(totalMilliseconds);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryParseFractionMilliseconds(
        string fractionText,
        out long milliseconds)
    {
        milliseconds = 0;
        if (fractionText.Length == 0) return true;
        if (fractionText.Any(c => !char.IsAsciiDigit(c))) return false;

        var millisecondText = fractionText.Length <= 3
            ? fractionText.PadRight(3, '0')
            : fractionText[..3];
        if (!long.TryParse(
                millisecondText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out milliseconds))
        {
            return false;
        }

        if (fractionText.Length > 3 && fractionText[3] >= '5')
            milliseconds++;

        return true;
    }

    private static bool TryParseNonNegativeInt(
        string value,
        out long parsed) =>
        long.TryParse(
            value.Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parsed) &&
        parsed >= 0;

    private static string StripInlineTimestamps(string text)
    {
        if (!text.Contains('<')) return text;

        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length;)
        {
            if (text[i] == '<')
            {
                var closeIndex = text.IndexOf('>', i + 1);
                if (closeIndex > i + 1)
                {
                    var candidate = text[(i + 1)..closeIndex].Trim();
                    if (TryParseTimestamp(candidate, out _))
                    {
                        i = closeIndex + 1;
                        continue;
                    }
                }
            }

            result.Append(text[i]);
            i++;
        }

        return result.ToString().Trim();
    }
}
