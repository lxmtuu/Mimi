using SkiaSharp;

namespace Mimi.Services;

/// <summary>
/// Decodes embedded cover art and encodes the two JPEG variants used by
/// Mimi. Unsupported or excessively large images deliberately report
/// failure so metadata scanning can fall back to caching the original bytes.
/// </summary>
internal static class CoverArtImageProcessor
{
    private const int JpegQuality = 85;
    private const long MaximumPixelCount = 25_000_000;

    public static bool TryCreateVariants(
        byte[] source,
        out byte[] largeVariant,
        out byte[] thumbnailVariant)
    {
        largeVariant = [];
        thumbnailVariant = [];

        if (source.Length == 0) return false;

        try
        {
            if (!HasSupportedDimensions(source)) return false;

            using var original = SKBitmap.Decode(source);
            if (original is null || original.Width <= 0 || original.Height <= 0)
                return false;

            largeVariant = EncodeVariant(
                original,
                CoverArtPaths.LargeSize);
            thumbnailVariant = EncodeVariant(
                original,
                CoverArtPaths.ThumbnailSize);

            if (largeVariant.Length > 0 && thumbnailVariant.Length > 0)
                return true;

            largeVariant = [];
            thumbnailVariant = [];
            return false;
        }
        catch (Exception)
        {
            // Native codec availability and damaged image data should never
            // turn an otherwise playable audio file into a failed scan.
            largeVariant = [];
            thumbnailVariant = [];
            return false;
        }
    }

    public static void WriteAllBytesAtomically(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    internal static (int Width, int Height) CalculateTargetSize(
        int sourceWidth,
        int sourceHeight,
        int maximumDimension)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || maximumDimension <= 0)
            return (0, 0);

        var largestDimension = Math.Max(sourceWidth, sourceHeight);
        if (largestDimension <= maximumDimension)
            return (sourceWidth, sourceHeight);

        var scale = maximumDimension / (double)largestDimension;
        return (
            Math.Max(1, (int)Math.Round(sourceWidth * scale)),
            Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }

    private static bool HasSupportedDimensions(byte[] source)
    {
        using var stream = new MemoryStream(source, writable: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null) return false;

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0) return false;

        return (long)info.Width * info.Height <= MaximumPixelCount;
    }

    private static byte[] EncodeVariant(SKBitmap original, int maximumDimension)
    {
        var (width, height) = CalculateTargetSize(
            original.Width,
            original.Height,
            maximumDimension);
        if (width <= 0 || height <= 0) return [];

        if (width == original.Width && height == original.Height)
            return EncodeJpeg(original);

        var targetInfo = new SKImageInfo(
            width,
            height,
            original.ColorType,
            original.AlphaType);
        using var resized = original.Resize(
            targetInfo,
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        return resized is null ? [] : EncodeJpeg(resized);
    }

    private static byte[] EncodeJpeg(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        if (image is null) return [];

        using var data = image.Encode(
            SKEncodedImageFormat.Jpeg,
            JpegQuality);
        return data?.ToArray() ?? [];
    }
}
