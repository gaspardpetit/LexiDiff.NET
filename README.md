[![Build & Tests](https://github.com/gaspardpetit/LexiDiff.NET/actions/workflows/run-tests.yml/badge.svg)](https://github.com/gaspardpetit/LexiDiff.NET/actions/workflows/run-tests.yml)
[![NuGet](https://img.shields.io/nuget/v/LexiDiff.svg)](https://www.nuget.org/packages/LexiDiff)


# LexiDiff

Token-aware text diffs with an objective to favour readability over compactness.

Deletions are shown with strikethrough; additions are shown in bold.

| Pure Levenshtein | LexiDiff |
|------------------|----------|
| Alice was ~~beginning to get~~**getting** very tired **t**o~~f~~ sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do. | Alice was ~~beginning to~~ get**ting** very tired ~~of~~**to** sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do. |

--------

Produce readable diffs that **never split randomly inside words**, optionally **promote** changes to **sentence** or **paragraph** granularity, and render as **unified diff** or **inline HTML**.

- ICU word segmentation + Snowball stemming (multi-language)
- Token-aware diff (Diff Match Patch), **no mid-token splits**
- Optional promotion to **Sentence** or **Paragraph**
- Output as **Delete-Add-Replace** sequences,  **Unified Diff** (line-level hunks) or **Inline HTML**

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

The `LexiDiffResult` contains delete, insert, and equal spans. `ApplyTo(original)` reconstructs the modified text; `ToInlineHtml()` HTML-encodes the text and highlights the changes:

> Alice was ~~beginning to~~ get**ting** very tired ~~of~~**to** sit~~ting~~ by her sister on the bank, ~~and of having~~**with** nothing to do.

Notice that we perform word and stemming aware diff: 

- get**ting** is allowed (stemming aware) but
- **t**o~~f~~ would have been preferred by Levenshtein distance, but is not allowed here and transformed as ~~of~~**to** instead

---

## Granularity Promotion (Sentence / Paragraph)

You can “promote” any in-sentence edits to a **whole-sentence** replacement (or paragraph-level), which is often what reviewers want to see.

```cs
using System.Globalization;
using LexiDiff;

var a = "A sentence. Next entry stays.";
var b = "A new sentence. Next entry stays.";

// Sentence-level promotion (locale-aware via ICU)
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

> Sentence boundaries use ICU’s Unicode Text Segmentation (UAX #29) with locale tailoring. Paragraphs split on newlines (blank line is its own paragraph).

Language selection defaults to English. Supply `LexiOptions.DetectLang` to select the culture used for word segmentation and stemming; the library does not detect languages automatically. Set `SentenceCulture` separately for sentence boundaries.

---

## Why token-aware?

Traditional diffs split anywhere in the character stream. LexiDiff:

- **Segments words with ICU**, so punctuation/whitespace tokens are preserved.
- **Stems with Snowball**, so a word such as *cats* can split into **cat** + **s**.
- **Diffs on tokens**, so we **never split inside a stem/suffix**.
- Guarantees **perfect reconstruction**: for every token, either a `Whole` token or `(Stem + Suffix)` where `stem + suffix == original`.

This makes deltas cleaner and more meaningful for reviewers.

---

## Known Limitations

- **Unified diff is line-level.** Inline word/suffix highlighting is available via `ToInlineHtml`, not in unified output.
- Snowball’s stemming is heuristic; some languages/words may not split (by design). We preserve the original text regardless.
- Sentence boundaries around abbreviations (`Me.`, `Dr.`, `art.`) may require additional tailoring for specialized text.

---

## License

Project code is licensed under [Apache-2.0](https://github.com/gaspardpetit/LexiDiff.NET/blob/main/LICENSE), matching the NuGet package metadata. Snowball is distributed under [BSD-3-Clause](https://github.com/snowballstem/snowball/blob/v3.2.0/COPYING). ICU4N includes its own [license and third-party notices](https://github.com/NightOwl888/ICU4N/blob/main/LICENSE.txt).
