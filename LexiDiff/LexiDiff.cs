using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using LexiDiff.Tokens;

namespace LexiDiff;

// Core result
public sealed record LexDiffResult(
	IReadOnlyList<LexSpan> Spans
)
{
	// A (Equal+Delete) and B (Equal+Insert) reconstructions
	public string ReconstructA() => string.Concat(Spans.Where(s => s.Op != LexOp.Insert).Select(s => s.Text));
	public string ReconstructB() => string.Concat(Spans.Where(s => s.Op != LexOp.Delete).Select(s => s.Text));

	// Apply to A -> B (throws if A mismatch)
	public string ApplyTo(string sourceA)
	{
		var a = ReconstructA();
		if (!string.Equals(sourceA, a, StringComparison.Ordinal))
			throw new InvalidOperationException("Patch set does not match source A.");
		return ReconstructB();
	}

	// Renderers (line-level unified, or inline HTML)
	public string ToUnifiedDiff(string aLabel = "a", string bLabel = "b", int context = 3)
		=> LexRender.UnifiedDiff(this, aLabel, bLabel, context);

	public string ToInlineHtml() // word/subword-level highlight
		=> LexRender.InlineHtml(this);
}

// One span, nothing else
public sealed record LexSpan(LexOp Op, string Text);
public enum LexOp { Equal, Insert, Delete }

// Minimal options
public sealed record LexOptions
{
	// none = token-aware diff
	public LexGranularity PromoteTo { get; init; } = LexGranularity.Tokens;
	public CultureInfo? SentenceCulture { get; init; } // only if PromoteTo=Sentence
	public Func<string, CultureInfo>? DetectLang { get; init; } // defaults to en-US
}

public enum LexGranularity { Tokens, Sentence, Paragraph }

// Single entry point
public static class LexDiff
{
	// The 90% use-case: token-aware diff with optional promotion
	public static LexDiffResult Compare(string a, string b, LexOptions? options = null, Func<string, IReadOnlyList<Token>>? tokenizer = null)
	{
		options ??= new LexOptions();
		var detect = options.DetectLang ?? (_ => CultureInfo.GetCultureInfo("en-US"));

		// 1) token-aware diff
		if (tokenizer == null)
		{
			StemmingTokenizer stemmingTokenizer = new StemmingTokenizer(detect);
			tokenizer = s => stemmingTokenizer.Tokenize(s);
		}
		var fine = DiffWithTokenizer.Diff(a, b, tokenizer);

		// 2) optional promotion
		List<DiffSpan> chosen = options.PromoteTo switch {
			LexGranularity.Sentence => DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Sentence, options.SentenceCulture ?? CultureInfo.InvariantCulture),
			LexGranularity.Paragraph => DiffPostProcessor.Promote(a, b, fine, DiffGranularity.Paragraph),
			_ => fine
		};

		// 3) project to lean spans
		var spans = chosen.Select(d => new LexSpan(
			d.Operation switch { Op.Equal => LexOp.Equal, Op.Insert => LexOp.Insert, Op.Delete => LexOp.Delete, _ => throw new() },
			d.Text
		)).ToList();

		return new LexDiffResult(spans);
	}

	// Convenience shorthands
	public static LexDiffResult CompareSentences(string a, string b, CultureInfo? culture = null)
		=> Compare(a, b, new LexOptions { PromoteTo = LexGranularity.Sentence, SentenceCulture = culture ?? CultureInfo.InvariantCulture });

	public static LexDiffResult CompareParagraphs(string a, string b)
		=> Compare(a, b, new LexOptions { PromoteTo = LexGranularity.Paragraph });
}

// Renderers live in one place (keeps result simple)
internal static class LexRender
{
	public static string UnifiedDiff(LexDiffResult result, string aLabel, string bLabel, int context)
		=> LexPatchFormatter.ToUnifiedDiff(result.ReconstructA(), result.ReconstructB(),
		   // adaptor to existing formatter
		   new LexPatchSet(result.Spans.Select(s => new LexPatch(
			   s.Op switch { LexOp.Equal => PatchOp.Equal, LexOp.Insert => PatchOp.Insert, LexOp.Delete => PatchOp.Delete, _ => throw new() },
			   s.Text)).ToList()),
		   aLabel, bLabel, context);

	public static string InlineHtml(LexDiffResult result)
	{
		// super-simple default: wrap + in <ins>, - in <del>, = passthrough.
		// (You already have token/subtoken info internally if you want richer highlighting later.)
		var sb = new System.Text.StringBuilder();
		foreach (var s in result.Spans)
		{
			switch (s.Op)
			{
				case LexOp.Equal:
					sb.Append(System.Net.WebUtility.HtmlEncode(s.Text));
					break;
				case LexOp.Insert:
					sb.Append("<ins>").Append(System.Net.WebUtility.HtmlEncode(s.Text)).Append("</ins>");
					break;
				case LexOp.Delete:
					sb.Append("<del>").Append(System.Net.WebUtility.HtmlEncode(s.Text)).Append("</del>");
					break;
			}
		}
		return sb.ToString();
	}
}

