using System.Globalization;
using Xunit;

namespace LexiDiff.Tests;

public class PromotionAlignmentTests
{
    private static LexiDiffResult Compare(string a, string b, LexGranularity granularity) =>
        Lexi.Compare(a, b, new LexiOptions {
            PromoteTo = granularity,
            // English sentence rules attach trailing spaces to the preceding sentence.
            SentenceCulture = CultureInfo.GetCultureInfo("en-US")
        });

    [Theory]
    [InlineData(LexGranularity.Sentence, "A sentence. ", "A new sentence. ", "Next entry stays.")]
    [InlineData(LexGranularity.Paragraph, "A sentence.\n", "A new sentence.\n", "Next entry stays.\n")]
    public void WordInsertionOrDeletion_PreservesFollowingContainer(
        LexGranularity granularity, string oldContainer, string newContainer, string unchanged)
    {
        foreach (var (a, b) in new[] {
            (oldContainer + unchanged, newContainer + unchanged),
            (newContainer + unchanged, oldContainer + unchanged)
        })
        {
            var diff = Compare(a, b, granularity);
            Assert.Equal(a, diff.ReconstructA());
            Assert.Equal(b, diff.ReconstructB());
            Assert.Equal(3, diff.Spans.Count);
            Assert.Equal(LexiOp.Delete, diff.Spans[0].Op);
            Assert.Equal(LexiOp.Insert, diff.Spans[1].Op);
            Assert.Equal(new LexSpan(LexiOp.Equal, unchanged), diff.Spans[2]);
        }
    }

    [Theory]
    [InlineData(LexGranularity.Sentence, "Lexicon. ", "New entry. ", "Etymology.")]
    [InlineData(LexGranularity.Paragraph, "Lexicon.\n", "New entry.\n", "Etymology.\n")]
    public void WholeContainerInsertionAndDeletion_AlignRepeatedContainers(
        LexGranularity granularity, string repeated, string added, string tail)
    {
        var original = repeated + repeated + tail;
        var modified = repeated + added + repeated + tail;
        foreach (var (a, b, op) in new[] {
            (original, modified, LexiOp.Insert), (modified, original, LexiOp.Delete)
        })
        {
            var diff = Compare(a, b, granularity);
            Assert.Equal(a, diff.ReconstructA());
            Assert.Equal(b, diff.ReconstructB());
            var change = Assert.Single(diff.Spans, s => s.Op != LexiOp.Equal);
            Assert.Equal(new LexSpan(op, added), change);
            Assert.Equal(original, string.Concat(diff.Spans.Where(s => s.Op == LexiOp.Equal).Select(s => s.Text)));
        }
    }

    [Theory]
    [InlineData(LexGranularity.Sentence, "New entry. ", "Lexicon.")]
    [InlineData(LexGranularity.Paragraph, "New entry.\n", "Lexicon.\n")]
    public void LeadingContainerInsertion_DoesNotReplaceExistingText(
        LexGranularity granularity, string added, string original)
    {
        var diff = Compare(original, added + original, granularity);
        Assert.Equal(new[] { new LexSpan(LexiOp.Insert, added), new LexSpan(LexiOp.Equal, original) }, diff.Spans);
    }

    [Theory]
    [InlineData(LexGranularity.Sentence)]
    [InlineData(LexGranularity.Paragraph)]
    public void EmptyInputs_ReconstructExactly(LexGranularity granularity)
    {
        Assert.Empty(Compare("", "", granularity).Spans);
        Assert.Equal("Lexicon.", Compare("", "Lexicon.", granularity).ReconstructB());
        Assert.Equal("Lexicon.", Compare("Lexicon.", "", granularity).ReconstructA());
    }
}
