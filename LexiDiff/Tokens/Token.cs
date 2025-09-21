namespace LexiDiff.Tokens;

public enum WordKind
{
	Word,        // letters / marks / numbers mixed (ICU "word-ish")
	Number,      // purely numeric token
	Punctuation, // .,;:!?—… () [] {} etc.
	Symbol,      // currency, math, emoji, etc.
	Whitespace,  // spaces/tabs (rarely emitted; we usually skip)
	Other
}

public sealed record Token(
	string Text,
	int Start,              // UTF-16 index in original string
	int Length,
	WordKind Kind
)
{
	public override string ToString() => $"{Kind}:{Text}@{Start}+{Length}";
}
