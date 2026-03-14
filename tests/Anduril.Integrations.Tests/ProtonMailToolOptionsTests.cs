namespace Anduril.Integrations.Tests;

public class ProtonMailToolOptionsTests
{
    [Test]
    public async Task DefaultEnabled_IsTrue()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.Enabled).IsTrue();
    }

    [Test]
    public async Task DefaultUsername_IsNull()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.Username).IsNull();
    }

    [Test]
    public async Task DefaultPassword_IsNull()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.Password).IsNull();
    }

    [Test]
    public async Task DefaultImapHost_IsLocalhost()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.ImapHost).IsEqualTo("localhost");
    }

    [Test]
    public async Task DefaultImapPort_Is1143()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.ImapPort).IsEqualTo(1143);
    }

    [Test]
    public async Task DefaultSmtpHost_IsLocalhost()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.SmtpHost).IsEqualTo("localhost");
    }

    [Test]
    public async Task DefaultSmtpPort_Is1025()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.SmtpPort).IsEqualTo(1025);
    }

    [Test]
    public async Task DefaultUseSsl_IsFalse()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.UseSsl).IsFalse();
    }

    [Test]
    public async Task DefaultAcceptSelfSignedCertificate_IsTrue()
    {
        var options = new ProtonMailToolOptions();
        await Assert.That(options.AcceptSelfSignedCertificate).IsTrue();
    }

    [Test]
    public async Task AllProperties_CanBeSet()
    {
        var options = new ProtonMailToolOptions
        {
            Enabled = false,
            Username = "user@pm.me",
            Password = "bridge-password",
            ImapHost = "127.0.0.1",
            ImapPort = 2143,
            SmtpHost = "127.0.0.1",
            SmtpPort = 2025,
            UseSsl = true,
            AcceptSelfSignedCertificate = false
        };

        await Assert.That(options.Enabled).IsFalse();
        await Assert.That(options.Username).IsEqualTo("user@pm.me");
        await Assert.That(options.Password).IsEqualTo("bridge-password");
        await Assert.That(options.ImapHost).IsEqualTo("127.0.0.1");
        await Assert.That(options.ImapPort).IsEqualTo(2143);
        await Assert.That(options.SmtpHost).IsEqualTo("127.0.0.1");
        await Assert.That(options.SmtpPort).IsEqualTo(2025);
        await Assert.That(options.UseSsl).IsTrue();
        await Assert.That(options.AcceptSelfSignedCertificate).IsFalse();
    }
}
