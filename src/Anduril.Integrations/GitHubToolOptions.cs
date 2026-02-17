namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the GitHub integration.
/// </summary>
public class GitHubToolOptions
{
    /// <summary>
    /// Gets or sets the GitHub personal access token.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the default repository owner.
    /// </summary>
    public string? DefaultOwner { get; set; }

    /// <summary>
    /// Gets or sets the default repository name.
    /// </summary>
    public string? DefaultRepo { get; set; }
}

