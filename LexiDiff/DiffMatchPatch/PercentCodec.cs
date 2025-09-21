using System;
using System.Collections.Generic;
using System.Text;

namespace LexiDiff.DiffMatchPatch;

public static class PercentCodec
{
	// JS encodeURI unescaped set:
	// A-Z a-z 0-9 ; , / ? : @ & = + $ - _ . ! ~ * ' ( ) #
	private static readonly HashSet<char> Unescaped = new HashSet<char>(
		"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
		";/,?:@&=+$-_.!~*'()#");

	public static string EncodeUri(string s)
	{
		if (string.IsNullOrEmpty(s))
			return s ?? string.Empty;

		var utf8 = Encoding.UTF8;
		var sb = new StringBuilder(s.Length + 16);

		for (int i = 0; i < s.Length;)
		{
			char c = s[i];

			// Fast path: single BMP char in unescaped set
			if (!char.IsSurrogate(c) && Unescaped.Contains(c))
			{
				sb.Append(c);
				i++;
				continue;
			}

			// Determine the scalar segment to encode: 1 char or a surrogate pair
			int len = 1;
			if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
				len = 2;

			// Encode this scalar (or BMP char that isn't allowed) as UTF-8 bytes
			var bytes = utf8.GetBytes(s.ToCharArray(), i, len);
			for (int b = 0; b < bytes.Length; b++)
				sb.Append('%').Append(bytes[b].ToString("X2"));

			i += len;
		}

		return sb.ToString();
	}

	public static string DecodeUri(string s)
	{
		if (string.IsNullOrEmpty(s))
			return s ?? string.Empty;

		var utf8 = Encoding.UTF8;
		var sb = new StringBuilder(s.Length);
		var byteBuf = new List<byte>(8); // small typical multibyte cluster

		int i = 0;
		while (i < s.Length)
		{
			if (s[i] == '%' && i + 2 < s.Length && IsHex(s[i + 1]) && IsHex(s[i + 2]))
			{
				// Collect a run of %XX%YY%ZZ ...
				byteBuf.Clear();
				while (i + 2 < s.Length && s[i] == '%' && IsHex(s[i + 1]) && IsHex(s[i + 2]))
				{
					byte b = (byte)(HexVal(s[i + 1]) << 4 | HexVal(s[i + 2]));
					byteBuf.Add(b);
					i += 3;
				}
				sb.Append(utf8.GetString(byteBuf.ToArray()));
				continue;
			}

			// Literal '+' must remain '+', spaces are encoded as %20 (never '+')
			sb.Append(s[i]);
			i++;
		}

		return sb.ToString();
	}

	private static bool IsHex(char c) =>
		c >= '0' && c <= '9' ||
		c >= 'A' && c <= 'F' ||
		c >= 'a' && c <= 'f';

	private static int HexVal(char c) =>
		c <= '9' ? c - '0' :
		c <= 'F' ? 10 + (c - 'A') :
				   10 + (c - 'a');
}
