using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Anduril.Integrations;

internal sealed class MailKitProtonMailImapClient : IProtonMailImapClient
{
    private readonly ImapClient _client = new();

    public async Task VerifyConnectionAsync(ProtonMailToolOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ProtonMailMessage>> ListMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            var folder = await OpenFolderAsync(mailbox, readOnly: true, cancellationToken);

            if (folder.Count == 0)
                return [];

            // Fetch only the last maxResults messages by sequence-number range
            // to avoid enumerating the full UID list on large mailboxes.
            int startIndex = Math.Max(0, folder.Count - maxResults);
            var summaries = await folder.FetchAsync(
                startIndex, folder.Count - 1,
                MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate,
                cancellationToken);

            return summaries
                .Select(summary => BuildMessageFromEnvelope(summary, mailbox))
                .OrderByDescending(message => message.Date ?? DateTimeOffset.MinValue)
                .ToList();
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async Task<ProtonMailMessage?> GetMessageAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            var folder = await OpenFolderAsync(mailbox, readOnly: true, cancellationToken);
            return await LoadMessageAsync(folder, mailbox, new UniqueId(messageUid), cancellationToken);
        }
        catch (MessageNotFoundException)
        {
            return null;
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ProtonMailMessage>> SearchMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            var folder = await OpenFolderAsync(mailbox, readOnly: true, cancellationToken);
            var searchQuery = BuildSearchQuery(query);
            var uids = await folder.SearchAsync(searchQuery, cancellationToken);
            return await LoadMessagesAsync(folder, mailbox, uids.Reverse().Take(maxResults).ToList(), cancellationToken);
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async Task MoveMessageAsync(
        ProtonMailToolOptions options,
        string sourceMailbox,
        string destinationMailbox,
        uint messageUid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            var sourceFolder = await OpenFolderAsync(sourceMailbox, readOnly: false, cancellationToken);
            var destinationFolder = await GetFolderAsync(destinationMailbox, cancellationToken);
            await sourceFolder.MoveToAsync(new UniqueId(messageUid), destinationFolder, cancellationToken);
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async Task SetReadStatusAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            var folder = await OpenFolderAsync(mailbox, readOnly: false, cancellationToken);
            var uniqueId = new UniqueId(messageUid);

            if (isRead)
                await folder.AddFlagsAsync(uniqueId, MessageFlags.Seen, true, cancellationToken);
            else
                await folder.RemoveFlagsAsync(uniqueId, MessageFlags.Seen, true, cancellationToken);
        }
        finally
        {
            await DisconnectIfConnectedAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectIfConnectedAsync(CancellationToken.None);
        _client.Dispose();
    }

    private async Task ConnectAndAuthenticateAsync(ProtonMailToolOptions options, CancellationToken cancellationToken)
    {
        // Always assign the callback so a previous value is never silently inherited.
        // Bypass validation only when explicitly opted in AND the host is loopback —
        // accepting any cert on a non-loopback host would enable MITM attacks.
        _client.ServerCertificateValidationCallback =
            options.AcceptSelfSignedCertificate && IsLoopbackHost(options.ImapHost)
                ? (_, _, _, _) => true
                : null;

        await _client.ConnectAsync(
            options.ImapHost,
            options.ImapPort,
            GetSecureSocketOptions(options.UseSsl),
            cancellationToken);

        await _client.AuthenticateAsync(options.Username!, options.Password!, cancellationToken);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private async Task DisconnectIfConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync(true, cancellationToken);
    }

    private async Task<IMailFolder> OpenFolderAsync(string mailbox, bool readOnly, CancellationToken cancellationToken)
    {
        var folder = await GetFolderAsync(mailbox, cancellationToken);
        var access = readOnly ? FolderAccess.ReadOnly : FolderAccess.ReadWrite;

        if (!folder.IsOpen || folder.Access != access)
            await folder.OpenAsync(access, cancellationToken);

        return folder;
    }

    private Task<IMailFolder> GetFolderAsync(string mailbox, CancellationToken cancellationToken)
    {
        if (mailbox.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
        {
            // MailKit 4.16+ annotates ImapClient.Inbox as nullable. In practice it is populated
            // once the client is authenticated; surface a clear error if that invariant is broken.
            var inbox = _client.Inbox
                ?? throw new InvalidOperationException("IMAP client has no INBOX folder available; ensure the client is authenticated before fetching mail.");
            return Task.FromResult<IMailFolder>(inbox);
        }

        return GetFolderFromNamespaceAsync(mailbox, cancellationToken);
    }

    private async Task<IMailFolder> GetFolderFromNamespaceAsync(string mailbox, CancellationToken cancellationToken)
    {
        if (_client.PersonalNamespaces.Count == 0)
            return await _client.GetFolderAsync(mailbox, cancellationToken);

        var rootFolder = _client.GetFolder(_client.PersonalNamespaces[0]);
        var folder = rootFolder;

        foreach (var segment in mailbox.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            folder = await folder.GetSubfolderAsync(segment, cancellationToken);
        }

        return folder;
    }

    private static async Task<IReadOnlyList<ProtonMailMessage>> LoadMessagesAsync(
        IMailFolder folder,
        string mailbox,
        IReadOnlyList<UniqueId> uids,
        CancellationToken cancellationToken)
    {
        if (uids.Count == 0)
            return [];

        // Batch-fetch all envelopes, flags, and dates in a single IMAP FETCH command.
        // Body is deliberately not fetched — list/search only needs envelope data.
        // Full body is fetched on demand via GetMessageAsync / LoadMessageAsync.
        var summaries = await folder.FetchAsync(
            uids.ToList(),
            MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate,
            cancellationToken);

        return summaries
            .Select(summary => BuildMessageFromEnvelope(summary, mailbox))
            .OrderByDescending(message => message.Date ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static async Task<ProtonMailMessage?> LoadMessageAsync(
        IMailFolder folder,
        string mailbox,
        UniqueId uid,
        CancellationToken cancellationToken)
    {
        var summaries = await folder.FetchAsync(
            [uid],
            MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate,
            cancellationToken);

        var summary = summaries.FirstOrDefault();
        if (summary is null)
            return null;

        var mimeMessage = await folder.GetMessageAsync(uid, cancellationToken);
        return BuildMessage(summary, mailbox, mimeMessage);
    }

    // Builds a lightweight summary from envelope data only — no body download.
    // Used by list/search operations where full body is not required.
    private static ProtonMailMessage BuildMessageFromEnvelope(IMessageSummary summary, string mailbox) =>
        new(
            summary.UniqueId.Id,
            mailbox,
            summary.Envelope?.Subject ?? "(no subject)",
            summary.Envelope?.From.ToString() ?? "unknown",
            summary.Envelope?.ReplyTo.ToString() ?? summary.Envelope?.From.ToString() ?? "unknown",
            summary.Envelope?.To.ToString() ?? "unknown",
            summary.InternalDate,
            Preview: "",
            Body: "",
            summary.Envelope?.MessageId,
            summary.Flags?.HasFlag(MessageFlags.Seen) == true);

    // Builds a full message including body and preview — used by GetMessageAsync.
    private static ProtonMailMessage BuildMessage(IMessageSummary summary, string mailbox, MimeMessage mimeMessage)
    {
        var body = GetBodyText(mimeMessage);
        var preview = string.IsNullOrWhiteSpace(body)
            ? "(no preview available)"
            : CollapseWhitespace(body).Trim();

        if (preview.Length > 160)
            preview = preview[..160] + "…";

        return new ProtonMailMessage(
            summary.UniqueId.Id,
            mailbox,
            summary.Envelope?.Subject ?? "(no subject)",
            summary.Envelope?.From.ToString() ?? "unknown",
            summary.Envelope?.ReplyTo.ToString() ?? summary.Envelope?.From.ToString() ?? "unknown",
            summary.Envelope?.To.ToString() ?? "unknown",
            summary.InternalDate,
            preview,
            body,
            mimeMessage.MessageId,
            summary.Flags?.HasFlag(MessageFlags.Seen) == true);
    }

    private static string GetBodyText(MimeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
            return message.TextBody;

        if (string.IsNullOrWhiteSpace(message.HtmlBody))
            return string.Empty;

        return CollapseWhitespace(Regex.Replace(message.HtmlBody, "<.*?>", " ", RegexOptions.Singleline)).Trim();
    }

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value, "\\s+", " ");

    private static SearchQuery BuildSearchQuery(string query)
    {
        SearchQuery? result = null;

        foreach (var token in TokenizeQuery(query))
        {
            var current = BuildSearchToken(token);
            result = result is null ? current : result.And(current);
        }

        return result ?? SearchQuery.All;
    }

    private static IReadOnlyList<string> TokenizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        return Regex.Matches(query, "\"[^\"]+\"|\\S+")
            .Select(match => match.Value.Trim().Trim('"'))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static SearchQuery BuildSearchToken(string token)
    {
        if (token.Equals("is:read", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("seen", StringComparison.OrdinalIgnoreCase))
        {
            return SearchQuery.Seen;
        }

        if (token.Equals("is:unread", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("unread", StringComparison.OrdinalIgnoreCase))
        {
            return SearchQuery.NotSeen;
        }

        if (TryGetPrefixedValue(token, "from", out var from))
            return SearchQuery.FromContains(from);

        if (TryGetPrefixedValue(token, "to", out var to))
            return SearchQuery.ToContains(to);

        if (TryGetPrefixedValue(token, "subject", out var subject))
            return SearchQuery.SubjectContains(subject);

        if (TryGetPrefixedValue(token, "body", out var body))
            return SearchQuery.BodyContains(body);

        if ((TryGetPrefixedValue(token, "after", out var after) || TryGetPrefixedValue(token, "since", out after)) &&
            DateTime.TryParse(after, out var afterDate))
        {
            return SearchQuery.DeliveredAfter(afterDate);
        }

        if (TryGetPrefixedValue(token, "before", out var before) && DateTime.TryParse(before, out var beforeDate))
            return SearchQuery.DeliveredBefore(beforeDate);

        return SearchQuery.FromContains(token)
            .Or(SearchQuery.SubjectContains(token))
            .Or(SearchQuery.BodyContains(token));
    }

    private static bool TryGetPrefixedValue(string token, string prefix, out string value)
    {
        var marker = prefix + ":";
        if (token.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            value = token[marker.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static SecureSocketOptions GetSecureSocketOptions(bool useSsl) =>
        useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
}
