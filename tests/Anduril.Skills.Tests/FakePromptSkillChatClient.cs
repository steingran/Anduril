using Microsoft.Extensions.AI;

namespace Anduril.Skills.Tests;

public sealed class FakePromptSkillChatClient(string responseText) : IChatClient
{
    public ChatClientMetadata Metadata => new();

    public string? CapturedSystemPrompt { get; private set; }

    public IReadOnlyList<string> CapturedToolNames { get; private set; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CapturedSystemPrompt = chatMessages.FirstOrDefault(message => message.Role == ChatRole.System)?.Text;
        CapturedToolNames = options?.Tools?.Select(tool => tool.Name).OrderBy(name => name).ToList() ?? [];
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
