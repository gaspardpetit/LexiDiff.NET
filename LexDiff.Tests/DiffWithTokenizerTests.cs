using System.Globalization;
using Xunit;
using LexiDiff.Tokens;

namespace LexiDiff.Tests;

public class DiffWithTokenizerTests
{
	private static readonly Func<string, CultureInfo> En = _ => CultureInfo.GetCultureInfo("en-US");
	private static readonly Func<string, CultureInfo> Fr = _ => CultureInfo.GetCultureInfo("fr-FR");

	[Fact]
	public void Reconstructs_A_and_B_Exactly()
	{
		var a = "Running, per the lexicon!";
		var b = "Runner, per the lexicon.";

		var diffs = DiffWithTokenizer.Diff(a, b, En);

		// Reconstruct A by concatenating Equal+Delete (what was in A)
		var reconA = string.Concat(diffs.Where(d => d.Operation != Op.Insert).Select(d => d.Text));
		// Reconstruct B by concatenating Equal+Insert (what ends up in B)
		var reconB = string.Concat(diffs.Where(d => d.Operation != Op.Delete).Select(d => d.Text));

		Assert.Equal(a, reconA);
		Assert.Equal(b, reconB);

		// Also ensure each span’s subtokens concatenate to its text (no partial token splits)
		foreach (var span in diffs)
		{
			var joined = string.Concat(span.Tokens.OrderBy(t => t.Start).Select(t => t.Text));
			Assert.Equal(span.Text, joined);
		}
	}


	[Fact]
	public void Replacement_Touches_Suffix_And_Preserves_Stem()
	{
		var a = "Running, per the lexicon!";
		var b = "Runner, per the lexicon!";

		var diffs = DiffWithTokenizer.Diff(a, b, En);

		// Whole-string reconstruction checks
		var reconA = string.Concat(diffs.Where(d => d.Operation != Op.Insert).Select(d => d.Text));
		var reconB = string.Concat(diffs.Where(d => d.Operation != Op.Delete).Select(d => d.Text));
		Assert.Equal(a, reconA);
		Assert.Equal(b, reconB);

		// Tokenize both sides
		var toksA = new StemmingTokenizer(En).Tokenize(a);
		var toksB = new StemmingTokenizer(En).Tokenize(b);

		// First-word groups by absolute span (don’t rely on token order)
		var aWord = "Running";
		var bWord = "Runner";
		var aStart = a.IndexOf(aWord, StringComparison.Ordinal);
		var bStart = b.IndexOf(bWord, StringComparison.Ordinal);
		Assert.True(aStart >= 0 && bStart >= 0);

		var groupA = toksA.Where(t => t.Start >= aStart && t.Start < aStart + aWord.Length)
						  .OrderBy(t => t.Start).ToList();
		var groupB = toksB.Where(t => t.Start >= bStart && t.Start < bStart + bWord.Length)
						  .OrderBy(t => t.Start).ToList();

		// Snowball splits “Running” -> Stem+Suffix; “Runner” may or may not split
		var stemA = groupA.FirstOrDefault(t => t.Role == TokenRole.Stem);
		Assert.NotNull(stemA);

		var stemB = groupB.FirstOrDefault(t => t.Role == TokenRole.Stem);
		// Stem preserved: either exact stem token, or B’s word starts with A’s stem text
		if (stemB is not null)
			Assert.Equal(stemA!.Text, stemB.Text);
		else
			Assert.StartsWith(stemA!.Text, bWord);

		// There must be at least one non-equal span overlapping the first word (on either side)
		bool changedWord = diffs.Any(d =>
			d.Operation != Op.Equal &&
			d.Tokens.Any(st =>
				st.Start >= aStart && st.Start < aStart + aWord.Length ||
				st.Start >= bStart && st.Start < bStart + bWord.Length));
		Assert.True(changedWord);

		// No mid-subtoken splits anywhere
		foreach (var span in diffs)
		{
			var joined = string.Concat(span.Tokens.OrderBy(t => t.Start).Select(t => t.Text));
			Assert.Equal(span.Text, joined);
		}
	}

	[Fact]
	public void Handles_Punctuation_And_Accents_With_Reconstruction()
	{
		var a = "sont arrivées.";
		var b = "sont arrivés.";
		var diffs = DiffWithTokenizer.Diff(a, b, _ => CultureInfo.GetCultureInfo("fr-FR"));

		var reconA = string.Concat(diffs.Where(d => d.Operation != Op.Insert).Select(d => d.Text));
		var reconB = string.Concat(diffs.Where(d => d.Operation != Op.Delete).Select(d => d.Text));
		Assert.Equal(a, reconA);
		Assert.Equal(b, reconB);

		// Expect the stem "mang" to be aligned as Equal
		Assert.Contains(diffs, d => d.Operation == Op.Equal && d.Text.Contains("arriv", StringComparison.Ordinal));

		// Expect one DELETE and one INSERT both consisting only of suffix subtokens
		var del = diffs.FirstOrDefault(d => d.Operation == Op.Delete);
		var ins = diffs.FirstOrDefault(d => d.Operation == Op.Insert);
		Assert.NotNull(del);
		Assert.NotNull(ins);
		Assert.All(del!.Tokens, st => Assert.Equal(TokenRole.Suffix, st.Role));
		Assert.All(ins!.Tokens, st => Assert.Equal(TokenRole.Suffix, st.Role));

		// Typical case: delete "ées", insert "é" (don’t overfit to exact strings)
		Assert.True(del.Text.Length >= 1);
		Assert.True(ins.Text.Length >= 1);

		// Each span’s text equals concat of its subtokens (no mid-subtoken splits)
		foreach (var span in diffs)
		{
			var joined = string.Concat(span.Tokens.Select(t => t.Text));
			Assert.Equal(span.Text, joined);
		}
	}
}
