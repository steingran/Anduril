using Anduril.Core.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace Anduril.Communication;

/// <summary>
/// Communication adapter for Slack using Socket Mode for real-time messaging.
/// Normalizes Slack events into <see cref="IncomingMessage"/> and sends responses
/// via the Slack Web API.
/// </summary>
public sealed class SlackAdapter(IOptions<SlackAdapterOptions> options, ILogger<SlackAdapter> logger)
    : ICommunicationAdapter
{
    private readonly SlackAdapterOptions _options = options.Value;
    private ISlackApiClient? _apiClient;
    private ISlackSocketModeClient? _socketClient;

    // Internal for testing via InternalsVisibleTo (avoids reflection in tests)
    internal string? _botUserId;

    public string Platform => "slack";

    public bool IsConnected { get; private set; }

    public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        string botToken = _options.BotToken
                          ?? throw new InvalidOperationException("Slack BotToken is not configured.");

        string appToken = _options.AppToken
                          ?? throw new InvalidOperationException("Slack AppToken is not configured for Socket Mode.");

        // Build the SlackNet service with both tokens and register our event handler
        var slackServices = new SlackServiceBuilder()
            .UseApiToken(botToken)
            .UseAppLevelToken(appToken)
            .RegisterEventHandler(ctx => new SlackMessageHandler(this));

        _apiClient = slackServices.GetApiClient();
        _socketClient = slackServices.GetSocketModeClient();

        // Resolve the bot's own user ID so we can filter out our own messages
        try
        {
            var authResponse = await _apiClient.Auth.Test();
            _botUserId = authResponse.UserId;
            logger.LogInformation("Slack bot authenticated as user {BotUserId}", _botUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve bot user ID — self-message filtering disabled");
        }

        await _socketClient.Connect();
        IsConnected = true;
        logger.LogInformation("Slack adapter connected via Socket Mode. Listening for messages...");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_socketClient is not null)
        {
            _socketClient.Dispose();
            _socketClient = null;
        }

        IsConnected = false;
        _apiClient = null;
        logger.LogInformation("Slack adapter stopped.");
    }

    public async Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (_apiClient is null)
            throw new InvalidOperationException("Slack adapter is not connected.");

        await _apiClient.Chat.PostMessage(new Message
        {
            Channel = message.ChannelId,
            Text = message.Text,
            ThreadTs = message.ThreadId
        });

        logger.LogDebug("Sent Slack message to channel {Channel}", message.ChannelId);
    }

    /// <summary>
    /// Handles a raw Slack message event and raises <see cref="MessageReceived"/>.
    /// Called by the Socket Mode event handler. Public for testability.
    /// </summary>
    public async Task HandleSlackMessageAsync(MessageEvent slackMessage)
    {
        // Filter out bot's own messages to prevent infinite loops
        if (_botUserId is not null && slackMessage.User == _botUserId)
            return;

        // Filter out message subtypes (edits, deletes, bot_message, etc.) — only handle plain user messages
        if (!string.IsNullOrEmpty(slackMessage.Subtype))
            return;

        var incoming = new IncomingMessage
        {
            Id = slackMessage.Ts ?? Guid.NewGuid().ToString(),
            Text = slackMessage.Text ?? string.Empty,
            UserId = slackMessage.User ?? "unknown",
            ChannelId = slackMessage.Channel ?? "unknown",
            Platform = Platform,
            ThreadId = slackMessage.ThreadTs,
            IsDirectMessage = slackMessage.ChannelType == "im"
        };

        await MessageReceived.Invoke(incoming);
    }

    public ValueTask DisposeAsync()
    {
        _socketClient?.Dispose();
        _socketClient = null;
        IsConnected = false;
        _apiClient = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Internal SlackNet event handler that bridges Socket Mode events into the adapter.
    /// </summary>
    private sealed class SlackMessageHandler(SlackAdapter adapter) : IEventHandler<MessageEvent>
    {
        public Task Handle(MessageEvent slackEvent) => adapter.HandleSlackMessageAsync(slackEvent);
    }
}
