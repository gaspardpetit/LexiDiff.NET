using LexiDiff.Tokens;
using System.Globalization;
using Xunit;

namespace LexiDiff.Tests;

public class StemmingTokenizerLocaleTests
{
    private sealed class CapturingStemmingTokenizer : StemmingTokenizer
    {
        public List<CultureInfo> SeenCultures { get; } = new();
        public List<IcuWordSegmenter> Segmenters { get; } = new();

        public CapturingStemmingTokenizer(Func<string, CultureInfo> detectLang)
            : base(detectLang)
        {
        }

        protected override IcuWordSegmenter CreateSegmenter(CultureInfo culture)
        {
            SeenCultures.Add(culture);
            var segmenter = base.CreateSegmenter(culture);
            Segmenters.Add(segmenter);
            return segmenter;
        }
    }

    [Fact]
    public void Tokenize_RespectsDetectedThaiCulture()
    {
		// This test makes sure that we use ICU4N with correct culture, since Thai has special handling of word.
		var thaiCulture = CultureInfo.GetCultureInfo("th-TH");
        var tokenizer = new CapturingStemmingTokenizer(_ => thaiCulture);

        var text = "\u0E20\u0E32\u0E29\u0E32\u0E44\u0E17\u0E22\u0E40\u0E1B\u0E47\u0E19\u0E20\u0E32\u0E29\u0E32\u0E17\u0E35\u0E48\u0E2A\u0E27\u0E22\u0E07\u0E32\u0E21";
        var tokens = tokenizer.Tokenize(text);

        Assert.Contains(tokenizer.SeenCultures, c => c.Name == thaiCulture.Name);
        Assert.Contains(tokenizer.Segmenters, seg => seg.Culture.Name == thaiCulture.Name);

        var wordTokens = tokens.Where(t => t.Kind == WordKind.Word).Select(t => t.Text).ToArray();
        var expected = new[]
        {
            "\u0E20\u0E32\u0E29\u0E32",
            "\u0E44\u0E17\u0E22",
            "\u0E40\u0E1B\u0E47\u0E19",
            "\u0E20\u0E32\u0E29\u0E32",
            "\u0E17\u0E35\u0E48",
            "\u0E2A\u0E27\u0E22\u0E07\u0E32\u0E21"
        };
        Assert.Equal(expected, wordTokens);
    }
}