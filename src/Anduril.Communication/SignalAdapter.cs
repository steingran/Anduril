using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anduril.Core.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Communication;

/// <summary>
/// Communication adapter for Signal using the signal-cli REST API.
/// Polls the API for incoming messages and sends responses via HTTP.
/// </summary>
public sealed class SignalAdapter : ICommunicationAdapter
{
    private readonly SignalAdapterOptions _options;
    private readonly ILogger<SignalAdapter> _logger;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _pollLoop;

    // Internal for testing via InternalsVisibleTo
    internal string? _botPhoneNumber;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SignalAdapter(IOptions<SignalAdapterOptions> options, ILogger<SignalAdapter> logger, IHttpClientFactory httpClientFactory)
        : this(options, logger, httpClientFactory.CreateClient(nameof(SignalAdapter)))
    {
    }

    internal SignalAdapter(IOptions<SignalAdapterOptions> options, ILogger<SignalAdapter> logger, HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public string Platform => "signal";

    public bool IsConnected { get; private set; }

    public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.PhoneNumber))
        {
            _logger.LogWarning("Signal adapter: PhoneNumber is not configured. Signal integration will be unavailable.");
            return;
        }

        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            _logger.LogWarning("Signal adapter: ApiUrl is not configured. Signal integration will be unavailable.");
            return;
        }

        _botPhoneNumber = _options.PhoneNumber;
        _httpClient.BaseAddress = new Uri(_options.ApiUrl.TrimEnd('/') + "/");

        // Verify connectivity by checking the API health
        try
        {
            var response = await _httpClient.GetAsync("v1/about", cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Signal adapter connected to API at {ApiUrl} for number {PhoneNumber}",
                _options.ApiUrl, _botPhoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signal adapter: Could not connect to signal-cli REST API at {ApiUrl}. " +
                                   "Signal integration will be unavailable.", _options.ApiUrl);
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsConnected = true;
        _pollLoop = Task.Run(() => PollForMessagesAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("Signal adapter started. Polling for messages...");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;

        if (_cts is not null)
            await _cts.CancelAsync();

        if (_pollLoop is not null)
        {
            try
            {
                await _pollLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        _logger.LogInformation("Signal adapter stopped.");
    }

    public async Task<string?> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Signal adapter is not connected.");

        // Convert standard Markdown to Signal-compatible formatting
        string signalText = SignalMarkdownConverter.ConvertToSignalMarkdown(message.Text);

        var payload = new SignalSendRequest
        {
            Number = _botPhoneNumber!,
            Recipients = [message.ChannelId],
            Message = signalText,
            QuoteTimestamp = message.ThreadId
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("v2/send", payload, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SignalSendResponse>(JsonOptions, cancellationToken);
            var timestamp = result?.Timestamp?.ToString();

            _logger.LogDebug("Sent Signal message to {Recipient}", message.ChannelId);
            return timestamp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Signal message to {Recipient}", message.ChannelId);
            throw;
        }
    }

    /// <summary>
    /// Handles a raw Signal message envelope and raises <see cref="MessageReceived"/>.
    /// Public for testability (mirrors <see cref="SlackAdapter.HandleSlackMessageAsync"/>).
    /// </summary>
    public async Task HandleSignalMessageAsync(SignalMessageEnvelope envelope)
    {
        // Filter out bot's own messages to prevent infinite loops
        if (_botPhoneNumber is not null && envelope.SourceNumber == _botPhoneNumber)
            return;

        // Only process data messages (not receipts, typing indicators, etc.)
        if (envelope.DataMessage is null)
            return;

        // Filter empty messages
        if (string.IsNullOrEmpty(envelope.DataMessage.Message))
            return;

        var incoming = new IncomingMessage
        {
            Id = envelope.DataMessage.Timestamp?.ToString() ?? Guid.NewGuid().ToString(),
            Text = envelope.DataMessage.Message,
            UserId = envelope.SourceNumber ?? "unknown",
            ChannelId = envelope.DataMessage.GroupInfo?.GroupId ?? envelope.SourceNumber ?? "unknown",
            Platform = Platform,
            ThreadId = envelope.DataMessage.Quote?.Id?.ToString(),
            IsDirectMessage = envelope.DataMessage.GroupInfo is null
        };

        await MessageReceived.Invoke(incoming);
    }

    private async Task PollForMessagesAsync(CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"v1/receive/{Uri.EscapeDataString(_botPhoneNumber!)}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var envelopes = await response.Content.ReadFromJsonAsync<SignalMessageEnvelope[]>(
                        JsonOptions, cancellationToken);

                    if (envelopes is not null)
                    {
                        foreach (var envelope in envelopes)
                        {
                            try
                            {
                                await HandleSignalMessageAsync(envelope);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error handling Signal message from {Source}",
                                    envelope.SourceNumber);
                            }
                        }
                    }
                }
                else
                {
                    string? responseBody = null;
                    try
                    {
                        responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    }
                    catch (Exception exReadBody)
                    {
                        _logger.LogDebug(exReadBody,
                            "Failed to read response body for non-successful Signal polling response.");
                    }

                    if (responseBody is not null && responseBody.Length > 1024)
                        responseBody = responseBody[..1024];

                    _logger.LogWarning(
                        "Non-successful HTTP response while polling Signal messages. StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}, Body: {Body}",
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        responseBody);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling Signal messages. Will retry in {Interval}s",
                    pollInterval.TotalSeconds);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Ensure the polling loop is stopped before disposing resources
        await StopAsync();

        _cts?.Dispose();
        // Do not dispose _httpClient — it is managed by IHttpClientFactory
        IsConnected = false;
    }

    // --- signal-cli REST API DTOs (internal for testability) ---

    internal sealed class SignalSendRequest
    {
        public string? Number { get; set; }
        public string[]? Recipients { get; set; }
        public string? Message { get; set; }

        [JsonPropertyName("quote_timestamp")]
        public string? QuoteTimestamp { get; set; }
    }

    internal sealed class SignalSendResponse
    {
        public long? Timestamp { get; set; }
    }

    public sealed class SignalMessageEnvelope
    {
        public string? SourceNumber { get; set; }
        public SignalDataMessage? DataMessage { get; set; }
    }

    public sealed class SignalDataMessage
    {
        public long? Timestamp { get; set; }
        public string? Message { get; set; }
        public SignalGroupInfo? GroupInfo { get; set; }
        public SignalQuote? Quote { get; set; }
    }

    public sealed class SignalGroupInfo
    {
        public string? GroupId { get; set; }
    }

    public sealed class SignalQuote
    {
        public long? Id { get; set; }
    }
}

