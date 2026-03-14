namespace Anduril.Integrations;

internal interface IProtonMailSmtpClient : IAsyncDisposable
{
    Task VerifyConnectionAsync(ProtonMailToolOptions options, CancellationToken cancellationToken = default);

    Task SendAsync(
        ProtonMailToolOptions options,
        ProtonMailOutgoingMessage message,
        CancellationToken cancellationToken = default);
}