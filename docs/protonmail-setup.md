# Proton Mail Integration

Anduril integrates with Proton Mail through the local [Proton Mail Bridge](https://proton.me/mail/bridge). The Bridge exposes standard IMAP and SMTP endpoints on your machine, which lets Anduril read, search, send, and manage email without talking directly to Proton's web APIs.

Authentication uses the **Bridge-generated mailbox username and password**, not your main Proton account password.

## Available Functions

| Function | Description |
|---|---|
| `protonmail_list_messages` | List recent Proton Mail messages from a mailbox such as `INBOX` or `Archive` |
| `protonmail_get_message` | Get the full details of a Proton Mail message by mailbox and IMAP UID |
| `protonmail_search` | Search Proton Mail messages using tokens like `from:`, `subject:`, `body:`, `is:read`, `is:unread`, `after:`, and `before:` |
| `protonmail_send` | Send a new email message through Proton Mail Bridge SMTP |
| `protonmail_reply` | Reply to an existing Proton Mail message by mailbox and IMAP UID |
| `protonmail_move_message` | Move a message between mailboxes |
| `protonmail_set_read_status` | Mark a message as read or unread |

## Why Anduril Uses Proton Mail Bridge

Proton Mail does not expose a Gmail-style mail API for normal third-party mailbox access. Instead, the supported desktop integration path is Proton Mail Bridge, which:

1. Runs locally on your machine
2. Authenticates you with Proton Mail
3. Exposes local IMAP and SMTP endpoints for mail clients and tools
4. Handles Proton's encryption model on your behalf

This makes **manual Bridge installation and account sign-in** the most user-friendly and reliable setup path. Anduril should not try to auto-install or auto-sign-in to Bridge because Bridge setup is interactive and tied to the local desktop session.

## Prerequisites

- A paid Proton Mail plan that includes Proton Mail Bridge support
- Proton Mail Bridge installed and signed in on the same machine where Anduril runs
- A Proton Mail account added inside Bridge
- The Bridge-generated mailbox credentials copied from Bridge

## Step 1 — Install Proton Mail Bridge

Download Proton Mail Bridge from Proton's official download page:

- https://proton.me/mail/download

Proton also documents the Bridge CLI here:

- https://proton.me/support/bridge-cli-guide

After installation:

1. Start Proton Mail Bridge
2. Sign in to your Proton account
3. Add the mailbox you want Anduril to use
4. Open the mailbox details in Bridge and copy the generated IMAP/SMTP credentials

Bridge commonly shows settings similar to:

- IMAP host: `127.0.0.1`
- IMAP port: `1143`
- SMTP host: `127.0.0.1`
- SMTP port: `1025`
- Security: `STARTTLS`
- Username: your Bridge mailbox username
- Password: the Bridge-generated mailbox password

## Step 2 — Configure Anduril

Add this section to `src/Anduril.Host/appsettings.json`:

```json
{
  "Integrations": {
    "ProtonMail": {
      "Enabled": true,
      "Username": "",
      "Password": "",
      "ImapHost": "localhost",
      "ImapPort": 1143,
      "SmtpHost": "localhost",
      "SmtpPort": 1025,
      "UseSsl": false,
      "AcceptSelfSignedCertificate": true
    }
  }
}
```

### User Secrets (recommended for sensitive values)

Store the Bridge credentials in user secrets instead of committing them:

```bash
cd src/Anduril.Host
dotnet user-secrets set "Integrations:ProtonMail:Username" "your-bridge-username"
dotnet user-secrets set "Integrations:ProtonMail:Password" "your-bridge-password"
```

The hostnames and ports can stay in `appsettings.json`. The username and password should be kept in user secrets, environment variables, or a secret manager.

## Connection Security Notes

`UseSsl` defaults to `false` because Bridge runs locally on loopback ports rather than on implicit TLS ports like 993 and 465.

Internally, Anduril uses MailKit's automatic socket negotiation when `UseSsl` is `false`, which works well with local Bridge endpoints that advertise `STARTTLS`.

`AcceptSelfSignedCertificate` defaults to `true` because Proton Mail Bridge always presents a self-signed certificate on its loopback ports. The bypass is restricted to loopback addresses (`localhost`, `127.0.0.1`, `::1`) — if you point `ImapHost` or `SmtpHost` at a non-loopback address, standard certificate validation is used regardless of this setting. Set `AcceptSelfSignedCertificate` to `false` only if you are using a trusted certificate (for example, a corporate IMAP relay with a properly signed cert).

If you want Anduril to require STARTTLS explicitly, set:

```json
{
  "Integrations": {
    "ProtonMail": {
      "UseSsl": true
    }
  }
}
```

## Example Queries

Once configured, Anduril can handle requests like:

- `Check my Proton Mail inbox`
- `Search Proton Mail for from:alice subject:invoice`
- `Show Proton Mail message 42 from INBOX`
- `Reply to Proton Mail message 42 and say thanks`
- `Mark Proton Mail message 42 as read`
- `Move Proton Mail message 42 from INBOX to Archive`

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "Proton Mail Bridge username not configured" | `Integrations:ProtonMail:Username` is missing | Set the Bridge username in user secrets or environment variables |
| "Proton Mail Bridge password not configured" | `Integrations:ProtonMail:Password` is missing | Set the Bridge password in user secrets or environment variables |
| "Proton Mail Bridge connectivity check failed" | Bridge is not running or credentials are wrong | Start Bridge, verify the account is added, and copy the Bridge-generated credentials again |
| IMAP or SMTP connection refused on `localhost` | Bridge is not running on this machine | Launch Proton Mail Bridge locally and verify the ports shown in Bridge |
| Authentication fails even though Proton login works | Using the main Proton account password instead of the Bridge password | Use the Bridge-generated mailbox password, not your regular Proton password |
| Search works but send fails | SMTP settings differ from defaults | Verify the SMTP host, port, and security settings shown by Bridge |
| Send works but read/search fails | IMAP settings differ from defaults | Verify the IMAP host, port, and security settings shown by Bridge |

## Notes on Automated Installation

It is technically possible to point users at Proton's installer or Bridge CLI, but **Anduril should not attempt to install or configure Proton Mail Bridge automatically**.

Reasons:

- Bridge installation is OS-specific
- Bridge sign-in is interactive
- Mailbox setup happens in the local user session
- The generated mailbox password must be handled as a secret

So the best UX for now is:

1. Provide a clear setup guide
2. Validate connectivity on startup
3. Log actionable guidance when Bridge is missing or misconfigured

---

← [Back to README](../README.md)
