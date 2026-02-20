using System.Text;
using System.Text.RegularExpressions;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Webhooks;
using Anduril.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Services;

/// <summary>
/// Orchestrates automated bugfix generation from Sentry webhook notifications.
/// Receives issue events, validates thresholds, clones the repo, invokes the Augment CLI
/// to generate a fix, opens a pull request, and notifies via the configured communication adapter.
/// </summary>
public sealed class SentryBugfixService(
    IOptions<SentryBugfixOptions> options,
    IOptions<GitHubToolOptions> gitHubOptions,
    IEnumerable<ICommunicationAdapter> adapters,
    IEnumerable<IIntegrationTool> integrationTools,
    IGitCommandRunner gitRunner,
    IAuggieCliRunner auggieRunner,
    IPullRequestCreator pullRequestCreator,
    ILogger<SentryBugfixService> logger)
{
    private readonly SentryBugfixOptions _options = options.Value;
    private readonly GitHubToolOptions _gitHubOptions = gitHubOptions.Value;

    /// <summary>
    /// Processes an incoming Sentry webhook payload end-to-end.
    /// </summary>
    public async Task HandleWebhookAsync(SentryWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogDebug("Sentry bugfix automation is disabled. Ignoring webhook.");
            return;
        }

        if (!string.Equals(payload.Action, "created", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Ignoring Sentry webhook with action '{Action}' (only 'created' is processed).", payload.Action);
            return;
        }

        var issue = payload.Data.Issue;
        var issueRef = issue.ShortId ?? issue.Id;

        if (!int.TryParse(issue.Count, out var occurrenceCount))
        {
            logger.LogWarning("Could not parse occurrence count '{Count}' for issue {IssueId}.", issue.Count, issue.Id);
            return;
        }

        if (occurrenceCount < _options.OccurrenceThreshold)
        {
            logger.LogInformation(
                "Issue {IssueRef} has {Count} occurrences (threshold: {Threshold}). Skipping.",
                issueRef, occurrenceCount, _options.OccurrenceThreshold);
            return;
        }

        if (Is401Error(issue))
        {
            logger.LogInformation("Issue {IssueRef} is an HTTP 401 error (configuration issue). Skipping.", issueRef);
            await NotifyAsync(
                $":warning: Sentry issue *{issueRef}* skipped — HTTP 401 (Unauthorized) errors are configuration issues, not code bugs.\n" +
                $"Title: _{issue.Title}_\nLink: {issue.Permalink ?? issue.WebUrl}",
                cancellationToken);
            return;
        }

        logger.LogInformation(
            "Processing Sentry issue {IssueRef}: '{Title}' ({Count} occurrences, {UserCount} users affected).",
            issueRef, issue.Title, occurrenceCount, issue.UserCount);

        await NotifyAsync(
            $":robot_face: Starting automated bugfix for Sentry issue *{issueRef}*: _{issue.Title}_\n" +
            $"Occurrences: {occurrenceCount} | Users affected: {issue.UserCount} | Level: {issue.Level}",
            cancellationToken);

        var (owner, repo) = ResolveGitHubRepo();
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            logger.LogError("GitHub owner/repo not configured. Cannot proceed with bugfix.");
            await NotifyAsync(":x: Sentry bugfix failed — GitHub owner/repo not configured.", cancellationToken);
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"sentry-bugfix-{Guid.NewGuid():N}");

        try
        {
            await ExecuteBugfixPipelineAsync(issue, issueRef, owner, repo, tempDir, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sentry bugfix pipeline failed for issue {IssueRef}.", issueRef);
            await NotifyAsync(
                $":x: Automated bugfix failed for *{issueRef}*: {ex.Message}",
                cancellationToken);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    private async Task ExecuteBugfixPipelineAsync(
        SentryWebhookIssue issue, string issueRef, string owner, string repo, string tempDir, CancellationToken cancellationToken)
    {
        var token = _gitHubOptions.Token
            ?? throw new InvalidOperationException("GitHub token is required for cloning and pushing.");

        var cloneUrl = $"https://github.com/{owner}/{repo}.git";
        var authHeader = $"Authorization: Bearer {token}";
        var branchName = $"{_options.BranchPrefix}{SanitizeGitRef(issueRef)}";

        // Clone the repository from the configured base branch to avoid basing the fix on the wrong history
        logger.LogInformation("Cloning {Owner}/{Repo} (branch {BaseBranch}) into {TempDir}...", owner, repo, _options.BaseBranch, tempDir);
        await gitRunner.RunAsync(null, $"-c http.extraHeader=\"{authHeader}\" clone --branch {_options.BaseBranch} --single-branch --depth 50 {cloneUrl} {tempDir}", cancellationToken);

        // Store the auth header in local git config so push can authenticate
        await gitRunner.RunAsync(tempDir, $"config http.extraHeader \"{authHeader}\"", cancellationToken);

        // Configure git user in the clone
        await gitRunner.RunAsync(tempDir, "config user.email \"anduril-bot@automated.dev\"", cancellationToken);
        await gitRunner.RunAsync(tempDir, "config user.name \"Anduril Bot\"", cancellationToken);

        // Create a feature branch
        await gitRunner.RunAsync(tempDir, $"checkout -b {branchName}", cancellationToken);

        // Fetch full Sentry issue details from the API
        var issueDetails = await GetSentryIssueDetailsAsync(issue.Id, cancellationToken);

        // Build the auggie prompt and invoke the CLI
        var prompt = BuildAuggiePrompt(issue, issueDetails);
        logger.LogInformation("Invoking auggie CLI for issue {IssueRef}...", issueRef);
        await auggieRunner.RunAsync(tempDir, prompt, cancellationToken);

        // Check if auggie made any changes
        var status = await gitRunner.RunAsync(tempDir, "status --porcelain", cancellationToken);
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogWarning("Auggie made no changes for issue {IssueRef}.", issueRef);
            await NotifyAsync(
                $":warning: Auggie could not generate a fix for *{issueRef}*: _{issue.Title}_",
                cancellationToken);
            return;
        }

        // Stage and commit using a temp file for the commit message (avoids shell escaping issues)
        await gitRunner.RunAsync(tempDir, "add -A", cancellationToken);
        var commitMessage = $"fix: auto-fix for Sentry issue {issueRef}\n\n" +
                            $"Automated bugfix generated by Anduril + Augment Code.\n" +
                            $"Issue: {issue.Title}\n" +
                            $"Occurrences: {issue.Count} | Users: {issue.UserCount}\n" +
                            $"Sentry: {issue.Permalink ?? issue.WebUrl}";
        var commitMsgFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(commitMsgFile, commitMessage, cancellationToken);
            await gitRunner.RunAsync(tempDir, $"commit -F \"{commitMsgFile}\"", cancellationToken);
        }
        finally
        {
            try { File.Delete(commitMsgFile); }
            catch { /* best-effort cleanup */ }
        }

        // Push the branch
        await gitRunner.RunAsync(tempDir, $"push origin {branchName}", cancellationToken);

        // Create pull request
        var prTitle = $"fix: auto-fix Sentry issue {issueRef} — {issue.Title}";
        var prBody = $"## Automated Bugfix — Sentry Issue {issueRef}\n\n" +
                     $"**Error:** {issue.Title}\n" +
                     $"**Level:** {issue.Level}\n" +
                     $"**Occurrences:** {issue.Count} ({issue.UserCount} users affected)\n" +
                     $"**Sentry Link:** {issue.Permalink ?? issue.WebUrl}\n\n" +
                     $"---\n\n" +
                     $"This PR was automatically generated by **Anduril** using **Augment Code (auggie)**.\n" +
                     $"Please review carefully before merging.";
        var prUrl = await pullRequestCreator.CreateAsync(owner, repo, branchName, _options.BaseBranch, prTitle, prBody, cancellationToken);

        logger.LogInformation("Pull request created for issue {IssueRef}: {PrUrl}", issueRef, prUrl);
        await NotifyAsync(
            $":white_check_mark: Automated bugfix PR created for Sentry issue *{issueRef}*: _{issue.Title}_\n" +
            $":link: {prUrl}",
            cancellationToken);
    }

    private static bool Is401Error(SentryWebhookIssue issue)
    {
        var patterns = new[] { "401", "Unauthorized", "HTTP 401" };
        var title = issue.Title ?? string.Empty;
        var metadataTitle = issue.Metadata?.Title ?? string.Empty;
        var combined = $"{title} {metadataTitle}";

        return patterns.Any(p => combined.Contains(p, StringComparison.OrdinalIgnoreCase))
               && Regex.IsMatch(combined, @"\b401\b");
    }

    private async Task NotifyAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.NotificationChannel))
        {
            logger.LogDebug("No notification channel configured. Skipping notification.");
            return;
        }

        var adapter = adapters.FirstOrDefault(a =>
            a.Platform.Equals(_options.NotificationPlatform, StringComparison.OrdinalIgnoreCase)
            && a.IsConnected);

        if (adapter is null)
        {
            logger.LogWarning("No connected adapter for platform '{Platform}'. Notification not sent.", _options.NotificationPlatform);
            return;
        }

        try
        {
            await adapter.SendMessageAsync(new OutgoingMessage
            {
                Text = message,
                ChannelId = _options.NotificationChannel
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send bugfix notification via {Platform}.", _options.NotificationPlatform);
        }
    }

    private (string? Owner, string? Repo) ResolveGitHubRepo()
    {
        var owner = string.IsNullOrWhiteSpace(_options.GitHubOwner) ? _gitHubOptions.DefaultOwner : _options.GitHubOwner;
        var repo = string.IsNullOrWhiteSpace(_options.GitHubRepo) ? _gitHubOptions.DefaultRepo : _options.GitHubRepo;
        return (owner, repo);
    }

    /// <summary>
    /// Sanitizes a string for use as a git ref component. Keeps only alphanumeric chars, dots,
    /// underscores, and hyphens; collapses consecutive hyphens; trims leading/trailing hyphens.
    /// </summary>
    private static string SanitizeGitRef(string input) =>
        Regex.Replace(
            Regex.Replace(input, @"[^A-Za-z0-9._-]", "-"),
            @"-{2,}", "-")
        .Trim('-');

    private async Task<string?> GetSentryIssueDetailsAsync(string issueId, CancellationToken cancellationToken)
    {
        var sentryTool = integrationTools.FirstOrDefault(t =>
            t.Name.Equals("Sentry", StringComparison.OrdinalIgnoreCase));

        if (sentryTool is null || !sentryTool.IsAvailable)
        {
            logger.LogWarning("Sentry integration tool not available. Proceeding without full issue details.");
            return null;
        }

        try
        {
            var functions = sentryTool.GetFunctions();
            var getIssueFn = functions.FirstOrDefault(f =>
                f.Name.Equals("sentry_get_issue", StringComparison.OrdinalIgnoreCase));

            if (getIssueFn is null)
                return null;

            var aiArgs = new AIFunctionArguments(new Dictionary<string, object?> { ["issueId"] = issueId });
            var result = await getIssueFn.InvokeAsync(aiArgs, cancellationToken);

            return result?.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Sentry issue details for {IssueId}.", issueId);
            return null;
        }
    }

    private static string BuildAuggiePrompt(SentryWebhookIssue issue, string? fullDetails)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fix the following production bug that was reported by Sentry error monitoring.");
        sb.AppendLine();
        sb.AppendLine($"**Error Title:** {issue.Title}");
        sb.AppendLine($"**Severity Level:** {issue.Level ?? "unknown"}");
        sb.AppendLine($"**Priority:** {issue.Priority ?? "unknown"}");
        sb.AppendLine($"**Occurrences:** {issue.Count} times, affecting {issue.UserCount} user(s)");

        if (!string.IsNullOrEmpty(issue.Culprit))
            sb.AppendLine($"**Culprit:** {issue.Culprit}");

        if (!string.IsNullOrEmpty(issue.Platform))
            sb.AppendLine($"**Platform:** {issue.Platform}");

        if (!string.IsNullOrEmpty(issue.Permalink ?? issue.WebUrl))
            sb.AppendLine($"**Sentry Issue URL:** {issue.Permalink ?? issue.WebUrl}");

        if (!string.IsNullOrEmpty(fullDetails))
        {
            sb.AppendLine();
            sb.AppendLine("## Full Sentry Issue Details");
            sb.AppendLine("```json");
            sb.AppendLine(fullDetails);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine("1. Analyze the error and identify the root cause in the codebase");
        sb.AppendLine("2. Implement a minimal, targeted fix — do not refactor unrelated code");
        sb.AppendLine("3. Add appropriate error handling and null checks where needed");
        sb.AppendLine("4. Do not introduce breaking changes to public APIs");
        sb.AppendLine("5. Add brief code comments explaining the fix");
        sb.AppendLine("6. If the fix requires a test, add or update the relevant test");
        sb.AppendLine("7. Make sure the code compiles and passes existing tests");

        return sb.ToString();
    }


    private void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up temp directory {Path}.", path);
        }
    }
}

