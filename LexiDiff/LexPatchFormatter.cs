using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LexiDiff.Tokens;

namespace LexiDiff;

public static class LexPatchFormatter
{
    /// <summary>Render complete source lines as a unified diff.</summary>
    public static string ToUnifiedDiff(string a, string b, LexPatchSet patch,
        string aLabel = "a", string bLabel = "b", int contextLines = 3)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!string.Equals(patch.ApplyTo(a), b, StringComparison.Ordinal))
            throw new InvalidOperationException("Patch set does not match target B.");
        int context = Math.Max(0, contextLines);

        // Word edits may begin or end inside a line. Re-diff complete lines so
        // every emitted context/delete/insert entry is a real source line.
        var items = DiffWithTokenizer.Diff(a, b, TokenizeLines, cleanupSemantic: false)
            .SelectMany(span => span.Tokens.Select(token => (span.Operation, token.Text)))
            .ToList();
        var beforeA = new int[items.Count + 1];
        var beforeB = new int[items.Count + 1];
        var changes = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            beforeA[i + 1] = beforeA[i] + (items[i].Operation == Op.Insert ? 0 : 1);
            beforeB[i + 1] = beforeB[i] + (items[i].Operation == Op.Delete ? 0 : 1);
            if (items[i].Operation != Op.Equal) changes.Add(i);
        }

        var sw = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        sw.WriteLine($"--- {aLabel}");
        sw.WriteLine($"+++ {bLabel}");
        int change = 0;
        while (change < changes.Count)
        {
            int first = changes[change];
            int last = first;
            // Merge hunks whose context windows overlap or touch.
            while (change + 1 < changes.Count &&
                   (long)changes[change + 1] - last - 1 <= 2L * context)
                last = changes[++change];
            int start = Math.Max(0, first - context);
            int end = (int)Math.Min(items.Count, (long)last + 1 + context);
            int lenA = beforeA[end] - beforeA[start];
            int lenB = beforeB[end] - beforeB[start];
            int startA = beforeA[start] + (lenA == 0 ? 0 : 1);
            int startB = beforeB[start] + (lenB == 0 ? 0 : 1);
            sw.WriteLine($"@@ -{startA},{lenA} +{startB},{lenB} @@");
            for (int i = start; i < end; i++)
            {
                var (op, text) = items[i];
                sw.Write(op == Op.Equal ? ' ' : op == Op.Delete ? '-' : '+');
                sw.Write(text);
                if (!text.EndsWith("\n", StringComparison.Ordinal))
                {
                    sw.WriteLine();
                    sw.WriteLine("\\ No newline at end of file");
                }
            }
            change++;
        }
        return sw.ToString();
    }

    private static IReadOnlyList<Token> TokenizeLines(string text)
    {
        var lines = new List<Token>();
        int start = 0;
        while (start < text.Length)
        {
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline + 1;
            lines.Add(new Token(lines.Count, start, end - start,
                text.Substring(start, end - start), WordKind.Other));
            start = end;
        }
        return lines;
    }
}
