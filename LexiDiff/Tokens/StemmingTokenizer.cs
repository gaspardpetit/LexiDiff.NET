using LexiDiff.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;

// You already have this:
public static class StemmingTokenizer
{
	/// <summary>
	/// Segment with ICU, then split word/number tokens using MultiLangStemSplitter.
	/// Non-words are passed through as Whole.
	/// The concatenation of emitted subtokens equals the original text exactly.
	/// </summary>
	/// <param name="s">input text</param>
	/// <param name="detectLang">language detector (word -> CultureInfo)</param>
	/// <returns>sequence of subtokens (Whole, or Stem+Suffix)</returns>
	public static IReadOnlyList<SubToken> TokenizeWithStems(string s, Func<string, CultureInfo> detectLang)
	{
		if (s == null)
			throw new ArgumentNullException(nameof(s));
		if (detectLang == null)
			throw new ArgumentNullException(nameof(detectLang));

		var seg = new IcuWordSegmenter("und", emitPunctuation: true, normalizeTo: null);
		var baseTokens = seg.Tokenize(s); // assumes items with .Text, .Kind, .Start, .Length

		var output = new List<SubToken>();
		int parentIdx = 0;

		foreach (var t in baseTokens)
		{
			// Adjust these checks to your token model if names differ:
			bool isWordLike =
				string.Equals(t.Kind.ToString(), "Word", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(t.Kind.ToString(), "Number", StringComparison.OrdinalIgnoreCase);

			if (!isWordLike)
			{
				// Pass-through: perfect reconstruction at token-level
				output.Add(new SubToken(parentIdx, t.Start, t.Length, t.Text, SubTokenKind.Whole));
				parentIdx++;
				continue;
			}

			var (stem, suffix) = MultiLangStemSplitter.Split(t.Text, detectLang);

			if (!string.IsNullOrEmpty(suffix) && stem.Length + suffix.Length == t.Text.Length)
			{
				// Two subtokens; preserve absolute spans
				output.Add(new SubToken(parentIdx, t.Start, stem.Length, stem, SubTokenKind.Stem));
				output.Add(new SubToken(parentIdx, t.Start + stem.Length, suffix.Length, suffix, SubTokenKind.Suffix));
			}
			else
			{
				// No confident split or conservative fallback: emit whole
				output.Add(new SubToken(parentIdx, t.Start, t.Length, t.Text, SubTokenKind.Whole));
			}

			parentIdx++;
		}

		return output;
	}
}
