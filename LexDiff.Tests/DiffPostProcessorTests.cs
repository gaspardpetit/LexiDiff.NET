using System.Globalization;
using Xunit;

namespace LexiDiff.Tests;

public class DiffPostProcessorTests
{
	private static readonly Func<string, CultureInfo> En = _ => CultureInfo.GetCultureInfo("en-US");

	private static List<DiffSpan> Fine(string a, string b)
		=> DiffWithTokenizer.Diff(a, b, En);

	private static string ReconstructA(IEnumerable<DiffSpan> spans)
		=> string.Concat(spans.Where(d => d.Operation != Op.Insert).Select(d => d.Text));

	private static string ReconstructB(IEnumerable<DiffSpan> spans)
		=> string.Concat(spans.Where(d => d.Operation != Op.Delete).Select(d => d.Text));

	[Fact]
	public void Promote_Sentence_Replaces_Only_Changed_Sentence()
	{
		var a = "Running, per the lexicon! Next entry stays.";
		var b = "Runner, per the lexicon! Next entry stays.";

		var fine = Fine(a, b);
		var coarse = DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Sentence, CultureInfo.GetCultureInfo("en-US"));

		// Reconstruction invariants
		Assert.Equal(a, ReconstructA(coarse));
		Assert.Equal(b, ReconstructB(coarse));

		// First sentence is replaced as a whole. ICU includes the trailing space in the sentence.
		Assert.Contains(coarse, d => d.Operation == Op.Delete && d.Text.EndsWith("! ", StringComparison.Ordinal));
		Assert.Contains(coarse, d => d.Operation == Op.Insert && d.Text.EndsWith("! ", StringComparison.Ordinal));

		// Second sentence remains Equal without a leading space.
		Assert.Contains(coarse, d => d.Operation == Op.Equal && d.Text == "Next entry stays.");
	}

	[Fact]
	public void Promote_Sentence_Collapses_Multiple_Changes_Into_One_Block()
	{
		var a = "We were Running through lexemes very quickly! The remaining entry is unchanged.";
		var b = "We were Runner through lexemes quite quickly! The remaining entry is unchanged.";

		var fine = Fine(a, b);
		Assert.True(fine.Count(d => d.Operation != Op.Equal) >= 2); // multiple small changes

		var coarse = DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Sentence, CultureInfo.GetCultureInfo("en-US"));

		Assert.Equal(a, ReconstructA(coarse));
		Assert.Equal(b, ReconstructB(coarse));

		// Expect one replace pair for the first sentence (with trailing space)
		Assert.Contains(coarse, d => d.Operation == Op.Delete && d.Text.EndsWith("! ", StringComparison.Ordinal));
		Assert.Contains(coarse, d => d.Operation == Op.Insert && d.Text.EndsWith("! ", StringComparison.Ordinal));

		// And an Equal second sentence (no leading space)
		Assert.Contains(coarse, d => d.Operation == Op.Equal && d.Text == "The remaining entry is unchanged.");
	}

	[Fact]
	public void Promote_Paragraph_Replaces_Only_Changed_Paragraph()
	{
		var a = "Para A line 1.\nPara A line 2.\n\nUnchanged para.\n";
		var b = "Para B line 1 changed.\nPara B line 2.\n\nUnchanged para.\n";

		var fine = Fine(a, b);
		var coarse = DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Paragraph);

		Assert.Equal(a, ReconstructA(coarse));
		Assert.Equal(b, ReconstructB(coarse));

		// We expect:
		// - one Delete for the changed paragraph A
		// - one Insert for the changed paragraph B
		// - then TWO Equals: the blank line and the unchanged paragraph
		Assert.Contains(coarse, d => d.Operation == Op.Delete && d.Text.Contains("Para A line 1.", StringComparison.Ordinal));
		Assert.Contains(coarse, d => d.Operation == Op.Insert && d.Text.Contains("Para B line 1 changed.", StringComparison.Ordinal));

		var equals = coarse.Where(d => d.Operation == Op.Equal).ToList();
		Assert.Equal(2, equals.Count);
		Assert.Contains(equals, e => e.Text == "\n");
		Assert.Contains(equals, e => e.Text == "Unchanged para.\n");
	}
}
