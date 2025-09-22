using LexiDiff.DiffMatchPatch;
using LexiDiff.Tokens;
using System.Globalization;
using System.Text;

namespace LexiDiff.Sample;


class Demo
{
	private static string dmp_to_str(List<Diff> diffs)
	{
		StringBuilder str = new StringBuilder();
		foreach (Diff diff in diffs)
		{
			switch (diff.operation)
			{
				case Operation.INSERT:
					str.Append("\x1b[4m");
					str.Append(diff.text);
					str.Append("\x1b[24m");
					break;
				case Operation.EQUAL:
					str.Append(diff.text);
					break;
				case Operation.DELETE:
					str.Append("[");
					str.Append(diff.text);
					str.Append("]");
					break;
			}
		}
		return str.ToString();
	}

	private static object result_to_str(LexiDiffResult result)
	{
		StringBuilder str = new StringBuilder();
		foreach (var span in result.Spans)
		{
			switch (span.Op)
			{
				case LexOp.Insert:
					str.Append("\x1b[4m");
					str.Append(span.Text);
					str.Append("\x1b[24m");
					break;
				case LexOp.Equal:
					str.Append(span.Text);
					break;
				case LexOp.Delete:
					str.Append("[");
					str.Append(span.Text);
					str.Append("]");
					break;
			}
		}
		return str.ToString();
	}

	static void Main()
	{
		string v1 = "Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do.";
		string v2 = "Alice was getting very tired to sit by her sister on the bank, with nothing to do.";

		Console.WriteLine("This example demonstrates how minor text differences are tracked.\n");

		{
			Console.WriteLine("\nThis is how a pure Levenshtein approach would diff the text with a character-level tokenization:\n");

			diff_match_patch dmp = new diff_match_patch { Match_Threshold = 0.4f, Diff_Timeout = 1.0f };
			List<Diff> diffs = dmp.diff_main(v1, v2, false);
			dmp.diff_cleanupSemantic(diffs);
			Console.WriteLine($"< {v1}");
			Console.WriteLine($"> {v2}");
			Console.WriteLine($"= {dmp_to_str(diffs)}\n");
			Console.WriteLine("Notice the \"\u001b[4mt\u001b[24mo[f]\" - Pure Levenshtein tends to recycle part of unrelated words. Here, \"\u001b[4mto\u001b[24m[of]\" would have been preferable.\n");
		}

		{
			Console.WriteLine("What if we perform Levenshtein at the word level:\n");

			IcuWordSegmenter workTokenizer = new IcuWordSegmenter();
			IReadOnlyList<Token> tok1 = workTokenizer.Tokenize(v1);
			IReadOnlyList<Token> tok2 = workTokenizer.Tokenize(v2);
			Console.WriteLine($"< {string.Join("|", tok1.Select(t => t.Text))}");
			Console.WriteLine($"> {string.Join("|", tok2.Select(t => t.Text))}");

			LexiDiffResult result = Lexi.Compare(v1, v2, null, workTokenizer.Tokenize);
			Console.WriteLine($"= {result_to_str(result)}");
			Console.WriteLine("\nThis is better, but it would be even clearer if we allowed some words to change when the stem is the same, ex. get -> get\u001b[4mting\u001b[24m.\n");
		}

		{
			Console.WriteLine("To achieve this, we use a stemming tokenizer:\n");

			IReadOnlyList<Token> tok1 = new StemmingTokenizer(_ => CultureInfo.GetCultureInfo("en-US")).Tokenize(v1);
			IReadOnlyList<Token> tok2 = new StemmingTokenizer(_ => CultureInfo.GetCultureInfo("en-US")).Tokenize(v2);

			Console.WriteLine($"< {string.Join("|", tok1.Select(t => t.Text))}");
			Console.WriteLine($"> {string.Join("|", tok2.Select(t => t.Text))}");

			var result = Lexi.Compare(v1, v2);
			Console.WriteLine($"= {result_to_str(result)}");
			S1();
		}
	}

	public static void S1()
	{
		LexiDiffResult result = Lexi.Compare(
			"Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do.",
			"Alice was getting very tired to sit by her sister on the bank, with nothing to do.");

		foreach (var span in result.Spans)
		{
			switch (span.Op)
			{
				case LexOp.Insert: Console.Write($"<ins>{span.Text}</ins>"); break;
				case LexOp.Equal: Console.Write(span.Text); break;
				case LexOp.Delete: Console.Write("<del>"); Console.Write(span.Text); Console.Write("</del>"); break;
			}
		}
	}
}
