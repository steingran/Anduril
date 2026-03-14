# Contributing to Anduril

Thanks for your interest in improving Anduril.

## Before you start

- Read the README for setup and architecture context.
- Check existing issues before starting larger work.
- For security issues, do **not** open a public issue. Follow `SECURITY.md` instead.
- Keep pull requests focused. Smaller PRs are easier to review and merge.

## Development environment

- .NET 10 SDK
- Git
- Optional: Docker / Docker Compose for container validation
- Optional provider credentials if you want to test Slack, GitHub, Sentry, Gmail, or Office 365 integrations locally

## Local workflow

```bash
# Restore
dotnet restore Anduril.slnx

# Build
dotnet build Anduril.slnx -c Release

# Test
dotnet test --solution Anduril.slnx -c Release
```

Prefer the smallest useful test scope while iterating, then run the solution tests before opening a PR.

## Project conventions

Please follow the conventions already used in the repository:

- Target `net10.0`
- Use latest C# language features supported by the SDK
- Nullable reference types stay enabled
- Warnings are treated as errors
- Keep **one class / interface / record per file**
- Match file name to type name
- Do not add `#region` / `#endregion`
- Do not add FluentAssertions
- Keep `Anduril.Core` free of implementation dependencies
- Do not register `IChatClient` directly in DI; register providers as `IAiProvider`
- Respect `Enabled` settings for adapters and providers

## Testing expectations

- Tests use **TUnit** on Microsoft.Testing.Platform
- Test methods should be `async Task`
- Use async assertions such as `await Assert.That(...)`
- Add or update tests when changing behavior

## Documentation expectations

Update docs when your change affects:

- setup steps
- configuration keys
- Docker behavior
- skill usage
- public APIs or expected outputs

## Pull request guidance

A good PR typically includes:

- a clear summary of the change
- motivation / context
- tests added or updated
- screenshots / logs when UI or runtime behavior changed
- docs updates when applicable

## Commit messages

A strict commit format is not required, but clear and descriptive commits help.

Examples:

- `Add support for Slack message subtype filtering`
- `Fix Docker session volume path`
- `Document non-interactive setup workflow`

## Reviewing and merge policy

Maintainers may ask for:

- narrower scope
- more tests
- better docs
- safer defaults
- follow-up issues instead of bundling unrelated work

Thanks again for contributing.
