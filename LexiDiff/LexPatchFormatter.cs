using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LexiDiff;

public static class LexPatchFormatter
{
	/// <summary>
	/// Render a unified diff from a patch set. 
	/// </summary>
	/// <param name="a">Original text (A). Used to validate and to compute line offsets.</param>
	/// <param name="b">Modified text (B). Used to compute line offsets if you want to double-check.</param>
	/// <param name="patch">Patch produced by LexDiffer.Patch(a, b, ...).</param>
	/// <param name="aLabel">Label for the original file (default: "a").</param>
	/// <param name="bLabel">Label for the modified file (default: "b").</param>
	/// <param name="contextLines">Lines of context to show around changes (default: 3).</param>
	public static string ToUnifiedDiff(string a, string b, LexPatchSet patch,
									   string aLabel = "a", string bLabel = "b",
									   int contextLines = 3)
	{
		if (a is null)
			throw new ArgumentNullException(nameof(a));
		if (b is null)
			throw new ArgumentNullException(nameof(b));
		if (patch is null)
			throw new ArgumentNullException(nameof(patch));
		if (contextLines < 0)
			contextLines = 0;

		// Validate patch against A (same invariant as ApplyTo)
		var reconA = string.Concat(patch.Spans.Where(s => s.Op != PatchOp.Insert).Select(s => s.Text));
		if (!string.Equals(a, reconA, StringComparison.Ordinal))
			throw new InvalidOperationException("Patch set does not match source A (Equal+Delete ≠ A).");

		// Build a flat sequence of line-level “events” from the patch.
		// Equal contributes lines present on BOTH sides,
		// Delete contributes lines from A only,
		// Insert contributes lines from B only.
		var items = ToLineItems(patch.Spans);

		// Now walk items and emit hunks.
		var hunks = BuildHunks(items, contextLines);

		// Assemble unified diff text.
		var sw = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
		sw.WriteLine($"--- {aLabel}");
		sw.WriteLine($"+++ {bLabel}");
		foreach (var h in hunks)
		{
			sw.WriteLine($"@@ -{h.StartA},{h.LenA} +{h.StartB},{h.LenB} @@");
			foreach (var (prefix, text) in h.Lines)
			{
				sw.Write(prefix);
				sw.WriteLine(text);
			}
		}
		return sw.ToString();
	}

	// ----- internals -----

	private enum LineKind { Context, Delete, Insert }

	private sealed class LineItem
	{
		public LineKind Kind { get; }
		public string Text { get; }   // without trailing newline
		public LineItem(LineKind k, string t) { Kind = k; Text = t; }
	}

	private static IEnumerable<LineItem> ToLineItems(IReadOnlyList<LexiPatch> spans)
	{
		foreach (var span in spans)
		{
			var lines = SplitLines(span.Text);
			switch (span.Op)
			{
				case PatchOp.Equal:
					foreach (var line in lines)
						yield return new LineItem(LineKind.Context, line);
					break;
				case PatchOp.Delete:
					foreach (var line in lines)
						yield return new LineItem(LineKind.Delete, line);
					break;
				case PatchOp.Insert:
					foreach (var line in lines)
						yield return new LineItem(LineKind.Insert, line);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(spans));
			}
		}
	}

	private static List<string> SplitLines(string s)
	{
		var list = new List<string>();
		using (var sr = new StringReader(s))
		{
			while (sr.ReadLine() is { } line)
				list.Add(line);
		}
		// Note: unified diff prints lines without trailing newline; if you
		// need "\ No newline at end of file", add a check here.
		return list;
	}

	private sealed class Hunk
	{
		public int StartA, StartB, LenA, LenB;
		public List<(char Prefix, string Text)> Lines { get; } = new List<(char, string)>();
	}

	private static List<Hunk> BuildHunks(IEnumerable<LineItem> items, int k)
	{
		var hunks = new List<Hunk>();

		// Running absolute line numbers in A and B (1-based)
		int aLine = 1, bLine = 1;

		// Sliding buffer for up to k context lines before a change
		var preContext = new Queue<(string Text, int ALine, int BLine)>(k);

		// Pending equal lines after a change to cap the hunk (we need up to k)
		var postBuffer = new Queue<LineItem>();

		Hunk? current = null;
		int hStartA = 0, hStartB = 0; // start lines of the current hunk

		Hunk EnsureHunk()
		{
			if (current is not null)
				return current;

			current = new Hunk();
			// Starting lines include the pre-context that we will flush into the hunk
			hStartA = preContext.Count > 0 ? preContext.Peek().ALine : aLine;
			hStartB = preContext.Count > 0 ? preContext.Peek().BLine : bLine;

			// Prime the hunk with pre-context
			while (preContext.Count > 0)
			{
				var (txt, _, _) = preContext.Dequeue();
				current.Lines.Add((' ', txt));
				// Context advances both sides
				// BUT we must keep aLine/bLine consistent with global iteration:
				// Since these were previously counted when they were seen,
				// do NOT advance here; we already advanced when reading them.
			}

			return current;
		}

		void CloseHunk()
		{
			if (current == null)
				return;

			// Compute lengths from accumulated lines
			int lenA = 0, lenB = 0;
			foreach (var (p, _) in current.Lines)
			{
				if (p != '+')
					lenA++; // context or delete count towards A
				if (p != '-')
					lenB++; // context or insert count towards B
			}
			current.StartA = hStartA;
			current.StartB = hStartB;
			current.LenA = Math.Max(1, lenA);
			current.LenB = Math.Max(1, lenB);

			hunks.Add(current);
			current = null;
			postBuffer.Clear();
		}

		foreach (var it in items)
		{
			if (it.Kind == LineKind.Context)
			{
				// We are in an equal run.
				// If there is an open hunk, we may need to buffer up to k trailing context lines
				if (current != null)
				{
					postBuffer.Enqueue(it);

					// Emit and advance until we have exactly k buffered
					if (postBuffer.Count > k)
					{
						var emit = postBuffer.Dequeue();
						current.Lines.Add((' ', emit.Text));
					}
				}
				else
				{
					// No open hunk: keep a sliding window of k lines
					preContext.Enqueue((it.Text, aLine, bLine));
					if (preContext.Count > k)
						preContext.Dequeue();
				}

				// Context advances both sides
				aLine++;
				bLine++;
			}
			else
			{
				// We hit a change; ensure we have a hunk and flush pre-context
				var active = EnsureHunk();

				// Flush any pending post-buffer (was impossible; we only buffer after a hunk starts)
				// Emit the change
				if (it.Kind == LineKind.Delete)
				{
					active.Lines.Add(('-', it.Text));
					aLine++; // only A advances
				}
				else // Insert
				{
					active.Lines.Add(('+', it.Text));
					bLine++; // only B advances
				}

				// Clear post-context buffer (we start collecting after we leave change run)
				postBuffer.Clear();
			}

			// When we have a hunk open and we have *k* trailing context lines buffered,
			// close it as soon as the next item is also context (we already keep buffering).
			if (current != null && postBuffer.Count == k)
			{
				// Lookahead is implicit; when the next non-context arrives, we’ll emit it and keep the hunk open.
				// But if the stream ended, we’ll close after loop.
			}
		}

		// If we finished while a hunk is open, flush the remaining buffered trailing context (<=k) and close
		if (current != null)
		{
			while (postBuffer.Count > 0)
			{
				var emit = postBuffer.Dequeue();
				current.Lines.Add((' ', emit.Text));
			}
			CloseHunk();
		}

		return hunks;
	}
}
