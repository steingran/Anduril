# Signal Communication Adapter

Anduril can send and receive messages via [Signal](https://signal.org/) using the [signal-cli REST API](https://github.com/bbernhard/signal-cli-rest-api), a Docker-based service that wraps the [signal-cli](https://github.com/AsamK/signal-cli) command-line client.

## Architecture

```
Signal Network ↔ signal-cli REST API (Docker) ↔ Anduril SignalAdapter (HTTP polling)
```

The adapter polls `GET /v1/receive/{phoneNumber}` at a configurable interval (default: 2 seconds) and sends replies via `POST /v2/send`. There is no WebSocket or push — it is a simple poll loop.

## Prerequisites

- **Docker** installed and running
- A **phone number** that can receive SMS or voice calls for Signal registration (this will be the bot's identity)
- Anduril host running on the same network as the signal-cli container

## Step 1 — Run signal-cli REST API

The signal-cli REST API service is included in the project's `docker-compose.yml`. Start it with:

```bash
docker compose up -d signal-cli
```

This starts the `signal-cli` service on **host port 9080** (mapped to container port 8080). The configuration data is persisted in the `signal-cli-config` Docker volume.

> **Note:** When running the full stack via `docker compose up`, the `anduril` service already has `Communication__Signal__ApiUrl=http://signal-cli:8080` configured and depends on `signal-cli`. You only need to set the phone number.

If you prefer to run signal-cli standalone (outside docker-compose):

```bash
# Bind to localhost only to avoid exposing the unauthenticated API on the network
docker run -d \
  --name signal-cli-rest-api \
  -p 127.0.0.1:9080:8080 \
  -v signal-cli-config:/home/.local/share/signal-cli \
  -e MODE=normal \
  bbernhard/signal-cli-rest-api:0.97
```

> **Security note:** The signal-cli REST API has no built-in authentication. Anyone who can reach port 9080 can send messages and inspect accounts. Always bind to `127.0.0.1` (or use a firewall) when running outside of Docker Compose.

Verify the container is running:

```bash
curl http://localhost:9080/v1/about
```

You should get a JSON response with version information.

## Step 2 — Register or Link a Phone Number

### Option A — Register a new number

```bash
# Request a verification code via SMS
curl -X POST 'http://localhost:9080/v1/register/+1234567890'

# Verify the code you received
curl -X POST 'http://localhost:9080/v1/register/+1234567890/verify/123456'
```

Replace `+1234567890` with your actual phone number (international format with `+` prefix).

### Option B — Link to an existing Signal account

The `/v1/qrcodelink` endpoint returns a **PNG image**. The easiest approach is to open the URL directly in your browser:

```
http://localhost:9080/v1/qrcodelink?device_name=AndurilBot
```

Alternatively, save the QR code to a file from the command line:

```bash
# Linux / macOS / Git Bash
curl -o qrcode.png 'http://localhost:9080/v1/qrcodelink?device_name=AndurilBot'

# Windows PowerShell (curl is an alias for Invoke-WebRequest, so use curl.exe or Invoke-WebRequest directly)
curl.exe -o qrcode.png 'http://localhost:9080/v1/qrcodelink?device_name=AndurilBot'
# or
Invoke-WebRequest 'http://localhost:9080/v1/qrcodelink?device_name=AndurilBot' -OutFile qrcode.png
```

Open the saved `qrcode.png`, then in Signal on your phone go to Settings → Linked Devices → scan the QR code.

> **Important — Option A vs Option B:**
>
> - **Option A (dedicated number):** The bot has its own phone number. You can message it directly from any Signal account, just like messaging a contact. This is the recommended approach for a proper bot setup.
> - **Option B (linked device):** The bot shares *your* phone number as a secondary device. This means **you cannot message yourself** to trigger the bot. You need a *different* Signal account (or a Signal group) to send a message to your number. The bot will pick it up and respond. Replies will appear to come from your personal number.
>
> If you want to message the bot directly from your own phone, use Option A with a separate SIM/number.

## Step 3 — Configure Anduril

### Using docker-compose (recommended)

The `ApiUrl` is already set in `docker-compose.yml` via the environment variable `Communication__Signal__ApiUrl=http://signal-cli:8080`. You only need to uncomment and set the phone number:

```yaml
# In docker-compose.yml, under the anduril service environment:
- Communication__Signal__PhoneNumber=+1234567890
```

### Using appsettings.json (local development)

Edit `src/Anduril.Host/appsettings.json` (or use environment variables / user secrets):

```json
{
  "Communication": {
    "Signal": {
      "PhoneNumber": "+1234567890",
      "ApiUrl": "http://localhost:9080",
      "PollingIntervalSeconds": 2
    }
  }
}
```

> **Note:** Use `http://localhost:9080` when running Anduril outside Docker (the host port), and `http://signal-cli:8080` when running inside Docker (the container name and internal port).

| Setting | Required | Description |
|---|---|---|
| `PhoneNumber` | Yes | The phone number registered in Step 2 (international format with `+`) |
| `ApiUrl` | Yes | Base URL of the signal-cli REST API container |
| `PollingIntervalSeconds` | No | How often to check for new messages (default: `2`). Minimum is `1`. |

If either `PhoneNumber` or `ApiUrl` is empty, the adapter logs a warning and stays inactive — the rest of Anduril continues running normally (best-effort startup).

## Step 4 — Start Anduril

```bash
dotnet run --project src/Anduril.Host
```

On startup you should see log lines like:

```
Signal adapter connected to API at http://localhost:9080 for number +1234567890
Communication adapter 'signal' started
Signal adapter started. Polling for messages...
```

Send a message to the bot's number from any Signal client and you should get a reply.

## How It Works

### Message Flow

1. **Polling**: The adapter calls `GET /v1/receive/{phoneNumber}` every N seconds
2. **Filtering**: Bot's own messages, non-data messages (receipts, typing indicators), and empty messages are discarded
3. **Normalization**: Signal envelopes are converted to `IncomingMessage` with platform set to `"signal"`
4. **Routing**: `MessageProcessingService` routes the message to a matching skill or falls back to AI chat
5. **Reply**: The response is sent via `POST /v2/send` with markdown converted to Signal formatting

### Direct Messages vs Groups

| Scenario | `ChannelId` | `IsDirectMessage` |
|---|---|---|
| 1-on-1 message | Sender's phone number | `true` |
| Group message | Signal group ID | `false` |

### Threading

Signal doesn't have native threads like Slack. The adapter uses Signal's **quote** feature to simulate threading — replies reference the original message's timestamp via `quote_timestamp`.

### Markdown Conversion

AI responses use standard Markdown. The `SignalMarkdownConverter` converts it to Signal-compatible formatting:

| Markdown | Signal | Example |
|---|---|---|
| `# Heading` | `*Heading*` | Bold text (no native headers) |
| `**bold**` | `*bold*` | Single-asterisk bold |
| `~~strike~~` | `~strike~` | Single-tilde strikethrough |
| `[text](url)` | `text (url)` | No hyperlink support |
| `- bullet` | `• bullet` | Unicode bullet |
| `---` | `───────────` | Unicode horizontal line |
| `` `code` `` | `` `code` `` | Preserved as-is |
| ```` ``` ```` | ```` ``` ```` | Preserved as-is |

## Troubleshooting

### Adapter logs "PhoneNumber is not configured"

Set `Communication:Signal:PhoneNumber` in `appsettings.json` or via environment variable `Communication__Signal__PhoneNumber`.

### Adapter logs "Could not connect to signal-cli REST API"

- Verify the container is running: `docker ps | grep signal-cli`
- Check the API URL is reachable: `curl http://localhost:9080/v1/about`
- If Anduril runs in Docker too, use the container name instead of `localhost`

### Messages are not received

- Verify the phone number is registered: `curl http://localhost:9080/v1/accounts`
- Check that the number matches exactly (including `+` and country code)
- Look for poll errors in Anduril logs at Debug level

### "Trust new identity" errors

If a contact reinstalled Signal, you may need to trust their new identity key:

```bash
curl -X PUT 'http://localhost:9080/v1/identities/+1234567890/trust-all-known-keys'
```

