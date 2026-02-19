# Gmail Integration

Anduril integrates with Gmail via the [Google Gmail API](https://developers.google.com/gmail/api). This lets the assistant read, search, send, and manage emails — overnight briefings, priority filtering, thread summaries, auto-responses, and real-time push notifications when new mail arrives.

Authentication uses **OAuth 2.0 with a stored refresh token**. Access tokens are acquired and refreshed automatically — no manual token management required.

## Available Functions

| Function | Description |
|---|---|
| `gmail_list_messages` | List recent email messages. Optionally filter by query |
| `gmail_get_message` | Get the full details of an email message by its ID |
| `gmail_search` | Search emails using Gmail search syntax (e.g., `from:alice subject:meeting`) |
| `gmail_get_thread` | Get all messages in an email thread by thread ID for summarization |
| `gmail_send` | Send a new email message |
| `gmail_reply` | Reply to an existing email message |
| `gmail_modify_labels` | Add or remove labels on an email (categorize, move to folders, archive) |
| `gmail_get_attachments` | List and optionally save attachments from an email |
| `gmail_messages_since` | Get email messages received since a given date/time |
| `gmail_unreplied_important` | Get important emails that have not been replied to |
| `gmail_setup_watch` | Set up Gmail push notifications via Google Cloud Pub/Sub |

## Triggering On Demand

Send any of these messages to Andúril in Slack, Teams, or the CLI:

| Trigger phrase | Operation |
|---|---|
| `check my email`, `check emails`, `read email`, `inbox summary` | Recent inbox summary (last 12 hours) |
| `overnight emails`, `morning email briefing` | Overnight briefing (since 6 PM yesterday) |
| `email last 24 hours`, `email today` | Last 24 hours summary |
| `emails last week`, `emails this week` | Last 7 days summary |
| `important email`, `prioritize email`, `email priority` | Important unread emails |
| `unreplied email`, `unanswered email`, `email I haven't responded to` | Important emails awaiting your reply |
| `summarize email thread`, `email thread summary` | Thread summary (prompts for thread ID) |
| `email from`, `emails about` | General email query |

## Configuration

### Gmail Integration Settings

All settings are under `Integrations:Gmail` in `appsettings.json`:

| Setting | Required | Default | Description |
|---|---|---|---|
| `ClientId` | Yes | — | OAuth 2.0 client ID from Google Cloud Console |
| `ClientSecret` | Yes | — | OAuth 2.0 client secret from Google Cloud Console |
| `RefreshToken` | Yes | — | OAuth 2.0 refresh token obtained through the consent flow |
| `UserId` | No | `"me"` | Gmail user ID to query (typically `"me"` for the authenticated user) |
| `PubSubTopic` | No | — | Google Cloud Pub/Sub topic for push notifications (format: `projects/{project}/topics/{topic}`) |
| `AttachmentSavePath` | No | — | Local filesystem path where extracted attachments are saved |
| `ImportantSenders` | No | `[]` | List of email addresses considered "important" for priority filtering |
| `Rules` | No | `[]` | List of email processing rules (see [Email Processing Rules](#email-processing-rules)) |

### Gmail Scheduler Settings

All settings are under `GmailScheduler` in `appsettings.json`:

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `false` | Set to `true` to activate the scheduler |
| `Schedule` | `string` | `"0 7 * * 1,2,3,4,5"` | Simplified cron expression (see [Schedule Format](#schedule-format)) |
| `TargetPlatform` | `string` | `"slack"` | Communication platform to post to (`"slack"` or `"teams"`) |
| `TargetChannel` | `string?` | `null` | Channel or conversation ID where the email summary is posted |
| `SummaryHours` | `int` | `12` | How many hours of email history to include in the summary |

### appsettings.json

```json
{
  "Integrations": {
    "Gmail": {
      "ClientId": "",
      "ClientSecret": "",
      "RefreshToken": "",
      "UserId": "me",
      "PubSubTopic": "",
      "AttachmentSavePath": "attachments",
      "ImportantSenders": [],
      "Rules": []
    }
  },
  "GmailScheduler": {
    "Enabled": true,
    "Schedule": "0 7 * * 1,2,3,4,5",
    "TargetPlatform": "slack",
    "TargetChannel": "C0123456789",
    "SummaryHours": 12
  }
}
```

### User Secrets (recommended for sensitive values)

```bash
cd src/Anduril.Host
dotnet user-secrets set "Integrations:Gmail:ClientId" "your-client-id.apps.googleusercontent.com"
dotnet user-secrets set "Integrations:Gmail:ClientSecret" "GOCSPX-your-client-secret"
dotnet user-secrets set "Integrations:Gmail:RefreshToken" "1//your-refresh-token"
```

The `UserId`, `PubSubTopic`, `AttachmentSavePath`, `ImportantSenders`, and `Rules` are not secrets and can stay in `appsettings.json`. Only the `ClientId`, `ClientSecret`, and `RefreshToken` should be stored in user secrets (or an environment variable / key vault in production).

## How to Obtain the Required Settings

### Step 1 — Create a Google Cloud Project

1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Click **Select a project** → **New Project**.
3. Name it (e.g. `Anduril`) and click **Create**.

### Step 2 — Enable the Gmail API

1. In the Cloud Console, go to **APIs & Services → Library**.
2. Search for **Gmail API** and click **Enable**.

### Step 3 — Create OAuth 2.0 Credentials

1. Go to **APIs & Services → Credentials**.
2. Click **Create Credentials → OAuth client ID**.
3. If prompted, configure the **OAuth consent screen** first:
   - Choose **External** (or **Internal** if using Google Workspace).
   - Fill in the required fields (app name, support email).
   - Add scopes: `gmail.readonly`, `gmail.send`, `gmail.modify`, `gmail.labels`.
   - Add your email as a test user (required for External apps in testing mode).
4. Back on **Create OAuth client ID**:
   - Application type: **Desktop app** (or **Web application** if you prefer).
   - Name: `Anduril`.
   - Click **Create**.
5. Copy the **Client ID** and **Client Secret**.

### Step 4 — Obtain a Refresh Token

The refresh token is obtained by completing the OAuth consent flow once. The simplest approach:

1. Use the [OAuth 2.0 Playground](https://developers.google.com/oauthplayground/):
   - Click the ⚙️ gear icon → check **Use your own OAuth credentials**.
   - Enter your **Client ID** and **Client Secret**.
   - In Step 1, select the Gmail API scopes:
     - `https://www.googleapis.com/auth/gmail.readonly`
     - `https://www.googleapis.com/auth/gmail.send`
     - `https://www.googleapis.com/auth/gmail.modify`
     - `https://www.googleapis.com/auth/gmail.labels`
   - Click **Authorize APIs** and complete the consent flow.
   - In Step 2, click **Exchange authorization code for tokens**.
   - Copy the **Refresh token**.

> ⚠️ The refresh token is shown only once. Store it securely. If you lose it, you must repeat the consent flow.

## Automatic Scheduling

The Gmail Scheduler background service sends morning email briefings on a configurable schedule. When enabled, it triggers the `gmail-email` skill and posts the result to a specific channel.

### Schedule Format

The schedule uses a simplified cron expression with five space-separated fields:

```
┌───────── minute (0–59)
│ ┌─────── hour (0–23, UTC)
│ │ ┌───── day of month (ignored, use *)
│ │ │ ┌─── month (ignored, use *)
│ │ │ │ ┌─ day of week (0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat)
│ │ │ │ │
0 7 * * 1,2,3,4,5
```

**Examples:**

| Expression | Meaning |
|---|---|
| `0 7 * * 1,2,3,4,5` | Every weekday at 07:00 UTC |
| `30 6 * * 1,2,3,4,5` | Every weekday at 06:30 UTC |
| `0 8 * * 1` | Monday at 08:00 UTC |
| `0 9 * * 1,3,5` | Monday, Wednesday, Friday at 09:00 UTC |

## Push Notifications (Real-Time Email)

Anduril can react to new emails in real time using Google Cloud Pub/Sub. When a new email arrives, Gmail sends a notification to a Pub/Sub topic, which forwards it to Anduril's HTTP endpoint. Anduril then applies your email processing rules.

### Architecture

```
Gmail Inbox → Gmail Watch API → Google Cloud Pub/Sub Topic → Push Subscription → POST /api/gmail/push → Anduril Rule Engine
```

### Setup

1. **Create a Pub/Sub topic** in the [Google Cloud Console → Pub/Sub](https://console.cloud.google.com/cloudpubsub):
   - Topic name: e.g. `gmail-notifications`
   - Full topic path: `projects/your-project/topics/gmail-notifications`

2. **Grant Gmail publish access** to the topic:
   - Add `gmail-api-push@system.gserviceaccount.com` as a **Publisher** on the topic.

3. **Create a push subscription** on the topic:
   - Delivery type: **Push**
   - Endpoint URL: `https://your-anduril-host/api/gmail/push`
   - The endpoint must be publicly accessible (or use a tunnel like ngrok for development).

4. **Configure the topic** in `appsettings.json`:
   ```json
   {
     "Integrations": {
       "Gmail": {
         "PubSubTopic": "projects/your-project/topics/gmail-notifications"
       }
     }
   }
   ```

5. **Watch renewal is automatic.** The `GmailWatchRenewalService` background service renews the Gmail watch every 6 days (Gmail watches expire after 7 days).

## Email Processing Rules

Rules are evaluated against each incoming email (via push notifications). When a rule matches, its action is executed automatically.

### Rule Properties

| Property | Required | Description |
|---|---|---|
| `Name` | Yes | Human-readable name for the rule |
| `Action` | Yes | Action to perform: `notify`, `auto-respond`, `label`, or `extract-attachments` |
| `FromFilter` | No | Sender email address or pattern to match (e.g., `@company.com`) |
| `SubjectFilter` | No | Subject line keyword to match |
| `BodyKeyword` | No | Keyword to search for in the email body |
| `ActionParameter` | No | Parameter for the action (see below) |
| `Enabled` | No | Whether the rule is active (default: `true`) |

At least one filter (`FromFilter`, `SubjectFilter`, or `BodyKeyword`) must be specified. All specified filters must match (AND logic). Matching is case-insensitive.

### Actions

| Action | ActionParameter | Behaviour |
|---|---|---|
| `notify` | _(unused)_ | Sends a notification to the configured channel: "📧 **Rule Name**: New email from sender — subject" |
| `auto-respond` | Response body text | Replies to the email with the given text. Default: "Thank you for your email. I will get back to you shortly." |
| `label` | Label name | Applies the specified Gmail label (e.g., `IMPORTANT`, `CATEGORY_PERSONAL`, or a custom label) |
| `extract-attachments` | Save path (optional) | Extracts and saves attachments. Overrides global `AttachmentSavePath` if provided |

### Example Rules

```json
{
  "Integrations": {
    "Gmail": {
      "ImportantSenders": [
        "boss@company.com",
        "cto@company.com",
        "hr@company.com"
      ],
      "Rules": [
        {
          "Name": "VIP Emails",
          "Action": "notify",
          "FromFilter": "@company.com"
        },
        {
          "Name": "Auto-respond to support",
          "Action": "auto-respond",
          "FromFilter": "support@",
          "SubjectFilter": "ticket",
          "ActionParameter": "Thank you for contacting support. We will review your ticket shortly."
        },
        {
          "Name": "Label deploy alerts",
          "Action": "label",
          "SubjectFilter": "deploy",
          "BodyKeyword": "production",
          "ActionParameter": "IMPORTANT"
        },
        {
          "Name": "Save invoice attachments",
          "Action": "extract-attachments",
          "SubjectFilter": "invoice",
          "ActionParameter": "C:/Documents/Invoices"
        }
      ]
    }
  }
}
```

## Graceful Degradation

If any of the required credentials (`ClientId`, `ClientSecret`, `RefreshToken`) are missing or empty, the integration logs a warning and reports itself as unavailable. Skills that depend on Gmail data will still run — they return a placeholder message noting that the Gmail integration is unavailable.

| Gmail | Pub/Sub | Scheduler | Result |
|---|---|---|---|
| ✅ Available | ✅ Configured | ✅ Enabled | Full functionality: on-demand, scheduled, and push-triggered |
| ✅ Available | ❌ Not configured | ✅ Enabled | On-demand and scheduled work; push notifications disabled |
| ✅ Available | ❌ Not configured | ❌ Disabled | On-demand only |
| ❌ Unavailable | — | — | All Gmail operations return "_gmail integration unavailable_" |

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "Gmail credentials not fully configured" in logs | `ClientId`, `ClientSecret`, or `RefreshToken` is missing | Set all three values (see [How to Obtain the Required Settings](#how-to-obtain-the-required-settings)) |
| `401 Unauthorized` / `invalid_grant` | Refresh token is invalid or revoked | Repeat the OAuth consent flow to obtain a new refresh token |
| `403 Forbidden` / `insufficientPermissions` | OAuth scopes are insufficient | Re-authorize with all required scopes (readonly, send, modify, labels) |
| "Gmail Pub/Sub topic not configured" in logs | `PubSubTopic` is empty | Set the full topic path (see [Push Notifications](#push-notifications-real-time-email)) |
| Push notifications not arriving | Pub/Sub subscription misconfigured | Verify the push endpoint URL is correct and publicly accessible; check that `gmail-api-push@system.gserviceaccount.com` has Publisher access on the topic |
| "Gmail scheduler enabled but no TargetChannel" | `TargetChannel` is empty | Set the Slack/Teams channel ID in `GmailScheduler:TargetChannel` |
| Watch expires / stops working | Watch renewal service not running | Ensure `GmailWatchRenewalService` is registered (it is by default). Check logs for renewal errors |
| "Gmail tool not available" on push endpoint | Credentials missing or initialization failed | Check that all three OAuth credentials are configured correctly |

---

← [Back to README](../README.md)

