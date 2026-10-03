using Letterbox;

namespace Letterbox.Tests;

public class LetterboxOptionsTests
{
    static string? Env(string name) => name switch
    {
        "LETTERBOX_HTTP_PORT" => "4700",
        "LETTERBOX_SMTP_PORT" => "2600",
        "LETTERBOX_BIND" => "0.0.0.0",
        "LETTERBOX_MAX_MESSAGES" => "50",
        "LETTERBOX_WEBHOOK_URL" => "http://127.0.0.1:9915/hook",
        _ => null,
    };

    [Fact]
    public void DefaultsApplyWithoutInput()
    {
        var options = LetterboxOptions.Load([], _ => null);
        Assert.Equal((4600, 2525, "127.0.0.1", 500, null), (options.HttpPort, options.SmtpPort, options.Bind, options.MaxMessages, options.WebhookUrl));
    }

    [Fact]
    public void EnvironmentAppliesBeforeArguments()
    {
        var options = LetterboxOptions.Load([], Env);
        Assert.Equal((4700, 2600, "0.0.0.0", 50, "http://127.0.0.1:9915/hook"),
            (options.HttpPort, options.SmtpPort, options.Bind, options.MaxMessages, options.WebhookUrl));

        var overridden = LetterboxOptions.Load(["--http-port", "4800"], Env);
        Assert.Equal(4800, overridden.HttpPort);
    }

    [Fact]
    public void EqualsFormAndSpaceFormAgree()
    {
        var equals = LetterboxOptions.Load(["--smtp-port=2555"], _ => null);
        var space = LetterboxOptions.Load(["--smtp-port", "2555"], _ => null);
        Assert.Equal(equals.SmtpPort, space.SmtpPort);
    }

    [Theory]
    [InlineData("--bogus", "1")]
    [InlineData("--http-port")]
    [InlineData("--http-port", "0")]
    [InlineData("--http-port", "70000")]
    [InlineData("--bind", "not-an-ip")]
    [InlineData("positional")]
    public void RejectsBadInput(params string[] args) =>
        Assert.ThrowsAny<ArgumentException>(() => LetterboxOptions.Load(args, _ => null));

    [Fact]
    public void SmtpTlsIsOffUnlessAsked()
    {
        var options = LetterboxOptions.Load([], _ => null);
        Assert.False(options.SmtpTls);
        Assert.Null(options.SmtpTlsCert);
    }

    [Theory]
    [InlineData(new[] { "--smtp-tls" }, true)]
    [InlineData(new[] { "--smtp-tls", "false" }, false)]
    [InlineData(new[] { "--smtp-tls=0" }, false)]
    [InlineData(new[] { "--smtp-tls", "--smtp-port", "2555" }, true)]
    public void SmtpTlsWorksAsASwitch(string[] args, bool expected)
    {
        Assert.Equal(expected, LetterboxOptions.Load(args, _ => null).SmtpTls);
    }

    [Fact]
    public void SmtpTlsReadsTheEnvironment()
    {
        var options = LetterboxOptions.Load([], name => name switch
        {
            "LETTERBOX_SMTP_TLS" => "true",
            "LETTERBOX_SMTP_TLS_CERT" => "/certs/letterbox.pfx",
            _ => null,
        });
        Assert.True(options.SmtpTls);
        Assert.Equal("/certs/letterbox.pfx", options.SmtpTlsCert);
        Assert.False(LetterboxOptions.Load(["--smtp-tls=false"], _ => "1").SmtpTls);
    }
}
