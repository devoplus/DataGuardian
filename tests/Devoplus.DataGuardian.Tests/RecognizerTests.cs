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
        // Hepsi geçersiz örnekler:
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
}