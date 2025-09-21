using LexiDiff.DiffMatchPatch;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// SubToken { ParentIndex, Start, Length, Text, Kind }
/// StemmingTokenizer.TokenizeWithStems(string, Func<string, CultureInfo>)

namespace LexiDiff;

public enum Op { Equal, Insert, Delete }

public sealed record DiffSpan(Op Operation, string Text, IReadOnlyList<SubToken> Subtokens);

public static class DiffWithTokenizer
{
	// Entry point: easiest overload – uses the stemming tokenizer by default.
	public static List<DiffSpan> Diff(string a, string b, Func<string, CultureInfo> detectLang)
		=> Diff(a, b, s => StemmingTokenizer.TokenizeWithStems(s, detectLang));

	// Overload: bring your own tokenizer (string -> IReadOnlyList<SubToken>)
	public static List<DiffSpan> Diff(string a, string b, Func<string, IReadOnlyList<SubToken>> tokenizer)
	{
		if (a is null)
			throw new ArgumentNullException(nameof(a));
		if (b is null)
			throw new ArgumentNullException(nameof(b));
		if (tokenizer is null)
			throw new ArgumentNullException(nameof(tokenizer));

		var toksA = tokenizer(a);
		var toksB = tokenizer(b);

		// ✅ pooling-by-text returns (charsA, charsB)
		var (charsA, charsB) = TokensToChars(toksA, toksB);

		var dmp = new diff_match_patch {
			Match_Threshold = 0.4f,
			Diff_Timeout = 1.0f
		};

		var diffs = dmp.diff_main(charsA, charsB);
		dmp.diff_cleanupSemantic(diffs);

		return MapBack(diffs, toksA, toksB);
	}

	private static (string a, string b) TokensToChars(
		IReadOnlyList<SubToken> toksA,
		IReadOnlyList<SubToken> toksB)
	{
		// Pool by subtoken *text* so equal tokens share the same codepoint
		const int PuaStart = 0xE000, PuaEnd = 0xF8FF;
		var pool = new Dictionary<string, char>(StringComparer.Ordinal);
		int next = PuaStart;

		string Encode(IReadOnlyList<SubToken> toks)
		{
			var sb = new System.Text.StringBuilder(toks.Count);
			foreach (var t in toks)
			{
				if (!pool.TryGetValue(t.Text, out var ch))
				{
					if (next > PuaEnd)
						throw new NotSupportedException("Exceeded PUA capacity; consider chunking or a wider encoding.");
					ch = (char)next++;
					pool[t.Text] = ch;
				}
				sb.Append(ch);
			}
			return sb.ToString();
		}

		return (Encode(toksA), Encode(toksB));
	}

	private static List<DiffSpan> MapBack(
		IList<Diff> diffs,
		IReadOnlyList<SubToken> toksA,
		IReadOnlyList<SubToken> toksB)
	{
		var result = new List<DiffSpan>(diffs.Count);
		int ia = 0, ib = 0;

		void Push(Op op, List<SubToken> bucket)
		{
			if (bucket.Count == 0)
				return;
			var text = string.Concat(bucket.Select(x => x.Text));
			result.Add(new DiffSpan(op, text, bucket.ToArray()));
			bucket.Clear();
		}

		var run = new List<SubToken>();
		Op? runOp = null;

		foreach (var d in diffs)
		{
			var op = d.operation switch {
				Operation.EQUAL => Op.Equal,
				Operation.INSERT => Op.Insert,
				Operation.DELETE => Op.Delete,
				_ => throw new InvalidOperationException()
			};

			if (runOp is not null && runOp.Value != op)
			{ Push(runOp.Value, run); }
			runOp = op;

			// Each char == one subtoken; consume from the correct side(s)
			foreach (var _ in d.text)
			{
				switch (d.operation)
				{
					case Operation.EQUAL:
						run.Add(toksA[ia++]); // prefer A’s token for Equal
						ib++;                  // also advance B in lockstep
						break;
					case Operation.DELETE:
						run.Add(toksA[ia++]);
						break;
					case Operation.INSERT:
						run.Add(toksB[ib++]);
						break;
				}
			}
		}
		if (runOp is not null)
			Push(runOp.Value, run);
		return result;
	}
}
