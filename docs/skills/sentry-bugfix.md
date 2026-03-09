# Sentry Bugfix Automation

Automated bugfix generation from Sentry error monitoring webhooks. When Sentry detects recurring production errors, this webhook-driven automation flow clones the affected repository, generates a targeted fix using the Augment Code CLI (`auggie`), optionally runs verification commands, opens a pull request, and notifies your team via Slack (or another configured platform).

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Pipeline Flow](#pipeline-flow)
- [Configuration](#configuration)
- [Sentry Webhook Setup](#sentry-webhook-setup)
- [Webhook Endpoint](#webhook-endpoint)
- [Decision Logic](#decision-logic)
- [401 Error Detection](#401-error-detection)
- [Auggie Prompt Construction](#auggie-prompt-construction)
- [Pull Request Format](#pull-request-format)
- [Notifications](#notifications)
- [Data Model (DTOs)](#data-model-dtos)
- [File Inventory](#file-inventory)
- [Prerequisites](#prerequisites)
- [Test Coverage](#test-coverage)
- [Troubleshooting](#troubleshooting)

---

## Overview

The sentry-bugfix flow is a **webhook-driven host service** inside `Anduril.Host`, not a compiled `ISkill`. Unlike prompt-based skills that respond to chat messages, this automation is triggered by an HTTP webhook from Sentry and runs a fully automated pipeline:

1. **Receive** — Sentry sends a webhook `POST` to `/webhooks/sentry`
2. **Validate** — Verify HMAC signature and deserialize the payload
3. **Evaluate** — Check occurrence count against a configurable threshold
4. **Filter** — Skip HTTP 401 errors and duplicate/in-flight issues
5. **Preflight** — Check for an existing open PR or remote branch
6. **Clone** — Shallow-clone the GitHub repository to a unique temp folder
7. **Generate** — Invoke `auggie` (Augment Code CLI) with a detailed prompt
8. **Verify** — Run configured shell verification commands before commit/PR creation
9. **Push** — Commit changes and push a feature branch
10. **PR** — Create a GitHub pull request via Octokit
11. **Notify** — Send Slack messages at each stage (start, skip, success, failure)

---

## Architecture

```
Sentry Cloud                          Anduril Host
┌──────────┐    POST /webhooks/sentry  ┌─────────────────────┐
│  Sentry  │ ────────────────────────► │ Minimal API Endpoint│
│  Alerts  │                           │  (Program.cs)       │
└──────────┘                           └─────────┬───────────┘
                                                 │ fire-and-forget
                                                 ▼
                                       ┌─────────────────────┐
                                       │ SentryBugfixService │
                                       │  HandleWebhookAsync │
                                       └──┬──────────────────┘
                                          │
                  ┌───────────────────────┼───────────────────────┐
                  ▼                       ▼                       ▼
             Threshold &          Pipeline Execution          Notify Slack
             401 checks           (ExecuteBugfixPipelineAsync) (ICommunicationAdapter)
                                          │
                        ┌─────────────────┼──────────────────┐
                        ▼                 ▼                  ▼
                 IGitCommandRunner  IAuggieCliRunner   IPullRequestCreator
                        │                 │                  │
                        ▼                 ▼                  ▼
                 GitCommandRunner   AuggieCliRunner   OctokitPullRequestCreator
                 (spawns git)       (spawns auggie)   (Octokit API)
```

### Interface-Based I/O Abstraction

The pipeline's four external I/O operations are abstracted behind interfaces, enabling full end-to-end testing without real git repos, CLI binaries, or GitHub API calls:

| Interface | Implementation | Responsibility |
|---|---|---|
| `IGitCommandRunner` | `GitCommandRunner` | Spawns `git` processes (clone, config, checkout, add, commit, push, ls-remote) |
| `IAuggieCliRunner` | `AuggieCliRunner` | Spawns the `auggie` CLI process, pipes prompt to stdin, enforces timeout |
| `IShellCommandRunner` | `ShellCommandRunner` | Runs configurable verification commands in the cloned repository |
| `IPullRequestCreator` | `OctokitPullRequestCreator` | Creates GitHub PRs via the Octokit library |

All four are registered as singletons in DI and injected into `SentryBugfixService` via its primary constructor. Request validation is handled separately by `SentryWebhookRequestValidator`.

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **Fire-and-forget** webhook handler | Sentry expects a fast HTTP response. The pipeline runs in a background `Task.Run`. |
| **Unique temp folder per invocation** | `sentry-bugfix-{guid}` ensures concurrent webhooks don't collide. |
| **Dedicated webhook validator** | Signature verification and payload parsing live in `SentryWebhookRequestValidator`, which keeps endpoint logic small and directly unit-testable. |
| **In-flight + remote duplicate checks** | Prevents duplicate work for the same issue by skipping in-progress issues, existing PRs, and existing remote branches. |
| **Verification before commit** | Configurable shell commands can fail fast before Anduril commits and opens a pull request. |
| **Shallow clone (`--depth 50`)** | Minimizes clone time while preserving enough history for `auggie` context. |
| **`sealed` service class** | No inheritance needed — all orchestration is internal. |
| **Records for DTOs** | Immutable data carriers with value equality and `with` expression support. |
| **Interface extraction for I/O** | `IGitCommandRunner`, `IAuggieCliRunner`, `IShellCommandRunner`, and `IPullRequestCreator` decouple the orchestrator from external processes, enabling recording fakes in tests. |
| **Primary constructor injection** | All dependencies are injected via the primary constructor — no service locator or manual resolution. |

---

## Pipeline Flow

### Happy Path

```
Webhook received (action=created)
  → Validate HMAC signature and deserialize payload
  → Parse occurrence count (string → int)
  → Count ≥ threshold? YES
  → Is 401 error? NO
  → Already processing the same issue? NO
  → Notify: "Starting automated bugfix..."
  → Resolve GitHub owner/repo (SentryBugfixOptions → fallback to GitHubToolOptions)
  → Existing PR or remote branch already present? NO
  → Clone repo to temp dir
  → Configure git user (anduril-bot@automated.dev)
  → Create branch: sentry-bugfix/{ShortId}
  → Fetch full Sentry issue details via SentryTool API (best-effort)
  → Build auggie prompt with error context + instructions
  → Pipe prompt to auggie CLI stdin
  → Check git status --porcelain
  → Run verification commands (if configured)
  → Stage, commit, push
  → Create PR via Octokit
  → Notify: "PR created: {url}"
  → Cleanup temp directory
```

### Early Exit Conditions

| Condition | Result |
|---|---|
| `Enabled = false` | Silent return |
| Feature enabled but `WebhookSecret` missing | HTTP `403 Forbidden` |
| Signature missing / invalid / mismatched | HTTP `401 Unauthorized` |
| Payload is invalid JSON | HTTP `400 Bad Request` |
| `Action ≠ "created"` | Silent return |
| `Count` is not a valid integer | Silent return (logged as warning) |
| `Count < OccurrenceThreshold` | Silent return (logged as info) |
| Issue is HTTP 401 | Slack notification: "skipped — configuration issue" |
| Issue is already in flight | Silent return |
| Open PR already exists for the branch | Notification: existing PR URL |
| Remote branch already exists | Notification: existing branch |
| GitHub owner/repo not configured | Slack notification: "failed — not configured" |
| `auggie` produces no changes | Slack notification: "could not generate a fix" |
| Verification command fails or times out | Slack notification: "failed: ..." |
| Any exception in the pipeline | Slack notification: "failed: {message}" |

---

## Configuration

All settings live under the `SentryBugfix` section in `appsettings.json`:

```json
{
  "SentryBugfix": {
    "Enabled": false,
    "OccurrenceThreshold": 10,
    "NotificationPlatform": "slack",
    "NotificationChannel": "",
    "GitHubOwner": "",
    "GitHubRepo": "",
    "AugmentCliPath": "auggie",
    "BranchPrefix": "sentry-bugfix/",
    "AuggieTimeoutMinutes": 10,
    "BaseBranch": "main",
    "VerificationCommands": [],
    "VerificationTimeoutMinutes": 10,
    "WebhookSecret": ""
  }
}
```

### Options Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `false` | Master switch. Must be `true` for webhooks to be processed. |
| `OccurrenceThreshold` | `int` | `10` | Minimum error occurrences before attempting a fix. Issues below this are silently ignored. |
| `NotificationPlatform` | `string` | `"slack"` | Which `ICommunicationAdapter.Platform` to send notifications to. Matches case-insensitively. |
| `NotificationChannel` | `string?` | `null` | Channel/conversation ID for notifications (e.g., Slack channel ID `C0123BUGFIX`). If `null`, all notifications are suppressed. |
| `GitHubOwner` | `string?` | `null` | GitHub repository owner. Falls back to `Integrations:GitHub:DefaultOwner` if not set. |
| `GitHubRepo` | `string?` | `null` | GitHub repository name. Falls back to `Integrations:GitHub:DefaultRepo` if not set. |
| `AugmentCliPath` | `string` | `"auggie"` | Path to the Augment Code CLI binary. Defaults to `auggie` (assumes it's on `PATH`). |
| `BranchPrefix` | `string` | `"sentry-bugfix/"` | Prefix for auto-created feature branch names. The Sentry issue `ShortId` is appended. |
| `AuggieTimeoutMinutes` | `int` | `10` | Maximum time (in minutes) to wait for `auggie` to complete before cancellation. |
| `BaseBranch` | `string` | `"main"` | Target branch for pull requests. The repo is shallow-cloned from this branch and PRs are opened against it. |
| `VerificationCommands` | `string[]` | `[]` | Shell commands to run in the cloned repository after Auggie changes are generated and before commit/PR creation. |
| `VerificationTimeoutMinutes` | `int` | `10` | Maximum time (in minutes) allowed for each verification command. |
| `WebhookSecret` | `string` | _required_ | Shared secret for HMAC-SHA256 webhook signature validation. Must be non-empty whenever the Sentry bugfix feature is enabled; webhook requests without a valid HMAC signature are rejected. |

### Related Configuration Sections

The service also reads from these existing config sections:

- **`Integrations:GitHub`** — Provides `Token` (required for clone/push/PR), `DefaultOwner`, and `DefaultRepo` as fallbacks.
- **`Integrations:Sentry`** — Used by `SentryTool` to fetch full issue details from the Sentry API (optional enrichment).

---

## Sentry Webhook Setup

To connect Sentry to Anduril:

1. Go to **Sentry → Settings → Integrations → Internal Integrations** (or use Webhooks under Project Settings)
2. Create a new integration or add a webhook
3. Set the **Webhook URL** to: `https://<your-anduril-host>/webhooks/sentry`
4. Under **Alerts**, enable the **issue** event (specifically `issue.created`)
5. Save the integration

### Webhook Authentication

Anduril supports HMAC-SHA256 signature validation for Sentry webhooks. When configured, each incoming request's `sentry-hook-signature` header is verified against the shared secret before the payload is processed.

**How it works:**

1. Sentry computes `HMAC-SHA256(secret, request_body)` and sends the hex-encoded result in the `sentry-hook-signature` header.
2. Anduril reads the raw request body, computes the same HMAC using the configured `WebhookSecret`, and compares the two using a constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks.
3. If the signatures don't match (or the header is missing), the request is rejected with `401 Unauthorized`.

**Configuration:**

- In **Sentry**: When creating the internal integration, Sentry generates a **Client Secret**. Copy this value.
- In **Anduril**: Set `SentryBugfix:WebhookSecret` in `appsettings.json` (or via environment variable / user secrets) to the same secret value.

> ⚠️ **Production warning:** Running without `WebhookSecret` configured means any HTTP client can trigger the bugfix pipeline. Always configure webhook authentication in production environments.

### Webhook Payload

Sentry sends a JSON payload matching this structure:

```json
{
  "action": "created",
  "data": {
    "issue": {
      "id": "123456789",
      "shortId": "PROJ-42",
      "title": "NullReferenceException in Foo.Bar()",
      "culprit": "Foo.Bar in /src/Foo.cs",
      "level": "error",
      "status": "unresolved",
      "platform": "csharp",
      "count": "157",
      "userCount": 23,
      "permalink": "https://sentry.io/issues/123456789/",
      "project": {
        "id": "1",
        "name": "My Project",
        "slug": "my-project",
        "platform": "csharp"
      },
      "metadata": {
        "title": "NullReferenceException",
        "severity": 2,
        "initial_priority": 75
      },
      "priority": "high"
    }
  },
  "actor": {
    "type": "application",
    "id": "sentry",
    "name": "Sentry"
  }
}
```

> **Note:** Sentry's `count` field is a **string**, not an integer. The service parses it with `int.TryParse`.

---

## Webhook Endpoint

Defined in `Program.cs` as a Minimal API endpoint:

```
POST /webhooks/sentry
```

**Behavior:**
- Deserializes the JSON body to `SentryWebhookPayload`
- Returns `400 Bad Request` if the payload is `null`
- Delegates processing to `SentryBugfixService.HandleWebhookAsync` in a fire-and-forget `Task.Run`
- Immediately returns `200 OK` with `{ "status": "accepted" }` so Sentry doesn't time out
- If deserialization throws, returns `500`

**DI Registration** (in `Program.cs`):
```csharp
builder.Services.Configure<SentryBugfixOptions>(config.GetSection("SentryBugfix"));
builder.Services.AddSingleton<IGitCommandRunner, GitCommandRunner>();
builder.Services.AddSingleton<IAuggieCliRunner, AuggieCliRunner>();
builder.Services.AddSingleton<IShellCommandRunner, ShellCommandRunner>();
builder.Services.AddSingleton<IPullRequestCreator, OctokitPullRequestCreator>();
builder.Services.AddSingleton<SentryWebhookRequestValidator>();
builder.Services.AddSingleton<SentryBugfixService>();
```

---

## Decision Logic

The `HandleWebhookAsync` method evaluates incoming payloads through a series of gates:

```
Is Enabled?  ──NO──►  return (silent)
     │ YES
     ▼
Action = "created"?  ──NO──►  return (silent)
     │ YES
     ▼
Count parseable?  ──NO──►  return (log warning)
     │ YES
     ▼
Count ≥ threshold?  ──NO──►  return (log info)
     │ YES
     ▼
Is 401 error?  ──YES──►  Notify "skipped" + return
     │ NO
     ▼
GitHub config present?  ──NO──►  Notify "failed" + return
     │ YES
     ▼
Execute bugfix pipeline...
```

---

## 401 Error Detection

HTTP 401 errors are treated as **configuration issues** (expired tokens, missing API keys), not code bugs. The `Is401Error` method checks:

1. Combines `issue.Title` and `issue.Metadata.Title` into one string
2. Checks for any of these patterns: `"401"`, `"Unauthorized"`, `"HTTP 401"`
3. **Also** requires the regex `\b401\b` to match (word boundary around the number)

This dual check prevents false positives:
- `"Unauthorized access denied"` (no `401` number) → **NOT** detected as 401
- `"Internal Server Error 500"` → **NOT** detected as 401
- `"HTTP 401 Unauthorized"` → **YES**, detected and skipped
- `"Error 401 Unauthorized"` in metadata → **YES**, detected and skipped

---

## Auggie Prompt Construction

The `BuildAuggiePrompt` method constructs a detailed prompt piped to `auggie`'s stdin:

```markdown
Fix the following production bug that was reported by Sentry error monitoring.

**Error Title:** NullReferenceException in Foo.Bar()
**Severity Level:** error
**Priority:** high
**Occurrences:** 157 times, affecting 23 user(s)
**Culprit:** Foo.Bar in /src/Foo.cs
**Sentry Issue URL:** https://sentry.io/issues/123456789/

## Full Sentry Issue Details
(JSON from the Sentry API, if available)

## Instructions
1. Analyze the error and identify the root cause in the codebase
2. Implement a minimal, targeted fix — do not refactor unrelated code
3. Add appropriate error handling and null checks where needed
4. Do not introduce breaking changes to public APIs
5. Add brief code comments explaining the fix
6. If the fix requires a test, add or update the relevant test
7. Make sure the code compiles and passes existing tests
```

The full Sentry issue details section is populated by calling `SentryTool.sentry_get_issue` via `IIntegrationTool.GetFunctions()`. This is **best-effort** — if the Sentry integration is unavailable, the prompt still includes the webhook data.

---

## Pull Request Format

The auto-created PR has:

**Title:** `fix: auto-fix Sentry issue {ShortId} — {Title}`

**Branch:** `sentry-bugfix/{ShortId}` → `main`

**Body:**
```markdown
## Automated Bugfix — Sentry Issue PROJ-42

**Error:** NullReferenceException in Foo.Bar()
**Level:** error
**Occurrences:** 157 (23 users affected)
**Sentry Link:** https://sentry.io/issues/123456789/

---

This PR was automatically generated by **Anduril** using **Augment Code (auggie)**.
Please review carefully before merging.
```

**Commit message:**
```
fix: auto-fix for Sentry issue PROJ-42

Automated bugfix generated by Anduril + Augment Code.
Issue: NullReferenceException in Foo.Bar()
Occurrences: 157 | Users: 23
Sentry: https://sentry.io/issues/123456789/
```

---

## Notifications

All notifications are sent via the configured `ICommunicationAdapter` (matched by platform name). Each notification is sent to `NotificationChannel`.

| Event | Emoji | Example Message |
|---|---|---|
| **Starting** | `:robot_face:` | Starting automated bugfix for Sentry issue **PROJ-42**: _NullReferenceException..._ |
| **401 Skip** | `:warning:` | Sentry issue **PROJ-42** skipped — HTTP 401 (Unauthorized) errors are configuration issues |
| **No Changes** | `:warning:` | Auggie could not generate a fix for **PROJ-42** |
| **PR Created** | `:white_check_mark:` | Automated bugfix PR created for **PROJ-42**: _NullReferenceException..._ 🔗 {url} |
| **Config Error** | `:x:` | Sentry bugfix failed — GitHub owner/repo not configured |
| **Pipeline Error** | `:x:` | Automated bugfix failed for **PROJ-42**: {error message} |

### Notification Fallback Behavior

- If `NotificationChannel` is `null` or empty → all notifications silently suppressed
- If no adapter matches `NotificationPlatform` → notification skipped (logged as warning)
- If matched adapter is disconnected → notification skipped (logged as warning)
- If `SendMessageAsync` throws → exception caught and logged, does **not** fail the pipeline

---


## Data Model (DTOs)

All webhook DTOs live in `Anduril.Core.Webhooks` and are C# **records** with `init`-only properties, compatible with `System.Text.Json` deserialization.

### SentryWebhookPayload
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Action` | `string` | `action` | Webhook action (`"created"`, `"resolved"`, etc.) |
| `Data` | `SentryWebhookData` | `data` | Container for the issue data |
| `Actor` | `SentryWebhookActor?` | `actor` | Who/what triggered the event |

### SentryWebhookData
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Issue` | `SentryWebhookIssue` | `issue` | The Sentry issue details |

### SentryWebhookIssue
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Id` | `string` | `id` | Sentry issue numeric ID |
| `ShortId` | `string?` | `shortId` | Human-readable ID (e.g., `PROJ-42`) |
| `Title` | `string` | `title` | Error title/message |
| `Culprit` | `string?` | `culprit` | Code location that caused the error |
| `Level` | `string?` | `level` | Severity level (`error`, `warning`, etc.) |
| `Status` | `string?` | `status` | Current status (`unresolved`, `resolved`, etc.) |
| `Substatus` | `string?` | `substatus` | Sub-status details |
| `Platform` | `string?` | `platform` | Language/platform (e.g., `csharp`) |
| `Count` | `string` | `count` | Occurrence count (**string**, not int) |
| `UserCount` | `int` | `userCount` | Number of affected users |
| `WebUrl` | `string?` | `web_url` | Web URL for the issue |
| `Permalink` | `string?` | `permalink` | Permanent link to the issue |
| `Project` | `SentryWebhookProject?` | `project` | Project metadata |
| `Metadata` | `SentryWebhookMetadata?` | `metadata` | Issue metadata (title, severity) |
| `FirstSeen` | `string?` | `firstSeen` | ISO 8601 timestamp |
| `LastSeen` | `string?` | `lastSeen` | ISO 8601 timestamp |
| `IssueType` | `string?` | `issueType` | Issue type identifier |
| `IssueCategory` | `string?` | `issueCategory` | Issue category |
| `Priority` | `string?` | `priority` | Priority level |

### SentryWebhookProject
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Id` | `string?` | `id` | Project ID |
| `Name` | `string?` | `name` | Project name |
| `Slug` | `string?` | `slug` | Project slug |
| `Platform` | `string?` | `platform` | Project platform |

### SentryWebhookMetadata
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Title` | `string?` | `title` | Metadata title (often the exception type) |
| `Severity` | `int?` | `severity` | Numeric severity |
| `SeverityReason` | `string?` | `severity_reason` | Why this severity was assigned |
| `InitialPriority` | `int?` | `initial_priority` | Initial priority score |

### SentryWebhookActor
| Property | Type | JSON Key | Description |
|---|---|---|---|
| `Type` | `string?` | `type` | Actor type (`application`, `user`) |
| `Id` | `string?` | `id` | Actor ID |
| `Name` | `string?` | `name` | Actor display name |

---

## File Inventory

| File | Project | Purpose |
|---|---|---|
| `src/Anduril.Core/Webhooks/SentryWebhookPayload.cs` | Core | Root webhook DTO record |
| `src/Anduril.Core/Webhooks/SentryWebhookData.cs` | Core | Data wrapper DTO record |
| `src/Anduril.Core/Webhooks/SentryWebhookIssue.cs` | Core | Issue DTO record (main payload) |
| `src/Anduril.Core/Webhooks/SentryWebhookProject.cs` | Core | Project metadata DTO record |
| `src/Anduril.Core/Webhooks/SentryWebhookMetadata.cs` | Core | Issue metadata DTO record |
| `src/Anduril.Core/Webhooks/SentryWebhookActor.cs` | Core | Webhook actor DTO record |
| `src/Anduril.Host/SentryBugfixOptions.cs` | Host | Configuration options class |
| `src/Anduril.Host/Services/IGitCommandRunner.cs` | Host | Interface — git CLI command abstraction |
| `src/Anduril.Host/Services/IAuggieCliRunner.cs` | Host | Interface — Augment CLI abstraction |
| `src/Anduril.Host/Services/IPullRequestCreator.cs` | Host | Interface — pull request creation abstraction |
| `src/Anduril.Host/Services/GitCommandRunner.cs` | Host | Implementation — spawns `git` processes |
| `src/Anduril.Host/Services/AuggieCliRunner.cs` | Host | Implementation — spawns `auggie` process, pipes prompt to stdin |
| `src/Anduril.Host/Services/OctokitPullRequestCreator.cs` | Host | Implementation — creates GitHub PRs via Octokit |
| `src/Anduril.Host/Services/SentryBugfixService.cs` | Host | Main orchestrator service (~315 lines) |
| `src/Anduril.Host/Program.cs` | Host | DI registration + webhook endpoint |
| `src/Anduril.Host/appsettings.json` | Host | Default configuration values |
| `tests/Anduril.Host.Tests/SentryBugfixOptionsTests.cs` | Tests | 9 tests for option defaults |
| `tests/Anduril.Host.Tests/SentryBugfixServiceTests.cs` | Tests | 27 tests — decision logic + full pipeline (~813 lines) |

---

## Prerequisites

1. **Augment Code CLI (`auggie`)** — Must be installed and accessible at the configured path (default: on `PATH`)
2. **Git** — Must be installed and on `PATH` for cloning, committing, and pushing
3. **GitHub Token** — Required in `Integrations:GitHub:Token` for authenticated clone, push, and PR creation
4. **Sentry Webhook** — Configured in Sentry to send `issue.created` events to your Anduril host
5. **Communication Adapter** — At least one connected adapter matching `NotificationPlatform` for notifications
6. **Network Access** — Outbound to GitHub (clone/push/API) and optionally to the Sentry API (for enriched details)

---

## Test Coverage

**36 sentry-bugfix tests** across two files (87 total in the Host test project):

### SentryBugfixOptionsTests (9 tests)
Verify all default values match the documented defaults:
- `Enabled` = `false`
- `OccurrenceThreshold` = `10`
- `NotificationPlatform` = `"slack"`
- `NotificationChannel` = `null`
- `GitHubOwner` = `null`
- `GitHubRepo` = `null`
- `AugmentCliPath` = `"auggie"`
- `BranchPrefix` = `"sentry-bugfix/"`
- `AuggieTimeoutMinutes` = `10`

### SentryBugfixServiceTests — Decision Logic (17 tests)
| Category | Tests | What's Verified |
|---|---|---|
| **Disabled** | 1 | Service returns immediately when disabled |
| **Action Filtering** | 2 | Only `"created"` action is processed; `"resolved"` and `"assigned"` are ignored |
| **Count Parsing** | 2 | Unparseable and empty count strings cause silent return |
| **Threshold** | 2 | Below-threshold issues are skipped; at-threshold issues proceed |
| **401 Detection** | 4 | 401 in title, 401 in metadata, "Unauthorized" without 401, and 500 errors |
| **GitHub Config** | 2 | Missing owner/repo sends failure notification; falls back to GitHubToolOptions |
| **Notification Routing** | 4 | No channel, no matching adapter, disconnected adapter, correct channel ID |

### SentryBugfixServiceTests — Full Pipeline (10 tests)

End-to-end tests that exercise the entire pipeline from webhook receipt through to PR creation, using recording fakes (`FakeGitCommandRunner`, `FakeAuggieCliRunner`, `FakePullRequestCreator`) instead of real external processes:

| Test | What's Verified |
|---|---|
| **HappyPath** | Full sequence: clone → config → checkout → auggie → status → add → commit → push → PR → success notification with PR URL |
| **AuggieNoChanges** | `status --porcelain` returns empty → "could not generate a fix" warning notification |
| **GitCloneFailure** | Git runner throws → error notification sent, auggie never invoked |
| **MissingGitHubToken** | No token configured → "GitHub token is required" error notification |
| **AuggiePromptContainsIssueDetails** | Prompt includes title, occurrence count, severity level, and Instructions section |
| **PrTitleAndBodyContainIssueInfo** | PR title/body include shortId; owner/repo passed correctly to `IPullRequestCreator` |
| **BranchNameUsesConfiguredPrefix** | Branch = configured prefix + shortId (verified in git checkout and push commands) |
| **WithSentryTool** | `FakeSentryTool` provides enriched JSON → prompt contains "Full Sentry Issue Details" section |
| **WithoutSentryTool** | No integration tools available → pipeline still generates fix successfully |
| **AuggieThrows** | Auggie throws `TimeoutException` → error notification sent, no PR created |

### Test Infrastructure

Tests use the following fakes (nested sealed classes in the test file):

| Fake | Purpose |
|---|---|
| `FakeAdapter` | Captures `OutgoingMessage` instances sent via `ICommunicationAdapter` |
| `NoOpGitCommandRunner` | Returns empty string for all git commands (used in decision-logic tests) |
| `NoOpAuggieCliRunner` | No-op auggie invocation (used in decision-logic tests) |
| `NoOpPullRequestCreator` | Returns a dummy PR URL (used in decision-logic tests) |
| `FakeGitCommandRunner` | Records all git commands and returns configurable per-command output |
| `FakeAuggieCliRunner` | Records the prompt and working directory passed to auggie |
| `FakePullRequestCreator` | Records PR creation parameters and returns a configurable URL |
| `FakeSentryTool` | Implements `IIntegrationTool` with a fake `sentry_get_issue` function |

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| Webhook returns 400 | Sentry payload format changed or is malformed | Check Sentry integration settings; verify JSON structure matches DTOs |
| No notifications sent | `NotificationChannel` is null/empty, or adapter is disconnected | Set `NotificationChannel` to a valid channel ID; verify adapter connectivity |
| "GitHub owner/repo not configured" | Neither `SentryBugfix:GitHubOwner/Repo` nor `Integrations:GitHub:DefaultOwner/Repo` is set | Configure at least one pair |
| "GitHub token is required" | `Integrations:GitHub:Token` is empty | Set a valid GitHub PAT or app token |
| Auggie produces no changes | The AI couldn't determine a fix from the prompt | Check auggie logs; the issue may need manual intervention |
| Auggie times out | Fix generation exceeded `AuggieTimeoutMinutes` | Increase the timeout or simplify the codebase context |
| Clone fails | Git not on PATH, network issues, or token lacks repo access | Verify `git` is available, network is reachable, and the token has `repo` scope |
| Duplicate branches | Same issue triggers multiple webhooks | Sentry may fire multiple events; consider deduplication or idempotency guards |
