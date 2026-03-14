namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Proton Mail Bridge integration.
/// </summary>
public sealed class ProtonMailToolOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Proton Mail integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Proton Mail Bridge username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the Proton Mail Bridge password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the IMAP host exposed by Proton Mail Bridge.
    /// </summary>
    public string ImapHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the IMAP port exposed by Proton Mail Bridge.
    /// </summary>
    public int ImapPort { get; set; } = 1143;

    /// <summary>
    /// Gets or sets the SMTP host exposed by Proton Mail Bridge.
    /// </summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP port exposed by Proton Mail Bridge.
    /// </summary>
    public int SmtpPort { get; set; } = 1025;

    /// <summary>
    /// Gets or sets a value indicating whether STARTTLS should be required when connecting.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to accept self-signed certificates from Proton Mail Bridge.
    /// Proton Mail Bridge always presents a self-signed certificate on its local IMAP and SMTP ports.
    /// Defaults to <c>true</c> because Bridge-local connections are inherently trusted.
    /// </summary>
    public bool AcceptSelfSignedCertificate { get; set; } = true;
}
