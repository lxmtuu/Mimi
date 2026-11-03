namespace Mimi.Services;

/// <summary>
/// Naming and size conventions for cached cover-art variants. The large
/// variant is the canonical <c>Track.CoverArtPath</c>; callers that need a
/// list thumbnail derive the matching small variant from that path.
/// </summary>
public static class CoverArtPaths
{
    public const int ThumbnailSize = 96;

    public const int LargeSize = 512;

    public const string ThumbnailSuffix = "-96.jpg";

    public const string LargeSuffix = "-512.jpg";

    public static bool IsLargeVariant(string? coverArtPath) =>
        !string.IsNullOrWhiteSpace(coverArtPath) &&
        coverArtPath.EndsWith(
            LargeSuffix,
            StringComparison.OrdinalIgnoreCase);

    public static string? GetThumbnailPath(string? coverArtPath)
    {
        if (string.IsNullOrWhiteSpace(coverArtPath)) return null;

        return IsLargeVariant(coverArtPath)
            ? coverArtPath[..^LargeSuffix.Length] + ThumbnailSuffix
            : coverArtPath;
    }

    public static string GetLargePath(
        string cacheDirectory,
        string contentHash) =>
        Path.Combine(cacheDirectory, contentHash + LargeSuffix);

    public static string GetThumbnailPath(
        string cacheDirectory,
        string contentHash) =>
        Path.Combine(cacheDirectory, contentHash + ThumbnailSuffix);
}
