using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Anduril.Integrations;

internal sealed class MailKitProtonMailSmtpClient : IProtonMailSmtpClient
{
    private readonly SmtpClient _client = new();

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

    public async Task SendAsync(
        ProtonMailToolOptions options,
        ProtonMailOutgoingMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ConnectAndAuthenticateAsync(options, cancellationToken);
            await _client.SendAsync(CreateMimeMessage(options, message), cancellationToken);
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
            options.AcceptSelfSignedCertificate && IsLoopbackHost(options.SmtpHost)
                ? (_, _, _, _) => true
                : null;

        await _client.ConnectAsync(
            options.SmtpHost,
            options.SmtpPort,
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

    private static MimeMessage CreateMimeMessage(ProtonMailToolOptions options, ProtonMailOutgoingMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(options.Username!));
        AddRecipients(mimeMessage.To, message.To);

        if (!string.IsNullOrWhiteSpace(message.Cc))
            AddRecipients(mimeMessage.Cc, message.Cc);

        if (!string.IsNullOrWhiteSpace(message.Bcc))
            AddRecipients(mimeMessage.Bcc, message.Bcc);

        mimeMessage.Subject = message.Subject;

        if (!string.IsNullOrWhiteSpace(message.InReplyTo))
            mimeMessage.Headers[HeaderId.InReplyTo] = message.InReplyTo;

        if (message.References.Count > 0)
            mimeMessage.Headers[HeaderId.References] = string.Join(" ", message.References);

        mimeMessage.Body = new TextPart(TextFormat.Plain)
        {
            Text = message.Body
        };

        return mimeMessage;
    }

    private static void AddRecipients(InternetAddressList recipients, string addresses)
    {
        recipients.AddRange(InternetAddressList.Parse(addresses));
    }

    private static SecureSocketOptions GetSecureSocketOptions(bool useSsl) =>
        useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
}
