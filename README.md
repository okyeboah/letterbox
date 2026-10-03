# letterbox

A standalone capture simulator for local testing. It receives email over SMTP
and SMS-shaped webhooks over HTTP, keeps every message in memory, and shows
them in a web UI. Any project can use it, from any language. It never sends
anything to a real provider.

## Security warning

letterbox accepts and records every message unconditionally. No
authentication, no TLS. Keep the bind address at `127.0.0.1` for local work.
The Docker image binds `0.0.0.0`: never expose its ports beyond a trusted
network.

## Quickstart

```bash
dotnet run --project src/Letterbox
```

Open http://127.0.0.1:4600.

## Configuration

| Argument         | Environment variable     | Default     | Meaning                                       |
|------------------|--------------------------|-------------|-----------------------------------------------|
| `--http-port`    | `LETTERBOX_HTTP_PORT`    | `4600`      | UI, REST API and SMS capture listener         |
| `--smtp-port`    | `LETTERBOX_SMTP_PORT`    | `2525`      | SMTP listener                                 |
| `--bind`         | `LETTERBOX_BIND`         | `127.0.0.1` | IP address to bind (the image sets `0.0.0.0`) |
| `--max-messages` | `LETTERBOX_MAX_MESSAGES` | `500`       | In-memory cap; oldest messages evicted first  |
| `--webhook-url`  | `LETTERBOX_WEBHOOK_URL`  | off         | Push every capture to this URL                |
| `--smtp-tls`     | `LETTERBOX_SMTP_TLS`     | off         | Advertise STARTTLS with a self-signed certificate, for SMTP clients that refuse plaintext |
| `--smtp-tls-cert`| `LETTERBOX_SMTP_TLS_CERT`| generated   | PFX file to serve instead of the generated throwaway, so clients can pin one stable identity |

Environment variables apply first; command-line arguments override them.

## Point your project at it

SMTP, with any client from any language:

```text
host:        127.0.0.1
port:        2525
TLS:         off
credentials: none needed; any credentials are accepted without a check
```

SMS or any webhook-shaped notification: POST a JSON or form body to any path
outside `/api/`:

```bash
curl -X POST http://127.0.0.1:4600/sms \
  -H "Content-Type: application/json" \
  -d '{"to": "+233201234567", "message": "Your code is 448821"}'
```

letterbox parses fields from three sources, weakest first: query string,
form-encoded body, JSON body. Common keys (`to`, `from`, `sender`,
`recipient`, `message`, `text`, `body`, `content`) feed the list preview and
the OTP extraction. The raw body is always kept. Chunked request bodies work:
.NET `JsonContent` sends them without a content length.

## Web UI

http://127.0.0.1:4600 shows both channels in two tabs. Search, auto-refresh
every 2 seconds, rendered HTML in a sandboxed frame, plain text, raw source,
attachment download, delete one, clear all. OTP codes appear as copy-ready
badges.

## REST API

| Method | Path                                           | Result                              |
|--------|------------------------------------------------|-------------------------------------|
| GET    | `/api/health`                                  | `200` readiness probe                |
| GET    | `/api/messages?type=email\|sms&search=&limit=` | message summaries, newest first      |
| GET    | `/api/messages/{id}`                           | full message, without raw            |
| GET    | `/api/messages/{id}/raw`                       | `.eml` file or raw SMS body          |
| GET    | `/api/messages/{id}/attachments/{attachmentId}`| attachment bytes                     |
| DELETE | `/api/messages/{id}`                           | delete one                           |
| DELETE | `/api/messages`                                | clear all                            |

The API sends permissive CORS headers, so a browser app on another origin can
poll it.

Read the latest SMS OTP:

```bash
curl -s "http://127.0.0.1:4600/api/messages?type=sms&limit=1" | jq '.[0].otpCodes[0]'
```

## OTP extraction

A run of 4 to 8 digits that touches no other digit is a candidate. Runs near
the words `code`, `otp` or `pin` rank first. Up to five candidates appear in
`otpCodes` on the API and as badges in the UI.

## Webhook relay

Set `--webhook-url` or `LETTERBOX_WEBHOOK_URL`. Every capture is POSTed there
as the same JSON that `GET /api/messages/{id}` returns. Fire-and-forget with
3 attempts and a 5 second timeout; failures log to the console.

## Docker

```bash
docker compose up --build
```

or:

```bash
docker build -t letterbox .
docker run --publish 4600:4600 --publish 2525:2525 letterbox
```

The compose file defines a healthcheck on `GET /api/health`. Inside the
container the bind is `0.0.0.0`; the published ports decide who can reach it.

## Client examples

.NET, with MailKit:

```csharp
using var client = new SmtpClient();
await client.ConnectAsync("127.0.0.1", 2525, SecureSocketOptions.None);
await client.SendAsync(message);
```

Node, with nodemailer:

```js
const transport = nodemailer.createTransport({ host: "127.0.0.1", port: 2525, secure: false });
await transport.sendMail({ from: "app@example.test", to: "user@example.test", subject: "Hi", text: "Body" });
```

Python:

```python
import smtplib
with smtplib.SMTP("127.0.0.1", 2525) as smtp:
    smtp.send_message(msg)

import requests
requests.post("http://127.0.0.1:4600/sms",
              json={"to": "+233201234567", "message": "Your code is 448821"})
```

## Limits

- Storage is in memory. A restart clears every capture.
- One message is capped at 10 MB on both channels; larger bodies are rejected.
- One instance is one shared sink. There is no per-project tagging.
- No UI authentication, no SMTP STARTTLS, no credential checks.
- The webhook relay is unauthenticated; anyone who can reach letterbox can
  trigger a push to the relay URL. Point it at a listener on your machine.
- No delivery to real providers. Capture only.
