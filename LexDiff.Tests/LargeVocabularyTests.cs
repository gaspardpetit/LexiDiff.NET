using LexiDiff.Tokens;
using Xunit;

namespace LexiDiff.Tests;

public class LargeVocabularyTests
{
    private static IReadOnlyList<Token> Words(string text)
    {
        var tokens = new List<Token>();
        int start = 0;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Keep separators inside each token to preserve exact reconstruction.
            int end = start + word.Length;
            if (end < text.Length) end++;
            tokens.Add(new Token(tokens.Count, start, end - start, text.Substring(start, end - start), WordKind.Word));
            start = end;
        }
        return tokens;
    }

    private static string Lexicon(int count) =>
        string.Join(" ", Enumerable.Range(0, count).Select(i => "lexeme" + i));

    private static void AssertRoundTrip(string a, string b, List<DiffSpan> diff)
    {
        Assert.Equal(a, string.Concat(diff.Where(d => d.Operation != Op.Insert).Select(d => d.Text)));
        Assert.Equal(b, string.Concat(diff.Where(d => d.Operation != Op.Delete).Select(d => d.Text)));
    }

    [Theory]
    [InlineData(6401)]
    [InlineData(70000)]
    public void VocabularyBeyondCharacterEncoding_StillDiffsSmallEdits(int count)
    {
        var a = Lexicon(count);
        var b = a.Replace("lexeme123 ", "lemma123 ").Replace("lexeme5000 ", "lemma5000 ");
        var diff = DiffWithTokenizer.Diff(a, b, Words);
        AssertRoundTrip(a, b, diff);
        Assert.Contains(diff, d => d.Operation == Op.Equal && d.Text.Contains("lexeme3000 "));
        Assert.Equal("lexeme123 lexeme5000 ", string.Concat(diff.Where(d => d.Operation == Op.Delete).Select(d => d.Text)));
        Assert.Equal("lemma123 lemma5000 ", string.Concat(diff.Where(d => d.Operation == Op.Insert).Select(d => d.Text)));
    }

    [Fact]
    public void CombinedVocabularyAcrossInputs_UsesOneUnlimitedDiff()
    {
        var shared = Lexicon(3500) + " ";
        var a = shared + string.Join(" ", Enumerable.Range(0, 3500).Select(i => "etymon" + i));
        var b = shared + string.Join(" ", Enumerable.Range(0, 3500).Select(i => "lemma" + i));
        var diff = DiffWithTokenizer.Diff(a, b, Words);
        AssertRoundTrip(a, b, diff);
        Assert.Contains(diff, d => d.Operation == Op.Equal && d.Text == shared);
    }

    [Fact]
    public void LargeDeletionAndInsertion_RoundTrip()
    {
        var text = Lexicon(6500);
        AssertRoundTrip(text, "", DiffWithTokenizer.Diff(text, "", Words));
        AssertRoundTrip("", text, DiffWithTokenizer.Diff("", text, Words));
        AssertRoundTrip(text, text, DiffWithTokenizer.Diff(text, text, Words));
    }

    [Fact]
    public void LargeVocabularyFallback_MatchesShortestEditCountsForRepeatedWords()
    {
        var random = new Random(1234);
        var vocabulary = Lexicon(6500);
        for (int trial = 0; trial < 100; trial++)
        {
            var left = Enumerable.Range(0, random.Next(16)).Select(_ => "term" + random.Next(4)).ToArray();
            var right = Enumerable.Range(0, random.Next(16)).Select(_ => "term" + random.Next(4)).ToArray();
            var a = string.Concat(left.Select(w => w + " ")) + vocabulary;
            var b = string.Concat(right.Select(w => w + " ")) + vocabulary;
            var diff = DiffWithTokenizer.Diff(a, b, Words);
            AssertRoundTrip(a, b, diff);
            var lcs = new int[left.Length + 1, right.Length + 1];
            for (int i = 1; i <= left.Length; i++)
                for (int j = 1; j <= right.Length; j++)
                    lcs[i, j] = left[i - 1] == right[j - 1] ? lcs[i - 1, j - 1] + 1
                        : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            Assert.Equal(left.Length + right.Length - 2 * lcs[left.Length, right.Length],
                diff.Where(d => d.Operation != Op.Equal).Sum(d => d.Tokens.Count));
        }
    }

    [Fact]
    public void RepeatedTokensBetweenUniqueEntries_AreAligned()
    {
        var vocabulary = Lexicon(6500) + " ";
        var a = "old meaning repeated term repeated sense " + vocabulary;
        var b = "new meaning repeated sense repeated term " + vocabulary;
        AssertRoundTrip(a, b, DiffWithTokenizer.Diff(a, b, Words));
    }
}
