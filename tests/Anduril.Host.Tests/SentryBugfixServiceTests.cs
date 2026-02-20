using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Webhooks;
using Anduril.Host.Services;
using Anduril.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Tests;

public class SentryBugfixServiceTests
{
    // ---------------------------------------------------------------
    // Disabled
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_WhenDisabled_DoesNothing()
    {
        var (service, adapter) = CreateService(opts => opts.Enabled = false);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Non-created action
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_NonCreatedAction_DoesNothing()
    {
        var (service, adapter) = CreateService();
        var payload = CreatePayload(action: "resolved", count: "100");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_AssignedAction_DoesNothing()
    {
        var (service, adapter) = CreateService();
        var payload = CreatePayload(action: "assigned", count: "100");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Invalid count
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_UnparseableCount_DoesNothing()
    {
        var (service, adapter) = CreateService();
        var payload = CreatePayload(action: "created", count: "not-a-number");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_EmptyCount_DoesNothing()
    {
        var (service, adapter) = CreateService();
        var payload = CreatePayload(action: "created", count: "");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Below threshold
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_BelowThreshold_DoesNothing()
    {
        var (service, adapter) = CreateService(opts => opts.OccurrenceThreshold = 50);
        var payload = CreatePayload(action: "created", count: "25");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_ExactlyAtThreshold_Proceeds()
    {
        var (service, adapter) = CreateService(opts =>
        {
            opts.OccurrenceThreshold = 10;
            opts.GitHubOwner = "test-owner";
            opts.GitHubRepo = "test-repo";
        });
        var payload = CreatePayload(action: "created", count: "10");

        // This will proceed past threshold but fail at the pipeline (no git/token).
        // We verify it got past the threshold by checking that a "Starting" notification was sent.
        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(adapter.SentMessages[0].Text).Contains("Starting automated bugfix");
    }

    // ---------------------------------------------------------------
    // 401 error detection
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_401InTitle_SendsSkipNotification()
    {
        var (service, adapter) = CreateService();
        var payload = CreatePayload(action: "created", count: "100", title: "HTTP 401 Unauthorized");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.SentMessages[0].Text).Contains("skipped");
        await Assert.That(adapter.SentMessages[0].Text).Contains("401");
    }

    [Test]
    public async Task HandleWebhookAsync_401InMetadataTitle_SendsSkipNotification()
    {
        var (service, adapter) = CreateService();
        var basePayload = CreatePayload(action: "created", count: "100", title: "Request failed");
        var payload = basePayload with
        {
            Data = basePayload.Data with
            {
                Issue = basePayload.Data.Issue with
                {
                    Metadata = new SentryWebhookMetadata { Title = "Error 401 Unauthorized" }
                }
            }
        };

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.SentMessages[0].Text).Contains("skipped");
        await Assert.That(adapter.SentMessages[0].Text).Contains("401");
    }

    [Test]
    public async Task HandleWebhookAsync_UnauthorizedWithout401Number_IsNot401Error()
    {
        // "Unauthorized" alone without the number 401 should NOT be detected as a 401
        // because Is401Error requires both a pattern match AND \b401\b regex
        var (service, adapter) = CreateService(opts =>
        {
            opts.GitHubOwner = "test-owner";
            opts.GitHubRepo = "test-repo";
        });
        var payload = CreatePayload(action: "created", count: "100", title: "Unauthorized access denied");

        await service.HandleWebhookAsync(payload);

        // Should NOT have sent a "skipped 401" message — should proceed past 401 check
        // It will send a "Starting automated bugfix" notification instead
        var has401Skip = adapter.SentMessages.Any(m => m.Text.Contains("skipped") && m.Text.Contains("401"));
        await Assert.That(has401Skip).IsFalse();
    }

    [Test]
    public async Task HandleWebhookAsync_Regular500Error_IsNot401Error()
    {
        var (service, adapter) = CreateService(opts =>
        {
            opts.GitHubOwner = "test-owner";
            opts.GitHubRepo = "test-repo";
        });
        var payload = CreatePayload(action: "created", count: "100", title: "Internal Server Error 500");

        await service.HandleWebhookAsync(payload);

        var has401Skip = adapter.SentMessages.Any(m => m.Text.Contains("skipped") && m.Text.Contains("401"));
        await Assert.That(has401Skip).IsFalse();
    }

    // ---------------------------------------------------------------
    // GitHub config missing
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_NoGitHubConfig_SendsFailureNotification()
    {
        var (service, adapter) = CreateService(opts =>
        {
            opts.GitHubOwner = null;
            opts.GitHubRepo = null;
        }, gitHubOpts: new GitHubToolOptions { DefaultOwner = null, DefaultRepo = null });
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Should send "Starting" notification, then "failed — GitHub owner/repo not configured"
        var failMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("GitHub owner/repo not configured"));
        await Assert.That(failMsg).IsNotNull();
    }

    [Test]
    public async Task HandleWebhookAsync_FallsBackToGitHubToolOptions()
    {
        var (service, adapter) = CreateService(
            opts =>
            {
                opts.GitHubOwner = null;
                opts.GitHubRepo = null;
            },
            gitHubOpts: new GitHubToolOptions
            {
                DefaultOwner = "fallback-owner",
                DefaultRepo = "fallback-repo"
            });
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Should NOT send a "GitHub owner/repo not configured" error — it falls back to GitHubToolOptions
        var failMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("GitHub owner/repo not configured"));
        await Assert.That(failMsg).IsNull();
    }

    [Test]
    public async Task HandleWebhookAsync_EmptyStringGitHubConfig_FallsBackToGitHubToolOptions()
    {
        var (service, adapter) = CreateService(
            opts =>
            {
                opts.GitHubOwner = "";
                opts.GitHubRepo = "";
            },
            gitHubOpts: new GitHubToolOptions
            {
                DefaultOwner = "fallback-owner",
                DefaultRepo = "fallback-repo"
            });
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Empty strings should fall back to GitHubToolOptions just like null does
        var failMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("GitHub owner/repo not configured"));
        await Assert.That(failMsg).IsNull();
    }

    // ---------------------------------------------------------------
    // Notification routing
    // ---------------------------------------------------------------

    [Test]
    public async Task HandleWebhookAsync_NoNotificationChannel_SkipsNotification()
    {
        var (service, adapter) = CreateService(opts => opts.NotificationChannel = null);
        var payload = CreatePayload(action: "created", count: "100", title: "HTTP 401 Unauthorized");

        await service.HandleWebhookAsync(payload);

        // Even though this is a 401 (which normally sends a skip notification), no channel = no message
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_NoMatchingAdapter_SkipsNotification()
    {
        var adapter = new FakeAdapter { PlatformName = "teams" };
        var service = CreateServiceWithAdapter(adapter, opts => opts.NotificationPlatform = "slack");
        var payload = CreatePayload(action: "created", count: "100", title: "HTTP 401 Unauthorized");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_DisconnectedAdapter_SkipsNotification()
    {
        var adapter = new FakeAdapter { PlatformName = "slack", ForceDisconnected = true };
        var service = CreateServiceWithAdapter(adapter, opts => opts.NotificationPlatform = "slack");
        var payload = CreatePayload(action: "created", count: "100", title: "HTTP 401 Unauthorized");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleWebhookAsync_NotificationSentToCorrectChannel()
    {
        var (service, adapter) = CreateService(opts => opts.NotificationChannel = "C-BUGFIX");
        var payload = CreatePayload(action: "created", count: "100", title: "HTTP 401 Unauthorized");

        await service.HandleWebhookAsync(payload);

        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.SentMessages[0].ChannelId).IsEqualTo("C-BUGFIX");
    }

    // ---------------------------------------------------------------
    // Pipeline — happy path
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_HappyPath_ClonesRunsAuggieCreatesPrAndNotifies()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M src/Foo.cs\n";

        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator { PrUrlToReturn = "https://github.com/test-owner/test-repo/pull/99" };

        var (service, adapter) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Verify git command sequence
        var gitArgs = gitRunner.Calls.Select(c => c.Arguments).ToList();
        await Assert.That(gitArgs.Any(a => a.Contains("clone") && a.Contains("http.extraHeader"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("config http.extraHeader"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("config user.email"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("config user.name"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("checkout -b"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("status --porcelain"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("add -A"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("commit -F"))).IsTrue();
        await Assert.That(gitArgs.Any(a => a.Contains("push origin"))).IsTrue();

        // Token must NOT be embedded in clone URL
        var cloneArgs = gitArgs.First(a => a.Contains("clone"));
        await Assert.That(cloneArgs).DoesNotContain("x-access-token");

        // Auggie was invoked
        await Assert.That(auggieRunner.ReceivedPrompt).IsNotNull();

        // PR was created
        await Assert.That(prCreator.LastCall).IsNotNull();

        // Success notification contains PR URL
        var successMsg = adapter.SentMessages.LastOrDefault();
        await Assert.That(successMsg).IsNotNull();
        await Assert.That(successMsg!.Text).Contains("https://github.com/test-owner/test-repo/pull/99");
        await Assert.That(successMsg.Text).Contains("white_check_mark");
    }

    // ---------------------------------------------------------------
    // Pipeline — auggie produces no changes
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_AuggieNoChanges_SendsWarningNotification()
    {
        var gitRunner = new FakeGitCommandRunner();
        // status --porcelain returns empty → no changes
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = "";

        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var (service, adapter) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // PR should NOT have been created
        await Assert.That(prCreator.LastCall).IsNull();

        // Warning notification sent
        var warningMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("could not generate a fix"));
        await Assert.That(warningMsg).IsNotNull();
    }

    // ---------------------------------------------------------------
    // Pipeline — git clone failure
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_GitCloneFailure_SendsErrorNotification()
    {
        var gitRunner = new FakeGitCommandRunner
        {
            ExceptionToThrow = new InvalidOperationException("git clone failed: repository not found")
        };
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var (service, adapter) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Error notification sent (not a crash)
        var errorMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("failed"));
        await Assert.That(errorMsg).IsNotNull();
        await Assert.That(errorMsg!.Text).Contains("git clone failed");

        // Auggie should NOT have been invoked
        await Assert.That(auggieRunner.ReceivedPrompt).IsNull();
    }

    // ---------------------------------------------------------------
    // Pipeline — missing GitHub token
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_MissingGitHubToken_SendsErrorNotification()
    {
        var gitRunner = new FakeGitCommandRunner();
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        // Token is null → should throw inside the pipeline
        var adapter = new FakeAdapter();
        var service = CreateServiceWithAdapter(adapter,
            opts =>
            {
                opts.GitHubOwner = "test-owner";
                opts.GitHubRepo = "test-repo";
            },
            gitHubOpts: new GitHubToolOptions { Token = null },
            gitRunner: gitRunner,
            auggieRunner: auggieRunner,
            pullRequestCreator: prCreator);

        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        var errorMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("failed"));
        await Assert.That(errorMsg).IsNotNull();
        await Assert.That(errorMsg!.Text).Contains("GitHub token is required");
    }

    // ---------------------------------------------------------------
    // Pipeline — auggie prompt content
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_AuggiePromptContainsIssueDetails()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M file.cs\n";
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var (service, _) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100", title: "NullReferenceException in Foo.Bar()");

        await service.HandleWebhookAsync(payload);

        await Assert.That(auggieRunner.ReceivedPrompt).IsNotNull();
        await Assert.That(auggieRunner.ReceivedPrompt!).Contains("NullReferenceException in Foo.Bar()");
        await Assert.That(auggieRunner.ReceivedPrompt).Contains("100 times");
        await Assert.That(auggieRunner.ReceivedPrompt).Contains("error");
        await Assert.That(auggieRunner.ReceivedPrompt).Contains("Instructions");
    }

    // ---------------------------------------------------------------
    // Pipeline — PR creation parameters
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_PrTitleAndBodyContainIssueInfo()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M file.cs\n";
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var (service, _) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100", shortId: "PROJ-42");

        await service.HandleWebhookAsync(payload);

        await Assert.That(prCreator.LastCall).IsNotNull();
        var (owner, repo, branch, baseBranch, title, body) = prCreator.LastCall!.Value;
        await Assert.That(owner).IsEqualTo("test-owner");
        await Assert.That(repo).IsEqualTo("test-repo");
        await Assert.That(baseBranch).IsEqualTo("main");
        await Assert.That(title).Contains("PROJ-42");
        await Assert.That(body).Contains("PROJ-42");
        await Assert.That(body).Contains("Augment Code");
    }

    // ---------------------------------------------------------------
    // Pipeline — correct branch name
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_BranchNameUsesConfiguredPrefix()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M file.cs\n";
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var adapter = new FakeAdapter();
        var service = CreateServiceWithAdapter(adapter,
            opts =>
            {
                opts.GitHubOwner = "test-owner";
                opts.GitHubRepo = "test-repo";
                opts.BranchPrefix = "bugfix/sentry-";
            },
            gitHubOpts: new GitHubToolOptions { Token = "fake-token" },
            gitRunner: gitRunner,
            auggieRunner: auggieRunner,
            pullRequestCreator: prCreator);

        var payload = CreatePayload(action: "created", count: "100", shortId: "PROJ-42");

        await service.HandleWebhookAsync(payload);

        var checkoutCall = gitRunner.Calls.FirstOrDefault(c => c.Arguments.Contains("checkout -b"));
        await Assert.That(checkoutCall.Arguments).Contains("bugfix/sentry-PROJ-42");

        await Assert.That(prCreator.LastCall).IsNotNull();
        await Assert.That(prCreator.LastCall!.Value.Branch).IsEqualTo("bugfix/sentry-PROJ-42");
    }

    // ---------------------------------------------------------------
    // Pipeline — with Sentry tool enrichment
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_WithSentryTool_PromptContainsEnrichedDetails()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M file.cs\n";
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        var sentryTool = new FakeSentryTool
        {
            IssueDetailsToReturn = "{\"stacktrace\":\"at Foo.Bar() in Foo.cs:line 42\"}"
        };

        var adapter = new FakeAdapter();
        var service = CreateServiceWithAdapter(adapter,
            opts =>
            {
                opts.GitHubOwner = "test-owner";
                opts.GitHubRepo = "test-repo";
            },
            gitHubOpts: new GitHubToolOptions { Token = "fake-token" },
            integrationTools: [sentryTool],
            gitRunner: gitRunner,
            auggieRunner: auggieRunner,
            pullRequestCreator: prCreator);

        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        await Assert.That(auggieRunner.ReceivedPrompt).IsNotNull();
        await Assert.That(auggieRunner.ReceivedPrompt!).Contains("stacktrace");
        await Assert.That(auggieRunner.ReceivedPrompt).Contains("Foo.cs:line 42");
    }

    // ---------------------------------------------------------------
    // Pipeline — without Sentry tool still works
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_WithoutSentryTool_StillGeneratesFix()
    {
        var gitRunner = new FakeGitCommandRunner();
        gitRunner.OutputByArgumentSubstring["status --porcelain"] = " M file.cs\n";
        var auggieRunner = new FakeAuggieCliRunner();
        var prCreator = new FakePullRequestCreator();

        // No integration tools at all
        var (service, adapter) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        // Should still have invoked auggie and created PR
        await Assert.That(auggieRunner.ReceivedPrompt).IsNotNull();
        await Assert.That(prCreator.LastCall).IsNotNull();
        var successMsg = adapter.SentMessages.LastOrDefault();
        await Assert.That(successMsg).IsNotNull();
        await Assert.That(successMsg!.Text).Contains("white_check_mark");
    }

    // ---------------------------------------------------------------
    // Pipeline — auggie failure sends error notification
    // ---------------------------------------------------------------

    [Test]
    public async Task Pipeline_AuggieThrows_SendsErrorNotification()
    {
        var gitRunner = new FakeGitCommandRunner();
        var auggieRunner = new FakeAuggieCliRunner
        {
            ExceptionToThrow = new TimeoutException("Auggie timed out after 10 minutes")
        };
        var prCreator = new FakePullRequestCreator();

        var (service, adapter) = CreatePipelineService(gitRunner, auggieRunner, prCreator);
        var payload = CreatePayload(action: "created", count: "100");

        await service.HandleWebhookAsync(payload);

        var errorMsg = adapter.SentMessages.FirstOrDefault(m => m.Text.Contains("failed"));
        await Assert.That(errorMsg).IsNotNull();
        await Assert.That(errorMsg!.Text).Contains("timed out");

        // PR should NOT have been created
        await Assert.That(prCreator.LastCall).IsNull();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static SentryWebhookPayload CreatePayload(
        string action = "created",
        string count = "100",
        string title = "NullReferenceException in Foo.Bar()",
        string issueId = "12345",
        string? shortId = "PROJ-42")
    {
        return new SentryWebhookPayload
        {
            Action = action,
            Data = new SentryWebhookData
            {
                Issue = new SentryWebhookIssue
                {
                    Id = issueId,
                    ShortId = shortId,
                    Title = title,
                    Count = count,
                    UserCount = 5,
                    Level = "error",
                    Permalink = "https://sentry.io/issues/12345/"
                }
            }
        };
    }

    private static (SentryBugfixService Service, FakeAdapter Adapter) CreateService(
        Action<SentryBugfixOptions>? configureOptions = null,
        GitHubToolOptions? gitHubOpts = null)
    {
        var adapter = new FakeAdapter();
        var service = CreateServiceWithAdapter(adapter, configureOptions, gitHubOpts);
        return (service, adapter);
    }

    private static SentryBugfixService CreateServiceWithAdapter(
        FakeAdapter adapter,
        Action<SentryBugfixOptions>? configureOptions = null,
        GitHubToolOptions? gitHubOpts = null,
        IEnumerable<IIntegrationTool>? integrationTools = null,
        IGitCommandRunner? gitRunner = null,
        IAuggieCliRunner? auggieRunner = null,
        IPullRequestCreator? pullRequestCreator = null)
    {
        var opts = new SentryBugfixOptions
        {
            Enabled = true,
            OccurrenceThreshold = 10,
            NotificationPlatform = "test",
            NotificationChannel = "C-TEST"
        };
        configureOptions?.Invoke(opts);

        gitHubOpts ??= new GitHubToolOptions();

        return new SentryBugfixService(
            Options.Create(opts),
            Options.Create(gitHubOpts),
            [adapter],
            integrationTools ?? [],
            gitRunner ?? new NoOpGitCommandRunner(),
            auggieRunner ?? new NoOpAuggieCliRunner(),
            pullRequestCreator ?? new NoOpPullRequestCreator(),
            NullLogger<SentryBugfixService>.Instance);
    }

    /// <summary>
    /// Convenience helper for pipeline tests: creates a service with recording fakes,
    /// pre-configured GitHub owner/repo, and a valid token.
    /// </summary>
    private static (SentryBugfixService Service, FakeAdapter Adapter) CreatePipelineService(
        FakeGitCommandRunner gitRunner,
        FakeAuggieCliRunner auggieRunner,
        FakePullRequestCreator prCreator)
    {
        var adapter = new FakeAdapter();
        var service = CreateServiceWithAdapter(adapter,
            opts =>
            {
                opts.GitHubOwner = "test-owner";
                opts.GitHubRepo = "test-repo";
            },
            gitHubOpts: new GitHubToolOptions { Token = "fake-token" },
            gitRunner: gitRunner,
            auggieRunner: auggieRunner,
            pullRequestCreator: prCreator);
        return (service, adapter);
    }

    // ---------------------------------------------------------------
    // Fakes
    // ---------------------------------------------------------------

    private sealed class FakeAdapter : ICommunicationAdapter
    {
        public string PlatformName { get; set; } = "test";
        public bool ForceDisconnected { get; set; }

        public string Platform => PlatformName;
        public bool IsConnected => !ForceDisconnected;
        public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;
        public List<OutgoingMessage> SentMessages { get; } = [];

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.FromResult<string?>($"ts-{SentMessages.Count}");
        }

        public Task UpdateMessageAsync(string messageId, OutgoingMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // No-op implementations used by decision-logic tests (never reach the pipeline)
    private sealed class NoOpGitCommandRunner : IGitCommandRunner
    {
        public Task<string> RunAsync(string? workingDirectory, string arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class NoOpAuggieCliRunner : IAuggieCliRunner
    {
        public Task RunAsync(string workingDirectory, string prompt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpPullRequestCreator : IPullRequestCreator
    {
        public Task<string> CreateAsync(string owner, string repo, string branchName, string baseBranch, string title, string body, CancellationToken cancellationToken = default)
            => Task.FromResult("https://github.com/test/test/pull/1");
    }

    // Recording fakes used by pipeline tests
    private sealed class FakeGitCommandRunner : IGitCommandRunner
    {
        public List<(string? WorkingDirectory, string Arguments)> Calls { get; } = [];
        public Dictionary<string, string> OutputByArgumentSubstring { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> RunAsync(string? workingDirectory, string arguments, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            Calls.Add((workingDirectory, arguments));

            foreach (var (substring, output) in OutputByArgumentSubstring)
            {
                if (arguments.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(output);
            }

            return Task.FromResult(string.Empty);
        }
    }

    private sealed class FakeAuggieCliRunner : IAuggieCliRunner
    {
        public string? ReceivedWorkingDirectory { get; private set; }
        public string? ReceivedPrompt { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task RunAsync(string workingDirectory, string prompt, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            ReceivedWorkingDirectory = workingDirectory;
            ReceivedPrompt = prompt;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePullRequestCreator : IPullRequestCreator
    {
        public string PrUrlToReturn { get; set; } = "https://github.com/test-owner/test-repo/pull/42";
        public (string Owner, string Repo, string Branch, string BaseBranch, string Title, string Body)? LastCall { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> CreateAsync(string owner, string repo, string branchName, string baseBranch, string title, string body, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            LastCall = (owner, repo, branchName, baseBranch, title, body);
            return Task.FromResult(PrUrlToReturn);
        }
    }

    private sealed class FakeSentryTool : IIntegrationTool
    {
        public string Name => "sentry";
        public string Description => "Fake Sentry tool for testing";
        public bool IsAvailable => true;
        public string? IssueDetailsToReturn { get; set; }

        public IReadOnlyList<AIFunction> GetFunctions()
        {
            var fn = AIFunctionFactory.Create(
                (string issueId) => IssueDetailsToReturn ?? "{}",
                "sentry_get_issue",
                "Get Sentry issue details");
            return [fn];
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

