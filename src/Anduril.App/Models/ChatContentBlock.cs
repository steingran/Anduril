namespace Anduril.App.Models;

public abstract record ChatContentBlock;

public sealed record TextChatContentBlock(string Text) : ChatContentBlock;

public sealed record CodeChatContentBlock(string Code, string? Language) : ChatContentBlock;

public sealed record TableChatContentBlock(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows) : ChatContentBlock;
