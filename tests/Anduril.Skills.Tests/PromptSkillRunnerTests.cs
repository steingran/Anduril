using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Skills.Tests;

public class PromptSkillRunnerTests
{
    [Test]
    public async Task ExecuteAsync_AttachesOnlyListedSkillTools()
    {
        var skillsDirectory = Path.Combine(Path.GetTempPath(), $"anduril-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillsDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(skillsDirectory, "tool-skill.md"),
                """
                # Tool Skill

                Uses a limited tool set.

                ## Trigger
                - tool skill

                ## Instructions
                Use the available tools when needed.

                ## Tools
                - alpha_tool
                - gamma_tool
                """);

            var provider = new FakePromptSkillAiProvider("done");
            var integrationTool = new RecordingPromptSkillIntegrationTool(
                "weekly-menu-planner",
                ["alpha_tool", "beta_tool", "gamma_tool"]);
            var runner = new PromptSkillRunner(
                new PromptSkillLoader(NullLogger<PromptSkillLoader>.Instance),
                [provider],
                [integrationTool],
                NullLogger<PromptSkillRunner>.Instance,
                skillsDirectory,
                null);

            await runner.RefreshSkillsAsync();
            var result = await runner.ExecuteAsync(
                "Tool Skill",
                new SkillContext
                {
                    UserId = "user-1",
                    ChannelId = "channel-1",
                    Message = new IncomingMessage
                    {
                        Id = "msg-1",
                        Text = "tool skill",
                        UserId = "user-1",
                        ChannelId = "channel-1",
                        Platform = "test"
                    }
                });

            await Assert.That(result.Success).IsTrue();
            await Assert.That(provider.RecordingChatClient.CapturedToolNames.Count).IsEqualTo(2);
            await Assert.That(provider.RecordingChatClient.CapturedToolNames).Contains("alpha_tool");
            await Assert.That(provider.RecordingChatClient.CapturedToolNames).Contains("gamma_tool");
            await Assert.That(provider.RecordingChatClient.CapturedToolNames.Contains("beta_tool")).IsFalse();
        }
        finally
        {
            Directory.Delete(skillsDirectory, recursive: true);
        }
    }

    [Test]
    public async Task GetSkillsAsync_LocalDirectoryOverridesBaseSkillByName()
    {
        var skillsDirectory = Path.Combine(Path.GetTempPath(), $"anduril-skills-{Guid.NewGuid():N}");
        var localSkillsDirectory = Path.Combine(Path.GetTempPath(), $"anduril-local-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillsDirectory);
        Directory.CreateDirectory(localSkillsDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(skillsDirectory, "weekly-menu-planner.md"),
                """
                # Weekly Menu Planner

                Base description.

                ## Trigger
                - weekly menu
                """);

            await File.WriteAllTextAsync(
                Path.Combine(localSkillsDirectory, "weekly-menu-planner.md"),
                """
                # Weekly Menu Planner

                Local override description.

                ## Trigger
                - weekly menu
                - menu planner
                """);

            var runner = new PromptSkillRunner(
                new PromptSkillLoader(NullLogger<PromptSkillLoader>.Instance),
                [],
                [],
                NullLogger<PromptSkillRunner>.Instance,
                skillsDirectory,
                localSkillsDirectory);

            var skills = await runner.GetSkillsAsync();
            var skill = skills.Single(s => s.Name == "Weekly Menu Planner");

            await Assert.That(skills.Count).IsEqualTo(1);
            await Assert.That(skill.Description).IsEqualTo("Local override description.");
            await Assert.That(skill.Triggers).Contains("menu planner");
            await Assert.That(skill.SourcePath).Contains(localSkillsDirectory);
        }
        finally
        {
            Directory.Delete(skillsDirectory, recursive: true);
            Directory.Delete(localSkillsDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_AdvertisesOnlyResolvedToolsAndOmitsTimestamp()
    {
        var skillsDirectory = Path.Combine(Path.GetTempPath(), $"anduril-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillsDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(skillsDirectory, "tool-skill.md"),
                """
                # Tool Skill

                Uses resolved tools only.

                ## Trigger
                - tool skill

                ## Instructions
                Use the available tools when needed.

                ## Tools
                - alpha_tool
                - missing_tool
                """);

            var provider = new FakePromptSkillAiProvider("done");
            var integrationTool = new RecordingPromptSkillIntegrationTool("weekly-menu-planner", ["alpha_tool"]);
            var runner = new PromptSkillRunner(
                new PromptSkillLoader(NullLogger<PromptSkillLoader>.Instance),
                [provider],
                [integrationTool],
                NullLogger<PromptSkillRunner>.Instance,
                skillsDirectory,
                null);

            await runner.RefreshSkillsAsync();
            _ = await runner.ExecuteAsync(
                "Tool Skill",
                new SkillContext
                {
                    UserId = "user-1",
                    ChannelId = "channel-1",
                    Message = new IncomingMessage
                    {
                        Id = "msg-1",
                        Text = "tool skill",
                        UserId = "user-1",
                        ChannelId = "channel-1",
                        Platform = "test"
                    }
                });

            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt).IsNotNull();
            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt!).Contains("Available tools: alpha_tool");
            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt!.Contains("missing_tool", StringComparison.Ordinal)).IsFalse();
            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt!.Contains("timestamp_utc", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(skillsDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteWithoutToolsAsync_DoesNotAttachOrAdvertiseTools()
    {
        var skillsDirectory = Path.Combine(Path.GetTempPath(), $"anduril-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillsDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(skillsDirectory, "tool-skill.md"),
                """
                # Tool Skill

                Uses tools.

                ## Trigger
                - tool skill

                ## Instructions
                Use the available tools when needed.

                ## Tools
                - alpha_tool
                """);

            var provider = new FakePromptSkillAiProvider("done");
            var integrationTool = new RecordingPromptSkillIntegrationTool("weekly-menu-planner", ["alpha_tool"]);
            var runner = new PromptSkillRunner(
                new PromptSkillLoader(NullLogger<PromptSkillLoader>.Instance),
                [provider],
                [integrationTool],
                NullLogger<PromptSkillRunner>.Instance,
                skillsDirectory,
                null);

            await runner.RefreshSkillsAsync();
            _ = await runner.ExecuteWithoutToolsAsync(
                "Tool Skill",
                new SkillContext
                {
                    UserId = "user-1",
                    ChannelId = "channel-1",
                    Message = new IncomingMessage
                    {
                        Id = "msg-1",
                        Text = "tool skill",
                        UserId = "user-1",
                        ChannelId = "channel-1",
                        Platform = "test"
                    }
                });

            await Assert.That(provider.RecordingChatClient.CapturedToolNames.Count).IsEqualTo(0);
            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt).IsNotNull();
            await Assert.That(provider.RecordingChatClient.CapturedSystemPrompt!.Contains("Available tools:", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(skillsDirectory, recursive: true);
        }
    }
}
