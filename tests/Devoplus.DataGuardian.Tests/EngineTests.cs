using System;
using Devoplus.DataGuardian;
using Xunit;

namespace Devoplus.DataGuardian.Tests;

public class EngineTests
{
    private static DataGuardianEngine NewEngine(Action<DataGuardianOptions>? configure = null)
    {
        var opt = new DataGuardianOptions();
        configure?.Invoke(opt);
        return new DataGuardianEngine(opt);
    }

    [Fact]
    public void Produces_Risk_For_Email()
    {
        var (risk, counts) = NewEngine().Analyze("Email: a@b.com");
        Assert.True(risk > 0);
        Assert.Contains(PiiTypes.Email, counts.Keys);
    }

    [Fact]
    public void Iban_Uses_Configured_Weight_Not_Fallback()
    {
        // Regression: recognizer type "IBAN" must match the "IBAN" weight key (was "IBAN_TR").
        var (risk, counts) = NewEngine().Analyze("IBAN: TR330006100519786457841326");
        Assert.Contains(PiiTypes.Iban, counts.Keys);
        // Weight 8 => 10*(1-e^(-0.15*8)) ~= 6.99, far above the weight-1 fallback (~1.39).
        Assert.True(risk > 5, $"expected IBAN weight 8 to dominate, got {risk}");
    }

    [Fact]
    public void Tckn_Detected_In_Ascii_Json_Body()
    {
        var (_, counts) = NewEngine().Analyze("{\"name\":\"Mehmet\",\"tckn\":\"10000000146\"}");
        Assert.Contains(PiiTypes.Tckn, counts.Keys);
    }

    [Fact]
    public void Risk_Is_Clamped_To_Ten()
    {
        var (risk, _) = NewEngine(o => o.K = 5).Analyze(
            "10000000146 10000000146 10000000146 10000000146 10000000146");
        Assert.True(risk <= 10);
    }

    [Fact]
    public void Explicit_Zero_Weight_Disables_Type()
    {
        var (risk, _) = NewEngine(o => o.Weights[PiiTypes.Email] = 0)
            .Analyze("a@b.com c@d.com e@f.com");
        Assert.Equal(0, risk);
    }

    [Fact]
    public void Exclude_Entity_Types_Removes_From_Counts()
    {
        var (_, counts) = NewEngine(o => o.ExcludeEntityTypes.Add(PiiTypes.Email))
            .Analyze("a@b.com and 4111 1111 1111 1111");
        Assert.DoesNotContain(PiiTypes.Email, counts.Keys);
    }

    [Fact]
    public void Empty_Text_Has_Zero_Risk()
    {
        var (risk, counts) = NewEngine().Analyze("   ");
        Assert.Equal(0, risk);
        Assert.Empty(counts);
    }
}
