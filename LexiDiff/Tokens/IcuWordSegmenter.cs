using ICU4N.Text;
using System.Globalization;
using System.Text;
using System;
using System.Collections.Generic;


namespace LexiDiff.Tokens;

public interface IWordSegmenter
{
	IReadOnlyList<Token> Tokenize(string text);
}

public sealed class IcuWordSegmenter : IWordSegmenter
{
	private readonly CultureInfo _culture;
	private readonly bool _emitPunctuation;
	private readonly NormalizationForm? _normalizeTo;
	private readonly bool _coalescePunctWithFollowingSpace;

	public IcuWordSegmenter(
		string locale = "und",
		bool emitPunctuation = true,
		bool coalescePunctWithFollowingSpace = true,
		NormalizationForm? normalizeTo = null)
	{
		_culture = TryGetCulture(locale) ?? CultureInfo.InvariantCulture;
		_emitPunctuation = emitPunctuation;
		_coalescePunctWithFollowingSpace = coalescePunctWithFollowingSpace;
		_normalizeTo = normalizeTo; // keep null for reversibility
	}

	public IReadOnlyList<Token> Tokenize(string text)
	{
		if (text is null)
			throw new ArgumentNullException(nameof(text));
		if (text.Length == 0)
			return Array.Empty<Token>();
		if (_normalizeTo is not null)
			text = text.Normalize(_normalizeTo.Value); // usually null for exact round-trip

		var bi = BreakIterator.GetWordInstance(_culture);
		bi.SetText(text);

		// Emit EVERYTHING: words, punctuation, symbols, whitespace — in order.
		var raw = new List<Token>(Math.Min(256, text.Length / 3 + 1));
		int start = bi.First();
		for (int end = bi.Next(); end != BreakIterator.Done; start = end, end = bi.Next())
		{
			if (end <= start)
				continue;
			string span = text.Substring(start, end - start);
			var kind = Classify(span);
			if (!_emitPunctuation && (kind == WordKind.Punctuation || kind == WordKind.Symbol))
				continue;
			raw.Add(new Token(span, start, end - start, kind));
		}

		if (!_coalescePunctWithFollowingSpace)
			return raw;

		// Merge a punctuation/symbol immediately followed by whitespace into one token.
		var merged = new List<Token>(raw.Count);
		for (int i = 0; i < raw.Count; i++)
		{
			var t = raw[i];
			if ((t.Kind == WordKind.Punctuation || t.Kind == WordKind.Symbol) &&
				i + 1 < raw.Count && raw[i + 1].Kind == WordKind.Whitespace &&
				t.Start + t.Length == raw[i + 1].Start)
			{
				var next = raw[i + 1];
				merged.Add(new Token(
					t.Text + next.Text,
					t.Start,
					t.Length + next.Length,
					WordKind.Punctuation)); // keep as punctuation
				i++; // skip the whitespace token we fused
			}
			else
			{
				merged.Add(t);
			}
		}
		return merged;
	}

	private static CultureInfo? TryGetCulture(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag))
			return null;
		try
		{ return CultureInfo.GetCultureInfo(tag); }
		catch { return null; }
	}


	private static WordKind Classify(string span)
	{
		bool hasLetter = false, hasMark = false, hasNumber = false;
		bool hasPunct = false, hasSymbol = false, hasSpace = false;

		for (int i = 0; i < span.Length; i++)
		{
			var uc = char.GetUnicodeCategory(span, i);
			switch (uc)
			{
				case UnicodeCategory.UppercaseLetter:
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
					hasLetter = true;
					break;

				case UnicodeCategory.NonSpacingMark:
				case UnicodeCategory.SpacingCombiningMark:
				case UnicodeCategory.EnclosingMark:
					hasMark = true;
					break;

				case UnicodeCategory.DecimalDigitNumber:
				case UnicodeCategory.LetterNumber:
				case UnicodeCategory.OtherNumber:
					hasNumber = true;
					break;

				case UnicodeCategory.SpaceSeparator:
				case UnicodeCategory.LineSeparator:
				case UnicodeCategory.ParagraphSeparator:
					hasSpace = true;
					break;

				case UnicodeCategory.ConnectorPunctuation:
				case UnicodeCategory.DashPunctuation:
				case UnicodeCategory.OpenPunctuation:
				case UnicodeCategory.ClosePunctuation:
				case UnicodeCategory.InitialQuotePunctuation:
				case UnicodeCategory.FinalQuotePunctuation:
				case UnicodeCategory.OtherPunctuation:
					hasPunct = true;
					break;

				case UnicodeCategory.MathSymbol:
				case UnicodeCategory.CurrencySymbol:
				case UnicodeCategory.ModifierSymbol:
				case UnicodeCategory.OtherSymbol:
					hasSymbol = true;
					break;
			}

			// Skip low surrogate increment; char.GetUnicodeCategory handles pairs
		}

		if (hasSpace && !(hasLetter || hasNumber || hasMark || hasPunct || hasSymbol))
			return WordKind.Whitespace;

		if ((hasLetter || hasMark) && !hasPunct && !hasSymbol)
			return WordKind.Word;

		if (hasNumber && !hasLetter && !hasMark && !hasPunct && !hasSymbol)
			return WordKind.Number;

		if (hasPunct && !hasLetter && !hasMark && !hasNumber && !hasSymbol)
			return WordKind.Punctuation;

		if (hasSymbol && !hasLetter && !hasMark && !hasNumber && !hasPunct)
			return WordKind.Symbol;

		// Mixed content falls back to Word (e.g., “C3PO”, “naïve-bayes”)
		if (hasLetter || hasMark || hasNumber)
			return WordKind.Word;

		return WordKind.Other;
	}
}
