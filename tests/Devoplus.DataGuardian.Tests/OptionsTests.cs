using System;
using System.Linq;
using Devoplus.DataGuardian;
using Xunit;

namespace Devoplus.DataGuardian.Tests;

public class OptionsTests
{
    [Fact]
    public void Default_Options_Are_Valid()
    {
        var ex = Record.Exception(() => new DataGuardianOptions().Validate());
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("K")]
    [InlineData("MaxCountPerType")]
    [InlineData("MaxBodySizeBytes")]
    [InlineData("MinNerConfidence")]
    public void Validate_Rejects_Invalid_Values(string field)
    {
        var opt = new DataGuardianOptions();
        switch (field)
        {
            case "K": opt.K = 0; break;
            case "MaxCountPerType": opt.MaxCountPerType = 0; break;
            case "MaxBodySizeBytes": opt.MaxBodySizeBytes = 0; break;
            case "MinNerConfidence": opt.MinNerConfidence = 2; break;
        }
        Assert.Throws<ArgumentException>(() => opt.Validate());
    }
}

public class TypeConsistencyTests
{
    // Guards against a recognizer type / options key mismatch like the historical "IBAN" vs "IBAN_TR".
    [Fact]
    public void Every_RedactType_Has_A_Weight()
    {
        var opt = new DataGuardianOptions();
        foreach (var type in opt.RedactTypes)
            Assert.True(opt.Weights.ContainsKey(type), $"RedactTypes contains '{type}' with no matching weight key");
    }

    [Fact]
    public void Weight_Keys_Are_Known_PiiTypes()
    {
        var known = new[]
        {
            PiiTypes.Tckn, PiiTypes.CreditCard, PiiTypes.Iban, PiiTypes.Dob,
            PiiTypes.Address, PiiTypes.Phone, PiiTypes.Email, PiiTypes.Person
        };
        var opt = new DataGuardianOptions();
        foreach (var key in opt.Weights.Keys)
            Assert.Contains(key, known);
    }
}
