using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Devoplus.DataGuardian.Recognizers;

/// <summary>Detects calendar dates that may represent a date of birth.</summary>
/// <remarks>
/// Matches <c>yyyy-mm-dd</c> and <c>dd-mm-yyyy</c> (also with <c>/</c> or <c>.</c> separators) and rejects
/// impossible dates (e.g. <c>31.02.2024</c>, <c>99/99/2024</c>) and dates in the future. The day/month
/// order for the day-first form is interpreted as day-month-year (common in Türkiye/EU).
/// </remarks>
public sealed class DobRecognizer : IPiiRecognizer
{
    static readonly Regex Rx = new(
        @"\b(?:(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})|(\d{1,2})[-/.](\d{1,2})[-/.](\d{4}))\b",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    public IReadOnlyList<PiiHit> Analyze(string text, string lang)
    {
        var list = new List<PiiHit>();
        foreach (Match m in Rx.Matches(text))
        {
            int year, month, day;
            if (m.Groups[1].Success)
            {
                year = int.Parse(m.Groups[1].Value);
                month = int.Parse(m.Groups[2].Value);
                day = int.Parse(m.Groups[3].Value);
            }
            else
            {
                day = int.Parse(m.Groups[4].Value);
                month = int.Parse(m.Groups[5].Value);
                year = int.Parse(m.Groups[6].Value);
            }

            if (IsPlausibleBirthDate(year, month, day))
                list.Add(new PiiHit(PiiTypes.Dob, m.Index, m.Length));
        }
        return list;
    }

    static bool IsPlausibleBirthDate(int year, int month, int day)
    {
        if (year < 1900 || year > DateTime.UtcNow.Year) return false;
        if (month < 1 || month > 12) return false;
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        return new DateTime(year, month, day) <= DateTime.UtcNow.Date;
    }
}
