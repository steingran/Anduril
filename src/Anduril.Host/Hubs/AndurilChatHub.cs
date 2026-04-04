using System.Collections.Concurrent;
using System.Diagnostics;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Hubs;

/// <summary>
/// SignalR hub for real-time chat communication between the Anduril desktop application
/// and the embedded ASP.NET Core host. Supports streaming AI responses, model selection,
/// and conversation management.
/// </summary>
public sealed class AndurilChatHub(
    IEnumerable<IAiProvider> aiProviders,
    IEnumerable<IIntegrationTool> integrationTools,
    IConversationSessionStore sessionStore,
    IOptionsMonitor<AiProviderOptions> providerOptions,
    ILogger<AndurilChatHub> logger)
    : Hub
{
    /// <summary>
    /// Tracks the selected provider per connection so each client can independently choose a model.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> SelectedProviders = new();

    /// <summary>
    /// Tracks in-flight CancellationTokenSources keyed by "{connectionId}:{conversationId}"
    /// so they can be cancelled via <see cref="CancelMessage"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveRequests = new();

    /// <summary>
    /// Returns the list of available AI providers with their current status.
    /// Providers that support multiple models (e.g. Copilot) are expanded to one entry per model.
    /// Called by the desktop app at startup to populate the model selector.
    /// </summary>
    public async Task<List<ProviderInfo>> GetAvailableProviders()
    {
        var result = new List<ProviderInfo>();

        foreach (var p in aiProviders)
        {
            var options = providerOptions.Get(p.Name);

            IReadOnlyList<ModelInfo> remoteModels = [];
            if (p.IsAvailable)
            {
                try { remoteModels = await p.GetAvailableModelsAsync(); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to list models for provider '{Provider}'", p.Name);
                }
            }

            if (remoteModels.Count > 0)
            {
                // Expand to one entry per remote model, using a compound ID
                foreach (var model in remoteModels)
                {
                    result.Add(new ProviderInfo
                    {
                        Id = $"{p.Name}::{model.Id}",
                        Name = p.Name,
                        Model = model.Id,
                        DisplayName = model.DisplayName,
                        IsAvailable = p.IsAvailable,
                        SupportsChatCompletion = p.SupportsChatCompletion,
                    });
                }
            }
            else
            {
                string model = !string.IsNullOrWhiteSpace(options.Model) ? options.Model : "default";
                result.Add(new ProviderInfo
                {
                    Id = p.Name,
                    Name = p.Name,
                    Model = model,
                    IsAvailable = p.IsAvailable,
                    SupportsChatCompletion = p.SupportsChatCompletion,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Sets the active AI provider for the current connection.
    /// </summary>
    public Task SelectModel(string providerId)
    {
        SelectedProviders[Context.ConnectionId] = providerId;
        logger.LogInformation("Connection {ConnectionId} selected provider '{Provider}'",
            Context.ConnectionId, providerId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new conversation session and returns its info.
    /// </summary>
    public Task<ConversationInfo> CreateConversation()
    {
        var id = $"desktop:{Guid.NewGuid():N}";
        return Task.FromResult(new ConversationInfo
        {
            Id = id,
            Title = "New conversation",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Loads conversation history for the given session.
    /// </summary>
    public async Task<List<SessionMessage>> GetConversationHistory(string conversationId)
    {
        var messages = await sessionStore.LoadAsync(conversationId);
        return messages.ToList();
    }

    /// <summary>
    /// Sends a user message and streams the AI response back token-by-token
    /// via the <c>ReceiveToken</c> client callback.
    /// </summary>
    /// <param name="conversationId">The session key for conversation history.</param>
    /// <param name="text">The user's message text.</param>
    /// <param name="providerId">Optional AI provider to use; falls back to the connection's selected provider.</param>
    /// <param name="repoPath">
    /// Optional absolute path to a local git repository. When supplied the system prompt is
    /// replaced with a code-assistant prompt that includes the top-level repository structure,
    /// giving the model the context it needs to answer questions about the codebase.
    /// </param>
    /// <param name="branchName">
    /// Optional git branch name currently selected in the UI. Included in the code-assistant
    /// system prompt so the model knows which branch the user is working on.
    /// </param>
    /// <summary>
    /// Cancels an in-flight <see cref="SendMessage"/> call for the given conversation.
    /// The streaming loop will be interrupted and a final completion token sent to the caller.
    /// </summary>
    public Task CancelMessage(string conversationId)
    {
        var key = $"{Context.ConnectionId}:{conversationId}";
        if (ActiveRequests.TryRemove(key, out var cts))
        {
            logger.LogInformation("Cancelling in-flight request for conversation '{ConversationId}'", conversationId);
            cts.Cancel();
        }
        return Task.CompletedTask;
    }

    public async Task SendMessage(string conversationId, string text, string? providerId, string? repoPath = null, string? branchName = null)
    {
        var effectiveProviderId = providerId
            ?? (SelectedProviders.TryGetValue(Context.ConnectionId, out var selected) ? selected : null);

        var (provider, chatClient) = ResolveProviderAndClient(effectiveProviderId);

        if (provider is null || chatClient is null)
        {
            await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
            {
                ConversationId = conversationId,
                Token = "No AI provider is available. Please check the configuration.",
                IsComplete = true,
                Error = "no_provider"
            });
            return;
        }

        var requestKey = $"{Context.ConnectionId}:{conversationId}";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);

        // Cancel any previous in-flight request for the same conversation to avoid orphaned CTS.
        if (ActiveRequests.TryRemove(requestKey, out var previousCts))
            previousCts.Cancel();
        ActiveRequests.TryAdd(requestKey, cts);

        try
        {
            // Collect tools. AI provider tools (e.g. Augment MCP) must NOT be included here
            // because FunctionInvokingChatClient would route requests through the MCP tool loop,
            // which hangs or produces empty responses with certain providers (e.g. Copilot SDK).
            //
            // Chat tab (repoPath == null): use integration tools (Sentry, GitHub, Calendar, etc.)
            // Code tab (repoPath != null): use repo-scoped tools (read_file, list_files, run_git)
            //   — EXCEPT when the Copilot SDK is the selected provider. The Copilot SDK runs its
            //   own internal agent loop and attempts to execute MEAI AIFunction tools through its
            //   own infrastructure (emitting ToolExecutionStart/Complete, SubagentStarted events)
            //   rather than returning FunctionCallContent to FunctionInvokingChatClient. This means
            //   our tools can never be invoked, the content builder stays empty, and the response
            //   is blank. For Copilot, skip explicit tools and rely on the streaming path with the
            //   repo structure in the system prompt.
            var isCopilotProvider = provider?.Name?.Equals("copilot", StringComparison.OrdinalIgnoreCase) == true;
            var tools = new List<AITool>();
            if (repoPath is null)
            {
                foreach (var tool in integrationTools.Where(t => t.IsAvailable))
                    tools.AddRange(tool.GetFunctions());
            }
            else if (!isCopilotProvider)
            {
                // Repo-scoped tools let the AI actually read files and query git rather than
                // relying solely on the top-level directory listing in the system prompt.
                tools.AddRange(BuildRepoTools(repoPath));
            }

            logger.LogInformation(
                "Hub assembled {ToolCount} tool(s) for conversation '{ConversationId}'{CodeTabNote}",
                tools.Count, conversationId,
                repoPath is not null
                    ? isCopilotProvider
                        ? " (code tab: Copilot provider — tools skipped, streaming path)"
                        : " (code tab: using repo-scoped tools)"
                    : string.Empty);

            // Load history and build message list
            var history = await sessionStore.LoadAsync(conversationId);
            var systemPrompt = repoPath is not null
                ? BuildCodeAssistantPrompt(repoPath, branchName)
                : """
                  You are Andúril, a personal AI assistant.
                  You have access to integration tools that let you interact with external services
                  like Gmail, GitHub, Sentry, Calendar, Slack, and Medium on behalf of the user.
                  When the user asks about their emails, calendar, code, errors, chat history, or linked articles,
                  use the available tools to fulfil their request.
                  Always be helpful and concise.

                  Formatting: Use clear structure in your responses.
                  Use **bold** for emphasis and section headings.
                  Use bullet points (- item) for lists.
                  Use `code` for inline code and ``` for code blocks.
                  Keep responses scannable with short paragraphs and clear section breaks.
                  """;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt)
            };

            foreach (var entry in history)
            {
                var role = entry.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, entry.Content));
            }

            messages.Add(new ChatMessage(ChatRole.User, text));

            // Persist the user message
            await sessionStore.AppendAsync(conversationId,
                new SessionMessage("user", text, DateTimeOffset.UtcNow));

            string responseText;

            if (tools.Count > 0)
            {
                // Use non-streaming GetResponseAsync when tools are present.
                // FunctionInvokingChatClient handles the full tool-call loop (AI calls tool →
                // result returned → AI responds) and returns the final text.
                // Anthropic's streaming API does not reliably surface tool-call events in the
                // format FunctionInvokingChatClient expects, so GetStreamingResponseAsync cannot
                // be used here — this matches the proven path in MessageProcessingService.
                var functionCallingClient = new FunctionInvokingChatClient(chatClient);
                var chatOptions = new ChatOptions { Tools = tools };
                var response = await functionCallingClient.GetResponseAsync(messages, chatOptions, cts.Token);
                responseText = response.Text ?? string.Empty;

                // Emit the complete response as a single token so the UI renders it
                if (responseText.Length > 0)
                    await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
                    {
                        ConversationId = conversationId,
                        Token = responseText,
                        IsComplete = false
                    });
            }
            else
            {
                // Stream token-by-token when no tools are needed
                var fullResponse = new System.Text.StringBuilder();
                var chunkCount = 0;

                logger.LogInformation(
                    "Hub starting streaming response for conversation '{ConversationId}' using provider '{Provider}'",
                    conversationId, effectiveProviderId ?? "default");

                await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: cts.Token))
                {
                    var chunk = update.Text ?? string.Empty;
                    if (chunk.Length > 0)
                    {
                        chunkCount++;
                        fullResponse.Append(chunk);
                        await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
                        {
                            ConversationId = conversationId,
                            Token = chunk,
                            IsComplete = false
                        });
                    }
                }

                responseText = fullResponse.ToString();

                if (chunkCount == 0)
                    logger.LogWarning(
                        "Streaming produced 0 chunks for conversation '{ConversationId}' (provider: '{Provider}', response length: {Length}). " +
                        "The model may have used an unrecognised event type — check provider adapter debug logs.",
                        conversationId, effectiveProviderId ?? "default", responseText.Length);
                else
                    logger.LogInformation(
                        "Streaming completed for conversation '{ConversationId}': {ChunkCount} chunk(s), {Length} char(s)",
                        conversationId, chunkCount, responseText.Length);
            }

            // Signal completion
            await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
            {
                ConversationId = conversationId,
                Token = string.Empty,
                IsComplete = true
            });

            // Persist the assistant message
            await sessionStore.AppendAsync(conversationId,
                new SessionMessage("assistant", responseText, DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Streaming cancelled for conversation '{ConversationId}'", conversationId);

            // Send a clean completion so the client unblocks and hides the streaming indicator.
            // WasCancelled = true lets the UI render a "Stopped" indicator in the conversation.
            await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
            {
                ConversationId = conversationId,
                Token = string.Empty,
                IsComplete = true,
                WasCancelled = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message in conversation {ConversationId}", conversationId);

            await Clients.Caller.SendAsync("ReceiveToken", new ChatStreamToken
            {
                ConversationId = conversationId,
                Token = "An error occurred while processing your message.",
                IsComplete = true,
                Error = ex.Message
            });
        }
        finally
        {
            ActiveRequests.TryRemove(requestKey, out _);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        SelectedProviders.TryRemove(Context.ConnectionId, out _);

        // Cancel and remove any in-flight requests for this connection
        var prefix = $"{Context.ConnectionId}:";
        foreach (var key in ActiveRequests.Keys.Where(k => k.StartsWith(prefix)))
        {
            if (ActiveRequests.TryRemove(key, out var cts))
                cts.Cancel();
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Builds a code-assistant system prompt that tells the model which repository it is
    /// working on and shows the top-level directory structure so it can answer questions
    /// about the codebase without the user having to repeat that context every time.
    /// </summary>
    private string BuildCodeAssistantPrompt(string repoPath, string? branchName)
    {
        var repoName = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var structure = BuildRepoStructure(repoPath);
        var branchLine = branchName is not null
            ? $"Currently on branch: {branchName}"
            : "(active branch unknown)";

        logger.LogInformation(
            "Building code-assistant prompt for repository '{RepoName}' (branch: '{Branch}') at '{RepoPath}'",
            repoName, branchName ?? "unknown", repoPath);

        return $"""
            You are Andúril, a code assistant specializing in software architecture and development.
            The user is working on a git repository named '{repoName}' located at: {repoPath}
            {branchLine}

            Top-level repository structure:
            {structure}

            You have tools to explore this codebase:
            - repo_read_file: read the contents of any file (path relative to the repo root)
            - repo_list_files: list files and subdirectories at a path (relative; omit for root)
            - repo_run_git: run a read-only git command (e.g. "log --oneline -10", "diff HEAD~1")

            Always use these tools to look at actual file contents before answering questions
            about code, patterns, or implementation details. Be concise and precise.
            """;
    }

    /// <summary>
    /// Returns a formatted string listing the top-level directories and files in the repository,
    /// excluding common noise folders (.git, bin, obj, node_modules, etc.).
    /// </summary>
    private static string BuildRepoStructure(string repoPath)
    {
        if (!Directory.Exists(repoPath))
            return "(repository path not found on this machine)";

        // Folders that add noise without useful signal
        var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", ".vscode",
            "bin", "obj", "out", "dist", "build",
            "node_modules", ".next", ".nuget",
            "__pycache__", ".mypy_cache", ".pytest_cache",
            "packages", ".packages"
        };

        var sb = new System.Text.StringBuilder();

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(repoPath)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (ignoredDirs.Contains(name)) continue;
                    sb.AppendLine($"📁 {name}/");
                }
                else
                {
                    sb.AppendLine($"   {name}");
                }
            }
        }
        catch (Exception)
        {
            return "(could not read repository structure)";
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(empty repository)";
    }

    /// <summary>
    /// Builds the set of repo-scoped <see cref="AITool"/> instances provided to the AI when
    /// the Code tab selects a repository. The tools are stateless lambdas closed over the
    /// normalised <paramref name="repoPath"/> so every call is automatically sandboxed to
    /// that directory — path traversal attempts are rejected.
    /// </summary>
    private static IReadOnlyList<AITool> BuildRepoTools(string repoPath)
    {
        // Append a separator so StartsWith can't match "/foo/bar-extra" against "/foo/bar".
        var basePath = Path.GetFullPath(repoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                       + Path.DirectorySeparatorChar;

        string ReadFile(string path)
        {
            var full = Path.GetFullPath(Path.Combine(basePath, path));
            if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return "Error: path is outside the repository.";
            // Resolve symlinks/reparse points to prevent traversal via symlink targets
            var resolved = Path.GetFullPath(new FileInfo(full).LinkTarget ?? full);
            if (!resolved.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return "Error: path resolves outside the repository.";
            if (!File.Exists(full))
                return $"File not found: {path}";
            var content = File.ReadAllText(full);
            return content.Length > 100_000
                ? content[..100_000] + "\n\n... (file truncated at 100 000 characters)"
                : content;
        }

        string ListFiles(string? subdirectory)
        {
            var dir = subdirectory is not null
                ? Path.GetFullPath(Path.Combine(basePath, subdirectory))
                : basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Ensure the base path ends with a separator so prefix checks cannot match sibling directories
            // (e.g., /foo/bar-other must not pass a prefix check against /foo/bar).
            var baseWithSep = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!dir.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(dir + Path.DirectorySeparatorChar, baseWithSep, StringComparison.OrdinalIgnoreCase))
                return "Error: path is outside the repository.";
            // Resolve symlinks/reparse points to prevent traversal via symlink targets
            var resolvedDir = Path.GetFullPath(new DirectoryInfo(dir).LinkTarget ?? dir);
            if (!resolvedDir.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolvedDir + Path.DirectorySeparatorChar, baseWithSep, StringComparison.OrdinalIgnoreCase))
                return "Error: path resolves outside the repository.";
            if (!Directory.Exists(dir))
                return $"Directory not found: {subdirectory}";

            var entries = Directory.EnumerateFileSystemEntries(dir)
                .Select(e => Directory.Exists(e) ? Path.GetFileName(e) + "/" : Path.GetFileName(e))
                .Order(StringComparer.OrdinalIgnoreCase);

            return string.Join("\n", entries);
        }

        async Task<string> RunGitAsync(string args)
        {
            var argv = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (argv.Length == 0)
                return "Error: no git subcommand provided.";

            // Allow only read-only git operations — never mutations like commit, push, reset.
            // Excluded: branch, remote, tag (can create/delete refs with certain flags).
            var allowedSubcommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "status", "log", "diff", "ls-files", "show", "describe", "rev-parse", "shortlog" };

            if (!allowedSubcommands.Contains(argv[0]))
                return $"Error: '{argv[0]}' is not permitted. Allowed read-only subcommands: {string.Join(", ", allowedSubcommands)}.";

            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = basePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in argv)
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi);
            if (proc is null) return "Error: could not start git process.";

            // Read stdout and stderr concurrently to avoid deadlock when either buffer fills.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
                return $"Error (exit code {proc.ExitCode}): {stderr.Trim()}";

            if (string.IsNullOrWhiteSpace(stdout))
                return "(no output)";

            return stdout.Length > 50_000
                ? stdout[..50_000] + "\n\n... (output truncated at 50 000 characters)"
                : stdout;
        }

        return
        [
            AIFunctionFactory.Create(ReadFile, "repo_read_file",
                "Read the full contents of a file in the repository. Provide the path relative to the repo root (e.g. \"src/Foo.cs\")."),
            AIFunctionFactory.Create(ListFiles, "repo_list_files",
                "List files and subdirectories at a path inside the repository. Path is relative to the repo root; pass null or omit to list the root."),
            AIFunctionFactory.Create(RunGitAsync, "repo_run_git",
                "Run a read-only git command in the repository. Pass the subcommand and arguments as a single string, e.g. \"log --oneline -10\" or \"diff HEAD~1 -- src/Foo.cs\"."),
        ];
    }

    /// <summary>
    /// Resolves a provider and (optionally) model-specific chat client from a provider ID.
    /// The ID may be a simple provider name ("anthropic") or a compound "provider::model" key
    /// for providers that expose multiple models (e.g. "copilot::gpt-4o").
    /// </summary>
    private (IAiProvider? Provider, IChatClient? ChatClient) ResolveProviderAndClient(string? providerId)
    {
        if (providerId is null)
        {
            var fallback = aiProviders.FirstOrDefault(p => p.IsAvailable && p.SupportsChatCompletion);
            return (fallback, fallback?.ChatClient);
        }

        // Check for compound "providerName::modelId" key
        var separatorIndex = providerId.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            var providerName = providerId[..separatorIndex];
            var modelId = providerId[(separatorIndex + 2)..];

            var provider = aiProviders.FirstOrDefault(p =>
                p.IsAvailable && p.SupportsChatCompletion &&
                p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

            if (provider is not null)
                return (provider, provider.GetChatClientForModel(modelId));

            logger.LogWarning("Provider '{Provider}' for model '{Model}' is not available", providerName, modelId);
            return (null, null);
        }
        else
        {
            var provider = aiProviders.FirstOrDefault(p =>
                p.IsAvailable && p.SupportsChatCompletion &&
                p.Name.Equals(providerId, StringComparison.OrdinalIgnoreCase));

            if (provider is not null)
                return (provider, provider.ChatClient);

            logger.LogWarning("Requested provider '{Provider}' is not available", providerId);
            return (null, null);
        }
    }
}
