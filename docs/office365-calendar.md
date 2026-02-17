# Office 365 Calendar Integration

Anduril integrates with Microsoft 365 (Office 365) calendars via the [Microsoft Graph API](https://learn.microsoft.com/en-us/graph/overview). This lets the assistant read your calendar events — today's schedule, upcoming meetings, and historical events — so skills like the [Standup Helper](standup-helper.md) can include meeting context automatically.

Authentication uses the **client credentials flow** via [Azure.Identity](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme). Tokens are acquired and refreshed automatically — no manual token management required.

## Available Functions

| Function | Description |
|---|---|
| `calendar_today` | Returns today's calendar events (up to 20), ordered by start time |
| `calendar_upcoming` | Returns events for the next *N* days (default 7, up to 50) |
| `calendar_events_since` | Returns events that occurred since a given date/time (up to 50) |

## Configuration

The integration requires four settings under `Integrations:Office365Calendar`:

| Setting | Required | Description |
|---|---|---|
| `TenantId` | Yes | Your Microsoft Entra (Azure AD) tenant ID |
| `ClientId` | Yes | The application (client) ID from your app registration |
| `ClientSecret` | Yes | A client secret generated for the app registration |
| `UserId` | Yes | The user ID or UPN (e.g. `user@contoso.com`) whose calendar to query |

> `UserId` is required because the client credentials flow runs without an interactive user session, so the Microsoft Graph `/me` endpoint is not available. You must specify which user's calendar to read.

### appsettings.json

```json
{
  "Integrations": {
    "Office365Calendar": {
      "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "ClientSecret": "",
      "UserId": "user@contoso.com"
    }
  }
}
```

### User Secrets (recommended for sensitive values)

```bash
cd src/Anduril.Host
dotnet user-secrets set "Integrations:Office365Calendar:ClientSecret" "your-client-secret-value"
```

The `TenantId`, `ClientId`, and `UserId` are not secrets and can stay in `appsettings.json`. Only the `ClientSecret` should be stored in user secrets (or an environment variable / key vault in production).

## How to Obtain the Required Settings

### Step 1 — Register an Application in Microsoft Entra

1. Go to the [Azure portal → Microsoft Entra ID → App registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade).
2. Click **New registration**.
3. Enter a name (e.g. `Anduril Calendar`).
4. Under **Supported account types**, choose *Accounts in this organizational directory only* (single tenant) unless you need multi-tenant access.
5. Leave **Redirect URI** blank — it is not needed for the client credentials flow.
6. Click **Register**.
7. On the app's **Overview** page, copy:
   - **Application (client) ID** → this is your `ClientId`
   - **Directory (tenant) ID** → this is your `TenantId`

### Step 2 — Create a Client Secret

1. In your app registration, go to **Certificates & secrets → Client secrets**.
2. Click **New client secret**.
3. Enter a description (e.g. `Anduril`) and choose an expiry period.
4. Click **Add**.
5. **Copy the secret value immediately** — it is only shown once. This is your `ClientSecret`.

> ⚠️ Client secrets expire. Set a calendar reminder to rotate the secret before it expires. You can have multiple active secrets to enable zero-downtime rotation.

### Step 3 — Add API Permissions

1. In your app registration, go to **API permissions → Add a permission**.
2. Select **Microsoft Graph → Application permissions** (not *Delegated*).
3. Search for and add **`Calendars.Read`**.
4. Click **Grant admin consent for \<your tenant\>**. Admin consent is **required** for application permissions — the integration will not work without it.

> **Why Application permissions?** The client credentials flow runs as the application itself, not as a user. Delegated permissions require an interactive sign-in, which is not possible for a background service.

### Step 4 — Find Your User ID

The `UserId` setting accepts either:
- A **user principal name** (UPN): e.g. `stein@contoso.com`
- A **user object ID**: e.g. `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`

To find your UPN or object ID:
1. Go to the [Azure portal → Microsoft Entra ID → Users](https://portal.azure.com/#view/Microsoft_AAD_UsersAndTenants/UserManagementMenuBlade/~/AllUsers).
2. Click on the user whose calendar you want to read.
3. Copy the **User principal name** or **Object ID** from the profile page.

Alternatively, if you know your email address in Microsoft 365, that is typically the same as your UPN.

## How It Works

Under the hood, the integration uses `Azure.Identity.ClientSecretCredential` to authenticate with Microsoft Entra. This credential:

1. Acquires an access token from Microsoft Entra using the client credentials grant.
2. Caches the token in memory.
3. Automatically refreshes the token before it expires.

You never need to manually obtain or rotate access tokens. As long as the client secret is valid, authentication is fully automatic.

## Graceful Degradation

If any of the required settings (`TenantId`, `ClientId`, `ClientSecret`, `UserId`) are missing or empty, the integration logs a warning and reports itself as unavailable. Skills that depend on calendar data (like Standup Helper) will still run — they simply omit the calendar sections and note that the integration is unavailable.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "credentials not fully configured" in logs | `TenantId`, `ClientId`, or `ClientSecret` is missing | Set all three values (see Steps 1–2 above) |
| "UserId not configured" in logs | `UserId` is empty | Set the UPN or object ID of the target user (see Step 4) |
| `401 Unauthorized` from Microsoft Graph | Client secret is invalid or expired | Generate a new client secret and update the configuration |
| `403 Forbidden` | Admin consent not granted for `Calendars.Read` | Ask your tenant admin to grant consent (see Step 3) |
| No events returned but calendar has events | `UserId` points to a different user | Verify the UPN or object ID matches the user whose calendar you want |

---

← [Back to README](../README.md)

