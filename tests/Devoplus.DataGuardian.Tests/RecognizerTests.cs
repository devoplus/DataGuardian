using Devoplus.DataGuardian;
using Devoplus.DataGuardian.Recognizers;
using FluentAssertions;
using Xunit;

public class RecognizerTests
{
    [Fact]
    public void Email_Should_Detect()
    {
        var r = new EmailRecognizer();
        var hits = r.Analyze("{ \"EmailAddress\": \"test@example.com\" }", "tr");
        hits.Should().NotBeEmpty();
    }

    [Fact]
    public void Tckn_Checksum_Works()
    {
        var r = new TcknRecognizer();
        var hits = r.Analyze("00000000000 10000000146 10000000145", "tr");
        hits.Should().BeEmpty();
    }

    [Fact]
    public void CreditCard_Luhn()
    {
        var r = new CreditCardRecognizer();
        var hits = r.Analyze("My card 4111 1111 1111 1111 ok?", "en");
        hits.Should().NotBeEmpty();
    }

    [Fact]
    public void Engine_Produces_Risk()
    {
        var opt = new DataGuardianOptions();
        var engine = new DataGuardianEngine(opt);
        var (risk, counts) = engine.Analyze("Email: a@b.com, Phone: 05551234567, IBAN: TR000000000000000000000000");
        risk.Should().BeGreaterThan(0);
        counts.Should().ContainKey("EMAIL");
    }
}
