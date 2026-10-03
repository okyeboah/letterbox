using System.Buffers;
using System.Globalization;
using System.Net;
using Letterbox.Api;
using Letterbox.Captures;
using Letterbox.Otp;
using Letterbox.Webhook;
using MimeKit;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace Letterbox.Smtp;

public sealed class SmtpCaptureServer(LetterboxOptions options, CaptureStore store, WebhookRelay relay) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serverOptions = new SmtpServerOptionsBuilder()
            .ServerName("letterbox")
            .Endpoint(builder =>
            {
                builder.Endpoint(new IPEndPoint(IPAddress.Parse(options.Bind), options.SmtpPort));
                // With a certificate the server advertises STARTTLS, which SMTP
                // clients that refuse plaintext (MailKit's StartTls posture)
                // require before the transaction. A throwaway self-signed pair
                // is enough: the sink holds no secrets, the certificate only
                // exists to carry the negotiation. A configured certificate
                // file keeps the identity stable, which lets clients that
                // validate against their own trust store pin it once.
                if (options.SmtpTls)
                    builder.Certificate(options.SmtpTlsCert is null
                        ? SelfSignedCertificate()
                        : System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(options.SmtpTlsCert, null));
            })
            .MaxMessageSize(10 * 1024 * 1024, MaxMessageSizeHandling.Strict)
            .Build();

        var services = new SmtpServer.ComponentModel.ServiceProvider();
        services.Add(new DelegatingMailboxFilter(_ => true));
        services.Add(new CaptureMessageStore(store, relay));

        return new SmtpServer.SmtpServer(serverOptions, services).StartAsync(stoppingToken);
    }

    static System.Security.Cryptography.X509Certificates.X509Certificate2 SelfSignedCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=letterbox", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        return System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
            certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), null);
    }

    sealed class CaptureMessageStore(CaptureStore store, WebhookRelay relay) : MessageStore
    {
        public override Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            var raw = buffer.ToArray();
            var message = TryParse(raw);
            var capture = new Capture
            {
                Id = store.NextId(),
                Channel = Channel.Email,
                ReceivedAt = DateTimeOffset.Now,
                EnvelopeFrom = transaction.From?.AsAddress(),
                EnvelopeTo = transaction.To.Select(mailbox => mailbox.AsAddress()).ToArray(),
                Subject = message.Subject,
                HeaderFrom = message.From.ToString(),
                HeaderTo = message.To.ToString(),
                PlainBody = message.TextBody,
                HtmlBody = message.HtmlBody,
                RawEml = raw,
                Attachments = LoadAttachments(message),
            };
            capture.OtpCodes = OtpExtractor.Extract(capture.PlainBody ?? CaptureDto.StripHtml(capture.HtmlBody));
            store.Add(capture);
            relay.Notify(capture);
            return Task.FromResult(SmtpResponse.Ok);
        }

        static MimeMessage TryParse(byte[] raw)
        {
            try
            {
                return MimeMessage.Load(new MemoryStream(raw));
            }
            catch (FormatException)
            {
                // Keep the capture with empty header fields; the raw message stays available.
                return new MimeMessage();
            }
        }

        static List<CaptureAttachment> LoadAttachments(MimeMessage message)
        {
            var attachments = new List<CaptureAttachment>();
            foreach (var part in message.BodyParts)
            {
                if (part is not MimePart { FileName: { Length: > 0 }, Content: not null } mimePart)
                    continue;

                using var memory = new MemoryStream();
                mimePart.Content.DecodeTo(memory);
                attachments.Add(new CaptureAttachment
                {
                    Id = attachments.Count.ToString(CultureInfo.InvariantCulture),
                    FileName = mimePart.FileName,
                    ContentType = mimePart.ContentType.MimeType,
                    Size = memory.Length,
                    Bytes = memory.ToArray(),
                });
            }
            return attachments;
        }
    }
}
