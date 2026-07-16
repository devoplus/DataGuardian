using System.Linq;
using Devoplus.DataGuardian;
using Devoplus.DataGuardian.Recognizers;
using Xunit;

namespace Devoplus.DataGuardian.Tests;

public class RecognizerTests
{
    // ---------------- Email ----------------
    [Fact]
    public void Email_Detects_Address()
    {
        var hits = new EmailRecognizer().Analyze("{ \"EmailAddress\": \"test@example.com\" }", "en");
        Assert.Contains(hits, h => h.Type == PiiTypes.Email);
    }

    [Fact]
    public void Email_Ignores_Text_Without_Tld_Dot()
    {
        var hits = new EmailRecognizer().Analyze("write to a@b then stop", "en");
        Assert.Empty(hits);
    }

    // ---------------- TCKN ----------------
    [Fact]
    public void Tckn_Valid_Detected_Without_Turkish_Chars()
    {
        // Regression: TCKN must be detected even when the text has no Turkish diacritics (lang => "en").
        var hits = new TcknRecognizer().Analyze("{\"tckn\":\"10000000146\"}", "en");
        Assert.Contains(hits, h => h.Type == PiiTypes.Tckn);
    }

    [Fact]
    public void Tckn_Invalid_Checksums_Rejected()
    {
        var hits = new TcknRecognizer().Analyze("00000000000 12345678901 11111111111", "tr");
        Assert.Empty(hits);
    }

    // ---------------- Phone ----------------
    [Fact]
    public void Phone_Detects_International_Format()
    {
        var hits = new PhoneRecognizer().Analyze("call +90 532 123 45 67 now", "en");
        Assert.Contains(hits, h => h.Type == PiiTypes.Phone);
    }

    [Fact]
    public void Phone_Detects_National_Format_With_Region()
    {
        // Regression: national numbers (no +) were never detected because of Parse(.., "ZZ").
        var hits = new PhoneRecognizer("TR").Analyze("0532 123 45 67", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.Phone);
    }

    [Fact]
    public void Phone_Ignores_Short_Digit_Runs()
    {
        var hits = new PhoneRecognizer("TR").Analyze("order 12 of 34", "en");
        Assert.Empty(hits);
    }

    // ---------------- IBAN ----------------
    [Fact]
    public void Iban_Detects_Compact()
    {
        var hits = new IbanRecognizer().Analyze("IBAN: TR330006100519786457841326", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.Iban);
    }

    [Fact]
    public void Iban_Detects_Grouped_With_Spaces()
    {
        // Regression: space-grouped IBANs (the standard printed form) were missed.
        var hits = new IbanRecognizer().Analyze("IBAN: TR33 0006 1005 1978 6457 8413 26", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.Iban);
    }

    [Fact]
    public void Iban_Rejects_Invalid_Checksum()
    {
        var hits = new IbanRecognizer().Analyze("TR000000000000000000000000", "tr");
        Assert.Empty(hits);
    }

    // ---------------- Credit card ----------------
    [Fact]
    public void CreditCard_Detects_Visa()
    {
        var hits = new CreditCardRecognizer().Analyze("card 4111 1111 1111 1111 ok", "en");
        Assert.Contains(hits, h => h.Type == PiiTypes.CreditCard);
    }

    [Fact]
    public void CreditCard_Detects_Troy()
    {
        // Luhn-valid Troy (TR national scheme) number, prefix 9792.
        var hits = new CreditCardRecognizer().Analyze("kart 9792030000000000", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.CreditCard);
    }

    [Fact]
    public void CreditCard_Rejects_Non_Luhn()
    {
        var hits = new CreditCardRecognizer().Analyze("1234 5678 9012 3456", "en");
        Assert.Empty(hits);
    }

    // ---------------- DOB ----------------
    [Fact]
    public void Dob_Detects_Valid_Past_Date()
    {
        var hits = new DobRecognizer().Analyze("dogum: 12.05.1990", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.Dob);
    }

    [Theory]
    [InlineData("31.02.2024")]  // impossible day
    [InlineData("99/99/2024")]  // impossible month/day
    [InlineData("2024-13-45")]  // impossible month/day
    public void Dob_Rejects_Impossible_Dates(string value)
    {
        var hits = new DobRecognizer().Analyze($"date {value} here", "tr");
        Assert.Empty(hits);
    }

    // ---------------- Address ----------------
    [Fact]
    public void Address_Detects_Turkish_Keywords()
    {
        var hits = new AddressRecognizer().Analyze("Ataturk Mah. Cicek Sok. No: 5", "tr");
        Assert.Contains(hits, h => h.Type == PiiTypes.Address);
    }

    [Fact]
    public void Address_Does_Not_Match_Substrings()
    {
        // Regression: "have"->"ave", "third"/"word"->"rd" must not be flagged.
        var hits = new AddressRecognizer().Analyze("I have a third word to say", "en");
        Assert.Empty(hits);
    }
}
