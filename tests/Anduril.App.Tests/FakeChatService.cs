using Anduril.App.Services;
using Anduril.Core.Communication;

namespace Anduril.App.Tests;

/// <summary>
/// In-memory implementation of <see cref="IChatService"/> for use in unit tests.
/// Configure <see cref="Providers"/> and <see cref="Conversations"/> before constructing
/// the ViewModel under test; call <see cref="RaiseTokenReceived"/> to simulate streaming.
/// </summary>
public sealed class FakeChatService : IChatService
{
    public event Action<ChatStreamToken>? TokenReceived;

    public List<ProviderInfo> Providers { get; set; } = [];
    public Queue<ConversationInfo> Conversations { get; set; } = new();

    public List<string> SelectedProviderIds { get; } = [];
    public List<(string ConversationId, string Text, string? ProviderId, string? RepoPath, string? Branch)> SentMessages { get; } = [];
    public List<string> CancelledConversationIds { get; } = [];

    public Task<List<ProviderInfo>> GetAvailableProvidersAsync() =>
        Task.FromResult(Providers);

    public Task SelectModelAsync(string providerId)
    {
        SelectedProviderIds.Add(providerId);
        return Task.CompletedTask;
    }

    public Task<ConversationInfo> CreateConversationAsync()
    {
        var conv = Conversations.Count > 0
            ? Conversations.Dequeue()
            : new ConversationInfo { Id = Guid.NewGuid().ToString(), CreatedAt = DateTimeOffset.UtcNow };
        return Task.FromResult(conv);
    }

    public Task<List<SessionMessage>> GetConversationHistoryAsync(string conversationId) =>
        Task.FromResult(new List<SessionMessage>());

    public Task SendMessageAsync(string conversationId, string text, string? providerId = null, string? repoPath = null, string? branchName = null)
    {
        SentMessages.Add((conversationId, text, providerId, repoPath, branchName));
        return Task.CompletedTask;
    }

    public Task CancelMessageAsync(string conversationId)
    {
        CancelledConversationIds.Add(conversationId);
        return Task.CompletedTask;
    }

    /// <summary>Fires the <see cref="TokenReceived"/> event, simulating a token arriving from the server.</summary>
    public void RaiseTokenReceived(ChatStreamToken token) => TokenReceived?.Invoke(token);
}
