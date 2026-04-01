using System.Text;
using Anduril.Core.Integrations;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for Gmail via the Google Gmail API.
/// Uses OAuth 2.0 with a stored refresh token for automatic token renewal.
/// Provides AI-callable functions for reading, searching, sending, and managing emails,
/// plus push notification support via Google Cloud Pub/Sub.
/// </summary>
public class GmailTool : IIntegrationTool, IAsyncDisposable
{
    private readonly GmailToolOptions _options;
    private readonly ILogger<GmailTool> _logger;
    private GmailService? _service;

    public GmailTool(IOptions<GmailToolOptions> options, ILogger<GmailTool> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "gmail";
    public string Description => "Gmail integration for reading, searching, sending, and managing emails with push notification support.";
    public bool IsAvailable => _service is not null;

    /// <summary>
    /// Gets the underlying Gmail service for use by other components (e.g., watch renewal, push handler).
    /// </summary>
    public GmailService? Service => _service;

    /// <summary>
    /// Gets the configured user ID for Gmail API calls.
    /// </summary>
    public string UserId => _options.UserId;

    /// <summary>
    /// Gets the configured Pub/Sub topic for Gmail watch.
    /// </summary>
    public string? PubSubTopic => _options.PubSubTopic;

    /// <summary>
    /// Gets the configured email processing rules.
    /// </summary>
    public IReadOnlyList<GmailEmailRule> Rules => _options.Rules;

    /// <summary>
    /// Gets the configured important senders list.
    /// </summary>
    public IReadOnlyList<string> ImportantSenders => _options.ImportantSenders;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.ClientId) ||
            string.IsNullOrEmpty(_options.ClientSecret) ||
            string.IsNullOrEmpty(_options.RefreshToken))
        {
            _logger.LogWarning(
                "Gmail credentials not fully configured (ClientId, ClientSecret, and RefreshToken are all required). " +
                "Gmail integration will be unavailable.");
            return Task.CompletedTask;
        }

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            },
            Scopes =
            [
                GmailService.Scope.GmailReadonly,
                GmailService.Scope.GmailSend,
                GmailService.Scope.GmailModify,
                GmailService.Scope.GmailLabels
            ]
        });

        var credential = new UserCredential(flow, _options.UserId, new TokenResponse
        {
            RefreshToken = _options.RefreshToken
        });

        _service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Anduril"
        });

        _logger.LogInformation("Gmail integration initialized for user '{UserId}'.", _options.UserId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AIFunction> GetFunctions()
    {
        return
        [
            AIFunctionFactory.Create(ListMessagesAsync, "gmail_list_messages",
                "List recent email messages. Optionally filter by query."),
            AIFunctionFactory.Create(GetMessageAsync, "gmail_get_message",
                "Get the full details of an email message by its ID."),
            AIFunctionFactory.Create(SearchMessagesAsync, "gmail_search",
                "Search emails using Gmail search syntax (e.g., 'from:alice subject:meeting')."),
            AIFunctionFactory.Create(GetThreadAsync, "gmail_get_thread",
                "Get all messages in an email thread by thread ID for summarization."),
            AIFunctionFactory.Create(SendEmailAsync, "gmail_send",
                "Send a new email message."),
            AIFunctionFactory.Create(ReplyToEmailAsync, "gmail_reply",
                "Reply to an existing email message."),
            AIFunctionFactory.Create(ModifyLabelsAsync, "gmail_modify_labels",
                "Add or remove labels on an email (categorize, move to folders, archive)."),
            AIFunctionFactory.Create(GetAttachmentsAsync, "gmail_get_attachments",
                "List and optionally save attachments from an email."),
            AIFunctionFactory.Create(GetMessagesSinceAsync, "gmail_messages_since",
                "Get email messages received since a given date/time."),
            AIFunctionFactory.Create(GetUnrepliedImportantAsync, "gmail_unreplied_important",
                "Get important emails that have not been replied to."),
            AIFunctionFactory.Create(SetupWatchAsync, "gmail_setup_watch",
                "Set up Gmail push notifications via Google Cloud Pub/Sub."),
        ];
    }

    private async Task<string> ListMessagesAsync(int maxResults = 10, string query = "")
    {
        var service = GetService();
        var request = service.Users.Messages.List(_options.UserId);
        request.MaxResults = maxResults;
        if (!string.IsNullOrEmpty(query))
            request.Q = query;

        var response = await request.ExecuteAsync();
        if (response.Messages is null || response.Messages.Count == 0)
            return "No messages found.";

        var sb = new StringBuilder();
        foreach (var msgRef in response.Messages)
        {
            var msg = await service.Users.Messages.Get(_options.UserId, msgRef.Id).ExecuteAsync();
            sb.AppendLine(FormatMessageSummary(msg));
        }

        return sb.ToString();
    }

    private async Task<string> GetMessageAsync(string messageId)
    {
        var service = GetService();
        var msg = await service.Users.Messages.Get(_options.UserId, messageId).ExecuteAsync();
        return FormatMessageDetail(msg);
    }

    private async Task<string> SearchMessagesAsync(string query, int maxResults = 10)
    {
        return await ListMessagesAsync(maxResults, query);
    }

    private async Task<string> GetThreadAsync(string threadId)
    {
        var service = GetService();
        var thread = await service.Users.Threads.Get(_options.UserId, threadId).ExecuteAsync();

        if (thread.Messages is null || thread.Messages.Count == 0)
            return "Thread is empty.";

        var sb = new StringBuilder();
        sb.AppendLine($"Thread: {thread.Messages.Count} message(s)");
        sb.AppendLine(new string('-', 40));

        foreach (var msg in thread.Messages)
        {
            sb.AppendLine(FormatMessageDetail(msg));
            sb.AppendLine(new string('-', 40));
        }

        return sb.ToString();
    }

    public async Task<string> SendEmailAsync(string to, string subject, string body)
    {
        var service = GetService();
        var mimeMessage = $"To: {to}\r\nSubject: {subject}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n{body}";
        var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(mimeMessage))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var message = new Message { Raw = encodedMessage };
        var result = await service.Users.Messages.Send(message, _options.UserId).ExecuteAsync();
        return $"Email sent successfully. Message ID: {result.Id}";
    }

    private async Task<string> ReplyToEmailAsync(string messageId, string body)
    {
        var service = GetService();
        var original = await service.Users.Messages.Get(_options.UserId, messageId).ExecuteAsync();

        string from = GetHeader(original, "From") ?? "unknown";
        string subject = GetHeader(original, "Subject") ?? "";
        string messageIdHeader = GetHeader(original, "Message-ID") ?? "";
        string threadId = original.ThreadId;

        if (!subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            subject = $"Re: {subject}";

        var mimeMessage = new StringBuilder();
        mimeMessage.AppendLine($"To: {from}");
        mimeMessage.AppendLine($"Subject: {subject}");
        mimeMessage.AppendLine($"In-Reply-To: {messageIdHeader}");
        mimeMessage.AppendLine($"References: {messageIdHeader}");
        mimeMessage.AppendLine("Content-Type: text/plain; charset=utf-8");
        mimeMessage.AppendLine();
        mimeMessage.Append(body);

        var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(mimeMessage.ToString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var reply = new Message { Raw = encodedMessage, ThreadId = threadId };
        var result = await service.Users.Messages.Send(reply, _options.UserId).ExecuteAsync();
        return $"Reply sent successfully to {from}. Message ID: {result.Id}";
    }

    private async Task<string> ModifyLabelsAsync(string messageId, string addLabels = "", string removeLabels = "")
    {
        var service = GetService();
        string? addValue = string.IsNullOrWhiteSpace(addLabels) ? null : addLabels;
        string? removeValue = string.IsNullOrWhiteSpace(removeLabels) ? null : removeLabels;
        var modRequest = new ModifyMessageRequest
        {
            AddLabelIds = addValue?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            RemoveLabelIds = removeValue?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };

        await service.Users.Messages.Modify(modRequest, _options.UserId, messageId).ExecuteAsync();
        return $"Labels updated on message {messageId}. Added: [{addValue ?? "none"}], Removed: [{removeValue ?? "none"}].";
    }

    private async Task<string> GetAttachmentsAsync(string messageId, bool save = false)
    {
        var service = GetService();
        var msg = await service.Users.Messages.Get(_options.UserId, messageId).ExecuteAsync();

        var parts = msg.Payload?.Parts?.Where(p => !string.IsNullOrEmpty(p.Filename) && !string.IsNullOrEmpty(p.Body?.AttachmentId));
        if (parts is null || !parts.Any())
            return "No attachments found on this message.";

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.AppendLine($"- {part.Filename} ({part.Body?.Size ?? 0} bytes)");

            if (save && !string.IsNullOrEmpty(_options.AttachmentSavePath))
            {
                var attachment = await service.Users.Messages.Attachments
                    .Get(_options.UserId, messageId, part.Body!.AttachmentId).ExecuteAsync();

                var data = Convert.FromBase64String(
                    attachment.Data.Replace('-', '+').Replace('_', '/'));

                Directory.CreateDirectory(_options.AttachmentSavePath);
                var filePath = Path.Combine(_options.AttachmentSavePath, part.Filename!);
                await File.WriteAllBytesAsync(filePath, data);
                sb.AppendLine($"  → Saved to: {filePath}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> GetMessagesSinceAsync(DateTime since, int maxResults = 50)
    {
        var epoch = new DateTimeOffset(since.ToUniversalTime()).ToUnixTimeSeconds();
        var query = $"after:{epoch}";
        return await ListMessagesAsync(maxResults, query);
    }

    private async Task<string> GetUnrepliedImportantAsync(int days = 7)
    {
        var epoch = new DateTimeOffset(DateTime.UtcNow.AddDays(-days)).ToUnixTimeSeconds();

        // Build a query for important messages that are unread or have no reply
        var importantQuery = $"after:{epoch} is:important -label:sent";

        if (_options.ImportantSenders.Count > 0)
        {
            var fromClauses = string.Join(" OR ", _options.ImportantSenders.Select(s => $"from:{s}"));
            importantQuery = $"after:{epoch} ({fromClauses}) -label:sent";
        }

        return await ListMessagesAsync(20, importantQuery);
    }

    public async Task<string> SetupWatchAsync()
    {
        var service = GetService();

        if (string.IsNullOrEmpty(_options.PubSubTopic))
            return "Pub/Sub topic not configured. Set 'PubSubTopic' in Gmail options (format: projects/{project}/topics/{topic}).";

        var watchRequest = new WatchRequest
        {
            TopicName = _options.PubSubTopic,
            LabelIds = ["INBOX"]
        };

        var response = await service.Users.Watch(watchRequest, _options.UserId).ExecuteAsync();
        return $"Gmail watch set up successfully. History ID: {response.HistoryId}, Expiration: {response.Expiration}";
    }

    /// <summary>
    /// Processes a push notification by fetching new messages since the given history ID
    /// and applying email processing rules.
    /// </summary>
    public async Task<IReadOnlyList<string>> ProcessPushNotificationAsync(
        ulong historyId, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var results = new List<string>();

        try
        {
            var historyRequest = service.Users.History.List(_options.UserId);
            historyRequest.StartHistoryId = historyId;
            historyRequest.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

            var history = await historyRequest.ExecuteAsync(cancellationToken);
            if (history.History is null)
                return results;

            foreach (var record in history.History)
            {
                if (record.MessagesAdded is null) continue;
                foreach (var added in record.MessagesAdded)
                {
                    var msg = await service.Users.Messages.Get(_options.UserId, added.Message.Id)
                        .ExecuteAsync(cancellationToken);
                    var ruleResult = await ApplyRulesAsync(msg, cancellationToken);
                    if (ruleResult is not null)
                        results.Add(ruleResult);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Gmail push notification for history ID {HistoryId}", historyId);
        }

        return results;
    }

    private async Task<string?> ApplyRulesAsync(Message message, CancellationToken cancellationToken)
    {
        var from = GetHeader(message, "From") ?? "";
        var subject = GetHeader(message, "Subject") ?? "";
        var body = GetPlainTextBody(message);

        foreach (var rule in _options.Rules.Where(r => r.Enabled))
        {
            if (!MatchesRule(rule, from, subject, body))
                continue;

            _logger.LogInformation("Email rule '{RuleName}' matched for message from '{From}': {Subject}",
                rule.Name, from, subject);

            return rule.Action.ToLowerInvariant() switch
            {
                "notify" => $"📧 **{rule.Name}**: New email from {from} — {subject}",
                "auto-respond" => await AutoRespondAsync(message, rule.ActionParameter ?? "Thank you for your email. I will get back to you shortly.", cancellationToken),
                "label" => await ApplyLabelAsync(message, rule.ActionParameter ?? "IMPORTANT", cancellationToken),
                "extract-attachments" => await ExtractAttachmentsAsync(message, rule.ActionParameter, cancellationToken),
                _ => null
            };
        }

        return null;
    }

    internal static bool MatchesRule(GmailEmailRule rule, string from, string subject, string body)
    {
        if (!string.IsNullOrEmpty(rule.FromFilter) &&
            !from.Contains(rule.FromFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(rule.SubjectFilter) &&
            !subject.Contains(rule.SubjectFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(rule.BodyKeyword) &&
            !body.Contains(rule.BodyKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        // At least one filter must be specified for a rule to match
        return !string.IsNullOrEmpty(rule.FromFilter) ||
               !string.IsNullOrEmpty(rule.SubjectFilter) ||
               !string.IsNullOrEmpty(rule.BodyKeyword);
    }

    private async Task<string> AutoRespondAsync(Message message, string responseBody, CancellationToken cancellationToken = default)
    {
        return await ReplyToEmailAsync(message.Id, responseBody);
    }

    private async Task<string> ApplyLabelAsync(Message message, string labelName, CancellationToken cancellationToken = default)
    {
        return await ModifyLabelsAsync(message.Id, addLabels: labelName);
    }

    private async Task<string> ExtractAttachmentsAsync(Message message, string? savePath, CancellationToken cancellationToken = default)
    {
        var originalPath = _options.AttachmentSavePath;
        if (!string.IsNullOrEmpty(savePath))
            _options.AttachmentSavePath = savePath;

        var result = await GetAttachmentsAsync(message.Id, save: true);

        _options.AttachmentSavePath = originalPath;
        return result;
    }

    // ---------------------------------------------------------------
    // Helper methods
    // ---------------------------------------------------------------

    private GmailService GetService() =>
        _service ?? throw new InvalidOperationException("Gmail integration is not initialized.");

    private static string? GetHeader(Message message, string name) =>
        message.Payload?.Headers?.FirstOrDefault(h =>
            h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string GetPlainTextBody(Message message)
    {
        // Try to find a text/plain part
        var body = FindBodyPart(message.Payload, "text/plain");
        if (body is not null && !string.IsNullOrEmpty(body.Data))
        {
            return Encoding.UTF8.GetString(
                Convert.FromBase64String(body.Data.Replace('-', '+').Replace('_', '/')));
        }

        return string.Empty;
    }

    private static MessagePartBody? FindBodyPart(MessagePart? part, string mimeType)
    {
        if (part is null) return null;
        if (part.MimeType == mimeType && part.Body?.Data is not null)
            return part.Body;

        if (part.Parts is null) return null;
        foreach (var child in part.Parts)
        {
            var result = FindBodyPart(child, mimeType);
            if (result is not null) return result;
        }

        return null;
    }

    private static string FormatMessageSummary(Message message)
    {
        var from = GetHeader(message, "From") ?? "unknown";
        var subject = GetHeader(message, "Subject") ?? "(no subject)";
        var date = GetHeader(message, "Date") ?? "";
        var snippet = message.Snippet ?? "";

        return $"[{message.Id}] {date} | From: {from} | Subject: {subject}\n  {snippet}";
    }

    private static string FormatMessageDetail(Message message)
    {
        var from = GetHeader(message, "From") ?? "unknown";
        var to = GetHeader(message, "To") ?? "unknown";
        var subject = GetHeader(message, "Subject") ?? "(no subject)";
        var date = GetHeader(message, "Date") ?? "";
        var body = GetPlainTextBody(message);

        var sb = new StringBuilder();
        sb.AppendLine($"Message ID: {message.Id}");
        sb.AppendLine($"Thread ID: {message.ThreadId}");
        sb.AppendLine($"Date: {date}");
        sb.AppendLine($"From: {from}");
        sb.AppendLine($"To: {to}");
        sb.AppendLine($"Subject: {subject}");
        sb.AppendLine($"Labels: {string.Join(", ", message.LabelIds ?? [])}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine(body);
        }
        else
        {
            sb.AppendLine(message.Snippet ?? "(no content)");
        }

        return sb.ToString();
    }

    public ValueTask DisposeAsync()
    {
        _service?.Dispose();
        _service = null;
        return ValueTask.CompletedTask;
    }
}

