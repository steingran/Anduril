namespace Anduril.App.Models;

public sealed record SlashCommandSuggestion
{
    public required string Command { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string InsertText => $"{Command} ";
}
