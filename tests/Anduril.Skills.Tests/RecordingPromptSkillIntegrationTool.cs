using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;

namespace Anduril.Skills.Tests;

public sealed class RecordingPromptSkillIntegrationTool(string name, IReadOnlyList<string> functionNames) : IIntegrationTool
{
    public string Name => name;

    public string Description => $"Recording prompt skill integration tool '{name}'";

    public bool IsAvailable => true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<AIFunction> GetFunctions() =>
        [.. functionNames.Select(functionName => AIFunctionFactory.Create(() => "ok", functionName, "Test function"))];
}