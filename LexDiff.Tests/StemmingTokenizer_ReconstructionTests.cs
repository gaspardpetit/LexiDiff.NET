using System.Globalization;
using Xunit;
using LexiDiff.Tokens;

namespace LexiDiff.Tests;

public class StemmingTokenizer_ReconstructionTests
{
	private static readonly Func<string, CultureInfo> En = _ => CultureInfo.GetCultureInfo("en-US");
	private static readonly Func<string, CultureInfo> Fr = _ => CultureInfo.GetCultureInfo("fr-FR");

	[Fact]
	public void Reconstructs_Exactly_With_English_Word_And_Punctuation()
	{
		var s = "Running, per the lexicon!";
		var toks = new StemmingTokenizer(En).Tokenize(s);

		// 1) Whole-string reconstruction (order by Start)
		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// 2) Find the group for "Running" -> expect exactly two parts: Stem + Suffix
		var running = toks
			.Where(t => t.Text.Equals("Run", StringComparison.Ordinal) ||
						t.Text.Equals("ning", StringComparison.Ordinal))
			.GroupBy(t => t.ParentIndex)
			.FirstOrDefault(g => g.Count() == 2);   // <-- add missing semicolon

		Assert.NotNull(running);

		// Assert the group has both a Stem and a Suffix token
		Assert.Contains(running, t => t.Role == TokenRole.Stem);
		Assert.Contains(running, t => t.Role == TokenRole.Suffix);

		// And the pair reconstructs the original word
		var pair = running!.OrderBy(t => t.Start).ToList();
		var joined = string.Concat(pair.Select(t => t.Text));
		Assert.Equal("Running", joined);

		// 3) Coverage has no gaps/overlaps
		AssertContiguousCoverage(s, toks);
	}

	[Fact]
	public void Reconstructs_Exactly_With_French_Accented_Word_And_Trailing_Period()
	{
		var s = "Elles sont arrivées.";
		var toks = new StemmingTokenizer(Fr).Tokenize(s);

		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// Find the pair for “mangées”: expect “mang” + “ées”
		// (Order and exact split depend on your splitter; this asserts pair behavior + reconstruction)
		var group = toks.Where(t => t.Start >= s.IndexOf("arrivées", StringComparison.Ordinal)
								 && t.Start < s.IndexOf("arrivées", StringComparison.Ordinal) + "arrivées".Length)
						.GroupBy(t => t.ParentIndex)
						.FirstOrDefault(g => g.Sum(x => x.Length) == "arrivées".Length);

		Assert.NotNull(group);
		var parts = group!.OrderBy(t => t.Start).ToList();
		Assert.True(parts.Count == 1 || parts.Count == 2, "Expected Whole or Stem+Suffix for 'arrivées'.");

		var joined = string.Concat(parts.Select(t => t.Text));
		Assert.Equal("arrivées", joined);

		AssertContiguousCoverage(s, toks);
	}

	[Fact]
	public void NoSplit_For_Short_Or_NonWord_Tokens_Reconstructs()
	{
		var s = "ABC 123 -- OK.";
		var toks = new StemmingTokenizer(En).Tokenize(s);

		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// Expect no Suffix tokens at all here (conservative behavior)
		Assert.DoesNotContain(toks, t => t.Role == TokenRole.Suffix);

		AssertContiguousCoverage(s, toks);
	}

	[Fact]
	public void Mixed_Text_Reconstructs_And_Pairs_Are_Locally_Consistent()
	{
		var s = "Running lexemes passed, arrivées aussi.";
		// Detector: English for ASCII words, French if word contains an accented char
		CultureInfo Detector(string w) =>
			w.Any(ch => ch >= 0x80) ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.GetCultureInfo("en-US");

		var toks = new StemmingTokenizer(Detector).Tokenize(s);
		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// For each ParentIndex, either 1 Whole or 2 (Stem+Suffix) covering the exact span
		foreach (var group in toks.GroupBy(t => t.ParentIndex))
		{
			var parts = group.OrderBy(t => t.Start).ToList();
			// adjacency: end of part i == start of part i+1
			for (int i = 0; i + 1 < parts.Count; i++)
				Assert.Equal(parts[i].Start + parts[i].Length, parts[i + 1].Start);

			// local reconstruction equals concatenation of texts
			var local = string.Concat(parts.Select(p => p.Text));
			// Using the global string as the ground truth
			var start = parts.First().Start;
			var end = parts.Last().Start + parts.Last().Length;
			Assert.Equal(local, s.Substring(start, end - start));

			// if there are two parts, make sure they are Stem + Suffix
			if (parts.Count == 2)
			{
				Assert.Equal(TokenRole.Stem, parts[0].Role);
				Assert.Equal(TokenRole.Suffix, parts[1].Role);
			}
			else
			{
				Assert.True(parts.Count == 1);
				Assert.Equal(TokenRole.Whole, parts[0].Role);
			}
		}

		AssertContiguousCoverage(s, toks);
	}

	// ---------- helpers ----------

	private static void AssertContiguousCoverage(string source, IReadOnlyList<Token> toks)
	{
		// Ensure subtokens cover the string without gaps/overlaps.
		// Note: ICU may emit tokens that skip positions (e.g., if it doesn't return whitespace);
		// If your segmenter emits *everything* (emitPunctuation=true), this should be contiguous.
		var ordered = toks.OrderBy(t => t.Start).ToList();

		int pos = 0;
		foreach (var t in ordered)
		{
			// allow gaps only if the skipped characters equal the slice in source
			if (t.Start > pos)
				Assert.Equal(source.Substring(pos, t.Start - pos), string.Concat(Enumerable.Repeat("", 1))); // no unexpected gap

			// token text matches source at span
			Assert.True(t.Start + t.Length <= source.Length);
			Assert.Equal(source.Substring(t.Start, t.Length), t.Text);

			// move forward
			pos = t.Start + t.Length;
		}

		// final position should be end of string
		Assert.Equal(source.Length, pos);
	}
}
