using Letterbox.Captures;

namespace Letterbox.Api;

public static class MessagesApi
{
    public static void MapMessagesApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok());

        api.MapGet("/messages", (string? type, string? search, int? limit, CaptureStore store) =>
        {
            if (type is not (null or "" or "email" or "sms"))
                return Results.BadRequest(new { error = "type must be 'email' or 'sms'." });

            Channel? channel = type switch
            {
                "email" => Channel.Email,
                "sms" => Channel.Sms,
                _ => null,
            };
            var messages = store.List(channel, search, limit ?? 100);
            return Results.Ok(messages.Select(CaptureDto.Summary));
        });

        api.MapGet("/messages/{id}", (string id, CaptureStore store) =>
            store.Get(id) is { } capture ? Results.Ok(CaptureDto.Detail(capture)) : Results.NotFound());

        api.MapGet("/messages/{id}/raw", (string id, CaptureStore store) =>
        {
            if (store.Get(id) is not { } capture)
                return Results.NotFound();

            return capture.Channel == Channel.Email
                ? Results.File(capture.RawEml ?? [], "message/rfc822", $"letterbox-{capture.Id}.eml")
                : Results.Text(capture.RawBody ?? "", contentType: "text/plain");
        });

        api.MapGet("/messages/{id}/attachments/{attachmentId}", (string id, string attachmentId, CaptureStore store) =>
        {
            var attachment = store.Get(id)?.Attachments.FirstOrDefault(candidate => candidate.Id == attachmentId);
            return attachment is null
                ? Results.NotFound()
                : Results.File(attachment.Bytes, attachment.ContentType, attachment.FileName);
        });

        api.MapDelete("/messages/{id}", (string id, CaptureStore store) =>
            store.Delete(id) ? Results.NoContent() : Results.NotFound());

        api.MapDelete("/messages", (CaptureStore store) =>
        {
            store.Clear();
            return Results.NoContent();
        });
    }
}
