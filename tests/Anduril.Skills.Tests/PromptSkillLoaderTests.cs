using Anduril.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Skills.Tests;

public class PromptSkillLoaderTests
{
    private readonly PromptSkillLoader _loader = new(NullLogger<PromptSkillLoader>.Instance);

    [Test]
    public async Task Parse_ExtractsTitle()
    {
        string md = "# My Skill\n\nA description.\n\n## Trigger\n- hello\n\n## Instructions\nDo things.";
        var skill = _loader.Parse(md);

        await Assert.That(skill.Name).IsEqualTo("My Skill");
    }

    [Test]
    public async Task Parse_ExtractsDescription()
    {
        string md = "# My Skill\n\nA short description.\n\n## Trigger\n- hello";
        var skill = _loader.Parse(md);

        await Assert.That(skill.Description).IsEqualTo("A short description.");
    }

    [Test]
    public async Task Parse_ExtractsTriggers()
    {
        string md = "# Test\n\n## Trigger\n- review code\n- check this PR\n- code review";
        var skill = _loader.Parse(md);

        await Assert.That(skill.Triggers).Count().IsEqualTo(3);
        await Assert.That(skill.Triggers).Contains("review code");
        await Assert.That(skill.Triggers).Contains("check this PR");
        await Assert.That(skill.Triggers).Contains("code review");
    }

    [Test]
    public async Task Parse_ExtractsInstructions()
    {
        string md = "# Test\n\n## Instructions\nYou are a helpful assistant.\nBe concise.";
        var skill = _loader.Parse(md);

        await Assert.That(skill.Instructions).Contains("You are a helpful assistant.");
        await Assert.That(skill.Instructions).Contains("Be concise.");
    }

    [Test]
    public async Task Parse_ExtractsTools()
    {
        string md = "# Test\n\n## Tools\n- github_list_issues\n- sentry_get_issue";
        var skill = _loader.Parse(md);

        await Assert.That(skill.Tools).Count().IsEqualTo(2);
        await Assert.That(skill.Tools).Contains("github_list_issues");
        await Assert.That(skill.Tools).Contains("sentry_get_issue");
    }

    [Test]
    public async Task Parse_ExtractsOutputFormat()
    {
        string md = "# Test\n\n## Output Format\nReturn a markdown table.";
        var skill = _loader.Parse(md);

        await Assert.That(skill.OutputFormat).IsEqualTo("Return a markdown table.");
    }

    [Test]
    public async Task Parse_FallsBackToSourcePath_WhenNoTitle()
    {
        string md = "Some content without a heading.";
        var skill = _loader.Parse(md, "skills/my-skill.md");

        await Assert.That(skill.Name).IsEqualTo("my-skill");
    }

    [Test]
    public async Task Parse_EmptyMarkdown_ReturnsUnnamed()
    {
        var skill = _loader.Parse("");

        await Assert.That(skill.Name).IsEqualTo("unnamed");
    }
}

