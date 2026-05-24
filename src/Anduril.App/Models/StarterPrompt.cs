namespace Anduril.App.Models;

public sealed record StarterPrompt
{
    public required string Title { get; init; }

    public required string Prompt { get; init; }

    public string? Description { get; init; }
}
