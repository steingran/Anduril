using Anduril.Core.AI;
using Microsoft.Extensions.AI;

namespace Anduril.Skills.Tests;

public sealed class FakePromptSkillAiProvider(string responseText) : IAiProvider
{
    public string Name => "fake-prompt-skill-ai";

    public bool IsAvailable => true;

    public bool SupportsChatCompletion => true;

    public FakePromptSkillChatClient RecordingChatClient { get; } = new(responseText);

    public IChatClient ChatClient => RecordingChatClient;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AITool>>([]);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}