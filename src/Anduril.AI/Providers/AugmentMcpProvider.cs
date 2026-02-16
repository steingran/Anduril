using Anduril.Core.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider backed by Augment Code CLI via the Model Context Protocol (MCP).
/// Launches <c>auggie --mcp</c> as a long-lived sidecar process and communicates
/// over stdio using the MCP wire format (JSON-RPC).
/// </summary>
public sealed class AugmentMcpProvider(IOptions<AiProviderOptions> options, ILogger<AugmentMcpProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private McpClient? _mcpClient;

    public string Name => "augment";

    public bool IsAvailable => _mcpClient is not null;

    public bool SupportsChatCompletion => false;

    public IChatClient ChatClient =>
        throw new NotSupportedException(
            "Augment MCP provider does not expose a direct IChatClient. " +
            "Use GetToolsAsync() to retrieve tools and pass them to another provider's ChatClient.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string cliPath = _options.AugmentCliPath ?? "auggie";

        try
        {
            _mcpClient = await McpClient.CreateAsync(
                new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Augment",
                    Command = cliPath,
                    Arguments = ["--mcp"]
                }),
                cancellationToken: cancellationToken);

            logger.LogInformation("Augment MCP provider initialized via {CliPath}", cliPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize Augment MCP provider via {CliPath}. Is Augment CLI installed?", cliPath);
            _mcpClient = null;
        }
    }

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        if (_mcpClient is null)
            throw new InvalidOperationException("Augment MCP provider has not been initialized.");

        var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        // McpClientTool inherits from AIFunction which inherits from AITool
        return tools.ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
            _mcpClient = null;
        }
    }
}

