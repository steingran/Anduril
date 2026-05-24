using Anduril.App.Models;
using Anduril.App.Services;
using Anduril.Core.Communication;

namespace Anduril.App.ScreenshotTool;

internal sealed class FakeChatService : IChatService
{
    public event Action<ChatStreamToken>? TokenReceived
    {
        add { }
        remove { }
    }

    public event Action<StagedActionModel>? StagedActionReceived
    {
        add { }
        remove { }
    }

    public List<ProviderInfo> Providers { get; init; } = [];
    public Queue<ConversationInfo> Conversations { get; init; } = new();

    public Task<List<ProviderInfo>> GetAvailableProvidersAsync() => Task.FromResult(Providers);

    public Task SelectModelAsync(string providerId) => Task.CompletedTask;

    public Task<ConversationInfo> CreateConversationAsync()
    {
        var conversation = Conversations.Count > 0
            ? Conversations.Dequeue()
            : new ConversationInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "New conversation",
                CreatedAt = DateTimeOffset.UtcNow
            };

        return Task.FromResult(conversation);
    }

    public Task<List<SessionMessage>> GetConversationHistoryAsync(string conversationId) =>
        Task.FromResult(new List<SessionMessage>());

    public Task SendMessageAsync(
        string conversationId,
        string text,
        string? providerId = null,
        string? repoPath = null,
        string? branchName = null) =>
        Task.CompletedTask;

    public Task CancelMessageAsync(string conversationId) => Task.CompletedTask;
}
