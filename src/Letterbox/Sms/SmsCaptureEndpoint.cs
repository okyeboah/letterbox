using System.Text;
using System.Text.Json;
using Letterbox.Api;
using Letterbox.Captures;
using Letterbox.Otp;
using Letterbox.Webhook;

namespace Letterbox.Sms;

public static class SmsCaptureEndpoint
{
    public static void MapSmsCapture(this IEndpointRouteBuilder app)
    {
        // Catch-all capture route: any POST outside /api becomes an SMS capture.
        app.MapPost("/{**path}", async (string? path, HttpRequest request, CaptureStore store, WebhookRelay relay, CancellationToken ct) =>
        {
            if (path is "api" || path?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true)
                return Results.NotFound();

            var body = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync(ct);
            var capture = new Capture
            {
                Id = store.NextId(),
                Channel = Channel.Sms,
                ReceivedAt = DateTimeOffset.Now,
                Url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
                ContentType = request.ContentType,
                Fields = ParseFields(request, body),
                RawBody = body,
            };
            capture.OtpCodes = OtpExtractor.Extract(CaptureDto.MessageText(capture));
            store.Add(capture);
            relay.Notify(capture);
            return Results.Text("{}", contentType: "application/json");
        });
    }

    // Field precedence, weakest first: query string, form body, JSON body.
    static Dictionary<string, string> ParseFields(HttpRequest request, string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in request.Query)
        {
            if (pair.Value.Count > 0)
                fields[pair.Key] = pair.Value[^1]!;
        }

        var contentType = request.ContentType?.ToLowerInvariant() ?? "";
        if (contentType.Contains("application/json"))
            MergeJsonFields(fields, body);
        else if (contentType.Contains("application/x-www-form-urlencoded"))
            MergeFormFields(fields, body);

        return fields;
    }

    internal static void MergeJsonFields(Dictionary<string, string> fields, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };
                if (value is not null)
                    fields[property.Name] = value;
            }
        }
        catch (JsonException)
        {
            // The raw body keeps the payload. Malformed JSON loses only field parsing.
        }
    }

    internal static void MergeFormFields(Dictionary<string, string> fields, string body)
    {
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            if (equals < 0)
            {
                fields[Decode(pair)] = "";
                continue;
            }
            fields[Decode(pair[..equals])] = Decode(pair[(equals + 1)..]);
        }
    }

    static string Decode(string value) => System.Net.WebUtility.UrlDecode(value.Replace('+', ' '));
}
