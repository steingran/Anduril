namespace Anduril.Integrations.Tests;

/// <summary>
/// Tests for the <see cref="GmailTool.MatchesRule"/> static method.
/// </summary>
public class GmailMatchesRuleTests
{
    // ---------------------------------------------------------------
    // Single-filter matching
    // ---------------------------------------------------------------

    [Test]
    public async Task MatchesRule_FromFilter_MatchesContainedSender()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", FromFilter = "@company.com" };
        var result = GmailTool.MatchesRule(rule, "alice@company.com", "Hello", "body");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_FromFilter_DoesNotMatchDifferentSender()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", FromFilter = "@company.com" };
        var result = GmailTool.MatchesRule(rule, "alice@other.com", "Hello", "body");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesRule_SubjectFilter_MatchesContainedKeyword()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", SubjectFilter = "urgent" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "URGENT: Server down", "body");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_SubjectFilter_DoesNotMatchMissingKeyword()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", SubjectFilter = "urgent" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "Weekly report", "body");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesRule_BodyKeyword_MatchesContainedKeyword()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", BodyKeyword = "deploy" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "Subject", "We need to deploy today");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_BodyKeyword_DoesNotMatchMissingKeyword()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", BodyKeyword = "deploy" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "Subject", "No action required");
        await Assert.That(result).IsFalse();
    }

    // ---------------------------------------------------------------
    // Case insensitivity
    // ---------------------------------------------------------------

    [Test]
    public async Task MatchesRule_FromFilter_IsCaseInsensitive()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", FromFilter = "@COMPANY.COM" };
        var result = GmailTool.MatchesRule(rule, "alice@company.com", "Hi", "body");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_SubjectFilter_IsCaseInsensitive()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", SubjectFilter = "URGENT" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "urgent request", "body");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_BodyKeyword_IsCaseInsensitive()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify", BodyKeyword = "DEPLOY" };
        var result = GmailTool.MatchesRule(rule, "from@test.com", "Subject", "we need to deploy");
        await Assert.That(result).IsTrue();
    }

    // ---------------------------------------------------------------
    // No filters / Multiple filters
    // ---------------------------------------------------------------

    [Test]
    public async Task MatchesRule_NoFilters_ReturnsFalse()
    {
        var rule = new GmailEmailRule { Name = "test", Action = "notify" };
        var result = GmailTool.MatchesRule(rule, "anyone@test.com", "Any subject", "Any body");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesRule_AllFiltersMatch_ReturnsTrue()
    {
        var rule = new GmailEmailRule
        {
            Name = "test", Action = "notify",
            FromFilter = "@company.com",
            SubjectFilter = "urgent",
            BodyKeyword = "deploy"
        };
        var result = GmailTool.MatchesRule(rule, "boss@company.com", "Urgent deploy", "Please deploy now");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesRule_OneOfMultipleFiltersFails_ReturnsFalse()
    {
        var rule = new GmailEmailRule
        {
            Name = "test", Action = "notify",
            FromFilter = "@company.com",
            SubjectFilter = "urgent",
            BodyKeyword = "deploy"
        };
        // FromFilter matches, SubjectFilter matches, but BodyKeyword does NOT match
        var result = GmailTool.MatchesRule(rule, "boss@company.com", "Urgent note", "No relevant content");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesRule_DisabledRule_StillMatchesLogically()
    {
        // MatchesRule checks filter logic only; Enabled is checked by the caller
        var rule = new GmailEmailRule
        {
            Name = "test", Action = "notify",
            FromFilter = "@company.com",
            Enabled = false
        };
        var result = GmailTool.MatchesRule(rule, "boss@company.com", "Hi", "body");
        await Assert.That(result).IsTrue();
    }
}

