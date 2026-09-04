namespace Letterbox.Captures;

public enum Channel
{
    Email,
    Sms,
}

public sealed class CaptureAttachment
{
    public required string Id { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long Size { get; init; }

    public required byte[] Bytes { get; init; }
}

public sealed class Capture
{
    public required string Id { get; init; }

    public required Channel Channel { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public List<string> OtpCodes { get; set; } = [];

    // SMS data: the raw HTTP request as received.
    public string? Url { get; init; }

    public string? ContentType { get; init; }

    public Dictionary<string, string> Fields { get; init; } = [];

    public string? RawBody { get; init; }

    // Email data. Envelope values come from the SMTP transaction; header values come from the message itself.
    public string? EnvelopeFrom { get; init; }

    public string[] EnvelopeTo { get; init; } = [];

    public string? Subject { get; init; }

    public string? HeaderFrom { get; init; }

    public string? HeaderTo { get; init; }

    public string? PlainBody { get; init; }

    public string? HtmlBody { get; init; }

    public byte[]? RawEml { get; init; }

    public List<CaptureAttachment> Attachments { get; init; } = [];
}
