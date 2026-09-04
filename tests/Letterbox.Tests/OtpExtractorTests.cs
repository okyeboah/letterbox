using Letterbox.Otp;

namespace Letterbox.Tests;

public class OtpExtractorTests
{
    [Theory]
    [InlineData("Your code is 448821", "448821")]
    [InlineData("Balance inquiry code 77123", "77123")]
    [InlineData("PIN: 0042", "0042")]
    public void ExtractsStandaloneDigitRun(string text, string expected) =>
        Assert.Equal([expected], OtpExtractor.Extract(text));

    [Fact]
    public void PrefersRunNearKeyword()
    {
        var text = "Ref 9931 confirmed. Your code is 448821.";
        Assert.Equal("448821", OtpExtractor.Extract(text)[0]);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("123")]
    [InlineData("order 12 and ref 345678901234")]
    [InlineData("")]
    [InlineData(null)]
    public void IgnoresNonCandidateText(string? text) =>
        Assert.Empty(OtpExtractor.Extract(text));

    [Fact]
    public void ReturnsDistinctCodesUpToFive()
    {
        var text = "1 2 1111 2222 3333 4444 5555 6666";
        Assert.Equal(5, OtpExtractor.Extract(text).Count);
    }
}
