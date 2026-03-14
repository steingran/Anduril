namespace Anduril.Integrations.Tests;

internal sealed class FakeProtonMailImapClient : IProtonMailImapClient
{
    public int VerifyConnectionCallCount { get; private set; }
    public Exception? VerifyConnectionException { get; init; }
    public IReadOnlyList<ProtonMailMessage> ListMessagesResult { get; init; } = [];
    public ProtonMailMessage? GetMessageResult { get; init; }
    public IReadOnlyList<ProtonMailMessage> SearchMessagesResult { get; init; } = [];
    public string? LastListedMailbox { get; private set; }
    public int LastListMaxResults { get; private set; }
    public string? LastSearchQuery { get; private set; }
    public string? LastSearchedMailbox { get; private set; }
    public int LastSearchMaxResults { get; private set; }
    public List<(uint MessageUid, string SourceMailbox, string DestinationMailbox)> MovedMessages { get; } = [];
    public List<(uint MessageUid, string Mailbox, bool IsRead)> ReadStatusUpdates { get; } = [];

    public Task VerifyConnectionAsync(ProtonMailToolOptions options, CancellationToken cancellationToken = default)
    {
        VerifyConnectionCallCount++;

        if (VerifyConnectionException is not null)
            throw VerifyConnectionException;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProtonMailMessage>> ListMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        LastListedMailbox = mailbox;
        LastListMaxResults = maxResults;
        return Task.FromResult(ListMessagesResult);
    }

    public Task<ProtonMailMessage?> GetMessageAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetMessageResult);

    public Task<IReadOnlyList<ProtonMailMessage>> SearchMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        LastSearchQuery = query;
        LastSearchedMailbox = mailbox;
        LastSearchMaxResults = maxResults;
        return Task.FromResult(SearchMessagesResult);
    }

    public Task MoveMessageAsync(
        ProtonMailToolOptions options,
        string sourceMailbox,
        string destinationMailbox,
        uint messageUid,
        CancellationToken cancellationToken = default)
    {
        MovedMessages.Add((messageUid, sourceMailbox, destinationMailbox));
        return Task.CompletedTask;
    }

    public Task SetReadStatusAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        ReadStatusUpdates.Add((messageUid, mailbox, isRead));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}