using System.Linq;

namespace Devoplus.DataGuardian;

public static class SimpleLanguage
{
    public static string Guess(string text)
    {
        if (string.IsNullOrEmpty(text)) return "en";
        var tChars = "ğĞşŞıİçÇöÖüÜ";
        int trScore = text.Count(c => tChars.Contains(c));
        return trScore > 0 ? "tr" : "en";
    }
}
