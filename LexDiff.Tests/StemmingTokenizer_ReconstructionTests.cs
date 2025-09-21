using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace LexDiff.Tests;

public class StemmingTokenizer_ReconstructionTests
{
	private static readonly Func<string, CultureInfo> En = _ => CultureInfo.GetCultureInfo("en-US");
	private static readonly Func<string, CultureInfo> Fr = _ => CultureInfo.GetCultureInfo("fr-FR");

	[Fact]
	public void Reconstructs_Exactly_With_English_Word_And_Punctuation()
	{
		var s = "Running, quickly!";
		var toks = StemmingTokenizer.TokenizeWithStems(s, En);

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
		Assert.Contains(running, t => t.Kind == SubTokenKind.Stem);
		Assert.Contains(running, t => t.Kind == SubTokenKind.Suffix);

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
		var s = "Elle a mangées.";
		var toks = StemmingTokenizer.TokenizeWithStems(s, Fr);

		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// Find the pair for “mangées”: expect “mang” + “ées”
		// (Order and exact split depend on your splitter; this asserts pair behavior + reconstruction)
		var group = toks.Where(t => t.Start >= s.IndexOf("mangées", StringComparison.Ordinal)
								 && t.Start < s.IndexOf("mangées", StringComparison.Ordinal) + "mangées".Length)
						.GroupBy(t => t.ParentIndex)
						.FirstOrDefault(g => g.Sum(x => x.Length) == "mangées".Length);

		Assert.NotNull(group);
		var parts = group!.OrderBy(t => t.Start).ToList();
		Assert.True(parts.Count == 1 || parts.Count == 2, "Expected Whole or Stem+Suffix for 'mangées'.");

		var joined = string.Concat(parts.Select(t => t.Text));
		Assert.Equal("mangées", joined);

		AssertContiguousCoverage(s, toks);
	}

	[Fact]
	public void NoSplit_For_Short_Or_NonWord_Tokens_Reconstructs()
	{
		var s = "AI 123 -- OK.";
		var toks = StemmingTokenizer.TokenizeWithStems(s, En);

		var reconstructed = string.Concat(toks.OrderBy(t => t.Start).Select(t => t.Text));
		Assert.Equal(s, reconstructed);

		// Expect no Suffix tokens at all here (conservative behavior)
		Assert.DoesNotContain(toks, t => t.Kind == SubTokenKind.Suffix);

		AssertContiguousCoverage(s, toks);
	}

	[Fact]
	public void Mixed_Text_Reconstructs_And_Pairs_Are_Locally_Consistent()
	{
		var s = "Running tests passed, mangées aussi.";
		// Detector: English for ASCII words, French if word contains an accented char
		CultureInfo Detector(string w) =>
			w.Any(ch => ch >= 0x80) ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.GetCultureInfo("en-US");

		var toks = StemmingTokenizer.TokenizeWithStems(s, Detector);
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
				Assert.Equal(SubTokenKind.Stem, parts[0].Kind);
				Assert.Equal(SubTokenKind.Suffix, parts[1].Kind);
			}
			else
			{
				Assert.True(parts.Count == 1);
				Assert.Equal(SubTokenKind.Whole, parts[0].Kind);
			}
		}

		AssertContiguousCoverage(s, toks);
	}

	// ---------- helpers ----------

	private static void AssertContiguousCoverage(string source, IReadOnlyList<SubToken> toks)
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
