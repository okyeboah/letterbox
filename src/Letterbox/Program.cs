using System.Net;
using Letterbox;
using Letterbox.Api;
using Letterbox.Captures;
using Letterbox.Sms;
using Letterbox.Smtp;
using Letterbox.Webhook;

var options = LetterboxOptions.Load(args, Environment.GetEnvironmentVariable);

// Content root at the app directory, not the shell working directory, so the UI loads from any cwd.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Listen(IPAddress.Parse(options.Bind), options.HttpPort);
    // A dev sink has no business holding a 30 MB body in memory 500 times over.
    kestrel.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new CaptureStore(options.MaxMessages));
builder.Services.AddSingleton<WebhookRelay>();
builder.Services.AddHostedService<SmtpCaptureServer>();
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
// Explicit routing after static files: the POST catch-all matches every path,
// so routing first would answer 405 for each UI asset.
app.UseRouting();
app.MapMessagesApi();
app.MapSmsCapture();

Console.WriteLine($"letterbox: UI, API and SMS capture on http://{options.Bind}:{options.HttpPort}");
Console.WriteLine($"letterbox: SMTP capture on {options.Bind}:{options.SmtpPort}");
if (options.WebhookUrl is not null)
    Console.WriteLine($"letterbox: webhook relay to {options.WebhookUrl}");

app.Run();
