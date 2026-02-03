using Notifier.Messages.Domain;
using Xunit;

namespace NotifierApi.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+34600123456", "+34600123456", "34600123456")]
    [InlineData("0034600123456", "+34600123456", "34600123456")]
    [InlineData("34600123456", "+34600123456", "34600123456")]
    [InlineData(" +34 600 123 456 ", "+34600123456", "34600123456")]
    public void TryParse_Normalizes_ToExpected(string input, string expectedE164, string expectedCanonical)
    {
        var ok = PhoneNumber.TryParse(input, out var phone);

        Assert.True(ok);
        Assert.Equal(expectedE164, phone.E164);
        Assert.Equal(expectedCanonical, phone.Canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("+123")]
    public void TryParse_Invalid_ReturnsFalse(string input)
    {
        var ok = PhoneNumber.TryParse(input, out _);

        Assert.False(ok);
    }
}
