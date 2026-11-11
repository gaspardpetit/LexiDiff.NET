using Xunit;

namespace LexiDiff.Tests;

public class UnifiedDiffFormatterTests
{
    private static string Format(string a, string b, int context = 3) =>
        Lexi.Compare(a, b).ToUnifiedDiff("a.txt", "b.txt", context);

    [Fact]
    public void WordEdits_EmitCompleteLines()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,2 +1,2 @@\n" +
            "-Running, per the lexicon!\n+Runner, per the lexicon!\n Next entry stays.\n",
            Format("Running, per the lexicon!\nNext entry stays.\n",
                   "Runner, per the lexicon!\nNext entry stays.\n"));
    }

    [Fact]
    public void NearbyEdits_PreserveInterveningContext()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,3 +1,3 @@\n" +
            "-old\n+new\n lexicon\n-last\n+end\n",
            Format("old\nlexicon\nlast\n", "new\nlexicon\nend\n", 1));
    }

    [Fact]
    public void DistantEdits_ProduceSeparateHunks()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,2 +1,2 @@\n-old\n+new\n one\n" +
            "@@ -5,2 +5,2 @@\n four\n-last\n+end\n",
            Format("old\none\ntwo\nthree\nfour\nlast\n",
                   "new\none\ntwo\nthree\nfour\nend\n", 1));
    }

    [Fact]
    public void ZeroContext_OnlyIncludesChanges()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,1 +1,1 @@\n-old\n+new\n",
            Format("old\nlexicon\n", "new\nlexicon\n", 0));
    }

    [Theory]
    [InlineData("", "lemma\n", "@@ -0,0 +1,1 @@\n+lemma\n")]
    [InlineData("lemma\n", "", "@@ -1,1 +0,0 @@\n-lemma\n")]
    [InlineData("alpha\n", "alpha\nbeta\n", "@@ -1,0 +2,1 @@\n+beta\n")]
    [InlineData("alpha\nbeta\n", "alpha\n", "@@ -2,1 +1,0 @@\n-beta\n")]
    public void InsertionsAndDeletions_UseZeroLengthRanges(string a, string b, string hunk)
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n" + hunk, Format(a, b, 0));
    }

    [Fact]
    public void MissingFinalNewline_IsRepresentedExplicitly()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,1 +1,1 @@\n" +
            "-lemma\n\\ No newline at end of file\n+lemma\n",
            Format("lemma", "lemma\n"));
    }

    [Fact]
    public void BlankLinesAndCrLf_ArePreserved()
    {
        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -1,3 +1,4 @@\n" +
            " Alpha\r\n+Beta\r\n \r\n Gamma\r\n",
            Format("Alpha\r\n\r\nGamma\r\n", "Alpha\r\nBeta\r\n\r\nGamma\r\n"));
    }

    [Fact]
    public void MismatchedTarget_IsRejected()
    {
        var patch = LexiDiffer.Patch("lemma", "lexeme");
        Assert.Throws<InvalidOperationException>(() =>
            LexPatchFormatter.ToUnifiedDiff("lemma", "unrelated", patch));
    }
}
