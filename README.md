[![Build & Tests](https://github.com/gaspardpetit/LexiDiff.NET/actions/workflows/run-tests.yml/badge.svg)](https://github.com/gaspardpetit/LexiDiff.NET/actions/workflows/run-tests.yml)
[![NuGet](https://img.shields.io/nuget/v/LexiDiff.svg)](https://www.nuget.org/packages/LexiDiff)

# LexiDiff

Token-aware text diffs that favour readability over compactness.

Deletions are shown with strikethrough; additions are shown in bold.

| Pure Levenshtein | LexiDiff |
|------------------|----------|
| Alice was ~~beginning to get~~**getting** very tired **t**o~~f~~ sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do. | Alice was ~~beginning to~~ get**ting** very tired ~~of~~**to** sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do. |

--------

LexiDiff compares words and their stems, with optional sentence or paragraph granularity.

- Language-aware word segmentation with ICU4N and stemming with Snowball
- Edits aligned to whole words, stems, and suffixes
- Sentence and paragraph comparison
- Equal, insert, and delete spans, with unified diff and inline HTML output

---

## Install

> **Compatibility:** Targets .NET Standard 2.0; consumable by .NET Framework 4.8 and .NET 8, 9, and 10.

    dotnet add package LexiDiff

---

## Quick Start

```cs
using LexiDiff;

LexiDiffResult result = Lexi.Compare(
    "Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do.",
    "Alice was getting very tired to sit by her sister on the bank, with nothing to do.");

Console.WriteLine(result.ToInlineHtml());
```

`LexiDiffResult.Spans` contains equal, insert, and delete spans. `ApplyTo(original)` applies the changes to the original text. `ToInlineHtml()` HTML-encodes the text and wraps changes in `<del>` and `<ins>` tags.

The example highlights these changes:

> Alice was ~~beginning to~~ get**ting** very tired ~~of~~**to** sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do.

The shared stem in get**ting** stays unchanged. Replacing *of* with *to* produces ~~of~~**to**, keeping both words intact.

---

## Granularity Promotion (Sentence / Paragraph)

Use sentence or paragraph granularity to show each changed sentence or paragraph as a whole replacement.

```cs
using System.Globalization;
using LexiDiff;

var a = "A sentence. Next entry stays.";
var b = "A new sentence. Next entry stays.";

// Compare whole sentences using English sentence boundaries
var sentenceDiff = Lexi.Compare(
    a, b,
    new LexiOptions {
        PromoteTo = LexGranularity.Sentence,
        SentenceCulture = CultureInfo.GetCultureInfo("en-US")
    });

Console.WriteLine(sentenceDiff.ToUnifiedDiff("a.txt", "b.txt"));

// Paragraph-level promotion
var paraDiff = Lexi.CompareParagraphs(a, b);
```

Sentence boundaries use ICU4N with the specified culture. Paragraphs split at each newline; a blank line forms a separate paragraph.

Word segmentation and stemming default to English (`en-US`). To use another language, supply a culture-selection callback through `LexiOptions.DetectLang`. Set `SentenceCulture` for sentence boundaries; it defaults to the invariant culture.

---

## Why token-aware?

LexiDiff uses ICU4N to identify words and Snowball to find stems. A word such as *cats* can split into **cat** + **s**, allowing suffix changes to appear independently. Edits preserve these token boundaries.

Original spelling, punctuation, and whitespace are preserved. Use `ReconstructA()` and `ReconstructB()` to recover the input texts.

---

## Known Limitations

- Unified diffs show changes by line. Use `ToInlineHtml()` for word and suffix highlighting.
- Snowball’s stemming is heuristic. Words without a usable stem split remain whole.
- Sentence boundaries around abbreviations (`Me.`, `Dr.`, `art.`) may require additional tailoring for specialized text.

---

## License

LexiDiff is licensed under [Apache-2.0](https://github.com/gaspardpetit/LexiDiff.NET/blob/main/LICENSE).

Dependency licenses:

- Snowball: [BSD-3-Clause](https://github.com/snowballstem/snowball/blob/v3.2.0/COPYING)
- ICU4N: [license and third-party notices](https://github.com/NightOwl888/ICU4N/blob/main/LICENSE.txt)
