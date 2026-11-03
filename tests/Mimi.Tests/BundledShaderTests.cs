using System.Collections;
using System.Resources;

namespace Mimi.Tests;

/// <summary>
/// The Aurora prototype loads compiled HLSL from the application assembly.
/// Verify the bytecode is really bundled at the pack-URI path used by
/// AuroraGlowEffect and still carries the pixel-shader 3.0 version token.
/// </summary>
public sealed class BundledShaderTests
{
    private const uint PixelShader30VersionToken = 0xFFFF0300;

    [Fact]
    public void AuroraGlowShaderIsBundledAsAnApplicationResource()
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

        byte[]? shader = null;
        using (var reader = new ResourceReader(resourceStream!))
        {
            foreach (DictionaryEntry entry in reader)
            {
                var key = Assert.IsType<string>(entry.Key);
                if (!key.Equals(
                        "effects/auroragloweffect.ps",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shader = entry.Value switch
                {
                    byte[] bytes => bytes,
                    Stream stream => ReadAllBytes(stream),
                    var value => throw new Xunit.Sdk.XunitException(
                        $"Unexpected resource type for {key}: " +
                        $"{value?.GetType().FullName ?? "<null>"}")
                };
            }
        }

        Assert.NotNull(shader);
        Assert.True(
            shader!.Length > 512,
            "Bundled Aurora shader looks truncated.");
        Assert.Equal(0, shader.Length % sizeof(uint));
        Assert.Equal(
            PixelShader30VersionToken,
            BitConverter.ToUInt32(shader, 0));
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
