# Agent Notes

- LexiDiff tokenizes text using ICU and Snowball to support lexicographic diffing.
- Locale fidelity matters: components should flow the detected `CultureInfo` through to `IcuWordSegmenter` so ICU can load language-specific word break rules.
- When adding or updating tests, lean into themes around lexicography, semantics, etymology, and cross-cultural writing practices. Prefer representative samples from multiple scripts and justify locale choices in assertions.
- For languages without whitespace word boundaries (Thai, Khmer, Chinese, etc.), rely on ICU dictionaries and use `\uXXXX` escapes to keep test sources ASCII unless a file already carries UTF-8 literals.
- `StemmingTokenizer` now exposes `CreateSegmenter` for customization; override it in tests to observe or stub locale-specific behavior without rewriting production code.
- Run `dotnet test` from the repo root; if the console runner stays silent, inspect the `LexDiff.Tests` output type and ensure the xUnit console runner is engaged when adding new suites.