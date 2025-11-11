using LexiDiff.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;


public interface ITokenizer
{
    IReadOnlyList<Token> Tokenize(string s);
}

public class StemmingTokenizer : ITokenizer
{
    private Func<string, CultureInfo> _detectLang;

    public StemmingTokenizer(Func<string, CultureInfo> detectLang)
    {
        _detectLang = detectLang ?? (_ => CultureInfo.GetCultureInfo("en"));
    }

    public static IReadOnlyList<Token> TokenizeWithStems(string text, Func<string, CultureInfo>? detectLang)
    {
        var detector = detectLang ?? (_ => CultureInfo.GetCultureInfo("en"));
        return new StemmingTokenizer(detector).Tokenize(text);
    }

    /// <summary>
    /// Segment with ICU, then split word/number tokens using MultiLangStemSplitter.
    /// Non-words are passed through as Whole.
    /// The concatenation of emitted tokens equals the original text exactly.
    /// </summary>
    public virtual IReadOnlyList<Token> Tokenize(string s)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));

		var culture = _detectLang.Invoke(s) ?? CultureInfo.InvariantCulture;
        var segmenter = CreateSegmenter(culture);
        var baseTokens = segmenter.Tokenize(s);

        if (baseTokens.Count == 0)
            return baseTokens;

        var output = new List<Token>(baseTokens.Count * 2);
        foreach (var t in baseTokens)
        {
            bool isWordLike = t.Kind == WordKind.Word || t.Kind == WordKind.Number;
            if (!isWordLike)
            {
                output.Add(t);
                continue;
            }

            var (stem, suffix) = MultiLangStemSplitter.Split(t.Text, _detectLang);
            if (!string.IsNullOrEmpty(suffix) && stem.Length + suffix.Length == t.Text.Length)
            {
                output.Add(new Token(t.ParentIndex, t.Start, stem.Length, stem, t.Kind, TokenRole.Stem));
                output.Add(new Token(t.ParentIndex, t.Start + stem.Length, suffix.Length, suffix, t.Kind, TokenRole.Suffix));
            }
            else
            {
                output.Add(new Token(t.ParentIndex, t.Start, t.Length, t.Text, t.Kind, TokenRole.Whole));
            }
        }

        return output;
    }

    protected virtual ITokenizer CreateSegmenter(CultureInfo culture)
    {
        culture ??= CultureInfo.InvariantCulture;
        return new BasicWordSegmenter(culture, emitPunctuation: true, normalizeTo: null);
    }
}
