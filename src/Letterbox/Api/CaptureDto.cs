using System.Text.RegularExpressions;
using Letterbox.Captures;
using Capture = Letterbox.Captures.Capture;

namespace Letterbox.Api;

public static partial class CaptureDto
{
    const int PreviewLength = 160;

    static readonly string[] MessageKeys = ["message", "text", "body", "content"];
    static readonly string[] SenderKeys = ["from", "sender"];
    static readonly string[] RecipientKeys = ["to", "recipient", "msisdn"];

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTags();

    public static object Summary(Capture capture) => new
    {
        id = capture.Id,
        channel = ChannelName(capture),
        receivedAt = capture.ReceivedAt,
        otpCodes = capture.OtpCodes,
        from = capture.Channel == Channel.Email ? FirstValue(capture.HeaderFrom, capture.EnvelopeFrom) : Lookup(capture.Fields, SenderKeys),
        to = capture.Channel == Channel.Email ? FirstValue(capture.HeaderTo, string.Join(", ", capture.EnvelopeTo)) : Lookup(capture.Fields, RecipientKeys),
        title = capture.Channel == Channel.Email ? capture.Subject : MessageText(capture),
        preview = Preview(capture),
    };

    public static object Detail(Capture capture) => new
    {
        id = capture.Id,
        channel = ChannelName(capture),
        receivedAt = capture.ReceivedAt,
        otpCodes = capture.OtpCodes,
        url = capture.Url,
        contentType = capture.ContentType,
        fields = capture.Fields.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(pair => pair.Key, pair => pair.Value),
        rawBody = capture.RawBody,
        envelopeFrom = capture.EnvelopeFrom,
        envelopeTo = capture.EnvelopeTo,
        subject = capture.Subject,
        headerFrom = capture.HeaderFrom,
        headerTo = capture.HeaderTo,
        plainBody = capture.PlainBody,
        htmlBody = capture.HtmlBody,
        attachments = capture.Attachments.Select(attachment => new
        {
            id = attachment.Id,
            fileName = attachment.FileName,
            contentType = attachment.ContentType,
            size = attachment.Size,
        }),
    };

    public static string MessageText(Capture capture) =>
        Lookup(capture.Fields, MessageKeys) is { Length: > 0 } message ? message : capture.RawBody ?? "";

    static string? Lookup(Dictionary<string, string> fields, string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && value.Length > 0)
                return value;
        }
        return null;
    }

    static string? FirstValue(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrEmpty(value));

    static string ChannelName(Capture capture) => capture.Channel == Channel.Email ? "email" : "sms";

    static string Preview(Capture capture)
    {
        var text = capture.Channel == Channel.Email
            ? FirstValue(capture.PlainBody, StripHtml(capture.HtmlBody))
            : MessageText(capture);
        text = text?.ReplaceLineEndings(" ").Trim();
        return text is null || text.Length <= PreviewLength ? text ?? "" : $"{text[..PreviewLength]}…";
    }

    internal static string? StripHtml(string? html) =>
        html is null ? null : HtmlTags().Replace(html, " ");
}
