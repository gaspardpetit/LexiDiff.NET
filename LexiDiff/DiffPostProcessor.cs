using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LexiDiff.Tokens;

namespace LexiDiff;

public enum DiffGranularity { Sentence, Paragraph }

public static class DiffPostProcessor
{
	public static List<DiffSpan> Promote(
		string a, string b,
		List<DiffSpan> diffs,
		DiffGranularity granularity,
		CultureInfo? culture = null)
	{
		culture ??= CultureInfo.InvariantCulture;

        // A pure word insertion/deletion marks only one side of a container.
        // Align whole containers by text so unchanged containers can anchor
        // either side of an edit, including repeated sentences and paragraphs.
        IReadOnlyList<Token> TokenizeContainers(string text)
        {
            var ranges = granularity == DiffGranularity.Sentence
                ? GetSentenceRanges(text, culture)
                : GetParagraphRanges(text);
            return ranges.Select((range, index) => new Token(index, range.Start,
                range.Length, text.Substring(range.Start, range.Length), WordKind.Other)).ToArray();
        }

        var coarse = DiffWithTokenizer.Diff(a, b, TokenizeContainers, cleanupSemantic: false);
        return coarse.SelectMany(span => span.Tokens.Select(token =>
            new DiffSpan(span.Operation, token.Text, Array.Empty<Token>()))).ToList();
    }

	// ----- helpers -----

	private static List<(int Start, int Length)> GetSentenceRanges(string s, CultureInfo culture)
	{
		var bi = ICU4N.Text.BreakIterator.GetSentenceInstance(culture);
		bi.SetText(s);
		var ranges = new List<(int, int)>();
		int start = bi.First();
		for (int end = bi.Next(); end != ICU4N.Text.BreakIterator.Done; start = end, end = bi.Next())
			ranges.Add((start, end - start));
		// handle trailing text if any
		if (ranges.Count == 0 && s.Length > 0)
			ranges.Add((0, s.Length));
		return ranges;
	}



	private static List<(int Start, int Length)> GetParagraphRanges(string s)
	{
		var ranges = new List<(int, int)>();
		int start = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == '\n')
			{
				ranges.Add((start, i - start + 1)); // include newline
				start = i + 1;
			}
		}
		if (start < s.Length)
			ranges.Add((start, s.Length - start));
		if (ranges.Count == 0 && s.Length > 0)
			ranges.Add((0, s.Length));
		return ranges;
	}

}
