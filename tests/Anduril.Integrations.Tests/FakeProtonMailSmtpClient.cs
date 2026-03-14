namespace Anduril.Integrations.Tests;

internal sealed class FakeProtonMailSmtpClient : IProtonMailSmtpClient
{
    public int VerifyConnectionCallCount { get; private set; }
    public List<ProtonMailOutgoingMessage> SentMessages { get; } = [];

    public Task VerifyConnectionAsync(ProtonMailToolOptions options, CancellationToken cancellationToken = default)
    {
        VerifyConnectionCallCount++;
        return Task.CompletedTask;
    }

    public Task SendAsync(
        ProtonMailToolOptions options,
        ProtonMailOutgoingMessage message,
        CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}