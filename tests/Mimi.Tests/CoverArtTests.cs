using Mimi.Services;
using SkiaSharp;

namespace Mimi.Tests;

public sealed class CoverArtTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiCoverArtTests-" + Guid.NewGuid().ToString("N"));

    public CoverArtTests() => Directory.CreateDirectory(root);

    [Fact]
    public void GetThumbnailPath_ForLargeVariant_ReturnsMatchingThumbnail()
    {
        var thumbnailPath = CoverArtPaths.GetThumbnailPath(
            @"C:\covers\abc123-512.jpg");

        Assert.Equal(@"C:\covers\abc123-96.jpg", thumbnailPath);
        Assert.True(CoverArtPaths.IsLargeVariant(@"C:\covers\abc123-512.jpg"));
        Assert.False(CoverArtPaths.IsLargeVariant(@"C:\covers\abc123.png"));
        Assert.Equal(
            @"C:\covers\abc123.png",
            CoverArtPaths.GetThumbnailPath(@"C:\covers\abc123.png"));
    }

    [Theory]
    [InlineData(1024, 512, 512, 512, 256)]
    [InlineData(300, 900, 96, 32, 96)]
    [InlineData(80, 40, 512, 80, 40)]
    public void CalculateTargetSize_PreservesAspectRatioWithoutUpscaling(
        int sourceWidth,
        int sourceHeight,
        int maximumDimension,
        int expectedWidth,
        int expectedHeight)
    {
        var size = CoverArtImageProcessor.CalculateTargetSize(
            sourceWidth,
            sourceHeight,
            maximumDimension);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }

    [Fact]
    public void TryCreateVariants_ForPng_CreatesLargeAndThumbnailJpegs()
    {
        var source = CreatePng(1024, 512);

        var created = CoverArtImageProcessor.TryCreateVariants(
            source,
            out var largeVariant,
            out var thumbnailVariant);

        Assert.True(created);
        AssertImageSize(largeVariant, 512, 256);
        AssertImageSize(thumbnailVariant, 96, 48);
    }

    [Fact]
    public void TryCreateVariants_ForInvalidBytes_ReturnsFalse()
    {
        var created = CoverArtImageProcessor.TryCreateVariants(
            [1, 2, 3, 4],
            out var largeVariant,
            out var thumbnailVariant);

        Assert.False(created);
        Assert.Empty(largeVariant);
        Assert.Empty(thumbnailVariant);
    }

    [Fact]
    public void CreateResizedVariants_ForLegacyCacheFile_WritesBothVariants()
    {
        var legacyPath = Path.Combine(root, "legacy.png");
        File.WriteAllBytes(legacyPath, CreatePng(640, 320));

        var largePath = new CoverArtOptimizer()
            .CreateResizedVariants(legacyPath);

        Assert.NotNull(largePath);
        Assert.EndsWith(CoverArtPaths.LargeSuffix, largePath);
        Assert.True(File.Exists(largePath));

        var thumbnailPath = CoverArtPaths.GetThumbnailPath(largePath);
        Assert.NotNull(thumbnailPath);
        Assert.True(File.Exists(thumbnailPath));
        AssertImageSize(File.ReadAllBytes(largePath), 512, 256);
        AssertImageSize(File.ReadAllBytes(thumbnailPath), 96, 48);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height));
        bitmap.Erase(SKColors.Cyan);

        using var image = SKImage.FromBitmap(bitmap);
        Assert.NotNull(image);

        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(data);
        return data.ToArray();
    }

    private static void AssertImageSize(
        byte[] encodedImage,
        int expectedWidth,
        int expectedHeight)
    {
        using var bitmap = SKBitmap.Decode(encodedImage);

        Assert.NotNull(bitmap);
        Assert.Equal(expectedWidth, bitmap.Width);
        Assert.Equal(expectedHeight, bitmap.Height);
    }
}
