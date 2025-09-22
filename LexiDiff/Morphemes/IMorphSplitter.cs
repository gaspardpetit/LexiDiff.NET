using System.Collections.Generic;

namespace LexiDiff.Morphemes;

// What type of sub-token it is when we split a word token.
public enum MorphemeKind { Stem, Suffix, Prefix, WholeWord }

// A morpheme is a *view* onto an existing base token: no text loss.
public sealed record Morpheme(
	int BaseTokenIndex,     // index into the lossless token list
	int StartOffsetInToken, // 0-based, relative to that token
	int Length,
	MorphemeKind Kind,
	string Text             // convenience, equals baseToken.Text.Substring(...)
);

// Splitter interface: given base tokens + culture, produce morphemes.
public interface IMorphSplitter
{
	// For each base token i, return either:
	//   - one Morpheme WholeWord (no split), or
	//   - 2+ Morphemes (Stem, Suffix[, ...]) that exactly cover the token.
	IReadOnlyList<IReadOnlyList<Morpheme>> Split(
		IReadOnlyList<Tokens.Token> baseTokens,
		System.Globalization.CultureInfo culture);
}
