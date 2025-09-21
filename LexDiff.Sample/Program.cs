using LexiDiff;
using System;
using System.Globalization;

namespace LexiDiff;

class Demo
{
	static void Main()
	{
		var a = "Running, quickly! Next sentence stays.";
		var b = "Runner, quicker! Next sentence stays.";

		// Token-level (default)
		var tokenPatch = LexDiffer.Patch(a, b);
		Console.WriteLine("Token-level:");
		foreach (var p in tokenPatch.Spans)
			Console.WriteLine(p);
		Console.WriteLine("Apply -> " + tokenPatch.ApplyTo(a));

		// Sentence-level promotion (ICU-aware)
		var opts = new LexDiffOptions {
			PromoteTo = Granularity.Sentence,
			SentenceCulture = CultureInfo.GetCultureInfo("en-US")
		};
		var sentPatch = LexDiffer.Patch(a, b, opts);
		Console.WriteLine("\nSentence-level:");
		foreach (var p in sentPatch.Spans)
			Console.WriteLine(p);
		Console.WriteLine("Apply -> " + sentPatch.ApplyTo(a));
	}
}
