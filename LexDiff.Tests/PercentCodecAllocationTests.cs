using LexiDiff.DiffMatchPatch;
using Xunit;

namespace LexiDiff.Tests;

public class PercentCodecAllocationTests
{
    private static long EncodeAllocation(string text)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var encoded = PercentCodec.EncodeUri(text);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(text, PercentCodec.DecodeUri(encoded));
        return allocated;
    }

    [Fact]
    public void NonAsciiEncoding_AllocationsScaleLinearly()
    {
        // Accented lexicographic text exercises UTF-8 encoding on every scalar.
        PercentCodec.EncodeUri("\u00E9tymologie");
        var small = EncodeAllocation(new string('\u00E9', 4096));
        var large = EncodeAllocation(new string('\u00E9', 8192));
        // Leave room for builder growth while rejecting quadratic input copies.
        Assert.True(large < small * 3, $"Doubling input allocated {small} then {large} bytes.");
        Assert.True(large < 1_000_000, $"Encoding 8K accented characters allocated {large} bytes.");
    }

    [Theory]
    [InlineData("\u00E9\u732B\U0001F642", "%C3%A9%E7%8C%AB%F0%9F%99%82")]
    public void ReusedBuffers_PreserveScalarAndReplacementEncoding(string text, string encoded)
    {
        Assert.Equal(encoded, PercentCodec.EncodeUri(text));
    }

    [Fact]
    public void UnpairedSurrogates_PreserveUtf8ReplacementBehavior()
    {
        // Construct at runtime: invalid UTF-16 cannot round-trip in test metadata.
        foreach (var surrogate in new[] { (char)0xD800, (char)0xDC00 })
        {
            Assert.Equal("%EF%BF%BD", PercentCodec.EncodeUri(new string(surrogate, 1)));
            Assert.Equal("%EF%BF%BDA", PercentCodec.EncodeUri(new string(surrogate, 1) + "A"));
        }
    }
}
