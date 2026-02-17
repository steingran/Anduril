# GitHub Integration

Anduril integrates with GitHub via the [Octokit](https://github.com/octokit/octokit.net) library. This lets the assistant list issues, browse pull requests, inspect changed files, and query recent activity — so skills like the [Standup Helper](standup-helper.md) can include development context automatically.

Authentication uses a **personal access token** (classic or fine-grained). The token is configured once and used for all API calls.

## Available Functions

| Function | Description |
|---|---|
| `github_list_issues` | List open issues for a repository |
| `github_get_issue` | Get details of a specific issue by number |
| `github_list_pull_requests` | List open pull requests for a repository |
| `github_get_pr_files` | Get the list of changed files in a pull request |
| `github_list_pull_requests_since` | List pull requests updated since a given date/time |
| `github_list_issues_since` | List issues updated since a given date/time |

All functions accept optional `owner` and `repo` parameters. If omitted, the `DefaultOwner` and `DefaultRepo` from configuration are used.

## Configuration

The integration requires one setting and supports two optional defaults, under `Integrations:GitHub`:

| Setting | Required | Description |
|---|---|---|
| `Token` | Yes | A GitHub personal access token |
| `DefaultOwner` | No | Default repository owner (e.g. `steingran`). Used when a function call omits the `owner` parameter |
| `DefaultRepo` | No | Default repository name (e.g. `Anduril`). Used when a function call omits the `repo` parameter |

### appsettings.json

```json
{
  "Integrations": {
    "GitHub": {
      "Token": "",
      "DefaultOwner": "steingran",
      "DefaultRepo": "Anduril"
    }
  }
}
```

### User Secrets (recommended for the token)

```bash
cd src/Anduril.Host
dotnet user-secrets set "Integrations:GitHub:Token" "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
```

The `DefaultOwner` and `DefaultRepo` are not secrets and can stay in `appsettings.json`. Only the `Token` should be stored in user secrets (or an environment variable / key vault in production).

## How to Create a Personal Access Token

### Option A — Classic Token

1. Go to [GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)](https://github.com/settings/tokens).
2. Click **Generate new token** → **Generate new token (classic)**.
3. Enter a note (e.g. `Anduril`).
4. Set an expiration period.
5. Select scopes (see [Token Scopes](#token-scopes) below).
6. Click **Generate token**.
7. **Copy the token immediately** — it is only shown once.

### Option B — Fine-Grained Token

1. Go to [GitHub → Settings → Developer settings → Personal access tokens → Fine-grained tokens](https://github.com/settings/personal-access-tokens/new).
2. Enter a token name (e.g. `Anduril`) and expiration.
3. Under **Repository access**, choose *Only select repositories* and pick the repositories you want Anduril to access.
4. Under **Permissions → Repository permissions**, grant:
   - **Issues**: Read-only
   - **Pull requests**: Read-only
5. Click **Generate token**.
6. **Copy the token immediately** — it is only shown once.

> Fine-grained tokens are recommended because they follow the principle of least privilege — you grant access only to the specific repositories and permissions needed.

## Token Scopes

| Token type | Scope | When needed |
|---|---|---|
| Classic | `repo` | Required for **private** repositories (grants full repo access) |
| Classic | `public_repo` | Sufficient for **public** repositories only |
| Classic | *(no scopes)* | Sufficient for reading public repository metadata |
| Fine-grained | Issues: Read-only | Reading issues |
| Fine-grained | Pull requests: Read-only | Reading pull requests and changed files |

> ⚠️ Classic tokens with `repo` scope grant broad access (read/write to all repositories). Prefer fine-grained tokens for tighter control.

## Token Lifetime

- **Classic tokens** can be set to expire after 30, 60, or 90 days, or set to no expiration (not recommended).
- **Fine-grained tokens** have a maximum lifetime of 1 year.

GitHub will send email reminders before a token expires. Set a calendar reminder to rotate the token before it expires.

## Graceful Degradation

If the `Token` is not configured or is empty, the integration logs a warning and reports itself as unavailable. Skills that depend on GitHub data (like Standup Helper) will still run — they simply omit the GitHub sections and note that the integration is unavailable.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "GitHub token not configured" in logs | `Token` is empty or missing | Set a valid personal access token (see above) |
| `401 Unauthorized` / `AuthorizationException` | Token is invalid or expired | Generate a new token and update the configuration |
| `403 Forbidden` | Token lacks required scopes, or rate limit exceeded | Check scopes (see table above); wait for rate limit reset or use an authenticated token |
| `404 Not Found` | Repository doesn't exist, or token lacks access to a private repo | Verify owner/repo names; ensure the token has `repo` scope (classic) or repository access (fine-grained) |
| "Repository owner is required" error | `DefaultOwner` not set and `owner` not passed to function | Set `DefaultOwner` in configuration |
| "Repository name is required" error | `DefaultRepo` not set and `repo` not passed to function | Set `DefaultRepo` in configuration |

---

← [Back to README](../README.md)

