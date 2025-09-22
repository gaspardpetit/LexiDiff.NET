using System.Globalization;

namespace Snowball;

public static class StemmerFactory
{
	// Snowball stemmers have mutable internal state. Create new instances per call,
	// or cache per-thread. Here we create a new instance each time.
	public static Stemmer? Create(CultureInfo culture)
	{
		var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();

		// Prefer specific variants if you want (“en-US”, “en-GB”, etc.)
		// For Snowball, algorithms are by language, not locale. So we map by lang only.
		return lang switch {
			"ar" => new ArabicStemmer(),
			"hy" => new ArmenianStemmer(),
			"eu" => new BasqueStemmer(),
			"ca" => new CatalanStemmer(),
			"da" => new DanishStemmer(),
			"nl" => new DutchStemmer(),           // or DutchPorterStemmer() if you prefer
			"en" => new EnglishStemmer(),         // (from englishStemmer.generated.cs)
			"eo" => new EsperantoStemmer(),
			"et" => new EstonianStemmer(),
			"fi" => new FinnishStemmer(),
			"fr" => new FrenchStemmer(),
			"de" => new GermanStemmer(),
			"el" => new GreekStemmer(),
			"hi" => new HindiStemmer(),
			"hu" => new HungarianStemmer(),
			"id" => new IndonesianStemmer(),
			"ga" => new IrishStemmer(),
			"it" => new ItalianStemmer(),
			"lt" => new LithuanianStemmer(),
			"ne" => new NepaliStemmer(),
			"no" => new NorwegianStemmer(),
			"pt" => new PortugueseStemmer(),
			"ro" => new RomanianStemmer(),
			"ru" => new RussianStemmer(),
			"sr" => new SerbianStemmer(),
			"es" => new SpanishStemmer(),
			"sv" => new SwedishStemmer(),
			"ta" => new TamilStemmer(),
			"tr" => new TurkishStemmer(),
			"yi" => new YiddishStemmer(),
			_ => null
		};
	}
}
