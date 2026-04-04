using Anduril.App.Models;
using Anduril.Core.Communication;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Anduril.App.Services;

/// <summary>
/// SignalR client that connects to the embedded ASP.NET Core host
/// and exposes chat operations for the Avalonia UI layer.
/// </summary>
public sealed class SignalRChatService : IChatService, IAsyncDisposable
{
    private readonly HubConnection _connection;

    // Tracks the selected provider client-side so it can be passed explicitly with
    // every SendMessage call, avoiding a race condition with the server-side
    // SelectedProviders dictionary (which relies on a separate SelectModel round-trip).
    private string? _selectedProviderId;

    public event Action<ChatStreamToken>? TokenReceived;
    public event Action<StagedActionModel>? StagedActionReceived;

    public SignalRChatService(string baseUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/chat")
            .WithAutomaticReconnect()
            .Build();

        // Dispatch SignalR callback to Avalonia UI thread to avoid cross-thread issues.
        _connection.On<ChatStreamToken>("ReceiveToken", token =>
        {
            Dispatcher.UIThread.Post(() => TokenReceived?.Invoke(token));
        });
    }

    public async Task ConnectAsync()
    {
        if (_connection.State != HubConnectionState.Disconnected)
        {
            // Wait for Connected state if still connecting/reconnecting.
            while (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                await Task.Delay(50);
            return;
        }

        await _connection.StartAsync();
    }

    public async Task<List<ProviderInfo>> GetAvailableProvidersAsync()
    {
        await ConnectAsync();
        return await _connection.InvokeAsync<List<ProviderInfo>>("GetAvailableProviders");
    }

    public async Task SelectModelAsync(string providerId)
    {
        _selectedProviderId = providerId;
        await ConnectAsync();
        await _connection.InvokeAsync("SelectModel", providerId);
    }

    public async Task<ConversationInfo> CreateConversationAsync()
    {
        await ConnectAsync();
        return await _connection.InvokeAsync<ConversationInfo>("CreateConversation");
    }

    public async Task<List<SessionMessage>> GetConversationHistoryAsync(string conversationId)
    {
        await ConnectAsync();
        return await _connection.InvokeAsync<List<SessionMessage>>("GetConversationHistory", conversationId);
    }

    public async Task SendMessageAsync(
        string conversationId,
        string text,
        string? providerId = null,
        string? repoPath = null,
        string? branchName = null)
    {
        var effectiveProviderId = providerId ?? _selectedProviderId;
        await ConnectAsync();
        await _connection.InvokeAsync("SendMessage", conversationId, text, effectiveProviderId, repoPath, branchName);
    }

    public async Task CancelMessageAsync(string conversationId)
    {
        await ConnectAsync();
        await _connection.InvokeAsync("CancelMessage", conversationId);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
