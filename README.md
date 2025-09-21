# LexiDiff

Token-aware text diffs with stemming. Produce readable diffs that **never split inside words**, optionally **promote** changes to **sentence** or **paragraph** granularity, and render as **unified diff** or **inline HTML**.

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

    using System.Globalization;
    using LexiDiff;
    
    // Inputs
    var a = "Running, quickly! Next sentence stays.";
    var b = "Runner, quicker! Next sentence stays.";
    
    // 1) Compute token-aware diff (default granularity = tokens)
    var result = LexiDiff.LexDiff.Compare(a, b);
    
    // 2) Render unified diff (line-level)
    string udiff = result.ToUnifiedDiff("a.txt", "b.txt", context: 3);
    Console.WriteLine(udiff);
    
    // 3) Render inline HTML (word/subword-aware highlighting)
    string html = result.ToInlineHtml();
    Console.WriteLine(html);
    
    // 4) Apply to reconstruct B (throws if A doesn't match the patch)
    string b2 = result.ApplyTo(a); // == b

---

## What you get

### Unified Diff (line hunks)
Outputs standard unified diff with `@@` headers and `-`/`+`/context lines (no word-inline marks). Because the *compute* step is token-aware, the changed lines reflect **intentional word-level changes**, but the **rendering** is still line-level for compatibility with standard diff tooling.

    --- a.txt
    +++ b.txt
    @@ -1,3 +1,3 @@
    -Running, quickly!
    +Runner, quicker!
     Next sentence stays.

### Inline HTML (word/subword)
A minimal inline rendering: deletions wrapped in `<del>…</del>`, insertions in `<ins>…</ins>`. Text is HTML-encoded. (Style with your CSS.)

    <del>Running, quickly!</del><ins>Runner, quicker!</ins> Next sentence stays.

---

## Granularity Promotion (Sentence / Paragraph)

You can “promote” any in-sentence edits to a **whole-sentence** replacement (or paragraph-level), which is often what reviewers want to see.

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

> Sentence boundaries use ICU’s Unicode Text Segmentation (UAX #29) with locale tailoring. Paragraphs split on newlines (blank line is its own paragraph).

---

## API Overview

### Entry point

    LexDiffResult LexiDiff.LexDiff.Compare(string a, string b, LexOptions? options = null)
    LexDiffResult LexiDiff.LexDiff.CompareSentences(string a, string b, CultureInfo? culture = null)
    LexDiffResult LexiDiff.LexDiff.CompareParagraphs(string a, string b)

### Result

    public sealed record LexDiffResult(IReadOnlyList<LexSpan> Spans)
    {
        string ReconstructA();          // Equal + Delete
        string ReconstructB();          // Equal + Insert
        string ApplyTo(string a);       // throws if A mismatch
        string ToUnifiedDiff(string aLabel = "a", string bLabel = "b", int context = 3);
        string ToInlineHtml();
    }
    
    public sealed record LexSpan(LexOp Op, string Text);
    public enum LexOp { Equal, Insert, Delete }

### Options

    public sealed record LexOptions
    {
        public LexGranularity PromoteTo { get; init; } = LexGranularity.Tokens;
        public CultureInfo? SentenceCulture { get; init; } // when PromoteTo = Sentence
        public Func<string, CultureInfo>? DetectLang { get; init; } // default: en-US
    }
    
    public enum LexGranularity { Tokens, Sentence, Paragraph }

**Language detection:** By default, words are stemmed as **en-US**. Provide a detector to switch language per word:

    var opts = new LexiDiff.LexOptions {
        DetectLang = w => w.Any(c => c >= 0x80) ? CultureInfo.GetCultureInfo("fr-FR")
                                                : CultureInfo.GetCultureInfo("en-US")
    };
    
    var r = LexiDiff.LexDiff.Compare(a, b, opts);

---

## Why token-aware?

Traditional diffs split anywhere in the character stream. LexiDiff:

- **Segments words with ICU**, so punctuation/whitespace tokens are preserved.
- **Stems with Snowball**, so variants like *Running → Runner* align on **Run**.
- **Diffs on tokens**, so we **never split inside a stem/suffix**.
- Guarantees **perfect reconstruction**: for every token, either a `Whole` token or `(Stem + Suffix)` where `stem + suffix == original`.

This makes deltas cleaner and more meaningful for reviewers.

---

## Performance Tips

- Very large inputs: diff in **chunks** (e.g., per paragraph) and merge.
- If you use **sentence promotion**, pass the correct **locale** for best boundaries (e.g., `fr-CA` for Canadian French).
- The unified formatter is **line-level** and streams fine; for HTML, render incrementally if needed.

---

## Known Limitations

- **Unified diff is line-level.** Inline word/suffix highlighting is available via `ToInlineHtml`, not in unified output.
- Snowball’s stemming is heuristic; some languages/words may not split (by design). We preserve the original text regardless.
- Sentence boundaries in may need RBBI tailoring; a light post-filter for abbreviations (`Me.`, `Dr.`, `art.`) is easy to add if needed.

---

## Tests

- Tokenization + stemming split/reconstruction (perfect reconstruction invariant)
- Diff correctness (no mid-token splits; stem preservation across edits)
- Promotion (sentence/paragraph) reconstruction and shape
- Unified diff formatting invariants and content

Run:

    dotnet test

---

## FAQ

**Q: Does unified diff show word-level inside a line?**  
A: No. Unified diff is **line-level** by spec. Use `ToInlineHtml()` for word/subword highlighting, or implement an inline text formatter for terminals (ANSI).

**Q: Can I plug my own tokenizer?**  
A: The public API is intentionally lean. Internally we use ICU + Snowball; if you need a custom pipeline, adapt `DiffWithTokenizer` and keep the facade.

**Q: Does it work without ICU?**  
A: The quality of tokenization relies on ICU4N. You can swap the segmenter, but cross-language behavior will degrade.

---

## License

MIT (project code). Snowball stemmers are BSD-style; ICU4N follows ICU/Unicode licenses. Review their licenses if redistributing.
