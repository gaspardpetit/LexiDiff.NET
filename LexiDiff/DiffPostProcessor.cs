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

		// 1) Build container ranges for A and B
		var containersA = granularity == DiffGranularity.Sentence
			? GetSentenceRanges(a, culture)
			: GetParagraphRanges(a);

		var containersB = granularity == DiffGranularity.Sentence
			? GetSentenceRanges(b, culture)
			: GetParagraphRanges(b);

		// 2) Identify changed token ranges on each side (from the DMP result)
		var changedRangesA = CoalesceRanges(
			diffs.Where(d => d.Operation != Op.Equal)
				 .SelectMany(d => d.Tokens.Where(st => d.Operation != Op.Insert)) // Deletes & Equals pull from A, but we only want changes
				 .Select(st => (st.Start, st.Length)));

		var changedRangesB = CoalesceRanges(
			diffs.Where(d => d.Operation != Op.Equal)
				 .SelectMany(d => d.Tokens.Where(st => d.Operation != Op.Delete)) // Inserts pull from B
				 .Select(st => (st.Start, st.Length)));

		// 3) Promote changed ranges to their containing container (sentence/paragraph)
		var promotedA = PromoteToContainers(changedRangesA, containersA);
		var promotedB = PromoteToContainers(changedRangesB, containersB);

		// 4) Merge overlapping/adjacent promotions on each side
		promotedA = CoalesceRanges(promotedA);
		promotedB = CoalesceRanges(promotedB);

		// 5) Rebuild a new diff: walk containers in order and emit Equal/Replace blocks
		return BuildContainerDiff(a, b, containersA, containersB, promotedA, promotedB);
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

	private static List<(int Start, int Length)> CoalesceRanges(IEnumerable<(int Start, int Length)> ranges)
	{
		var list = ranges.Where(r => r.Length > 0).OrderBy(r => r.Start).ToList();
		if (list.Count == 0)
			return list;

		var merged = new List<(int, int)>();
		int curS = list[0].Start;
		int curE = list[0].Start + list[0].Length;

		for (int i = 1; i < list.Count; i++)
		{
			int s = list[i].Start, e = s + list[i].Length;
			if (s <= curE)
			{ curE = Math.Max(curE, e); }
			else
			{ merged.Add((curS, curE - curS)); curS = s; curE = e; }
		}
		merged.Add((curS, curE - curS));
		return merged;
	}

	private static List<(int Start, int Length)> PromoteToContainers(
		List<(int Start, int Length)> changes,
		List<(int Start, int Length)> containers)
	{
		var result = new List<(int, int)>();
		foreach (var (s, len) in changes)
		{
			int e = s + len;
			// find first container that intersects [s,e)
			foreach (var (cs, clen) in containers)
			{
				int ce = cs + clen;
				if (ce <= s)
					continue;
				if (cs >= e)
					break;
				// overlap => promote to full container
				result.Add((cs, clen));
			}
		}
		return result;
	}

	private static List<DiffSpan> BuildContainerDiff(
		string a, string b,
		List<(int Start, int Length)> containersA,
		List<(int Start, int Length)> containersB,
		List<(int Start, int Length)> promotedA,
		List<(int Start, int Length)> promotedB)
	{
		// Mark container indices that are “changed” on each side
		var changedA = new HashSet<int>();
		for (int i = 0; i < containersA.Count; i++)
		{
			var r = containersA[i];
			if (promotedA.Any(p => RangesOverlap(r.Start, r.Length, p.Start, p.Length)))
				changedA.Add(i);
		}

		var changedB = new HashSet<int>();
		for (int i = 0; i < containersB.Count; i++)
		{
			var r = containersB[i];
			if (promotedB.Any(p => RangesOverlap(r.Start, r.Length, p.Start, p.Length)))
				changedB.Add(i);
		}

		// Walk both lists in lockstep by text order; when either side is marked changed,
		// emit a Replace pair (Delete from A, Insert into B). Otherwise emit Equal.
		// This is a simple alignment; for realignment you could match by text similarity.
		var spans = new List<DiffSpan>();
		int ia = 0, ib = 0;

		while (ia < containersA.Count || ib < containersB.Count)
		{
			bool aChanged = ia < containersA.Count && changedA.Contains(ia);
			bool bChanged = ib < containersB.Count && changedB.Contains(ib);

			if (aChanged || bChanged)
			{
				if (ia < containersA.Count && aChanged)
				{
					var (s, l) = containersA[ia++];
					var text = a.Substring(s, l);
					spans.Add(new DiffSpan(Op.Delete, text, Array.Empty<Token>()));
				}
				if (ib < containersB.Count && bChanged)
				{
					var (s, l) = containersB[ib++];
					var text = b.Substring(s, l);
					spans.Add(new DiffSpan(Op.Insert, text, Array.Empty<Token>()));
				}
			}
			else
			{
				if (ia < containersA.Count && ib < containersB.Count)
				{
					var (sa, la) = containersA[ia++];
					var (sb, lb) = containersB[ib++];
					var ta = a.Substring(sa, la);
					var tb = b.Substring(sb, lb);
					// If identical, keep Equal; else treat as replacement block
					if (string.Equals(ta, tb, StringComparison.Ordinal))
						spans.Add(new DiffSpan(Op.Equal, ta, Array.Empty<Token>()));
					else
					{
						spans.Add(new DiffSpan(Op.Delete, ta, Array.Empty<Token>()));
						spans.Add(new DiffSpan(Op.Insert, tb, Array.Empty<Token>()));
					}
				}
				else
				{
					// Tail on one side
					if (ia < containersA.Count)
					{ var (s, l) = containersA[ia++]; spans.Add(new DiffSpan(Op.Delete, a.Substring(s, l), Array.Empty<Token>())); }
					if (ib < containersB.Count)
					{ var (s, l) = containersB[ib++]; spans.Add(new DiffSpan(Op.Insert, b.Substring(s, l), Array.Empty<Token>())); }
				}
			}
		}

		return spans;
	}

	private static bool RangesOverlap(int s1, int l1, int s2, int l2)
	{
		int e1 = s1 + l1, e2 = s2 + l2;
		return !(e1 <= s2 || e2 <= s1);
	}
}
