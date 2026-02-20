# Setting Up Slack for Andúril

This guide walks through creating a Slack app that Andúril can use to receive and respond to messages in real time via Socket Mode.

## Overview

Andúril connects to Slack using **Socket Mode**, which means it opens a WebSocket connection from your machine to Slack — no public URL or ingress required. You need two tokens:

| Token | Prefix | Purpose |
|---|---|---|
| **Bot Token** | `xoxb-` | Calls the Slack Web API (send messages, look up users, etc.) |
| **App Token** | `xapp-` | Opens the Socket Mode WebSocket connection |

## Step 1 — Create a Slack App

1. Go to [api.slack.com/apps](https://api.slack.com/apps) and click **Create New App**.
2. Choose **From scratch**.
3. Name the app (e.g. `Andúril`) and pick your workspace.
4. Click **Create App**.

## Step 2 — Enable Socket Mode

1. In the left sidebar, go to **Socket Mode**.
2. Toggle **Enable Socket Mode** on.
3. You will be prompted to create an **App-Level Token**:
   - Name it something like `anduril-socket`.
   - Add the scope `connections:write`.
   - Click **Generate**.
4. Copy the token (`xapp-...`). This is your **App Token**.

## Step 3 — Subscribe to Events

1. In the left sidebar, go to **Event Subscriptions**.
2. Toggle **Enable Events** on.
3. Under **Subscribe to bot events**, add:
   - `message.channels` — messages in public channels the bot is in
   - `message.groups` — messages in private channels the bot is in
   - `message.im` — direct messages to the bot
   - `message.mpim` — messages in group DMs the bot is in
4. Click **Save Changes**.

## Step 4 — Configure Bot Scopes

1. In the left sidebar, go to **OAuth & Permissions**.
2. Under **Scopes → Bot Token Scopes**, add:
   - `chat:write` — send messages
   - `channels:history` — read public channel messages
   - `groups:history` — read private channel messages
   - `im:history` — read DM messages
   - `im:read` — view DM channel metadata
   - `im:write` — open/initiate DM conversations (required for users to message the bot)
   - `mpim:history` — read group DM messages
   - `users:read` — look up user info (needed to resolve the bot's own user ID)
3. Scroll up and click **Install to Workspace** (or **Reinstall** if already installed).
4. Approve the permissions.
5. Copy the **Bot User OAuth Token** (`xoxb-...`). This is your **Bot Token**.

## Step 5 — Enable Direct Messages

To allow users to send DMs to the bot, you need to enable the Messages Tab:

1. In the left sidebar, go to **App Home**.
2. Under **Show Tabs**, toggle on **Messages Tab**.
3. Check the box **"Allow users to send Slash commands and messages from the messages tab"**.

Without this, users will see _"Sending messages to this app has been turned off"_ when trying to DM the bot.

> **Note:** If you've already installed the app, you may need to **reinstall it** after changing scopes (Step 4). Go to **OAuth & Permissions** and click **Reinstall to Workspace** at the top.

## Step 6 — Configure Andúril

Store both tokens using .NET user secrets (recommended) or in `appsettings.json`.

### Using user secrets (recommended)

```bash
cd src/Anduril.Host
dotnet user-secrets set "Communication:Slack:BotToken" "xoxb-your-bot-token"
dotnet user-secrets set "Communication:Slack:AppToken" "xapp-your-app-token"
```

### Using appsettings.json

```json
{
  "Communication": {
    "Slack": {
      "BotToken": "xoxb-your-bot-token",
      "AppToken": "xapp-your-app-token"
    }
  }
}
```

> **Note:** Both tokens are required. Andúril will throw an `InvalidOperationException` at startup if either is missing.

## Step 7 — Invite the Bot

In Slack, invite the bot to any channel you want it to listen in:

```
/invite @Andúril
```

Or simply send the bot a direct message — it will respond in a thread.

## Step 8 — Run Andúril

```bash
dotnet run --project src/Anduril.Host
```

You should see output like:

```
[INF] Communication adapter 'Slack' started
[INF] Message processing pipeline is ready
```

Send a message in a channel the bot is in (or DM it directly) and it will respond in a thread.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidOperationException` on startup | Missing `BotToken` or `AppToken` | Double-check both tokens are set |
| Bot connects but never responds | Bot not invited to the channel | Run `/invite @Andúril` in the channel |
| "No chat-capable AI provider" reply | No chat AI provider is configured | Configure at least one chat provider (see [README](../README.md#configuration)) |
| Bot replies to its own messages | Bot user ID filtering failed | Ensure the `users:read` scope is granted |
| "Sending messages to this app has been turned off" | Messages Tab not enabled or missing `im:write` scope | Enable **Messages Tab** in App Home (Step 5) and add `im:write` scope (Step 4), then reinstall the app |
| Duplicate replies | Bot subscribed to overlapping event types | Check **Event Subscriptions** — each event type should appear only once |

## How It Works

Andúril's `SlackAdapter` uses the [SlackNet](https://github.com/soxtoby/SlackNet) library:

1. On startup, it resolves the bot's own user ID via `auth.test` so it can filter out self-messages.
2. It opens a Socket Mode WebSocket using the App Token.
3. Incoming `message` events are normalised into `IncomingMessage` objects and routed through the skill system.
4. Responses are sent back as **threaded replies** — the reply is always posted in the thread of the original message.

---

[← Back to README](../README.md)

