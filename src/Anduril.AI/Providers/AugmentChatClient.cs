using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.AI.Providers;

/// <summary>
/// Custom <see cref="IChatClient"/> that communicates with the Augment Code HTTP API.
/// Translates between <see cref="Microsoft.Extensions.AI"/> abstractions and Augment's
/// custom <c>POST /chat-stream</c> protocol (newline-delimited JSON streaming).
/// </summary>
internal sealed class AugmentChatClient(
    string apiUrl,
    string apiKey,
    string model,
    ILogger logger,
    HttpClient? httpClient = null)
    : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly string _apiUrl = apiUrl.TrimEnd('/');
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly bool _ownsHttpClient = httpClient is null;

    public ChatClientMetadata Metadata => new("augment-chat", new Uri(_apiUrl), model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options);
        using var response = await SendRequestAsync(payload, cancellationToken);

        (string text, List<FunctionCallContent> toolCalls, UsageDetails? usage, int? stopReason) = await ParseFullResponseAsync(response, cancellationToken);

        var chatMessage = new ChatMessage(ChatRole.Assistant, text);
        foreach (var tc in toolCalls)
        {
            chatMessage.Contents.Add(tc);
        }

        return new ChatResponse([chatMessage])
        {
            FinishReason = MapStopReason(stopReason),
            Usage = usage
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options);
        using var response = await SendRequestAsync(payload, cancellationToken);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            AugmentStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AugmentStreamChunk>(line, JsonOptions);
            }
            catch (JsonException)
            {
                logger.LogDebug("Failed to parse Augment stream line: {Line}", line);
                continue;
            }

            if (chunk is null) continue;

            if (!string.IsNullOrEmpty(chunk.Text))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(chunk.Text)]
                };
            }

            if (chunk.Nodes is not null)
            {
                foreach (var node in chunk.Nodes)
                {
                    if (node.Type == 5 && node.ToolUse is not null) // TOOL_USE
                    {
                        yield return new ChatResponseUpdate
                        {
                            Role = ChatRole.Assistant,
                            Contents = [new FunctionCallContent(
                                node.ToolUse.ToolUseId ?? Guid.NewGuid().ToString(),
                                node.ToolUse.ToolName ?? "unknown",
                                JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                    node.ToolUse.InputJson ?? "{}", JsonOptions))]
                        };
                    }
                }
            }

            if (chunk.StopReason is not null)
            {
                yield return new ChatResponseUpdate
                {
                    FinishReason = MapStopReason(chunk.StopReason.Value)
                };
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(AugmentChatClient)) return this;
        return null;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    // ---------------------------------------------------------------
    // Request building
    // ---------------------------------------------------------------

    private AugmentChatPayload BuildPayload(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var chatHistory = new List<AugmentChatHistoryEntry>();
        var pendingNodes = new List<AugmentRequestNode>();
        var pendingText = new StringBuilder();
        int nodeId = 0;

        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System)
            {
                string sysText = $"System: {msg.Text}";
                pendingNodes.Add(new AugmentRequestNode
                {
                    Id = nodeId++,
                    Type = 0, // TEXT
                    TextNode = new AugmentTextNode { Content = sysText }
                });
                pendingText.Append(sysText).Append("\n\n");
            }
            else if (msg.Role == ChatRole.User)
            {
                string userText = msg.Text ?? "";
                pendingNodes.Add(new AugmentRequestNode
                {
                    Id = nodeId++,
                    Type = 0, // TEXT
                    TextNode = new AugmentTextNode { Content = userText }
                });
                if (pendingText.Length > 0 && userText.Length > 0)
                    pendingText.Append('\n');
                pendingText.Append(userText);
            }
            else if (msg.Role == ChatRole.Tool)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionResultContent frc)
                    {
                        pendingNodes.Add(new AugmentRequestNode
                        {
                            Id = nodeId++,
                            Type = 1, // TOOL_RESULT
                            ToolResultNode = new AugmentToolResultNode
                            {
                                ToolUseId = frc.CallId,
                                Content = frc.Result?.ToString() ?? "",
                                IsError = false
                            }
                        });
                    }
                }
            }
            else if (msg.Role == ChatRole.Assistant)
            {
                var responseNodes = new List<AugmentResponseNode>();
                var responseText = new StringBuilder();
                int rId = 0;

                foreach (var content in msg.Contents)
                {
                    if (content is TextContent tc)
                    {
                        responseText.Append(tc.Text);
                        responseNodes.Add(new AugmentResponseNode
                        {
                            Id = rId++,
                            Type = 0, // RAW_RESPONSE
                            Content = tc.Text
                        });
                    }
                    else if (content is FunctionCallContent fcc)
                    {
                        responseNodes.Add(new AugmentResponseNode
                        {
                            Id = rId++,
                            Type = 5, // TOOL_USE
                            ToolUse = new AugmentToolUse
                            {
                                ToolUseId = fcc.CallId,
                                ToolName = fcc.Name,
                                InputJson = JsonSerializer.Serialize(fcc.Arguments, JsonOptions)
                            }
                        });
                    }
                }

                chatHistory.Add(new AugmentChatHistoryEntry
                {
                    RequestMessage = pendingText.ToString(),
                    RequestNodes = pendingNodes.ToList(),
                    ResponseText = responseText.ToString(),
                    ResponseNodes = responseNodes
                });

                pendingNodes.Clear();
                pendingText.Clear();
                nodeId = 0;
            }
        }

        // Re-index pending nodes
        for (int i = 0; i < pendingNodes.Count; i++)
            pendingNodes[i].Id = i;

        return new AugmentChatPayload
        {
            Mode = "CLI_AGENT",
            Model = model,
            Message = pendingText.ToString(),
            Nodes = pendingNodes,
            ChatHistory = chatHistory,
            ConversationId = _sessionId
        };
    }

    // ---------------------------------------------------------------
    // HTTP transport
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendRequestAsync(AugmentChatPayload payload, CancellationToken ct)
    {
        string requestId = Guid.NewGuid().ToString();
        string url = $"{_apiUrl}/chat-stream";

        string json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("X-Request-Session-Id", _sessionId);
        request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("conversation-id", _sessionId);
        request.Headers.TryAddWithoutValidation("X-Mode", "sdk");
        request.Headers.TryAddWithoutValidation("User-Agent", "anduril/1.0 (csharp)");

        logger.LogDebug("POST {Url} (request {RequestId})", url, requestId);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Augment API error: {(int)response.StatusCode} {response.ReasonPhrase} - {errorBody}");
        }

        return response;
    }

    // ---------------------------------------------------------------
    // Response parsing (non-streaming)
    // ---------------------------------------------------------------

    private async Task<(string Text, List<FunctionCallContent> ToolCalls, UsageDetails? Usage, int? StopReason)>
        ParseFullResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var textBuffer = new StringBuilder();
        var toolCalls = new List<FunctionCallContent>();
        var toolCallIds = new HashSet<string>();
        UsageDetails? usage = null;
        int? stopReason = null;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            AugmentStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AugmentStreamChunk>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk is null) continue;

            if (!string.IsNullOrEmpty(chunk.Text))
                textBuffer.Append(chunk.Text);

            if (chunk.Nodes is not null)
            {
                foreach (var node in chunk.Nodes)
                {
                    if (node.Type == 5 && node.ToolUse is not null)
                    {
                        string id = node.ToolUse.ToolUseId ?? Guid.NewGuid().ToString();
                        if (toolCallIds.Add(id))
                        {
                            toolCalls.Add(new FunctionCallContent(
                                id,
                                node.ToolUse.ToolName ?? "unknown",
                                JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                    node.ToolUse.InputJson ?? "{}", JsonOptions)));
                        }
                    }
                    else if (node.Type == 10 && node.TokenUsage is not null)
                    {
                        usage = new UsageDetails
                        {
                            InputTokenCount = node.TokenUsage.InputTokens,
                            OutputTokenCount = node.TokenUsage.OutputTokens,
                            TotalTokenCount = (node.TokenUsage.InputTokens ?? 0) + (node.TokenUsage.OutputTokens ?? 0)
                        };
                    }
                }
            }

            if (chunk.StopReason is not null)
                stopReason = chunk.StopReason;
        }

        return (textBuffer.ToString(), toolCalls, usage, stopReason);
    }

    private static ChatFinishReason? MapStopReason(int? stopReason) => stopReason switch
    {
        1 => ChatFinishReason.Stop,
        2 => ChatFinishReason.Length,
        3 => ChatFinishReason.ToolCalls,
        4 or 5 => ChatFinishReason.ContentFilter,
        _ => null
    };
}

// ===================================================================
// DTOs for the Augment HTTP API (internal, file-scoped)
// ===================================================================

#pragma warning disable CA1812 // Avoid uninstantiated internal classes — used by JSON deserialization

internal sealed class AugmentChatPayload
{
    public string Mode { get; set; } = "CLI_AGENT";
    public string Model { get; set; } = "";
    public string Message { get; set; } = "";
    public List<AugmentRequestNode> Nodes { get; set; } = [];
    public List<AugmentChatHistoryEntry> ChatHistory { get; set; } = [];
    public string ConversationId { get; set; } = "";
}

internal sealed class AugmentRequestNode
{
    public int Id { get; set; }
    public int Type { get; set; }
    public AugmentTextNode? TextNode { get; set; }
    public AugmentToolResultNode? ToolResultNode { get; set; }
}

internal sealed class AugmentTextNode
{
    public string Content { get; set; } = "";
}

internal sealed class AugmentToolResultNode
{
    public string? ToolUseId { get; set; }
    public string Content { get; set; } = "";
    public bool IsError { get; set; }
}

internal sealed class AugmentChatHistoryEntry
{
    public string RequestMessage { get; set; } = "";
    public List<AugmentRequestNode> RequestNodes { get; set; } = [];
    public string ResponseText { get; set; } = "";
    public List<AugmentResponseNode> ResponseNodes { get; set; } = [];
}

internal sealed class AugmentResponseNode
{
    public int Id { get; set; }
    public int Type { get; set; }
    public string? Content { get; set; }
    public AugmentToolUse? ToolUse { get; set; }
    public AugmentThinking? Thinking { get; set; }
    public AugmentTokenUsage? TokenUsage { get; set; }
}

internal sealed class AugmentToolUse
{
    public string? ToolUseId { get; set; }
    public string? ToolName { get; set; }
    public string? InputJson { get; set; }
}

internal sealed class AugmentThinking
{
    public string? Content { get; set; }
    public string? Summary { get; set; }
}

internal sealed class AugmentTokenUsage
{
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

internal sealed class AugmentStreamChunk
{
    public string? Text { get; set; }
    public List<AugmentResponseNode>? Nodes { get; set; }
    public int? StopReason { get; set; }
}

#pragma warning restore CA1812

