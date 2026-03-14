# Andúril

A personal AI assistant built from scratch in C#. Andúril connects to Slack (and later Teams), routes messages through a skill system, and falls back to general AI conversation. Designed for a software architect's daily workflow: code review, Sentry triage, standup prep, and meeting summaries.

## Features

- **Multi-provider AI** — swap between OpenAI, Anthropic, Augment Code, Ollama, or LLamaSharp without changing a line of code
- **Dual-mode skill system** — write skills as simple Markdown prompt files or as compiled C# plugins
- **Slack integration** — real-time messaging via Socket Mode with threaded replies ([setup guide](docs/slack-setup.md))
- **Tool augmentation** — Augment Code MCP provider exposes codebase-aware tools that chat providers can call
- **Integration hooks** — GitHub, Sentry, Office 365 Calendar, and Proton Mail adapters for pulling context into conversations ([GitHub setup](docs/github.md) · [Office 365 setup](docs/office365-calendar.md) · [Proton Mail setup](docs/protonmail-setup.md))
- **Best-effort startup** — providers and adapters initialise independently; failures are logged, never fatal

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Andúril.Host                           │
│  ASP.NET Core  ·  DI wiring  ·  Background services        │
├──────────┬──────────┬───────────┬───────────┬───────────────┤
│ .AI      │ .Skills  │ .Comms    │ .Integr.  │ .Core         │
│ OpenAI   │ Prompt   │ Slack     │ GitHub    │ Interfaces    │
│ Anthropic│ Compiled │ Teams*    │ Sentry    │ Models        │
│ Augment  │          │ CLI       │ Calendar  │               │
│ Ollama   │          │           │           │               │
│ LLamaSharp│         │           │           │               │
└──────────┴──────────┴───────────┴───────────┴───────────────┘
                              * stub
```

| Project | Purpose |
|---|---|
| `Anduril.Core` | Interfaces and models — zero implementation dependencies |
| `Anduril.AI` | AI provider implementations |
| `Anduril.Skills` | Skill system — prompt-based (Markdown) and compiled (DLL) runners |
| `Anduril.Communication` | Platform adapters (Slack, Teams, CLI) |
| `Anduril.Integrations` | External tool integrations (GitHub, Sentry, Calendar, Proton Mail) |
| `Anduril.Host` | ASP.NET Core host — DI, background services, HTTP endpoints |

## AI Providers

| Provider | Type | Notes |
|---|---|---|
| OpenAI | Chat | GPT-4o and compatible models |
| Anthropic | Chat | Claude Sonnet, Opus, etc. |
| Augment Chat | Chat | Augment Code HTTP API — no separate API key needed if you have an Augment subscription |
| Augment MCP | Tools | Exposes codebase-aware tools via MCP for other chat providers to call |
| Ollama | Chat | Local models (e.g. Qwen, Llama) — no API key required |
| LLamaSharp | Chat | Run GGUF models directly in-process |

## Support and Stability

Andúril is maintained as a personal daily-driver assistant project. Support is strongest for the paths used regularly by the maintainer; everything else should be treated as best-effort unless noted otherwise.

### Communication adapters

| Adapter | Status | Notes |
|---|---|---|
| CLI | Supported | Best local development and smoke-test path |
| Slack | Supported | Primary real-time adapter; threaded replies supported |
| Signal | Experimental | Available, but expect configuration and operational edge cases |
| Teams | Stub / experimental | Present in the architecture, not positioned as production-ready yet |

### Provider and integration maturity

| Area | Status | Notes |
|---|---|---|
| OpenAI / Anthropic / Augment Chat / Ollama | Supported | Best-maintained chat-provider paths |
| Augment MCP | Supported (advanced) | Intended as a tool provider for richer code-aware workflows |
| LLamaSharp | Experimental | Local in-process model support; expect more manual setup/tuning |
| GitHub / Sentry / Office 365 Calendar / Slack Query | Supported | First-class workflow-oriented integrations |
| Gmail / Medium article retrieval / personal schedulers | Experimental | Useful, but more likely to change as the project evolves |

## Built-in Skills

| Skill | Trigger | Description |
|---|---|---|
| Code Reviewer | "review this PR" | Analyses pull requests and provides actionable feedback |
| Sentry Triage | "triage this error" | Summarises and suggests fixes for production errors |
| Standup Helper | "standup" | Generates a daily status update from GitHub and calendar activity ([docs](docs/standup-helper.md)) |
| Meeting Summary | "summarize this meeting" | Produces structured notes and action items from a transcript |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- At least one AI provider configured (see [Configuration](#configuration))
- For Slack: a Slack app with Socket Mode enabled ([setup guide](docs/slack-setup.md))

## Getting Started

```bash
# Clone the repository
git clone https://github.com/steingran/Anduril.git
cd Anduril

# Build
dotnet build Anduril.slnx

# Run tests
dotnet test --solution Anduril.slnx

# Run the host
dotnet run --project src/Anduril.Host
```

## Configuration

For a quieter first-time setup, start from `src/Anduril.Host/appsettings.example.json` and enable only the components you actually want to run. The checked-in `src/Anduril.Host/appsettings.json` remains the full reference surface used by development.

Sensitive values (API keys, tokens) should be stored in [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) or environment variables instead:

```bash
cd src/Anduril.Host
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "AI:Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "AI:AugmentChat:ApiKey" "your-augment-api-key"
dotnet user-secrets set "Communication:Slack:BotToken" "xoxb-..."
dotnet user-secrets set "Communication:Slack:AppToken" "xapp-..."
```

Most AI providers and communication adapters also support an `Enabled` switch, so you can turn individual components on or off in `appsettings.json` or via environment variables. Examples: `AI:Ollama:Enabled=false`, `Communication:Slack:Enabled=false`, or env vars such as `AI__Ollama__Enabled=false`.

Recommended public/open-source onboarding flow:

1. Copy values from `src/Anduril.Host/appsettings.example.json` into your local config.
2. Enable `Communication:Cli` and exactly one chat-capable provider.
3. Keep secrets out of tracked files and inject them with user-secrets or environment variables.
4. Only enable adapters once their credentials and endpoints are ready. Most integration tools (GitHub, Sentry, Office 365 Calendar, Gmail, SlackQuery, Medium) become available automatically once their credentials are configured and do not need an explicit `Enabled` switch. ProtonMail and WeeklyMenuPlanner are exceptions — they are off by default and require `Enabled: true` in config to register.

You only need **one** chat-capable AI provider to get started. The quickest options:

| Option | What to configure |
|---|---|
| **Augment Code** | Set `AI:AugmentChat:ApiKey` — uses your existing Augment subscription |
| **Ollama** | Run `ollama serve` locally — no API key needed |
| **OpenAI** | Set `AI:OpenAI:ApiKey` |
| **Anthropic** | Set `AI:Anthropic:ApiKey` |

## Non-interactive setup

`Anduril.Setup` can run headlessly in containers or other non-interactive environments.

Supported providers in non-interactive mode:

- `ollama`
- `anthropic`
- `openai`

CLI flags:

- `--non-interactive`
- `--provider`
- `--model`
- `--api-key`
- `--endpoint`
- `--config` / `--config-path`

Environment variables:

- `ANDURIL_SETUP_NON_INTERACTIVE`
- `ANDURIL_SETUP_PROVIDER`
- `ANDURIL_SETUP_MODEL`
- `ANDURIL_SETUP_API_KEY`
- `ANDURIL_SETUP_ENDPOINT`
- `ANDURIL_SETUP_CONFIG_PATH`

Command-line arguments override environment variables.

Example:

```bash
ANDURIL_SETUP_NON_INTERACTIVE=true \
ANDURIL_SETUP_PROVIDER=ollama \
ANDURIL_SETUP_MODEL=llama3.1:8b \
ANDURIL_SETUP_ENDPOINT=http://ollama:11434 \
dotnet run --project src/Anduril.Setup -- --config src/Anduril.Host/appsettings.json
```

## Docker

```bash
# Build and run with Docker Compose (includes Ollama sidecar)
docker compose up --build
```

The Docker image keeps startup quieter by disabling providers/adapters that are usually missing in a fresh container. Re-enable only the components you want via environment variables:

- `AI__OpenAI__Enabled=true` + `AI__OpenAI__ApiKey=...`
- `AI__Anthropic__Enabled=true` + `AI__Anthropic__ApiKey=...`
- `AI__AugmentChat__Enabled=true` + `AI__AugmentChat__ApiKey=...`
- `AI__Augment__Enabled=true` if you have the Augment CLI available in the container
- `AI__Ollama__Enabled=true` + `AI__Ollama__Endpoint=...` + `AI__Ollama__Model=...`
- `Communication__Slack__Enabled=true` + Slack tokens
- `Communication__Signal__Enabled=true` + Signal settings
- `Communication__Teams__Enabled=true` + Teams app credentials
- `Communication__Cli__Enabled=true` to enable the console adapter

If you use the included `docker-compose.yml` and want Anduril to talk to the bundled Ollama sidecar, add `AI__Ollama__Enabled=true` under the `anduril` service environment alongside the existing Ollama endpoint/model settings.

## Running Tests

Andúril uses [TUnit](https://github.com/thomhurst/TUnit) on the Microsoft Testing Platform:

```bash
dotnet test --solution Anduril.slnx --verbosity normal
```

## Release Integrity

Tagged releases publish platform archives plus SHA-256 checksum files. The Docker release path publishes to GHCR with provenance and SBOM attestations enabled, and binary release artifacts are attested in GitHub Actions.

If you consume Andúril from a release rather than from source, prefer verifying checksums and attestations as part of your own supply-chain policy.

## Community

- [Contributing guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security policy](SECURITY.md)
- [Support guide](SUPPORT.md)

## License

[MIT](LICENSE) © Stein J. Gran
