using LexiDiff.Tokens;
using Xunit;

namespace LexDiff.Tests;

public class IcuWordSegmenterTests
{
	public static IReadOnlyList<int> WordViewIndices(IReadOnlyList<Token> tokens)
	=> Enumerable.Range(0, tokens.Count)
				 .Where(i => tokens[i].Kind == WordKind.Word || tokens[i].Kind == WordKind.Number)
				 .ToArray();


	[Theory]
	[InlineData("Lexicographers catalog words.", new[] { "Lexicographers", " ", "catalog", " ", "words", "." })]
	[InlineData("Les lexicographes décrivent les mots!", new[] { "Les", " ", "lexicographes", " ", "décrivent", " ", "les", " ", "mots", "!" })]
	// ideally พจนานุกรม would be kept together, but we would need a complete Thai dictionary
	[InlineData("พจนานุกรมไทยอธิบายคำ", new[] { "พจนานุกรม", "ไทย", "อธิบาย", "คำ" })]
	[InlineData("辞書は語源を語る。", new[] { "辞書", "は", "語源", "を", "語る", "。" })]
	public void Tokenize_Words_NoPunct(string input, string[] expected)
	{
		var seg = new IcuWordSegmenter("und", emitPunctuation: true);
		var toks = seg.Tokenize(input);
		Assert.Equal(expected, toks.Select(t => t.Text).ToArray());
	}

	[Fact]
	public void Tokenize_Offsets_AreCorrect()
	{
		var s = "Merci, AI!";
		var seg = new IcuWordSegmenter("fr", emitPunctuation: true);
		var toks = seg.Tokenize(s);

		// Find “Merci”
		var merci = toks.First(t => t.Text == "Merci");
		Assert.Equal("Merci", s.Substring(merci.Start, merci.Length));

		// Find “,” punctuation
		var comma = toks.First(t => t.Text == ", ");
		Assert.Equal(WordKind.Punctuation, comma.Kind);
		Assert.Equal(", ", s.Substring(comma.Start, comma.Length));
	}

	[Fact]
	public void Tokenize_Reconstruct_RoundTrips_Exactly()
	{
		var s = "Hello,  (world)🙂 \r\nTabs\tand  spaces.";
		var seg = new IcuWordSegmenter("und", emitPunctuation: true, normalizeTo: null);
		var toks = seg.Tokenize(s);

		// Coverage check: spans cover the whole string with no gaps/overlaps
		int pos = 0;
		foreach (var t in toks)
		{
			Assert.Equal(pos, t.Start);
			pos += t.Length;
		}
		Assert.Equal(s.Length, pos);

		// Exact reconstruction
		Assert.Equal(s, string.Concat(toks.Select(t => t.Text)));
	}

	[Fact]
	public void WordView_IsAProjection_NotAFilterOnBase()
	{
		var s = "Hi, world!";
		var seg = new IcuWordSegmenter("und", emitPunctuation: true);
		var toks = seg.Tokenize(s);
		var wordIdx = WordViewIndices(toks);

		// Base sequence remains lossless
		Assert.Equal(s, string.Concat(toks.Select(t => t.Text)));

		// Word-view contains only word-ish tokens (no punctuation/whitespace)
		Assert.All(wordIdx, i => Assert.True(
			toks[i].Kind == WordKind.Word || toks[i].Kind == WordKind.Number));
	}
}
