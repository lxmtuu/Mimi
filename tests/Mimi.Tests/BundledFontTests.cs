using System.Collections;
using System.Resources;
using System.Threading;
using System.Windows.Media;

namespace Mimi.Tests;

public sealed class BundledFontTests
{
    private static readonly (string FileName, int Weight)[] ExpectedFaces =
    [
        ("BeVietnamPro-Regular.ttf", 400),
        ("BeVietnamPro-Medium.ttf", 500),
        ("BeVietnamPro-SemiBold.ttf", 600),
        ("BeVietnamPro-Bold.ttf", 700),
        ("BeVietnamPro-ExtraBold.ttf", 800)
    ];

    private static readonly int[] VietnameseCodePoints =
    [
        0x0102, // Ă
        0x0111, // đ
        0x01A0, // Ơ
        0x01AF, // Ư
        0x1EC7, // ệ
        0x1ED9, // ộ
        0x1EE3, // ợ
        0x1EEF, // ữ
        0x0300, // combining grave
        0x0301, // combining acute
        0x0302, // combining circumflex
        0x0303, // combining tilde
        0x0309, // combining hook above
        0x031B, // combining horn
        0x0323  // combining dot below
    ];

    [Fact]
    public void BundledBeVietnamProFacesLoadWithExpectedWeightsAndGlyphs()
    {
        RunOnSta(() =>
        {
            var fontResources = LoadBundledFonts();
            Assert.Equal(
                ExpectedFaces
                    .Select(face => face.FileName)
                    .OrderBy(name => name, StringComparer.Ordinal),
                fontResources.Keys.OrderBy(
                    name => name,
                    StringComparer.Ordinal));

            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                $"mimi-font-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                foreach (var (fileName, bytes) in fontResources)
                    File.WriteAllBytes(
                        Path.Combine(tempDirectory, fileName),
                        bytes);

                var family = new FontFamily(
                    new Uri(
                        Path.GetFullPath(tempDirectory) +
                        Path.DirectorySeparatorChar),
                    "./#Be Vietnam Pro");
                var actualWeights = family.GetTypefaces()
                    .Select(typeface => typeface.Weight.ToOpenTypeWeight())
                    .Distinct()
                    .OrderBy(weight => weight)
                    .ToArray();
                Assert.Equal(
                    ExpectedFaces.Select(face => face.Weight).ToArray(),
                    actualWeights);

                foreach (var (fileName, expectedWeight) in ExpectedFaces)
                {
                    var glyphTypeface = new GlyphTypeface(
                        new Uri(Path.Combine(tempDirectory, fileName)));

                    Assert.Contains(
                        "Be Vietnam Pro",
                        glyphTypeface.FamilyNames.Values);
                    Assert.Equal(
                        expectedWeight,
                        glyphTypeface.Weight.ToOpenTypeWeight());

                    foreach (var codePoint in VietnameseCodePoints)
                    {
                        Assert.True(
                            glyphTypeface.CharacterToGlyphMap.TryGetValue(
                                codePoint,
                                out var glyphIndex),
                            $"{fileName} has no encoded glyph for U+{codePoint:X4}.");
                        Assert.NotEqual((ushort)0, glyphIndex);
                    }
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(
                        tempDirectory,
                        recursive: true);
                }
                catch (IOException)
                {
                    // WPF may retain a memory-mapped font handle briefly.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best-effort cleanup only; the OS temp cleaner owns it.
                }
            }
        });
    }

    private static IReadOnlyDictionary<string, byte[]> LoadBundledFonts()
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

        var result = new Dictionary<string, byte[]>(
            StringComparer.OrdinalIgnoreCase);
        using var reader = new ResourceReader(resourceStream!);
        foreach (DictionaryEntry entry in reader)
        {
            var key = Assert.IsType<string>(entry.Key);
            var fileName = ExpectedFaces
                .Select(face => face.FileName)
                .SingleOrDefault(candidate => key.Equals(
                    $"assets/fonts/{candidate.ToLowerInvariant()}",
                    StringComparison.OrdinalIgnoreCase));
            if (fileName is null) continue;

            result.Add(fileName, entry.Value switch
            {
                byte[] bytes => bytes,
                Stream stream => ReadAllBytes(stream),
                var value => throw new Xunit.Sdk.XunitException(
                    $"Unexpected resource type for {key}: {value?.GetType().FullName ?? "<null>"}")
            });
        }

        return result;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException(
                $"STA/WPF test failed: {failure}");
    }
}
