namespace Anduril.Integrations;

internal sealed record ProtonMailOutgoingMessage(
    string To,
    string Subject,
    string Body,
    string? Cc,
    string? Bcc,
    string? InReplyTo,
    IReadOnlyList<string> References);