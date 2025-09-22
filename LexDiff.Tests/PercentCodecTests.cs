using LexiDiff.DiffMatchPatch;
using Xunit;

namespace LexiDiff.Tests;

public sealed class PercentCodecTests
{
	// --- ENCODE TESTS ---

	[Theory]
	[InlineData("", "")]
	[InlineData("abc", "abc")]
	[InlineData("A B", "A%20B")]               // space -> %20 (never '+')
	[InlineData("a+b", "a+b")]                 // '+' must be left as-is (encodeURI behavior)
	[InlineData("100%", "100%25")]             // '%' must be encoded
	[InlineData("hello/world", "hello/world")] // '/' is unescaped by encodeURI
	[InlineData("a;b,c", "a;b,c")]             // ';' and ',' unescaped
	[InlineData("?:@&=+$-_.!~*'()#", "?:@&=+$-_.!~*'()#")] // reserved set unescaped
	public void Encode_Basic(string input, string expected)
	{
		Assert.Equal(expected, PercentCodec.EncodeUri(input));
	}

	[Theory]
	[InlineData("café", "caf%C3%A9")]            // é -> UTF-8 %C3%A9
	[InlineData("猫", "%E7%8C%AB")]               // CJK
	[InlineData("🙂", "%F0%9F%99%82")]            // emoji (surrogate pair)
	[InlineData("Зд", "%D0%97%D0%B4")]            // Cyrillic
	public void Encode_Unicode_Utf8(string input, string expected)
	{
		Assert.Equal(expected, PercentCodec.EncodeUri(input));
	}

	// --- DECODE TESTS ---

	[Theory]
	[InlineData("", "")]
	[InlineData("abc", "abc")]
	[InlineData("A%20B", "A B")]                // %20 -> space
	[InlineData("a+b", "a+b")]                  // '+' must remain '+'
	[InlineData("100%25", "100%")]              // %25 -> '%'
	[InlineData("caf%C3%A9", "café")]           // UTF-8 decode
	[InlineData("%E7%8C%AB", "猫")]
	[InlineData("%F0%9F%99%82", "🙂")]
	public void Decode_Basic(string input, string expected)
	{
		Assert.Equal(expected, PercentCodec.DecodeUri(input));
	}

	[Theory]
	[InlineData("%2B", "+")]                    // explicit plus
	[InlineData("A%2B%2B", "A++")]
	[InlineData("+%2B+", "+++")]
	public void Decode_PlusHandling_IsLiteralPlus(string input, string expected)
	{
		Assert.Equal(expected, PercentCodec.DecodeUri(input));
	}

	[Theory]
	[InlineData("%", "%")]                      // dangling '%': leave as-is
	[InlineData("%Z", "%Z")]                    // bad hex: leave as-is
	[InlineData("%0", "%0")]                    // incomplete
	[InlineData("%GG", "%GG")]                  // invalid hexes
	[InlineData("A%G1B", "A%G1B")]              // mixed invalid
	public void Decode_MalformedSequences_LeftAsIs(string input, string expected)
	{
		Assert.Equal(expected, PercentCodec.DecodeUri(input));
	}

	// --- ROUND-TRIP / INTEROP WITH DMP EXPECTATIONS ---

	[Theory]
	[InlineData("The quick brown fox")]
	[InlineData("spaces  and   tabs\tstay distinct")] // Encode keeps tabs as-is? (tabs are not in unescaped set -> %09)
	[InlineData("plus+signs+are+literal")]
	[InlineData("control:\n\r\t should encode")]
	[InlineData("café 猫 🙂 100% ok? yes!")]
	[InlineData("?:@&=+$-_.!~*'()#;,/")]        // reserved set that encodeURI doesn’t escape
	public void RoundTrip_EncodeDecode(string original)
	{
		var encoded = PercentCodec.EncodeUri(original);
		var decoded = PercentCodec.DecodeUri(encoded);
		Assert.Equal(original, decoded);
	}

	[Fact]
	public void Encode_Matches_EncodeUri_Contract_For_Reserved_Set()
	{
		// Characters that JS encodeURI leaves unescaped must remain unescaped here too.
		const string reserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
								";/?:@&=+$-_.!~*'()#";
		var encoded = PercentCodec.EncodeUri(reserved);
		Assert.Equal(reserved, encoded);
	}

	[Fact]
	public void Decode_DoesNotTreatPlusAsSpace()
	{
		// DMP protects '+' by replacing with %2b before decoding. Our decoder must not convert '+' to ' '.
		var input = "A+B+C%2B%2B";
		var decoded = PercentCodec.DecodeUri(input);
		Assert.Equal("A+B+C++", decoded);
	}

	[Fact]
	public void Encode_SpacesBecomePercent20_NotPlus()
	{
		var encoded = PercentCodec.EncodeUri("a b c");
		Assert.Equal("a%20b%20c", encoded);
	}

	[Fact]
	public void Decode_MixedLiteralAndEncodedPlus()
	{
		// Mirrors how DMP pre-processes: literal '+' preserved; %2b decoded to '+'
		var decoded = PercentCodec.DecodeUri("+%2b+");
		Assert.Equal("+++", decoded);
	}

	// Optional: fuzz-ish sanity to catch regressions on multibyte edges.
	[Theory]
	[InlineData("\u0000\u0001\u007F")]        // control ASCII
	[InlineData("\u00A0\u2009\u202F")]        // non-breaking/thin spaces
	[InlineData("\uD83D\uDE02")]              // 😂 (surrogate pair explicit)
	public void RoundTrip_OddWhitespace_And_Controls(string s)
	{
		var encoded = PercentCodec.EncodeUri(s);
		var decoded = PercentCodec.DecodeUri(encoded);
		Assert.Equal(s, decoded);
	}
}
