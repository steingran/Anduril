using System.Text;
using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for Proton Mail via the local Proton Mail Bridge IMAP and SMTP endpoints.
/// </summary>
public sealed class ProtonMailTool : IIntegrationTool
{
    private readonly ProtonMailToolOptions _options;
    private readonly ILogger<ProtonMailTool> _logger;
    private readonly Func<IProtonMailImapClient> _imapClientFactory;
    private readonly Func<IProtonMailSmtpClient> _smtpClientFactory;
    private bool _isAvailable;

    public ProtonMailTool(IOptions<ProtonMailToolOptions> options, ILogger<ProtonMailTool> logger)
        : this(
            options,
            logger,
            static () => new MailKitProtonMailImapClient(),
            static () => new MailKitProtonMailSmtpClient())
    {
    }

    internal ProtonMailTool(
        IOptions<ProtonMailToolOptions> options,
        ILogger<ProtonMailTool> logger,
        Func<IProtonMailImapClient> imapClientFactory,
        Func<IProtonMailSmtpClient> smtpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _imapClientFactory = imapClientFactory;
        _smtpClientFactory = smtpClientFactory;
    }

    public string Name => "protonmail";

    public string Description =>
        "Proton Mail Bridge integration for reading, searching, sending, and managing emails over local IMAP and SMTP.";

    public bool IsAvailable => _isAvailable;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _isAvailable = false;

        if (!_options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_options.Username))
        {
            _logger.LogWarning(
                "Proton Mail Bridge Username not configured. Set Integrations:ProtonMail:Username and store the bridge password in user secrets.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning(
                "Proton Mail Bridge Password not configured. Set Integrations:ProtonMail:Password in user secrets.");
            return;
        }

        try
        {
            await using var imapClient = _imapClientFactory();
            await imapClient.VerifyConnectionAsync(_options, cancellationToken);

            await using var smtpClient = _smtpClientFactory();
            await smtpClient.VerifyConnectionAsync(_options, cancellationToken);

            _isAvailable = true;
            _logger.LogInformation(
                "Proton Mail Bridge integration initialized for user '{Username}' via IMAP {ImapHost}:{ImapPort} and SMTP {SmtpHost}:{SmtpPort}.",
                _options.Username,
                _options.ImapHost,
                _options.ImapPort,
                _options.SmtpHost,
                _options.SmtpPort);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Proton Mail Bridge connectivity check failed. Ensure Proton Mail Bridge is installed, running locally, and configured with the same bridge credentials.");
        }
    }

    public IReadOnlyList<AIFunction> GetFunctions() =>
    [
        AIFunctionFactory.Create(ListMessagesAsync, "protonmail_list_messages",
            "List recent Proton Mail messages from a mailbox such as INBOX or Archive."),
        AIFunctionFactory.Create(GetMessageAsync, "protonmail_get_message",
            "Get the full details of a Proton Mail message by mailbox and IMAP UID."),
        AIFunctionFactory.Create(SearchMessagesAsync, "protonmail_search",
            "Search Proton Mail messages. Supports tokens like from:, to:, subject:, body:, is:read, is:unread, after:, and before:."),
        AIFunctionFactory.Create(SendEmailAsync, "protonmail_send",
            "Send a new Proton Mail message through Proton Mail Bridge SMTP."),
        AIFunctionFactory.Create(ReplyToEmailAsync, "protonmail_reply",
            "Reply to an existing Proton Mail message by mailbox and IMAP UID."),
        AIFunctionFactory.Create(MoveMessageAsync, "protonmail_move_message",
            "Move a Proton Mail message from one mailbox to another."),
        AIFunctionFactory.Create(SetReadStatusAsync, "protonmail_set_read_status",
            "Mark a Proton Mail message as read or unread."),
    ];

    private async Task<string> ListMessagesAsync(int maxResults = 10, string mailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        var messages = await imapClient.ListMessagesAsync(_options, NormalizeMailbox(mailbox), ClampMaxResults(maxResults));

        if (messages.Count == 0)
            return $"No messages found in mailbox '{mailbox}'.";

        return string.Join("\n", messages.Select(FormatMessageSummary));
    }

    private async Task<string> GetMessageAsync(uint messageUid, string mailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        var message = await imapClient.GetMessageAsync(_options, NormalizeMailbox(mailbox), messageUid);

        return message is null
            ? $"Message UID {messageUid} was not found in mailbox '{mailbox}'."
            : FormatMessageDetail(message);
    }

    private async Task<string> SearchMessagesAsync(string query, int maxResults = 10, string mailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        var messages = await imapClient.SearchMessagesAsync(_options, NormalizeMailbox(mailbox), query, ClampMaxResults(maxResults));

        if (messages.Count == 0)
            return $"No messages found in mailbox '{mailbox}' for query '{query}'.";

        return string.Join("\n", messages.Select(FormatMessageSummary));
    }

    private async Task<string> SendEmailAsync(string to, string subject, string body, string cc = "", string bcc = "")
    {
        EnsureAvailable();

        await using var smtpClient = _smtpClientFactory();
        string? ccValue = string.IsNullOrWhiteSpace(cc) ? null : cc.Trim();
        string? bccValue = string.IsNullOrWhiteSpace(bcc) ? null : bcc.Trim();
        await smtpClient.SendAsync(_options, new ProtonMailOutgoingMessage(to, subject, body, ccValue, bccValue, null, []));
        return $"Email sent successfully to {to}.";
    }

    private async Task<string> ReplyToEmailAsync(uint messageUid, string body, string mailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        var originalMessage = await imapClient.GetMessageAsync(_options, NormalizeMailbox(mailbox), messageUid);

        if (originalMessage is null)
            return $"Message UID {messageUid} was not found in mailbox '{mailbox}'.";

        var replySubject = originalMessage.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? originalMessage.Subject
            : $"Re: {originalMessage.Subject}";

        IReadOnlyList<string> references = string.IsNullOrWhiteSpace(originalMessage.InternetMessageId)
            ? []
            : [originalMessage.InternetMessageId];

        await using var smtpClient = _smtpClientFactory();
        await smtpClient.SendAsync(_options, new ProtonMailOutgoingMessage(
            originalMessage.ReplyTo,
            replySubject,
            body,
            null,
            null,
            originalMessage.InternetMessageId,
            references));

        return $"Reply sent successfully to {originalMessage.ReplyTo}.";
    }

    private async Task<string> MoveMessageAsync(uint messageUid, string destinationMailbox, string sourceMailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        await imapClient.MoveMessageAsync(_options, NormalizeMailbox(sourceMailbox), NormalizeMailbox(destinationMailbox), messageUid);
        return $"Moved message UID {messageUid} from '{sourceMailbox}' to '{destinationMailbox}'.";
    }

    private async Task<string> SetReadStatusAsync(uint messageUid, bool isRead, string mailbox = "INBOX")
    {
        EnsureAvailable();

        await using var imapClient = _imapClientFactory();
        await imapClient.SetReadStatusAsync(_options, NormalizeMailbox(mailbox), messageUid, isRead);

        return isRead
            ? $"Marked message UID {messageUid} in '{mailbox}' as read."
            : $"Marked message UID {messageUid} in '{mailbox}' as unread.";
    }

    private void EnsureAvailable()
    {
        if (!_isAvailable)
            throw new InvalidOperationException("Proton Mail integration is not initialized.");
    }

    private static int ClampMaxResults(int maxResults) => Math.Clamp(maxResults, 1, 50);

    private static string NormalizeMailbox(string mailbox) =>
        string.IsNullOrWhiteSpace(mailbox) ? "INBOX" : mailbox.Trim();

    private static string FormatMessageSummary(ProtonMailMessage message)
    {
        var date = message.Date?.ToString("u") ?? "unknown date";
        var readStatus = message.IsRead ? "read" : "unread";
        var line = $"[{message.Uid}] {date} | {readStatus} | From: {message.From} | Subject: {message.Subject}";
        return string.IsNullOrEmpty(message.Preview) ? line : $"{line}\n  {message.Preview}";
    }

    private static string FormatMessageDetail(ProtonMailMessage message)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Message UID: {message.Uid}");
        builder.AppendLine($"Mailbox: {message.Mailbox}");
        builder.AppendLine($"Date: {message.Date?.ToString("u") ?? "unknown"}");
        builder.AppendLine($"Read: {message.IsRead}");
        builder.AppendLine($"From: {message.From}");
        builder.AppendLine($"Reply-To: {message.ReplyTo}");
        builder.AppendLine($"To: {message.To}");
        builder.AppendLine($"Subject: {message.Subject}");

        if (!string.IsNullOrWhiteSpace(message.InternetMessageId))
            builder.AppendLine($"Message-Id: {message.InternetMessageId}");

        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(message.Body) ? message.Preview : message.Body);
        return builder.ToString().TrimEnd();
    }
}
