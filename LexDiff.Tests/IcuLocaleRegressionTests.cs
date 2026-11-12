using System.Globalization;
using System.Text;
using LexiDiff.Tokens;
using Xunit;

namespace LexiDiff.Tests;

public class IcuLocaleRegressionTests
{
    [Theory]
    [InlineData("th-TH", "\u0E20\u0E32\u0E29\u0E32\u0E44\u0E17\u0E22")]
    [InlineData("zh-CN", "\u4E2D\u6587\u8BCD\u5178")]
    [InlineData("ja-JP", "\u8F9E\u66F8\u306F\u8A9E\u6E90\u3092\u8A9E\u308B\u3002")]
    public void DictionarySegmentation_PreservesTextAndFindsWords(string locale, string text)
    {
        // These scripts require dictionary/rule segmentation without whitespace.
        var culture = CultureInfo.GetCultureInfo(locale);
        var segmenter = new IcuWordSegmenter(culture);
        var tokens = segmenter.Tokenize(text);
        Assert.Equal(culture, segmenter.Culture);
        Assert.True(tokens.Count(t => t.Kind == WordKind.Word) > 1,
            $"{locale} must use ICU word boundaries within continuous script.");
        Assert.Equal(text, string.Concat(tokens.Select(t => t.Text)));
        foreach (var token in tokens)
            Assert.Equal(token.Text, text.Substring(token.Start, token.Length));
    }

    [Fact]
    public void StringLocaleConstructor_ForwardsNormalizationOption()
    {
        var tokens = new IcuWordSegmenter("fr-FR", normalizeTo: NormalizationForm.FormC)
            .Tokenize("e\u0301tymologie");
        Assert.Equal("\u00E9tymologie", string.Concat(tokens.Select(t => t.Text)));
    }

    [Fact]
    public void SentencePromotion_DoesNotSplitDecimalNumbers()
    {
        // English sentence rules distinguish a decimal point from a sentence end.
        var a = "The lexicon has 3.5 entries. Next entry stays.";
        var b = "The lexicon has 3.6 entries. Next entry stays.";
        var diff = Lexi.Compare(a, b, new LexiOptions {
            PromoteTo = LexGranularity.Sentence,
            SentenceCulture = CultureInfo.GetCultureInfo("en-GB")
        });
        Assert.Equal(a, diff.ReconstructA());
        Assert.Equal(b, diff.ReconstructB());
        Assert.Contains(diff.Spans, s => s.Op == LexiOp.Delete && s.Text == "The lexicon has 3.5 entries. ");
        Assert.Contains(diff.Spans, s => s.Op == LexiOp.Equal && s.Text == "Next entry stays.");
    }
}
