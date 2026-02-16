namespace Anduril.Core.Skills;

/// <summary>
/// Represents the result of executing a skill.
/// </summary>
public class SkillResult
{
    /// <summary>
    /// Gets or sets the text response to send back to the user.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the skill executed successfully.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Gets or sets an optional error message if the skill failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets optional structured data returned by the skill (e.g., JSON, a list of items).
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets or sets the name of the skill that produced this result.
    /// </summary>
    public string? SkillName { get; init; }

    /// <summary>
    /// Creates a successful result with the given response text.
    /// </summary>
    public static SkillResult Ok(string response, object? data = null) =>
        new() { Response = response, Success = true, Data = data };

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static SkillResult Fail(string errorMessage) =>
        new() { Response = errorMessage, Success = false, ErrorMessage = errorMessage };
}

