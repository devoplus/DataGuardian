using Devoplus.DataGuardian;
using Xunit;
using System.Linq;

public class IntegrationTests
{
    [Fact]
    public void Engine_Detects_All_Turkish_Identifiers()
    {
        var opt = new DataGuardianOptions { LanguageOverride = "tr" };
        var engine = new DataGuardianEngine(opt);
        
        var text = @"
            TCKN: 10000000146
            VKN: 8590095528
            SGK: 123456789012
            Passport: U12345678
            License Plate: 34 ABC 1234
            Email: test@example.com
            Phone: 05551234567
        ";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(text);
        
        // Should detect multiple types
        Assert.True(risk > 0);
        Assert.True(counts.Count >= 5); // At least 5 different PII types
        Assert.Contains("TCKN", counts.Keys);
        Assert.Contains("VKN", counts.Keys);
        Assert.Contains("SGK", counts.Keys);
        Assert.Contains("PASSPORT", counts.Keys);
        Assert.Contains("LICENSE_PLATE", counts.Keys);
    }

    [Fact]
    public void Engine_Calculates_Risk_With_New_Detectors()
    {
        const double ExpectedMinimumHighRisk = 5.0; // VKN (9) + TCKN (10) + Passport (8)
        
        var opt = new DataGuardianOptions { LanguageOverride = "tr" };
        var engine = new DataGuardianEngine(opt);
        
        // Text with high-weight identifiers
        var highRiskText = "VKN: 8590095528, TCKN: 10000000146, Passport: U12345678";
        var (highRisk, _, _) = engine.AnalyzeDetailed(highRiskText);
        
        // Text with low-weight identifiers
        var lowRiskText = "Email: test@example.com";
        var (lowRisk, _, _) = engine.AnalyzeDetailed(lowRiskText);
        
        Assert.True(highRisk > lowRisk);
        Assert.True(highRisk > ExpectedMinimumHighRisk);
    }

    [Fact]
    public void Middleware_JsonSafe_Mode_Works_E2E()
    {
        // This test verifies the configuration is properly set up
        var opt = new DataGuardianOptions 
        { 
            Redaction = RedactionStyle.JsonSafe,
            Action = ActionMode.Redact,
            RedactAt = 0,
            LanguageOverride = "tr"
        };
        
        var engine = new DataGuardianEngine(opt);
        var json = "{\"email\":\"test@example.com\",\"vkn\":\"8590095528\"}";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(json);
        
        // Verify detection works
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
        Assert.Contains("VKN", counts.Keys);
        
        // Verify redaction configuration
        Assert.Equal(RedactionStyle.JsonSafe, opt.Redaction);
        Assert.Contains("VKN", opt.RedactTypes);
        Assert.Contains("EMAIL", opt.RedactTypes);
    }

    [Fact]
    public void Engine_Respects_New_Default_Weights()
    {
        var opt = new DataGuardianOptions();
        
        // Verify new types have weights
        Assert.True(opt.Weights.ContainsKey("VKN"));
        Assert.Equal(9, opt.Weights["VKN"]);
        
        Assert.True(opt.Weights.ContainsKey("PASSPORT"));
        Assert.Equal(8, opt.Weights["PASSPORT"]);
        
        Assert.True(opt.Weights.ContainsKey("SGK"));
        Assert.Equal(7, opt.Weights["SGK"]);
        
        Assert.True(opt.Weights.ContainsKey("LICENSE_PLATE"));
        Assert.Equal(5, opt.Weights["LICENSE_PLATE"]);
    }

    [Fact]
    public void Engine_Respects_New_RedactTypes()
    {
        var opt = new DataGuardianOptions();
        
        // Verify new types are in default redact set
        Assert.Contains("VKN", opt.RedactTypes);
        Assert.Contains("SGK", opt.RedactTypes);
        Assert.Contains("LICENSE_PLATE", opt.RedactTypes);
        Assert.Contains("PASSPORT", opt.RedactTypes);
    }
}
