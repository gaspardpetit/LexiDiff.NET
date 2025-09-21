using System.Globalization;

// You already have this:
using ICU4N.Text; // if your IcuWordSegmenter lives here; adjust if needed
				  // using LexiDiff.Tokens; // if you want to adapt to your existing Token type
				  // using LexiDiff.Morphemes; // if you prefer emitting Morphemes instead

public enum SubTokenKind
{
	Whole,   // no split was applied (or non-word tokens)
	Stem,    // stem part of a word
	Suffix   // suffix part of a word
}

public sealed class SubToken
{
	public int ParentIndex { get; }     // index of the parent ICU token in the original stream
	public int Start { get; }           // absolute char index in the input string
	public int Length { get; }
	public string Text { get; }
	public SubTokenKind Kind { get; }

	public SubToken(int parentIndex, int start, int length, string text, SubTokenKind kind)
	{
		ParentIndex = parentIndex;
		Start = start;
		Length = length;
		Text = text;
		Kind = kind;
	}

	public override string ToString() => $"{Kind}:{Text}@{Start}+{Length} (p={ParentIndex})";
}
