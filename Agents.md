# Agents.md — Anduril AI Assistant

Anduril is a personal AI assistant built from scratch in C#. It connects to Slack (and later Teams), routes messages through a skill system, and falls back to general AI conversation. Designed for a software architect's daily workflow: code review, Sentry triage, standup prep, meeting summaries.

---

## Constraints

### Runtime & Language
- **.NET 10.0** (`net10.0`) — all projects target this
- **C# latest** (`<LangVersion>latest</LangVersion>`)
- **Nullable reference types** enabled globally
- **Implicit usings** enabled globally
- **Treat warnings as errors** (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- Shared build properties live in `Directory.Build.props` at the repo root

### Solution Format
- **`.slnx`** (new XML-based solution format, not `.sln`) — `Anduril.slnx`

### Project Structure
| Project | Purpose |
|---|---|
| `src/Anduril.Core` | Interfaces and models — zero implementation dependencies |
| `src/Anduril.AI` | AI provider implementations (OpenAI, Anthropic, Ollama, LLamaSharp, Augment MCP) |
| `src/Anduril.Skills` | Skill system — prompt-based (markdown) and compiled (DLL) runners |
| `src/Anduril.Communication` | Platform adapters (Slack, Teams, CLI) |
| `src/Anduril.Integrations` | External tool integrations (GitHub, Sentry, Calendar, Gmail, Proton Mail) |
| `src/Anduril.Host` | ASP.NET Core host — DI wiring, background services, HTTP endpoints |
| `src/Anduril.App` | Avalonia desktop app — publishable UI host with chat and code views |
| `src/Anduril.Setup` | Interactive and headless setup tool for first-run configuration |
| `tests/Anduril.Core.Tests` | Core unit tests |
| `tests/Anduril.AI.Tests` | AI provider unit tests |
| `tests/Anduril.Skills.Tests` | Skill system unit tests |
| `tests/Anduril.Communication.Tests` | Communication adapter unit tests |
| `tests/Anduril.Integrations.Tests` | Integration tool unit tests |
| `tests/Anduril.App.Tests` | Desktop app unit tests |
| `tests/Anduril.Host.Tests` | Host/startup unit tests |
| `tests/Anduril.Setup.Tests` | Setup tool unit tests |

### Key NuGet Packages

- **AI**: `Microsoft.Extensions.AI` (10.4.1), `Microsoft.Extensions.AI.Abstractions` (10.4.1), `Microsoft.Extensions.AI.OpenAI` (10.4.1), `Anthropic.SDK` (5.10.0), `OllamaSharp` (5.4.25), `LLamaSharp` (0.26.0), `ModelContextProtocol` (1.1.0), `GitHub.Copilot.SDK` (0.1.25)
- **Desktop**: `Avalonia` (11.3.2) — plus `Avalonia.Desktop`, `Avalonia.Fonts.Inter`, `Avalonia.ReactiveUI`, `Avalonia.Themes.Fluent` at the same version. `Markdown.Avalonia` (11.0.2), `Microsoft.AspNetCore.SignalR.Client` (10.0.5)
- **Communication**: `SlackNet` (0.17.10), `SlackNet.Extensions.DependencyInjection` (0.17.10) — Socket Mode is built into the base package, no separate `SlackNet.SocketMode` needed. `Microsoft.Bot.Builder.Integration.AspNet.Core` (4.23.1)
- **Integrations**: `Octokit` (14.0.0), `Sentry` (6.2.0), `Microsoft.Graph` (5.103.0), `Google.Apis.Gmail.v1` (1.73.0.4029), `MailKit` (4.15.1), `Azure.Identity` (1.19.0)
- **Infrastructure**: `Serilog.AspNetCore` (10.0.0), `Velopack` (0.0.1298), `Microsoft.EntityFrameworkCore.Sqlite` (10.0.5), `Spectre.Console` (0.54.0)
- **Testing**: `TUnit` (1.22.3) + `TUnit.Engine` (1.22.3), `Microsoft.Playwright` (1.58.0)

### Testing Platform
- **TUnit** on **Microsoft.Testing.Platform (MTP)** — configured via `global.json`:
  ```json
  { "test": { "runner": "Microsoft.Testing.Platform" } }
  ```
- **Not xUnit** — Do not add xUnit packages.
- Run tests: `dotnet test --solution Anduril.slnx`
- For project-scoped runs, prefer `dotnet test --project <path-to-csproj>` rather than passing the project path positionally.

### Docker & Containers
- The Docker image must publish and copy both `Anduril.Host` and `Anduril.Setup`; the host may invoke setup on first startup.
- The runtime image runs as the non-root `anduril` user. Runtime-writable directories must be created and owned during image build.
- `/app/sessions` is required at runtime and must stay writable for the session store.
- Container defaults are intentionally quiet: providers/adapters may be disabled by default via `...__Enabled=false` and should be explicitly opted back in.
- Keep `.dockerignore` aligned with local build outputs (`bin/`, `obj/`, `publish/`, test artifacts, etc.) so Windows/local artifacts do not pollute container builds.

---

## Hard Rules

1. **Anduril.Core has zero implementation dependencies.** It contains only interfaces (`IAiProvider`, `ICommunicationAdapter`, `ISkillRunner`, `ISkillRouter`, `IIntegrationTool`) and plain models. It must never reference any other Anduril project.

2. **Never register `IChatClient` directly in DI.** AI providers are registered as `IAiProvider` singletons. To get an `IChatClient`, inject `IEnumerable<IAiProvider>` and resolve the first available provider's `.ChatClient` at execution time.

3. **AI providers require explicit initialization.** Every `IAiProvider` must have `InitializeAsync()` called before use. The `MessageProcessingService` does this on startup. A provider's `IsAvailable` is `false` until initialization succeeds.

4. **Best-effort startup.** Providers and adapters are initialized in a try/catch loop. Failures are logged as warnings, not fatal. The system keeps running with whatever succeeded. At least one AI provider must succeed for useful responses.

5. **One class per file, filename matches class name.** E.g., `SkillResult` lives in `SkillResult.cs`, `SlackAdapter` in `SlackAdapter.cs`.

6. **All test methods must be `async Task`.** TUnit assertions are all async (`await Assert.That(...)`). The `[Test]` attribute replaces xUnit's `[Fact]`.

7. **Filter bot's own messages.** Communication adapters must resolve the bot's user ID at connect time and filter out messages from that ID to prevent infinite reply loops. Also filter out message subtypes (edits, deletes, `bot_message`).

8. **Slack Socket Mode requires two tokens.** `BotToken` (`xoxb-...`) for the Web API, `AppToken` (`xapp-...`) for Socket Mode WebSocket. Both are mandatory — throw `InvalidOperationException` if missing.

9. **Threaded replies.** Responses are always sent as thread replies: `ThreadId = message.ThreadId ?? message.Id`.

10. **Use `SlackServiceBuilder` for Slack wiring.** Never construct `SlackSocketModeClient` directly. The builder handles token binding, handler registration, and client creation. The interface is `ISlackSocketModeClient` (not `ISocketModeClient`), in the `SlackNet` namespace (not `SlackNet.SocketMode`).

11. **Never launch interactive setup in containers or headless sessions.** Use the startup setup policy to decide whether setup should launch. In Docker, redirected-input, or other non-interactive environments, log actionable guidance instead of prompting.

12. **Respect `Enabled` settings for providers and adapters.** Disabled components do not count as configured, should not be registered in DI, and should not produce startup noise.

13. **Container runtime paths must remain writable by `anduril`.** If you add or move runtime storage paths, ensure they are created and owned in the image build.

14. **Keep startup logging actionable.** Do not double-register console logging sinks, and only log adapters as "started" when they are actually connected.

15. **Always build after edits.** Run `dotnet build Anduril.slnx` from the repo root after any code change. `TreatWarningsAsErrors=true` means warnings fail the build — verify the build is clean before considering a change done. In worktrees, run from `.claude/worktrees/<name>/`.

---

## Patterns

### Provider Abstraction
All AI backends implement `IAiProvider` which wraps `IChatClient` (from `Microsoft.Extensions.AI`). The provider handles initialization, availability tracking, and tool exposure. Consumers never depend on a specific provider — they work through the abstraction.

```
IAiProvider → InitializeAsync() → IsAvailable? → ChatClient / GetToolsAsync()
```

### Skill System (Dual-Mode)
Two types of skills coexist:
- **Prompt skills**: Markdown files in `skills/` with `## Trigger`, `## Instructions`, `## Tools`, `## Output Format` sections. Loaded by `PromptSkillLoader`, executed by `PromptSkillRunner`.
- **Compiled skills**: C# classes implementing `ISkill`, loaded from DLLs by `CompiledSkillRunner`.

Both runners implement `ISkillRunner` and are registered with `ISkillRouter` via `RegisterRunner()`.

### Message Processing Pipeline
`MessageProcessingService` (an `IHostedService`) orchestrates everything on startup:
Before normal startup continues, evaluate the startup setup policy; skip interactive setup in containers/non-interactive environments and log configuration guidance instead.
1. Initialize AI providers (best-effort)
2. Register skill runners → `ISkillRouter.RefreshAsync()`
3. Start communication adapters → subscribe to `MessageReceived`
4. On each message: route → execute skill or fallback AI chat → send reply

### Event-Driven Communication
Adapters expose `event Func<IncomingMessage, Task> MessageReceived`. The pipeline subscribes to this event. Adapters normalize platform-specific events into `IncomingMessage` and accept `OutgoingMessage` for replies.

### Configuration via Options Pattern
All configurable components use `IOptions<T>` / `IOptionsMonitor<T>`:
```csharp
builder.Services.Configure<SlackAdapterOptions>(config.GetSection("Communication:Slack"));
builder.Services.Configure<AiProviderOptions>("openai", config.GetSection("AI:OpenAI"));
```
Named options (e.g., `"openai"`) are used for providers since multiple instances share the same options type.

Most providers/adapters also expose an `Enabled` boolean. When adding new ones, default it to `true`, allow environment-variable override, and ensure registration/startup logic honors it.

### Setup Tool
`Anduril.Setup` supports both interactive desktop setup and headless non-interactive setup.

- Non-interactive setup is driven by CLI args and `ANDURIL_SETUP_*` environment variables
- Command-line arguments override environment variables
- Supported non-interactive providers are currently `ollama`, `anthropic`, and `openai`
- The host should rely on the setup tool for configuration bootstrapping rather than duplicating setup logic

### DI Registration
- **AI providers**: `AddSingleton<IAiProvider, ConcreteProvider>()` — multiple implementations of the same interface
- **Communication adapters**: `AddSingleton<ICommunicationAdapter, ConcreteAdapter>()` — same pattern
- **Skill components**: Registered as concrete singletons (`PromptSkillRunner`, `CompiledSkillRunner`, `SkillRouter`)
- **Background services**: `AddHostedService<T>()` for `MessageProcessingService` and `UpdateService`

### TUnit Assertions
```csharp
await Assert.That(result.Success).IsTrue();
await Assert.That(result.Response).IsEqualTo("Hello!");
await Assert.That(result.Data).IsNull();
await Assert.That(result.Data).IsSameReferenceAs(expected);
await Assert.That(list).HasCount().EqualTo(4);  // ← WRONG (obsolete)
await Assert.That(list.Count).IsEqualTo(4);     // ← CORRECT
```

### Error Responses
On processing failure, send a user-facing error message ("Sorry, something went wrong...") back through the same adapter. Log the full exception separately. Wrap the error-response send in its own try/catch to avoid masking the original error.

### Logging
Serilog with structured logging throughout. Use `ILogger<T>` via DI. Use message templates with named parameters: `_logger.LogInformation("AI provider '{Name}' initialized", provider.Name)`.

### Docker Verification
- Prefer in-container or file-based validation when `docker logs` output appears stale, duplicated, or misleading.
- Validate both image contents and runtime behavior: environment variables, directory ownership, and ability to write required files.

