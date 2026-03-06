namespace Anduril.Setup;

internal sealed class SetupCliOptions
{
    public bool NonInteractive { get; init; }

    public bool ShowHelp { get; init; }

    public string? ConfigPath { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public string? ApiKey { get; init; }

    public string? Endpoint { get; init; }
}