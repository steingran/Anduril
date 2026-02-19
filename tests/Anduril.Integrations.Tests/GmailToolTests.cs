using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class GmailToolTests
{
    private static GmailTool CreateTool(
        string? clientId = null,
        string? clientSecret = null,
        string? refreshToken = null,
        string? pubSubTopic = null,
        List<GmailEmailRule>? rules = null,
        List<string>? importantSenders = null)
    {
        var options = Options.Create(new GmailToolOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            RefreshToken = refreshToken,
            PubSubTopic = pubSubTopic,
            Rules = rules ?? [],
            ImportantSenders = importantSenders ?? []
        });
        return new GmailTool(options, NullLogger<GmailTool>.Instance);
    }

    // ---------------------------------------------------------------
    // Property tests
    // ---------------------------------------------------------------

    [Test]
    public async Task Name_IsGmail()
    {
        var tool = CreateTool();
        await Assert.That(tool.Name).IsEqualTo("gmail");
    }

    [Test]
    public async Task Description_IsNotEmpty()
    {
        var tool = CreateTool();
        await Assert.That(tool.Description).IsNotEmpty();
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var tool = CreateTool();
        await Assert.That(tool.IsAvailable).IsFalse();
    }

    [Test]
    public async Task PubSubTopic_ReturnsConfiguredValue()
    {
        var tool = CreateTool(pubSubTopic: "projects/test/topics/gmail");
        await Assert.That(tool.PubSubTopic).IsEqualTo("projects/test/topics/gmail");
    }

    [Test]
    public async Task ImportantSenders_ReturnsConfiguredList()
    {
        var tool = CreateTool(importantSenders: ["boss@company.com", "cto@company.com"]);
        await Assert.That(tool.ImportantSenders.Count).IsEqualTo(2);
    }

    // ---------------------------------------------------------------
    // InitializeAsync tests
    // ---------------------------------------------------------------

    [Test]
    public async Task InitializeAsync_WithMissingClientId_LeavesUnavailable()
    {
        var tool = CreateTool(clientSecret: "secret", refreshToken: "token");
        await tool.InitializeAsync();
        await Assert.That(tool.IsAvailable).IsFalse();
    }

    [Test]
    public async Task InitializeAsync_WithMissingClientSecret_LeavesUnavailable()
    {
        var tool = CreateTool(clientId: "id", refreshToken: "token");
        await tool.InitializeAsync();
        await Assert.That(tool.IsAvailable).IsFalse();
    }

    [Test]
    public async Task InitializeAsync_WithMissingRefreshToken_LeavesUnavailable()
    {
        var tool = CreateTool(clientId: "id", clientSecret: "secret");
        await tool.InitializeAsync();
        await Assert.That(tool.IsAvailable).IsFalse();
    }

    [Test]
    public async Task InitializeAsync_WithAllEmpty_LeavesUnavailable()
    {
        var tool = CreateTool();
        await tool.InitializeAsync();
        await Assert.That(tool.IsAvailable).IsFalse();
    }

    [Test]
    public async Task InitializeAsync_WithAllCredentials_BecomesAvailable()
    {
        var tool = CreateTool(clientId: "id", clientSecret: "secret", refreshToken: "token");
        await tool.InitializeAsync();
        await Assert.That(tool.IsAvailable).IsTrue();
    }

    // ---------------------------------------------------------------
    // GetFunctions tests
    // ---------------------------------------------------------------

    [Test]
    public async Task GetFunctions_Returns11Functions()
    {
        var tool = CreateTool();
        var functions = tool.GetFunctions();
        await Assert.That(functions.Count).IsEqualTo(11);
    }

    [Test]
    public async Task GetFunctions_ContainsListMessages()
    {
        var tool = CreateTool();
        var names = tool.GetFunctions().Select(f => f.Name).ToList();
        await Assert.That(names).Contains("gmail_list_messages");
    }

    [Test]
    public async Task GetFunctions_ContainsSearch()
    {
        var tool = CreateTool();
        var names = tool.GetFunctions().Select(f => f.Name).ToList();
        await Assert.That(names).Contains("gmail_search");
    }

    [Test]
    public async Task GetFunctions_ContainsSend()
    {
        var tool = CreateTool();
        var names = tool.GetFunctions().Select(f => f.Name).ToList();
        await Assert.That(names).Contains("gmail_send");
    }

    [Test]
    public async Task GetFunctions_ContainsSetupWatch()
    {
        var tool = CreateTool();
        var names = tool.GetFunctions().Select(f => f.Name).ToList();
        await Assert.That(names).Contains("gmail_setup_watch");
    }
}

