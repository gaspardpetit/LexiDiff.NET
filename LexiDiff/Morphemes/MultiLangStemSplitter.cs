using Snowball;
using System;
using System.Collections.Generic;
using System.Globalization;

// If you use Lucene's Snowball instead, swap the using + factory below accordingly.

public static class MultiLangStemSplitter
{
	/// <summary>
	/// Split a single word into (stem, suffix) using language detection + Snowball.
	/// Returns (word, "") if no reliable split.
	/// </summary>
	public static (string Stem, string Suffix) Split(string word, Func<string, CultureInfo>? detectLang)
	{
		if (string.IsNullOrWhiteSpace(word))
			return (word, "");

		var culture = detectLang?.Invoke(word) ?? CultureInfo.GetCultureInfo("en");
		var stemmer = StemmerFactory.Create(culture);

		var original = word;
		if (stemmer is null)
			return (original, "");

		// Normalize for stemming (Snowball is case-sensitive; use lower)
		var lower = word.ToLowerInvariant();

		stemmer.Current = lower;
		if (!stemmer.Stem())
			return (original, "");

		var stemLower = stemmer.Current;

		// Use the longest common prefix to preserve original casing/diacritics in the slice.
		var prefixLen = LongestCommonPrefix(original, stemLower, ignoreCase: true);

		// Guardrails: if the “stem” is not a prefix or is too short to be useful, bail out.
		if (prefixLen <= 1)
			return (original, "");

		var stem = original.Substring(0, prefixLen);
		var suffix = original.Substring(prefixLen);

		return (stem, suffix);
	}

	private static int LongestCommonPrefix(string a, string b, bool ignoreCase)
	{
		var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var max = Math.Min(a.Length, b.Length);
		int i = 0;
		while (i < max && string.Compare(a, i, b, i, 1, cmp) == 0)
			i++;
		return i;
	}
}
