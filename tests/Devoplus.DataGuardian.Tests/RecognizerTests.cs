using Devoplus.DataGuardian;
using Devoplus.DataGuardian.Recognizers;
using Xunit;

public class RecognizerTests
{
    [Fact]
    public void Email_Should_Detect()
    {
        var r = new EmailRecognizer();
        var hits = r.Analyze("{ \"EmailAddress\": \"test@example.com\" }", "tr");
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Tckn_Checksum_Works()
    {
        var r = new TcknRecognizer();
        // Hepsi ge�ersiz �rnekler:
        var hits = r.Analyze("00000000000 10000000147 12345678901 11111111111", "tr");
        Assert.Empty(hits);
    }

    [Fact]
    public void Tckn_Valid_Sample_Is_Detected()
    {
        var r = new TcknRecognizer();
        var hits = r.Analyze("Foo 10000000146 Bar", "tr");
        Assert.Contains(hits, h => h.Type == "TCKN");
    }

    [Fact]
    public void CreditCard_Luhn()
    {
        var r = new CreditCardRecognizer();
        var hits = r.Analyze("My card 4111 1111 1111 1111 ok?", "en");
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Engine_Produces_Risk()
    {
        var opt = new DataGuardianOptions();
        var engine = new DataGuardianEngine(opt);
        var (risk, counts) = engine.Analyze("Email: a@b.com, Phone: 05551234567, IBAN: TR000000000000000000000000");
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
    }

    [Fact]
    public void Vkn_Valid_Sample_Is_Detected()
    {
        var r = new VknRecognizer();
        // Valid VKN example with valid checksum
        var hits = r.Analyze("Company VKN: 8590095528", "tr");
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal("VKN", h.Type));
    }

    [Fact]
    public void Vkn_Invalid_Sample_Is_Rejected()
    {
        var r = new VknRecognizer();
        // Invalid VKN (wrong checksum) - should be 0, not 1
        var hits = r.Analyze("Invalid: 1234567891", "tr");
        Assert.Empty(hits);
    }

    [Fact]
    public void Sgk_Valid_Sample_Is_Detected()
    {
        var r = new SgkRecognizer();
        // SGK is 12 digits
        var hits = r.Analyze("SGK No: 123456789012", "tr");
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal("SGK", h.Type));
    }

    [Fact]
    public void LicensePlate_Turkish_Format_Is_Detected()
    {
        var r = new LicensePlateRecognizer();
        var hits = r.Analyze("Plates: 34 ABC 1234 and 06 XY 9876", "tr");
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal("LICENSE_PLATE", h.Type));
    }

    [Fact]
    public void LicensePlate_Invalid_Format_Is_Rejected()
    {
        var r = new LicensePlateRecognizer();
        // Not Turkish format or wrong language
        var hits = r.Analyze("Plate: 34 ABC 1234", "en");
        Assert.Empty(hits);
    }

    [Fact]
    public void Passport_Turkish_Format_Is_Detected()
    {
        var r = new PassportRecognizer();
        var hitsTr = r.Analyze("Passport: U12345678", "tr");
        Assert.NotEmpty(hitsTr);
        Assert.All(hitsTr, h => Assert.Equal("PASSPORT", h.Type));

        var hitsEn = r.Analyze("Passport: U12345678", "en");
        Assert.NotEmpty(hitsEn);
        Assert.All(hitsEn, h => Assert.Equal("PASSPORT", h.Type));
    }
}