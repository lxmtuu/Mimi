using System.Security.Cryptography;

namespace Mimi.Services;

/// <summary>
/// Upgrades an original-byte cover cache entry to the 512/96 JPEG variants
/// produced for newly scanned tracks.
/// </summary>
public sealed class CoverArtOptimizer : ICoverArtOptimizer
{
    public string? CreateResizedVariants(string coverArtPath)
    {
        if (string.IsNullOrWhiteSpace(coverArtPath) ||
            !File.Exists(coverArtPath))
        {
            return null;
        }

        var cacheDirectory = Path.GetDirectoryName(coverArtPath);
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return null;

        try
        {
            var bytes = File.ReadAllBytes(coverArtPath);
            if (!CoverArtImageProcessor.TryCreateVariants(
                    bytes,
                    out var largeVariant,
                    out var thumbnailVariant))
            {
                return null;
            }

            if (CoverArtPaths.IsLargeVariant(coverArtPath))
            {
                var thumbnailPath =
                    CoverArtPaths.GetThumbnailPath(coverArtPath)!;
                if (!File.Exists(thumbnailPath))
                {
                    CoverArtImageProcessor.WriteAllBytesAtomically(
                        thumbnailPath,
                        thumbnailVariant);
                }

                return coverArtPath;
            }

            var hash = Convert.ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant();
            var largePath = CoverArtPaths.GetLargePath(cacheDirectory, hash);
            var thumbnailCachePath = CoverArtPaths.GetThumbnailPath(
                cacheDirectory,
                hash);

            if (!File.Exists(largePath) || !File.Exists(thumbnailCachePath))
            {
                CoverArtImageProcessor.WriteAllBytesAtomically(
                    largePath,
                    largeVariant);
                CoverArtImageProcessor.WriteAllBytesAtomically(
                    thumbnailCachePath,
                    thumbnailVariant);
            }

            return largePath;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
