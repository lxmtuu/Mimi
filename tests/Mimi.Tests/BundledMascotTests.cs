using System.Collections;
using System.Resources;

namespace Mimi.Tests;

/// <summary>
/// The original Mimi mascot ships inside the application assembly as a WPF
/// resource; if the project include or pack URI drifts, the hero and
/// Settings images would silently render empty. Read the bundled bytes the
/// same way the font tests do and verify a real PNG is present.
/// </summary>
public sealed class BundledMascotTests
{
    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void MascotV1IsBundledAsAnApplicationResource()
    {
        var assembly = typeof(global::Mimi.App).Assembly;
        var resourceStreamName = Assert.Single(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(
                ".g.resources",
                StringComparison.OrdinalIgnoreCase));
        using var resourceStream =
            assembly.GetManifestResourceStream(resourceStreamName);
        Assert.NotNull(resourceStream);

        byte[]? mascot = null;
        using (var reader = new ResourceReader(resourceStream!))
        {
            foreach (DictionaryEntry entry in reader)
            {
                var key = Assert.IsType<string>(entry.Key);
                if (!key.Equals(
                        "assets/mascot/mimi-mascot-v1.png",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mascot = entry.Value switch
                {
                    byte[] bytes => bytes,
                    Stream stream => ReadAllBytes(stream),
                    var value => throw new Xunit.Sdk.XunitException(
                        $"Unexpected resource type for {key}: " +
                        $"{value?.GetType().FullName ?? "<null>"}")
                };
            }
        }

        Assert.NotNull(mascot);
        Assert.True(
            mascot!.Length > 10_000,
            "Bundled mascot looks truncated.");
        Assert.Equal(
            PngSignature,
            mascot.Take(PngSignature.Length).ToArray());
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
