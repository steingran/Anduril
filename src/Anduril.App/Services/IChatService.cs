using Anduril.Core.Communication;

namespace Anduril.App.Services;

/// <summary>
/// Abstracts the real-time chat service used by ViewModels,
/// allowing fake implementations in tests.
/// </summary>
public interface IChatService
{
    event Action<ChatStreamToken>? TokenReceived;

    Task<List<ProviderInfo>> GetAvailableProvidersAsync();
    Task SelectModelAsync(string providerId);
    Task<ConversationInfo> CreateConversationAsync();
    Task<List<SessionMessage>> GetConversationHistoryAsync(string conversationId);
    Task SendMessageAsync(string conversationId, string text, string? providerId = null, string? repoPath = null, string? branchName = null);
    Task CancelMessageAsync(string conversationId);
}
