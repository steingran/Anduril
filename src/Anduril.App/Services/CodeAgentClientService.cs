using Anduril.App.Models;
using Anduril.Core.Communication;
using System.Diagnostics;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Anduril.App.Services;

/// <summary>
/// SignalR client that connects to the code-agent hub (/hubs/codeagent).
/// Delivers streamed text tokens and staged file-action proposals to the UI.
/// </summary>
public sealed class CodeAgentClientService : IAsyncDisposable
{
    private readonly HubConnection _connection;

    // Mirrors the SignalRChatService pattern: track provider client-side to
    // avoid race conditions with the server-side selection round-trip.
    private string? _selectedProviderId;

    /// <summary>Raised for each streamed text token from the code agent.</summary>
    public event Action<ChatStreamToken>? TokenReceived;

    /// <summary>Raised when the agent proposes a staged file action (create/edit/delete).</summary>
    public event Action<StagedActionModel>? StagedActionReceived;

    public CodeAgentClientService(string baseUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/codeagent")
            .WithAutomaticReconnect()
            .Build();

        // Dispatch SignalR callbacks to the Avalonia UI thread to avoid cross-thread issues.
        _connection.On<ChatStreamToken>("ReceiveToken",
            token => Dispatcher.UIThread.Post(() => TokenReceived?.Invoke(token)));

        _connection.On<StagedActionModel>("ReceiveStagedAction",
            action => Dispatcher.UIThread.Post(() => StagedActionReceived?.Invoke(action)));
    }

    public async Task ConnectAsync()
    {
        if (_connection.State != HubConnectionState.Disconnected)
        {
            // Wait for Connected state if still connecting/reconnecting, with timeout.
            var sw = Stopwatch.StartNew();
            while (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                if (sw.Elapsed.TotalSeconds > 30)
                    throw new TimeoutException("Timed out waiting for SignalR connection.");
                await Task.Delay(50);
            }
            if (_connection.State == HubConnectionState.Connected)
                return;
        }

        await _connection.StartAsync();
    }

    public async Task SelectModelAsync(string providerId)
    {
        _selectedProviderId = providerId;
        await ConnectAsync();
        await _connection.InvokeAsync("SelectModel", providerId);
    }

    /// <summary>Creates a new isolated code-agent session on the server.</summary>
    public async Task<ConversationInfo> CreateSessionAsync()
    {
        await ConnectAsync();
        return await _connection.InvokeAsync<ConversationInfo>("CreateSession");
    }

    /// <summary>Sends a user message to the code agent for the given session.</summary>
    public async Task SendCodeAgentMessageAsync(string sessionId, string text, string? providerId = null)
    {
        var effectiveProviderId = providerId ?? _selectedProviderId;
        await ConnectAsync();
        await _connection.InvokeAsync("SendMessage", sessionId, text, effectiveProviderId);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

