using System.Globalization;
using LexiDiff.Tokens;
using Xunit;

namespace LexiDiff.Tests;

public class UnicodeSegmentationTests
{
    [Theory]
    [InlineData("\U0001F642", WordKind.Symbol)]
    [InlineData("\U00020000", WordKind.Word)]
    [InlineData("\U00010400", WordKind.Word)]
    public void SupplementaryCharacters_RemainWhole(string text, WordKind expectedKind)
    {
        // Invariant culture isolates Unicode scalar handling from language rules.
        var tokenizer = new BasicWordSegmenter(CultureInfo.InvariantCulture);
        var token = Assert.Single(tokenizer.Tokenize(text));
        Assert.Equal(text, token.Text);
        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(0, token.Start);
        Assert.Equal(2, token.Length);
    }

    [Fact]
    public void EmojiReplacement_RendersValidHtml()
    {
        var diff = Lexi.Compare("\U0001F642", "\U0001F643");
        Assert.Equal("\U0001F642", diff.ReconstructA());
        Assert.Equal("\U0001F643", diff.ReconstructB());
        Assert.Equal("<del>\U0001F642</del><ins>\U0001F643</ins>",
            System.Net.WebUtility.HtmlDecode(diff.ToInlineHtml()));
    }

    [Fact]
    public void MixedScripts_PreserveOffsetsAndSurrogatePairs()
    {
        var text = "lexicon \U0001F642 \U00020000";
        var tokens = new BasicWordSegmenter().Tokenize(text);
        Assert.Equal(text, string.Concat(tokens.Select(t => t.Text)));
        foreach (var token in tokens)
        {
            Assert.Equal(token.Text, text.Substring(token.Start, token.Length));
            Assert.False(char.IsLowSurrogate(token.Text[0]));
            Assert.False(char.IsHighSurrogate(token.Text[token.Text.Length - 1]));
        }
    }
}
