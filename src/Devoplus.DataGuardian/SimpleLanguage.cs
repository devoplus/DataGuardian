using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian;

/// <summary>
/// Lightweight Turkish/English guesser. Returns <c>"tr"</c> when Turkish diacritics are present or when
/// several common Turkish words appear (so ASCII-written Turkish is still recognised), otherwise <c>"en"</c>.
/// </summary>
/// <remarks>
/// This only influences language-dependent recognizers (address keywords, phone default region).
/// Structural detectors such as TCKN, IBAN, credit card and e-mail run regardless of language.
/// </remarks>
public static class SimpleLanguage
{
    const string TurkishChars = "ğĞşŞıİçÇöÖüÜ";

    static readonly Regex TurkishWords = new(
        @"\b(ve|bir|ile|bu|icin|için|adres|mahalle|sokak|cadde|telefon|numara|tarih|isim|soyisim|merhaba)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static string Guess(string text)
    {
        if (string.IsNullOrEmpty(text)) return "en";
        if (text.Any(c => TurkishChars.IndexOf(c) >= 0)) return "tr";

        // ASCII-written Turkish: require at least two distinct common Turkish words to avoid
        // misfiring on English text that happens to contain "bu", "bir", etc.
        var distinct = TurkishWords.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .Distinct()
            .Count();
        return distinct >= 2 ? "tr" : "en";
    }
}
