using System.Collections.Concurrent;
using Anduril.Core.Communication;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Communication;

/// <summary>
/// Communication adapter for Microsoft Teams using the Bot Framework SDK.
/// Normalizes Teams activities into <see cref="IncomingMessage"/> and sends responses
/// via the Bot Framework turn context.
/// </summary>
public sealed class TeamsAdapter(IOptions<TeamsAdapterOptions> options, ILogger<TeamsAdapter> logger)
    : ICommunicationAdapter
{
    private readonly TeamsAdapterOptions _options = options.Value;

    // Stores turn contexts by conversation ID to avoid race conditions when multiple messages arrive quickly.
    // In a production implementation, this would use a conversation reference store with proper expiration.
    private readonly ConcurrentDictionary<string, ITurnContext> _turnContexts = new();

    public string Platform => "teams";

    public bool IsConnected { get; private set; }

    public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // The Teams adapter is driven by incoming HTTP requests via ASP.NET Core.
        // StartAsync just marks the adapter as ready to receive.
        if (string.IsNullOrEmpty(_options.MicrosoftAppId))
        {
            logger.LogWarning("Teams adapter: MicrosoftAppId is not configured. Teams integration will be unavailable.");
            return Task.CompletedTask;
        }

        IsConnected = true;
        logger.LogInformation("Teams adapter started. Waiting for Bot Framework webhook messages...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        _turnContexts.Clear();
        logger.LogInformation("Teams adapter stopped.");
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (!_turnContexts.TryGetValue(message.ChannelId, out var turnContext))
        {
            logger.LogWarning("No turn context found for conversation {ConversationId}. Cannot send message.", message.ChannelId);
            return;
        }

        var activity = MessageFactory.Text(message.Text);
        await turnContext.SendActivityAsync(activity, cancellationToken);
        logger.LogDebug("Sent Teams message to conversation {ConversationId}", message.ChannelId);
    }

    /// <summary>
    /// Processes an incoming Bot Framework activity (called from the ASP.NET controller/endpoint).
    /// Normalizes the activity into an <see cref="IncomingMessage"/> and raises <see cref="MessageReceived"/>.
    /// </summary>
    public async Task ProcessActivityAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        var activity = turnContext.Activity;

        if (activity.Type != ActivityTypes.Message)
            return;

        string conversationId = activity.Conversation?.Id ?? "unknown";

        // Store the turn context by conversation ID to avoid race conditions
        _turnContexts[conversationId] = turnContext;

        var incoming = new IncomingMessage
        {
            Id = activity.Id ?? Guid.NewGuid().ToString(),
            Text = activity.Text ?? string.Empty,
            UserId = activity.From?.Id ?? "unknown",
            UserName = activity.From?.Name,
            ChannelId = conversationId,
            Platform = Platform,
            ThreadId = activity.ReplyToId,
            IsDirectMessage = activity.Conversation?.IsGroup != true
        };

        await MessageReceived.Invoke(incoming);
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        _turnContexts.Clear();
        return ValueTask.CompletedTask;
    }
}

