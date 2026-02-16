using Microsoft.Extensions.AI;

namespace Anduril.Core.Integrations;

/// <summary>
/// Represents an external integration tool (GitHub, Sentry, Calendar, etc.)
/// that can be exposed to the AI as callable functions.
/// </summary>
public interface IIntegrationTool
{
    /// <summary>
    /// Gets the unique name of this integration (e.g., "github", "sentry", "calendar").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of this integration.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a value indicating whether this integration is currently configured and available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns the AI-callable functions this integration provides.
    /// These are registered with the AI provider for function calling / tool use.
    /// </summary>
    IReadOnlyList<AIFunction> GetFunctions();

    /// <summary>
    /// Initializes the integration, verifying configuration and connectivity.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

