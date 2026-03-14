# Security Policy

## Supported versions

Anduril is an actively evolving project. Security fixes are most likely to be made on:

- the latest release
- the `main` branch

Older versions may not receive fixes.

## Reporting a vulnerability

Please **do not** open public GitHub issues for suspected vulnerabilities.

Instead:

1. Use GitHub's private vulnerability reporting for this repository if it is enabled.
2. If private reporting is not available, contact the maintainer privately through GitHub before disclosing details publicly.

Please include:

- a clear description of the issue
- affected version / commit
- reproduction steps or proof of concept
- impact assessment
- any suggested mitigation

## What to expect

The maintainer will try to:

- acknowledge receipt in a reasonable timeframe
- validate the report
- assess severity and scope
- coordinate a fix and disclosure plan

Please avoid public disclosure until a fix or mitigation is available.

## Scope guidance

This repository includes components that may interact with:

- AI provider APIs
- Slack, Signal, Teams, GitHub, Sentry, Gmail, and Office 365
- local session storage
- Docker and self-hosted runtime environments

Security reports are especially helpful for:

- secret leakage
- authentication / authorization flaws
- webhook validation issues
- unsafe command execution
- container misconfiguration
- data exposure across sessions, users, or integrations

## Secure usage reminders

Operators are responsible for:

- storing secrets outside committed files
- rotating credentials if exposure is suspected
- restricting third-party tokens to least privilege
- reviewing provider and integration configuration before production use

## Disclosure

Once a fix is available, the project may publish:

- a patched release
- release notes or advisory text
- recommended mitigation steps for users who cannot upgrade immediately
