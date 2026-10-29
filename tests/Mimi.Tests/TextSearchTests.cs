using Mimi.Core.Text;

namespace Mimi.Tests;

public sealed class TextSearchTests
{
    [Theory]
    [InlineData("Sơn Tùng", "son tung")]
    [InlineData("Đà Lạt", "da lat")]
    [InlineData("Phở", "pho")]
    [InlineData("  MIMI   Neon  ", "mimi neon")]
    [InlineData("", "")]
    public void Normalize_RemovesCaseDiacriticsAndExtraSpaces(
        string input,
        string expected) =>
        Assert.Equal(expected, TextSearch.Normalize(input));

    [Fact]
    public void Normalize_NullReturnsEmpty() =>
        Assert.Equal(string.Empty, TextSearch.Normalize(null));

    [Fact]
    public void ContainsNormalized_MatchesVietnameseTextWithoutDiacritics()
    {
        var query = TextSearch.Normalize("doi ta");

        Assert.True(TextSearch.ContainsNormalized("Chuyện Đôi Ta", query));
    }

    [Fact]
    public void ContainsNormalized_EmptyQueryMatchesEverything() =>
        Assert.True(TextSearch.ContainsNormalized("Any song", string.Empty));
}
