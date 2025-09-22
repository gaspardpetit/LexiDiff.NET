using System.Globalization;
using Xunit;

namespace LexiDiff.Tests;

public class MultiLangStemSplitterTests
{
	private static readonly Func<string, CultureInfo> En = _ => CultureInfo.GetCultureInfo("en-US");
	private static readonly Func<string, CultureInfo> Fr = _ => CultureInfo.GetCultureInfo("fr-FR");
	private static readonly Func<string, CultureInfo> De = _ => CultureInfo.GetCultureInfo("de-DE");
	private static readonly Func<string, CultureInfo> Es = _ => CultureInfo.GetCultureInfo("es-ES");

	// -------- French with accents --------
	[Theory]
	// Snowball(fr): "mangé" -> "mang"
	[InlineData("mangé", "mang", "é")]
	// "mangées" (fem. pl. past participle) -> "mang"
	[InlineData("mangées", "mang", "ées")]
	// "nationalité" -> "national" (typical -ité -> -al)
	[InlineData("nationalité", "national", "ité")]
	public void French_Accented(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, Fr);
		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
		Assert.Equal(word, stem + suffix);
	}

	// casing still preserved
	[Theory]
	[InlineData("Mangé", "Mang", "é")]
	public void French_Accented_Casing(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, Fr);
		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
	}

	// -------- German with umlauts/ß --------
	[Theory]
	// "Häusern" (dat.pl. of Haus) -> stem "haus" (Snowball de folds ä->a)
	[InlineData("Häusern", "Häusern", "")]
	// "großen" -> stem "gross" (ß→ss); LCP keeps "gro" prefix safely
	// Depending on rules, Snowball may yield "gross"; our split should be reasonable:
	[InlineData("großen", "gro", "ßen")] // conservative LCP result
	public void German_Umlauts(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, De);
		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
		Assert.Equal(word, stem + suffix);
	}

	[Theory]
	[InlineData("HÄUSERN", "HÄUSERN", "")]
	public void German_Umlauts_Casing(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, De);
		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
	}

	// -------- Spanish with accents --------
	[Theory]
	// "canción" -> Snowball(es) stems to "cancion" (accent removed)
	[InlineData("canción", "canci", "ón")]
	[InlineData("niñas", "niñ", "as")]
	public void Spanish_Accented(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, Es);
		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
		Assert.Equal(word, stem + suffix);
	}

	[Theory]
	[InlineData("running", "run", "ning")]
	[InlineData("Running", "Run", "ning")]     // preserves original casing in the stem slice
	[InlineData("caresses", "caress", "es")]
	[InlineData("ponies", "poni", "es")]       // Snowball stems "ponies" -> "poni"
	[InlineData("cats", "cat", "s")]
	[InlineData("jumped", "jump", "ed")]
	[InlineData("studies", "studi", "es")]     // Porter2: "studies" -> "studi"
	public void English_Common_Words(string word, string expectedStem, string expectedSuffix)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, En);

		Assert.Equal(expectedStem, stem);
		Assert.Equal(expectedSuffix, suffix);
		Assert.Equal(word, stem + suffix); // integrity check
	}

	[Theory]
	[InlineData("AI")]         // too short to split safely
	[InlineData("X")]          // single letter
	[InlineData("")]           // empty
	[InlineData("   ")]        // whitespace
	public void Short_Or_Empty_NoSplit(string word)
	{
		var (stem, suffix) = MultiLangStemSplitter.Split(word, En);

		Assert.Equal(word, stem);
		Assert.Equal(string.Empty, suffix);
	}

	[Fact]
	public void NullDetector_DefaultsToEnglish()
	{
		var (stem, suffix) = MultiLangStemSplitter.Split("running", detectLang: null);

		Assert.Equal("run", stem);
		Assert.Equal("ning", suffix);
	}

	[Fact]
	public void Detector_IsInvoked_AndResultUsed()
	{
		var called = false;
		string? observedWord = null;

		// Detector that returns en-US and flips a flag to prove it was called.
		CultureInfo Detector(string w)
		{
			called = true;
			observedWord = w;
			return CultureInfo.GetCultureInfo("en-US");
		}

		var (stem, suffix) = MultiLangStemSplitter.Split("running", Detector);

		Assert.True(called);
		Assert.Equal("running", observedWord);
		Assert.Equal("run", stem);
		Assert.Equal("ning", suffix);
	}
}
