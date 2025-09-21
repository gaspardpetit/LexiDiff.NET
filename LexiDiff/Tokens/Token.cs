using System;

namespace LexiDiff.Tokens;

public enum WordKind
{
    Word,
    Number,
    Punctuation,
    Symbol,
    Whitespace,
    Other
}

public enum TokenRole
{
    Whole,
    Stem,
    Suffix
}

public sealed class Token
{
    public Token(int parentIndex, int start, int length, string text, WordKind kind, TokenRole role = TokenRole.Whole)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        ParentIndex = parentIndex;
        Start = start;
        Length = length;
        Text = text;
        Kind = kind;
        Role = role;
    }

    public int ParentIndex { get; }

    public int Start { get; }

    public int Length { get; }

    public string Text { get; }

    public WordKind Kind { get; }

    public TokenRole Role { get; }

    public override string ToString() => $"{Kind}/{Role}:{Text}@{Start}+{Length} (p={ParentIndex})";
}
