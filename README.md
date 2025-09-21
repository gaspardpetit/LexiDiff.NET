# LexiDiff

Token-aware text diffs with an objective to favour readability over compactness.

| Pure Leveinstein | LexiDiff |
|------------------|----------|
| Alice was <del>beginning to get</del><ins>getting</ins> very tired <ins>t</ins>o<del>f</del> sit<del>ting</del> by her sister on the bank, <del>and of having</del><ins>with</ins> nothing to do. | Alice was <del>beginning to </del>get<ins>ting</ins> very tired <del>of</del><ins>to</ins> sit<del>ting</del> by her sister on the bank, <del>and of having</del><ins>with</ins> nothing to do. |
--------

Produce readable diffs that **never split randomly inside words**, optionally **promote** changes to **sentence** or **paragraph** granularity, and render as **unified diff** or **inline HTML**.

- ICU word segmentation + Snowball stemming (multi-language)
- Token-aware diff (Diff Match Patch), **no mid-token splits**
- Optional promotion to **Sentence** or **Paragraph**
- Output as **Delete-Add-Replace** sequences,  **Unified Diff** (line-level hunks) or **Inline HTML**

---

## Install

> **Requirements:** .NET 4.8+ / .NET 8 / .NET 9

    dotnet add package LexiDiff

---

## Quick Start

```cs
using System.Globalization;
using LexiDiff;

LexDiffResult result = LexDiff.Compare(
    "Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do.",
    "Alice was getting very tired to sit by her sister on the bank, with nothing to do.");

foreach (var span in result.Spans) {
    switch (span.Op) {
        case LexOp.Insert: Console.Write($"<ins>{span.Text}</ins>"); break;
        case LexOp.Equal:  Console.Write(span.Text);                 break;
        case LexOp.Delete: Console.Write($"<del>{span.Text}</del>"); break;
    }
}
```

The `LexDiffResult` result contains a list of operation (delete, insert, equal) which, when applied onto the original string, generates the second:

> Alice was <del>beginning to </del>get<ins>ting</ins> very tired <del>of</del><ins>to</ins> sit<del>ting</del> by her sister on the bank, <del>and of having</del><ins>with</ins> nothing to do.

Notice that we perform word and stemming aware diff: 
- get<u>ting</u> is allowed (stemming aware) but
- <u>to</u>[f] would have been preferred by Levenshtein distance, but is not allowed here and transformed as ~~of~~ <u>to</u> instead

---

## Granularity Promotion (Sentence / Paragraph)

You can “promote” any in-sentence edits to a **whole-sentence** replacement (or paragraph-level), which is often what reviewers want to see.

```cs
// Sentence-level promotion (locale-aware via ICU)
var sentenceDiff = LexiDiff.LexDiff.Compare(
    a, b,
    new LexiDiff.LexOptions {
        PromoteTo = LexiDiff.LexGranularity.Sentence,
        SentenceCulture = CultureInfo.GetCultureInfo("en-US")
    });

Console.WriteLine(sentenceDiff.ToUnifiedDiff("a.txt", "b.txt"));

// Paragraph-level promotion
var paraDiff = LexiDiff.LexDiff.CompareParagraphs(a, b);
```

> Sentence boundaries use ICU’s Unicode Text Segmentation (UAX #29) with locale tailoring. Paragraphs split on newlines (blank line is its own paragraph).

---

## Why token-aware?

Traditional diffs split anywhere in the character stream. LexiDiff:

- **Segments words with ICU**, so punctuation/whitespace tokens are preserved.
- **Stems with Snowball**, so variants like *Running → Runner* align on **Run**.
- **Diffs on tokens**, so we **never split inside a stem/suffix**.
- Guarantees **perfect reconstruction**: for every token, either a `Whole` token or `(Stem + Suffix)` where `stem + suffix == original`.

This makes deltas cleaner and more meaningful for reviewers.

---

## Known Limitations

- **Unified diff is line-level.** Inline word/suffix highlighting is available via `ToInlineHtml`, not in unified output.
- Snowball’s stemming is heuristic; some languages/words may not split (by design). We preserve the original text regardless.
- Sentence boundaries in may need RBBI tailoring; a light post-filter for abbreviations (`Me.`, `Dr.`, `art.`) is easy to add if needed.

---

## License

MIT (project code). Snowball stemmers are BSD-style; ICU4N follows ICU/Unicode licenses. Review their licenses if redistributing.
