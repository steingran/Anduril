namespace Anduril.Integrations;

internal sealed record ProtonMailMessage(
    uint Uid,
    string Mailbox,
    string Subject,
    string From,
    string ReplyTo,
    string To,
    DateTimeOffset? Date,
    string Preview,
    string Body,
    string? InternetMessageId,
    bool IsRead);