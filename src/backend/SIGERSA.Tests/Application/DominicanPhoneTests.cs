using SIGERSA.Application.Common;

namespace SIGERSA.Tests.Application;

public sealed class DominicanPhoneTests
{
    [Theory]
    [InlineData("809-555-1212")]
    [InlineData("(829) 555-1212")]
    [InlineData("8495551212")]
    public void AcceptsValidDominicanNumbers(string phone) =>
        Assert.True(DominicanPhone.IsValid(phone));

    [Theory]
    [InlineData("800-555-1212")]
    [InlineData("809-555-121")]
    [InlineData("809-555-12123")]
    [InlineData("809-ABC-1212")]
    public void RejectsInvalidPrefixesLengthsAndCharacters(string phone) =>
        Assert.False(DominicanPhone.IsValid(phone));
}
