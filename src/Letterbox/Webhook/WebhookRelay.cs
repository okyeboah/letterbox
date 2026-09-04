using System.Text;
using System.Text.Json;
using Letterbox.Api;
using Letterbox.Captures;

namespace Letterbox.Webhook;

public sealed class WebhookRelay
{
    const int MaxAttempts = 3;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    readonly LetterboxOptions _options;
    readonly ILogger<WebhookRelay> _logger;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public WebhookRelay(LetterboxOptions options, ILogger<WebhookRelay> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Notify(Capture capture)
    {
        if (_options.WebhookUrl is null)
            return;
        _ = SendWithRetriesAsync(capture);
    }

    async Task SendWithRetriesAsync(Capture capture)
    {
        var json = JsonSerializer.Serialize(CaptureDto.Detail(capture), JsonOptions);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await _http.PostAsync(_options.WebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                    return;
                _logger.LogWarning("Webhook attempt {Attempt} of {Max} returned {Status}.", attempt, MaxAttempts, (int)response.StatusCode);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning("Webhook attempt {Attempt} of {Max} failed: {Message}", attempt, MaxAttempts, exception.Message);
            }

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
        }
    }
}
