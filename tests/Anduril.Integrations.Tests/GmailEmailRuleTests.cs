namespace Anduril.Integrations.Tests;

public class GmailEmailRuleTests
{
    [Test]
    public async Task DefaultEnabled_IsTrue()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        await Assert.That(rule.Enabled).IsTrue();
    }

    [Test]
    public async Task Name_IsSet()
    {
        var rule = new GmailEmailRule { Name = "Important Emails", Action = "notify" };
        await Assert.That(rule.Name).IsEqualTo("Important Emails");
    }

    [Test]
    public async Task Action_IsSet()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "auto-respond" };
        await Assert.That(rule.Action).IsEqualTo("auto-respond");
    }

    [Test]
    public async Task FromFilter_DefaultsToNull()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        await Assert.That(rule.FromFilter).IsNull();
    }

    [Test]
    public async Task SubjectFilter_DefaultsToNull()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        await Assert.That(rule.SubjectFilter).IsNull();
    }

    [Test]
    public async Task BodyKeyword_DefaultsToNull()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        await Assert.That(rule.BodyKeyword).IsNull();
    }

    [Test]
    public async Task ActionParameter_DefaultsToNull()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        await Assert.That(rule.ActionParameter).IsNull();
    }

    [Test]
    public async Task AllProperties_CanBeSet()
    {
        var rule = new GmailEmailRule
        {
            Name = "Deploy Alert",
            Action = "label",
            FromFilter = "@ops.com",
            SubjectFilter = "deploy",
            BodyKeyword = "production",
            ActionParameter = "IMPORTANT",
            Enabled = false
        };

        await Assert.That(rule.Name).IsEqualTo("Deploy Alert");
        await Assert.That(rule.Action).IsEqualTo("label");
        await Assert.That(rule.FromFilter).IsEqualTo("@ops.com");
        await Assert.That(rule.SubjectFilter).IsEqualTo("deploy");
        await Assert.That(rule.BodyKeyword).IsEqualTo("production");
        await Assert.That(rule.ActionParameter).IsEqualTo("IMPORTANT");
        await Assert.That(rule.Enabled).IsFalse();
    }
}

