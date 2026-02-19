using Microsoft.Extensions.AI;

namespace Anduril.AI.Tests;

/// <summary>
/// Minimal fake <see cref="IChatClient"/> that captures the options passed to it
/// and returns an empty response. Used to verify wrapper behavior in tests.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    public ChatOptions? LastOptions { get; private set; }

    public ChatClientMetadata Metadata { get; } = new ChatClientMetadata();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "fake response"));
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        return AsyncEnumerable.Empty<ChatResponseUpdate>();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

