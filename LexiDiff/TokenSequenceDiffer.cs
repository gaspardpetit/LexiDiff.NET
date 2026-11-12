// Bisection adapted from diff-match-patch (Copyright 2018 The diff-match-patch
// Authors), licensed under Apache-2.0. Operates on token text instead of chars.
using System;
using System.Collections.Generic;
using LexiDiff.DiffMatchPatch;
using LexiDiff.Tokens;

namespace LexiDiff;

internal static class TokenSequenceDiffer
{
    // MapBack consumes operation lengths, so placeholder chars carry counts only.
    private static string Count(int length) => new string('\0', length);

    private readonly struct TokenSlice
    {
        private readonly IReadOnlyList<Token> tokens;
        private readonly int start;
        public int Length { get; }
        public string this[int index] => tokens[start + index].Text;
        public TokenSlice(IReadOnlyList<Token> tokens, int start, int length)
        { this.tokens = tokens; this.start = start; Length = length; }
        public TokenSlice Slice(int offset, int length) => new TokenSlice(tokens, start + offset, length);
    }

    public static List<Diff> Diff(IReadOnlyList<Token> a, IReadOnlyList<Token> b) =>
        Main(new TokenSlice(a, 0, a.Count), new TokenSlice(b, 0, b.Count), DateTime.UtcNow.AddSeconds(1));

    private static List<Diff> Main(TokenSlice a, TokenSlice b, DateTime deadline)
    {
        int prefix = 0;
        while (prefix < Math.Min(a.Length, b.Length) && a[prefix] == b[prefix]) prefix++;
        a = a.Slice(prefix, a.Length - prefix);
        b = b.Slice(prefix, b.Length - prefix);
        int suffix = 0;
        while (suffix < Math.Min(a.Length, b.Length) &&
               a[a.Length - suffix - 1] == b[b.Length - suffix - 1]) suffix++;
        a = a.Slice(0, a.Length - suffix);
        b = b.Slice(0, b.Length - suffix);
        List<Diff> result;
        if (a.Length == 0 || b.Length == 0 || DateTime.UtcNow > deadline)
            result = Replace(a, b);
        else if (a.Length == 1 || b.Length == 1)
        {
            bool deleting = b.Length == 1;
            var longer = deleting ? a : b;
            var shorter = deleting ? b : a;
            int match = 0;
            while (match < longer.Length && longer[match] != shorter[0]) match++;
            if (match == longer.Length) result = Replace(a, b);
            else
            {
                result = new List<Diff>();
                var op = deleting ? Operation.DELETE : Operation.INSERT;
                if (match > 0) result.Add(new Diff(op, Count(match)));
                result.Add(new Diff(Operation.EQUAL, Count(1)));
                if (match + 1 < longer.Length) result.Add(new Diff(op, Count(longer.Length - match - 1)));
            }
        }
        else result = Bisect(a, b, deadline);
        if (prefix > 0) result.Insert(0, new Diff(Operation.EQUAL, Count(prefix)));
        if (suffix > 0) result.Add(new Diff(Operation.EQUAL, Count(suffix)));
        return result;
    }

    private static List<Diff> Replace(TokenSlice a, TokenSlice b)
    {
        var result = new List<Diff>();
        if (a.Length > 0) result.Add(new Diff(Operation.DELETE, Count(a.Length)));
        if (b.Length > 0) result.Add(new Diff(Operation.INSERT, Count(b.Length)));
        return result;
    }

    private static List<Diff> Split(TokenSlice a, TokenSlice b, int x, int y, DateTime deadline)
    {
        if ((x == 0 && y == 0) || (x == a.Length && y == b.Length)) return Replace(a, b);
        var result = Main(a.Slice(0, x), b.Slice(0, y), deadline);
        result.AddRange(Main(a.Slice(x, a.Length - x), b.Slice(y, b.Length - y), deadline));
        return result;
    }
	private static List<Diff> Bisect(TokenSlice text1, TokenSlice text2,
		DateTime deadline)
	{
		// Cache the text lengths to prevent multiple calls.
		int text1_length = text1.Length;
		int text2_length = text2.Length;
		int max_d = (text1_length + text2_length + 1) / 2;
		int v_offset = max_d;
		int v_length = 2 * max_d;
		int[] v1 = new int[v_length];
		int[] v2 = new int[v_length];
		for (int x = 0; x < v_length; x++)
		{
			v1[x] = -1;
			v2[x] = -1;
		}
		v1[v_offset + 1] = 0;
		v2[v_offset + 1] = 0;
		int delta = text1_length - text2_length;
		// If the total number of characters is odd, then the front path will
		// collide with the reverse path.
		bool front = delta % 2 != 0;
		// Offsets for start and end of k loop.
		// Prevents mapping of space beyond the grid.
		int k1start = 0;
		int k1end = 0;
		int k2start = 0;
		int k2end = 0;
		for (int d = 0; d < max_d; d++)
		{
			// Bail out if deadline is reached.
			if (DateTime.UtcNow > deadline)
			{
				break;
			}

			// Walk the front path one step.
			for (int k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
			{
				int k1_offset = v_offset + k1;
				int x1;
				if (k1 == -d || k1 != d && v1[k1_offset - 1] < v1[k1_offset + 1])
				{
					x1 = v1[k1_offset + 1];
				}
				else
				{
					x1 = v1[k1_offset - 1] + 1;
				}
				int y1 = x1 - k1;
				while (x1 < text1_length && y1 < text2_length
					  && text1[x1] == text2[y1])
				{
					x1++;
					y1++;
				}
				v1[k1_offset] = x1;
				if (x1 > text1_length)
				{
					// Ran off the right of the graph.
					k1end += 2;
				}
				else if (y1 > text2_length)
				{
					// Ran off the bottom of the graph.
					k1start += 2;
				}
				else if (front)
				{
					int k2_offset = v_offset + delta - k1;
					if (k2_offset >= 0 && k2_offset < v_length && v2[k2_offset] != -1)
					{
						// Mirror x2 onto top-left coordinate system.
						int x2 = text1_length - v2[k2_offset];
						if (x1 >= x2)
						{
							// Overlap detected.
							return Split(text1, text2, x1, y1, deadline);
						}
					}
				}
			}

			// Walk the reverse path one step.
			for (int k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
			{
				int k2_offset = v_offset + k2;
				int x2;
				if (k2 == -d || k2 != d && v2[k2_offset - 1] < v2[k2_offset + 1])
				{
					x2 = v2[k2_offset + 1];
				}
				else
				{
					x2 = v2[k2_offset - 1] + 1;
				}
				int y2 = x2 - k2;
				while (x2 < text1_length && y2 < text2_length
					&& text1[text1_length - x2 - 1]
					== text2[text2_length - y2 - 1])
				{
					x2++;
					y2++;
				}
				v2[k2_offset] = x2;
				if (x2 > text1_length)
				{
					// Ran off the left of the graph.
					k2end += 2;
				}
				else if (y2 > text2_length)
				{
					// Ran off the top of the graph.
					k2start += 2;
				}
				else if (!front)
				{
					int k1_offset = v_offset + delta - k2;
					if (k1_offset >= 0 && k1_offset < v_length && v1[k1_offset] != -1)
					{
						int x1 = v1[k1_offset];
						int y1 = v_offset + x1 - k1_offset;
						// Mirror x2 onto top-left coordinate system.
						x2 = text1_length - v2[k2_offset];
						if (x1 >= x2)
						{
							// Overlap detected.
							return Split(text1, text2, x1, y1, deadline);
						}
					}
				}
			}
		}
		// Diff took too long and hit the deadline or
		// number of diffs equals number of characters, no commonality at all.
		List<Diff> diffs = new List<Diff>();
		diffs.Add(new Diff(Operation.DELETE, Count(text1.Length)));
		diffs.Add(new Diff(Operation.INSERT, Count(text2.Length)));
		return diffs;
	}

}
