namespace Anduril.Integrations;

internal interface IProtonMailImapClient : IAsyncDisposable
{
    Task VerifyConnectionAsync(ProtonMailToolOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProtonMailMessage>> ListMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<ProtonMailMessage?> GetMessageAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProtonMailMessage>> SearchMessagesAsync(
        ProtonMailToolOptions options,
        string mailbox,
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task MoveMessageAsync(
        ProtonMailToolOptions options,
        string sourceMailbox,
        string destinationMailbox,
        uint messageUid,
        CancellationToken cancellationToken = default);

    Task SetReadStatusAsync(
        ProtonMailToolOptions options,
        string mailbox,
        uint messageUid,
        bool isRead,
        CancellationToken cancellationToken = default);
}