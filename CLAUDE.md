# CLAUDE.md

This file guides Claude Code (and other AI assistants) working in this repository.

@Agents.md

The import above (`Agents.md`) is the source of truth for hard rules, patterns, and
conventions — read it in full before making changes. This file adds orientation,
day-to-day workflow commands, and pointers that are specific to working as an
interactive coding agent in this repo.

---

## What this project is

Andúril is a personal AI assistant written in C# (.NET 10). It listens on chat
platforms (Slack today; Signal experimental; Teams a stub), routes incoming
messages through a skill system, and falls back to general AI chat when no skill
matches. It's built around swappable AI providers, swappable communication
adapters, and two kinds of skills (Markdown prompt skills and compiled C#
skills). See `README.md` for the user-facing feature overview and setup guide.

## Repository layout

```
src/
  Anduril.Core           interfaces + models only, zero implementation deps
  Anduril.AI             AI provider implementations (Providers/, Detection/)
  Anduril.Skills          skill runners (prompt-based + compiled), Compiled/
  Anduril.Communication  chat platform adapters (Slack, Signal, Teams, CLI)
  Anduril.Integrations   external tools (GitHub, Sentry, Gmail, ProtonMail,
                         Office365 Calendar, Slack query, Medium article
                         retrieval, weekly menu planner)
  Anduril.Host           ASP.NET Core host: DI wiring, hosted/background
                         services, SignalR hubs, webhooks (Services/, Hubs/)
  Anduril.App            Avalonia desktop app (chat + code views)
  Anduril.Setup          interactive + headless first-run setup tool
tests/                   one test project per src project, mirrors its name
docs/                    setup guides per integration/adapter + skill docs
skills/                  Markdown prompt-skill definitions
Anduril.slnx             solution file (new XML .slnx format)
Directory.Build.props    shared MSBuild properties (net10.0, nullable, etc.)
Directory.Packages.props centrally managed NuGet package versions
```

`Anduril.Core` sits at the bottom of the dependency graph and must never
reference another Anduril project (see Agents.md hard rule #1). Everything
else depends inward on `Core`'s interfaces (`IAiProvider`, `ICommunicationAdapter`,
`ISkill`/`ISkillRunner`/`ISkillRouter`, `IIntegrationTool`).

Not all integrations are "skills" in the `ISkill` sense — `Anduril.Integrations`
tools (`IIntegrationTool`) are exposed to AI providers as callable tools, while
the Sentry-bugfix automation in `Anduril.Host/Services` is a webhook-triggered
`IHostedService` pipeline, not a chat-invoked skill. See
`docs/skills/sentry-bugfix.md` for that flow specifically.

## Everyday commands

```bash
dotnet restore Anduril.slnx
dotnet build Anduril.slnx                       # do this after every code change
dotnet test --solution Anduril.slnx             # full test run
dotnet test --project tests/Anduril.Core.Tests/Anduril.Core.Tests.csproj  # scoped run
dotnet run --project src/Anduril.Host           # run the host locally
dotnet run --project src/Anduril.Setup          # interactive setup
```

`TreatWarningsAsErrors` is enabled solution-wide, so a build with warnings
fails outright — always confirm `dotnet build Anduril.slnx` is clean before
considering a change done. If you're in a `.claude/worktrees/<name>/` checkout,
run these commands from that worktree directory, not the primary checkout.

CI (`.github/workflows/ci.yml`) runs `dotnet restore` → `dotnet build -c Release`
→ `dotnet test -c Release` on every push/PR to `main`. There are also separate
CodeQL, dependency-review, secret-scan, and release workflows under `.github/workflows/`.

## Conventions quick-reference

- One type per file, filename matches the type name.
- `net10.0`, latest C#, nullable reference types on, implicit usings on.
- Tests use **TUnit** on Microsoft.Testing.Platform, not xUnit — no FluentAssertions,
  no `#region`/`#endregion`. All test methods are `async Task` with `await Assert.That(...)`.
- AI providers/communication adapters are registered as their interface type
  (`IAiProvider` / `ICommunicationAdapter`), never `IChatClient` directly in DI.
- Configuration goes through `IOptions<T>`/`IOptionsMonitor<T>`; most providers
  and adapters expose an `Enabled` switch that must be respected.
- Full details, numbered hard rules, and code patterns live in `Agents.md`
  (imported above) — treat that file as authoritative when it and this file
  seem to disagree, and update it (not just this file) when conventions change.

## Where to look for more

- `README.md` — features, architecture diagram, provider/adapter maturity
  matrix, configuration and Docker instructions.
- `CONTRIBUTING.md` — PR workflow and review expectations.
- `docs/*.md` — per-integration setup guides (Slack, Teams, Signal, GitHub,
  Gmail, Office 365 Calendar, ProtonMail) and `docs/standup-helper.md` /
  `docs/skills/sentry-bugfix.md` for feature-specific behavior.
- `skills/*.md` — the actual Markdown prompt-skill definitions loaded at runtime.
