namespace Anduril.Setup;

internal sealed class SetupRequest
{
    public required string ConfigPath { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required string ApiKey { get; init; }

    public required string Endpoint { get; init; }
}