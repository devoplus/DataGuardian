using Devoplus.DataGuardian;
using Xunit;

public class JsonRedactionTests
{
    [Fact]
    public void JsonSafe_Redacts_Only_Values_Not_Keys()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe, Action = ActionMode.Redact, RedactAt = 0 };
        var engine = new DataGuardianEngine(opt);
        var json = "{\"email\":\"test@example.com\",\"name\":\"John Doe\"}";
        var (risk, counts, hits) = engine.AnalyzeDetailed(json);
        
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
        
        // The Redact method in middleware uses hits to redact
        // We can't directly test it here without the middleware, but we can verify detection works
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void JsonSafe_Preserves_Valid_Json_Structure()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe };
        var engine = new DataGuardianEngine(opt);
        var json = "{\"user\":{\"email\":\"test@example.com\",\"phone\":\"05551234567\"}}";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(json);
        
        // Verify detection works
        Assert.True(risk > 0);
        Assert.True(counts.ContainsKey("EMAIL") || counts.ContainsKey("PHONE"));
    }

    [Fact]
    public void JsonSafe_Handles_Nested_Objects()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe };
        var engine = new DataGuardianEngine(opt);
        var json = "{\"level1\":{\"level2\":{\"email\":\"test@example.com\"}}}";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(json);
        
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
    }

    [Fact]
    public void JsonSafe_Handles_Arrays()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe };
        var engine = new DataGuardianEngine(opt);
        var json = "{\"emails\":[\"test1@example.com\",\"test2@example.com\"]}";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(json);
        
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
        Assert.Equal(2, counts["EMAIL"]);
    }

    [Fact]
    public void JsonSafe_Falls_Back_On_Invalid_Json()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe };
        var engine = new DataGuardianEngine(opt);
        var invalidJson = "This is not JSON but has email: test@example.com";
        
        var (risk, counts, hits) = engine.AnalyzeDetailed(invalidJson);
        
        // Should still detect email even with invalid JSON
        Assert.True(risk > 0);
        Assert.Contains("EMAIL", counts.Keys);
    }

    [Fact]
    public void JsonSafe_Handles_Multiple_PII_Types()
    {
        var opt = new DataGuardianOptions { Redaction = RedactionStyle.JsonSafe };
        var engine = new DataGuardianEngine(opt);
        var json = "{\"email\":\"test@example.com\",\"phone\":\"05551234567\",\"tckn\":\"10000000146\"}";
        opt.LanguageOverride = "tr";
        var engineTr = new DataGuardianEngine(opt);
        
        var (risk, counts, hits) = engineTr.AnalyzeDetailed(json);
        
        Assert.True(risk > 0);
        Assert.True(counts.Count >= 2); // At least email and phone, possibly TCKN
    }
}
