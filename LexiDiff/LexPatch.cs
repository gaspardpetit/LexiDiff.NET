// LexDiff API surface
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LexiDiff.Tokens;

namespace LexiDiff;

public enum PatchOp { Equal, Insert, Delete }

public sealed class LexPatch
{
	public PatchOp Op { get; }
	public string Text { get; }

	public LexPatch(PatchOp? op, string text)
	{
		Op = op ?? throw new ArgumentNullException(nameof(op));
		Text = text ?? throw new ArgumentNullException(nameof(text));
	}

	public override string ToString() => $"{Op}: \"{Text}\"";
}

public sealed class LexPatchSet
{
	public IReadOnlyList<LexPatch> Spans { get; }

	public LexPatchSet(IReadOnlyList<LexPatch> spans)
	{
		Spans = spans ?? throw new ArgumentNullException(nameof(spans));
	}

	/// Apply to source A to obtain B (Equal+Insert).
	public string ApplyTo(string sourceA)
	{
		// (Optional) safety: ensure A matches Equal+Delete reconstruction.
		// Comment out if you don’t want the guard.
		var reconA = string.Concat(Spans.Where(s => s.Op != PatchOp.Insert).Select(s => s.Text));
		if (!string.Equals(sourceA, reconA, StringComparison.Ordinal))
			throw new InvalidOperationException("Patch set does not match source A.");

		return string.Concat(Spans.Where(s => s.Op != PatchOp.Delete).Select(s => s.Text));
	}

	public override string ToString() => string.Join("", Spans.Select(s => s.ToString()));
}

public enum Granularity { Tokens, Sentence, Paragraph }

public sealed class LexDiffOptions
{
	/// Word→Culture detector (defaults to en-US).
	public Func<string, CultureInfo> DetectLang { get; set; } =
		_ => CultureInfo.GetCultureInfo("en-US");

	/// Optional promotion level (defaults to Tokens = none).
	public Granularity PromoteTo { get; set; } = Granularity.Tokens;

	/// Culture for sentence segmentation (if PromoteTo=Sentence). Defaults to Invariant.
	public CultureInfo SentenceCulture { get; set; } = CultureInfo.InvariantCulture;

	/// Optional custom tokenizer. If null, uses StemmingTokenizer.TokenizeWithStems.
	public Func<string, IReadOnlyList<Token>>? Tokenizer { get; set; }
}

public static class LexDiffer
{
	/// Compute a token-aware patch from A→B.
	public static LexPatchSet Patch(string a, string b, LexDiffOptions? options = null)
	{
		if (a == null)
			throw new ArgumentNullException(nameof(a));
		if (b == null)
			throw new ArgumentNullException(nameof(b));
		options ??= new LexDiffOptions();

		// Tokenizer selection
		Func<string, IReadOnlyList<Token>>? tokenizer = options.Tokenizer;
		if (tokenizer == null)
		{
			var stemmingTokenizer = new StemmingTokenizer(options.DetectLang);
			tokenizer = s => stemmingTokenizer.Tokenize(s);
		}

		// Fine-grained diff (respects subtokens)
		var fine = DiffWithTokenizer.Diff(a, b, tokenizer);

		// Optional promotion
		List<DiffSpan> chosen;
		switch (options.PromoteTo)
		{
			case Granularity.Sentence:
				chosen = DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Sentence, options.SentenceCulture);
				break;
			case Granularity.Paragraph:
				chosen = DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Paragraph);
				break;
			default:
				chosen = fine;
				break;
		}

		// Convert to public Patch model
		var patches = chosen.Select(d => new LexPatch(ToPatchOp(d.Operation), d.Text)).ToList();
		return new LexPatchSet(patches);
	}

	private static PatchOp ToPatchOp(Op op)
	{
		switch (op)
		{
			case Op.Equal:
				return PatchOp.Equal;
			case Op.Insert:
				return PatchOp.Insert;
			case Op.Delete:
				return PatchOp.Delete;
			default:
				throw new ArgumentOutOfRangeException(nameof(op));
		}
	}
}

