using System.Net;

namespace Letterbox;

/// <summary>Runtime configuration. Environment variables apply first; command-line arguments override them.</summary>
public sealed record LetterboxOptions(int HttpPort, int SmtpPort, string Bind, int MaxMessages, string? WebhookUrl)
{
    public static LetterboxOptions Load(string[] args, Func<string, string?> env)
    {
        var httpPort = ParsePort(env("LETTERBOX_HTTP_PORT"), 4600, "LETTERBOX_HTTP_PORT");
        var smtpPort = ParsePort(env("LETTERBOX_SMTP_PORT"), 2525, "LETTERBOX_SMTP_PORT");
        var bind = BlankToNull(env("LETTERBOX_BIND")) ?? "127.0.0.1";
        var maxMessages = ParseInt(env("LETTERBOX_MAX_MESSAGES"), 500);
        var webhookUrl = BlankToNull(env("LETTERBOX_WEBHOOK_URL"));

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (!name.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{name}'.");

            string value;
            var equals = name.IndexOf('=');
            if (equals >= 0)
            {
                value = name[(equals + 1)..];
                name = name[..equals];
            }
            else if (i + 1 < args.Length)
            {
                value = args[++i];
            }
            else
            {
                throw new ArgumentException($"Option '{name}' needs a value.");
            }

            switch (name)
            {
                case "--http-port":
                    httpPort = ParsePort(value, httpPort, name);
                    break;
                case "--smtp-port":
                    smtpPort = ParsePort(value, smtpPort, name);
                    break;
                case "--bind":
                    bind = value;
                    break;
                case "--max-messages":
                    maxMessages = ParseInt(value, maxMessages);
                    break;
                case "--webhook-url":
                    webhookUrl = BlankToNull(value);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{name}'.");
            }
        }

        if (!IPAddress.TryParse(bind, out _))
            throw new ArgumentException($"Bind address '{bind}' is not an IP address. Use for example 127.0.0.1 or 0.0.0.0.");

        return new LetterboxOptions(httpPort, smtpPort, bind, maxMessages, webhookUrl);
    }

    static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    static int ParsePort(string? value, int fallback, string source)
    {
        var port = ParseInt(value, fallback);
        return port is >= 1 and <= 65535
            ? port
            : throw new ArgumentException($"Port '{value}' from {source} is not between 1 and 65535.");
    }

    static int ParseInt(string? value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;
}
