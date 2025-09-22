using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace LexiDiff.Tests;

public class UnifiedDiffFormatterTests
{
	private static readonly Func<string, CultureInfo> En
		= _ => CultureInfo.GetCultureInfo("en-US");

	private static readonly Regex HunkHeader =
		new Regex(@"^@@ -(?<la>\d+),(?<sa>\d+) \+(?<lb>\d+),(?<sb>\d+) @@$",
				  RegexOptions.Compiled | RegexOptions.Multiline);

	[Fact]
	public void UnifiedDiff_Basic_OneLineChanged_ShowsMinusPlusAndContext()
	{
		var a = "Running, per the lexicon!\nNext entry stays.\n";
		var b = "Runner, per the lexicon!\nNext entry stays.\n";

		var patch = LexiDiffer.Patch(a, b, new LexiDiffOptions {
			DetectLang = En,
			PromoteTo = Granularity.Tokens
		});

		var udiff = LexPatchFormatter.ToUnifiedDiff(a, b, patch, aLabel: "a.txt", bLabel: "b.txt", contextLines: 3);

		// Headers present
		Assert.Contains("--- a.txt\n", udiff);
		Assert.Contains("+++ b.txt\n", udiff);

		// Hunk header present and sane
		var m = HunkHeader.Match(udiff);
		Assert.True(m.Success, "Missing unified hunk header.");
		int sa = int.Parse(m.Groups["sa"].Value);
		int sb = int.Parse(m.Groups["sb"].Value);
		Assert.Equal(sa, sb);
		Assert.True(sa >= 2); // changed line + at least one context

		// We expect minus/plus lines for the changed lemma plus a context line carrying the punctuation
		Assert.Contains("\n-Running\n", udiff);
		Assert.Contains("\n+Runner\n", udiff);
		Assert.Contains("\n , per the lexicon!\n", udiff);

		// Context second line should be present
		Assert.Contains("\n Next entry stays.\n", udiff);

		// No CRs (we force \n)
		Assert.DoesNotContain("\r", udiff);
	}

	[Fact]
	public void UnifiedDiff_InsertLine_And_BlankLineContext()
	{
		var a = "Alpha\n\nGamma\n";
		var b = "Alpha\nBeta\n\nGamma\n";

		var patch = LexiDiffer.Patch(a, b, new LexiDiffOptions {
			DetectLang = En,
			PromoteTo = Granularity.Tokens
		});

		var udiff = LexPatchFormatter.ToUnifiedDiff(a, b, patch, aLabel: "a.txt", bLabel: "b.txt", contextLines: 1);

		// Headers present
		Assert.Contains("--- a.txt\n", udiff);
		Assert.Contains("+++ b.txt\n", udiff);

		// Hunk header present
		var m = HunkHeader.Match(udiff);
		Assert.True(m.Success, "Missing unified hunk header.");

		// We should see context "Alpha", inserted "Beta", and a blank context line
		Assert.Contains("\n Alpha\n", udiff);
		Assert.Contains("\n+Beta\n", udiff);
		Assert.Contains("\n \n", udiff); // blank line context

		// No deletions for Alpha or Gamma in this pure insertion case
		Assert.DoesNotContain("\n-Alpha\n", udiff);
		Assert.DoesNotContain("\n-Gamma\n", udiff);
	}

	[Fact]
	public void UnifiedDiff_IsLineLevel_NotWordInline()
	{
		var a = "Running, per the lexicon!\n";
		var b = "Runner, per the lexicon!\n";

		var patch = LexiDiffer.Patch(a, b, new LexiDiffOptions { DetectLang = En });
		var udiff = LexPatchFormatter.ToUnifiedDiff(a, b, patch);

		// The unified output shows whole changed lines, not inline word markers
		Assert.Contains("\n-Running\n", udiff);
		Assert.Contains("\n+Runner\n", udiff);

		// No inline tokens like “[Stem]” etc.
		Assert.DoesNotContain("[", udiff);
		Assert.DoesNotContain("]", udiff);
	}
}
