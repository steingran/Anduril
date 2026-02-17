# Standup Helper Skill

The Standup Helper is a compiled skill that automatically generates daily standup summaries by pulling recent activity from GitHub (pull requests and issues) and Microsoft 365 Calendar (past and upcoming meetings). It can be triggered on demand via chat or run automatically on a configurable schedule.

## How It Works

When triggered, the skill:

1. Calculates the timestamp of the **last standup** (the most recent Monday or Wednesday at 09:25 UTC before now).
2. Queries **GitHub** for pull requests and issues updated since that timestamp.
3. Queries **Microsoft 365 Calendar** for meetings that occurred since the last standup and meetings scheduled for today.
4. Formats everything into a ready-to-paste standup summary.

The output follows the standard three-section format:

```
### 📋 Standup — 2026-02-18

**Since Last Standup**
_Pull Requests:_
#42: Add retry logic (merged, updated 2026-02-17 14:30)
#43: Fix null ref in parser (open, updated 2026-02-17 09:12)

_Issues:_
#15: Investigate flaky test (open, by steingran, updated 2026-02-16 16:00)

_Meetings Attended:_
2026-02-17T09:00 - 2026-02-17T09:30: Sprint Planning (Teams Room 3)

**Today**
_Scheduled Meetings:_
2026-02-18T13:00 - 2026-02-18T13:30: 1:1 with Tech Lead (Office)

**Blockers**
- _Review and update as needed_
```

## Triggering On Demand

Send any of these messages to Andúril in Slack, Teams, or the CLI:

- `standup`
- `daily standup`
- `what did I do yesterday`
- `my standup update`
- `generate standup`

The skill will respond in a thread with the generated summary.

## Automatic Scheduling

The Standup Helper can run automatically via the **Standup Scheduler** background service. When enabled, it generates a standup at the configured times and posts it to a specific channel.

### Scheduler Configuration

All settings are under the `StandupScheduler` section in `appsettings.json`:

```json
{
  "StandupScheduler": {
    "Enabled": true,
    "Schedule": "25 9 * * 1,3",
    "TargetPlatform": "slack",
    "TargetChannel": "C0123456789"
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `false` | Set to `true` to activate the scheduler |
| `Schedule` | `string` | `"25 9 * * 1,3"` | Simplified cron expression (see below) |
| `TargetPlatform` | `string` | `"slack"` | Communication platform to post to (`"slack"` or `"teams"`) |
| `TargetChannel` | `string?` | `null` | Channel or conversation ID where the standup is posted |

### Schedule Format

The schedule uses a simplified cron expression with five space-separated fields:

```
┌───────── minute (0–59)
│ ┌─────── hour (0–23, UTC)
│ │ ┌───── day of month (ignored, use *)
│ │ │ ┌─── month (ignored, use *)
│ │ │ │ ┌─ day of week (0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat)
│ │ │ │ │
25 9 * * 1,3
```

**Examples:**

| Expression | Meaning |
|---|---|
| `25 9 * * 1,3` | Monday and Wednesday at 09:25 UTC |
| `0 8 * * 1,2,3,4,5` | Every weekday at 08:00 UTC |
| `30 14 * * 5` | Friday at 14:30 UTC |
| `0 10 * * 1` | Monday at 10:00 UTC |

## Integration Prerequisites

The skill depends on two integration tools. Both are optional — if an integration is unavailable, the corresponding section shows a placeholder message instead of failing.

### GitHub

Configure under `Integrations:GitHub` in `appsettings.json`:

```json
{
  "Integrations": {
    "GitHub": {
      "Token": "ghp_your_personal_access_token",
      "DefaultOwner": "your-org-or-username",
      "DefaultRepo": "your-repo"
    }
  }
}
```

Or via user secrets:

```bash
cd src/Anduril.Host
dotnet user-secrets set "Integrations:GitHub:Token" "ghp_..."
dotnet user-secrets set "Integrations:GitHub:DefaultOwner" "steingran"
dotnet user-secrets set "Integrations:GitHub:DefaultRepo" "Anduril"
```

The token needs `repo` scope for private repositories, or no special scopes for public repositories.

### Microsoft 365 Calendar

Configure under `Integrations:Office365Calendar` in `appsettings.json`:

```json
{
  "Integrations": {
    "Office365Calendar": {
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "ClientSecret": "",
      "UserId": "user@contoso.com"
    }
  }
}
```

Store the client secret securely:

```bash
dotnet user-secrets set "Integrations:Office365Calendar:ClientSecret" "your-client-secret"
```

For full setup instructions (app registration, permissions, client secret creation), see the [Office 365 Calendar integration guide](office365-calendar.md).

## Graceful Degradation

The skill is designed to work even when integrations are partially configured:

| GitHub | Calendar | Result |
|---|---|---|
| ✅ Available | ✅ Available | Full standup with all sections populated |
| ✅ Available | ❌ Unavailable | PR and issue sections populated; meeting sections show "_office365-calendar integration unavailable_" |
| ❌ Unavailable | ✅ Available | Meeting sections populated; PR and issue sections show "_github integration unavailable_" |
| ❌ Unavailable | ❌ Unavailable | All sections show unavailable placeholders ("_github integration unavailable_" and "_office365-calendar integration unavailable_") — still returns a valid standup structure |

---

[← Back to README](../README.md)

